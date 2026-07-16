param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [int] $StartMineLevel = 130,
    [int] $TargetMineLevel = 220,
    [int] $StartupTimeoutSeconds = 120,
    [string] $RunId = ("runtime-skull-cavern-shaft-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-skull-cavern-shaft",
    [switch] $KeepProcessesRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") { return $response }
            $lastError = "status=$($response.status)"
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=route"
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastError = "save_id=$($snapshot.save_id.status)"
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for isolated save. Last error: $lastError"
}

function Wait-SkullCavernSnapshot {
    param([int] $ExpectedMineLevel, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=mining"
            $mine = $snapshot.state.mining.current_mine.value
            $shafts = @($snapshot.state.mining.tiles.value.shafts)
            if ($snapshot.state.mining.completeness.value.status -eq "complete" -and
                [int]$mine.mine_level -eq $ExpectedMineLevel -and
                [string]$mine.mine_kind -eq "skull_cavern" -and
                [int]$mine.mine_area -eq 121 -and
                $shafts.Count -gt 0) {
                return $snapshot
            }
            $lastError = "level=$($mine.mine_level);kind=$($mine.mine_kind);area=$($mine.mine_area);shafts=$($shafts.Count)"
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for Skull Cavern shaft snapshot. Last error: $lastError"
}

function Read-ExecutionPrimitive {
    param($Execution)
    $steps = @($Execution.step_results)
    if ($steps.Count -gt 0) { return $steps[-1] }
    return $Execution
}

if ($StartMineLevel -le 120 -or $StartMineLevel -eq 77377) {
    throw "StartMineLevel must be a Skull Cavern level greater than 120 and not the quarry mine sentinel."
}
if ($TargetMineLevel -le $StartMineLevel) {
    throw "TargetMineLevel must be greater than StartMineLevel."
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$runtimeSaves = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$trainingRoot = Join-Path $runDirectory "training"
$backendLog = Join-Path $runDirectory "backend.log"
$backendErrorLog = Join-Path $runDirectory "backend-error.log"
$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Debug\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Debug\net8.0\StardewAI.LiveTrainingLoop.dll"
$artifactRunId = $RunId + "-step"
$gameProcess = $null
$backendProcess = $null

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $runtimeSaves -PathType Container)) { throw "Isolated saves path not found: $runtimeSaves" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $runtimeSaves -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $runtimeSaves" }
    $SaveSlot = $slot.Name
}

$occupiedPorts = Get-NetTCPConnection -State Listen -LocalPort 5108, 8765, 8767 -ErrorAction SilentlyContinue
if (@($occupiedPorts).Count -gt 0) {
    throw "Required runtime ports are occupied: $(@($occupiedPorts.LocalPort | Sort-Object -Unique) -join ',')"
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_MINING_CALIBRATION_LOADOUT = $env:STARDEWAI_MINING_CALIBRATION_LOADOUT
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

try {
    & dotnet build (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj") --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Backend build failed with exit code $LASTEXITCODE." }
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop build failed with exit code $LASTEXITCODE." }
    & (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
    & (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

    $env:STARDEWAI_TEST_SAVES = $runtimeSaves
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $runtimeSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_MINING_CALIBRATION_LOADOUT = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30 | Out-Null
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-skull-cavern-shaft"
        queue_item_id = "runtime-skull-cavern-shaft.setup"
        before_state_hash = $worldSnapshot.state_hash
        option_id = "debug.setup_skull_cavern_shaft"
        mine_level = $StartMineLevel
        save_isolation_path = $runtimeSaves
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Skull Cavern fixture failed: status=$($setupResult.status); reasons=$(@($setupResult.block_reasons) -join ',')"
    }

    $before = Wait-SkullCavernSnapshot -ExpectedMineLevel $StartMineLevel -TimeoutSeconds $StartupTimeoutSeconds
    Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $before
    $shaft = @($before.state.mining.tiles.value.shafts | Sort-Object tile_y, tile_x | Select-Object -First 1)
    if ($shaft.Count -ne 1) { throw "Transparent snapshot did not expose a Skull Cavern shaft." }

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @($backendDll, "--urls", "http://127.0.0.1:5108") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendLog `
        -RedirectStandardError $backendErrorLog `
        -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:5108/health" -TimeoutSeconds 60 | Out-Null

    $arguments = @(
        $loopDll,
        "--root", $trainingRoot,
        "--backend-url", "http://127.0.0.1:5108",
        "--bridge-snapshot-url", "http://127.0.0.1:8765/api/v1/snapshot?profile=full",
        "--executor-url", "http://127.0.0.1:8767",
        "--no-manifest",
        "--run-id", $RunId,
        "--artifact-run-id", $artifactRunId,
        "--save-isolation-path", $runtimeSaves,
        "--iterations", "1",
        "--required-verified-actions", "1",
        "--train-every", "1",
        "--sleep-ms", "0",
        "--use-parameterized-action",
        "--action-option-id", "mining.reach_depth",
        "--action-parameter", "target_depth=$TargetMineLevel",
        "--action-parameter", "target_location_family=skull_cavern",
        "--action-parameter", "latest_exit_time=2400",
        "--action-parameter", "minimum_reserve_health=1",
        "--action-parameter", "minimum_reserve_energy=0",
        "--action-parameter", "resource_preservation_policy=preserve_staircases",
        "--max-queue-item-attempts", "1"
    )
    & dotnet @arguments | Set-Content -LiteralPath (Join-Path $runDirectory "loop.json") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop failed with exit code $LASTEXITCODE." }

    $executionPath = Join-Path $trainingRoot (Join-Path "runs" (Join-Path $artifactRunId "live-snapshots\execution-0001.json"))
    if (-not (Test-Path -LiteralPath $executionPath -PathType Leaf)) {
        throw "LiveTrainingLoop did not write execution-0001.json."
    }
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $primitive = Read-ExecutionPrimitive $execution
    if ($primitive.option_id -ne "executor.descend_shaft" -or
        $primitive.status -ne "applied" -or
        $primitive.primitive_verification_status -ne "verified" -or
        -not [bool]$primitive.shaft_native_dialogue_handled) {
        throw "Unexpected shaft primitive result: option=$($primitive.option_id); status=$($primitive.status); verification=$($primitive.primitive_verification_status)"
    }

    $afterPath = [string]$execution.after_snapshot_path
    if ([string]::IsNullOrWhiteSpace($afterPath) -or -not (Test-Path -LiteralPath $afterPath -PathType Leaf)) {
        throw "Verified shaft execution did not write a readable after snapshot."
    }
    $after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $after
    $afterMine = $after.state.mining.current_mine.value
    if ([string]$afterMine.mine_kind -ne "skull_cavern" -or [int]$afterMine.mine_area -ne 121) {
        throw "Shaft execution crossed into the wrong mine family."
    }

    $expectedDelta = [int]$shaft[0].expected_level_delta
    $expectedAfterLevel = [int]$shaft[0].expected_mine_level_after
    $expectedHealthAfter = [int]$shaft[0].expected_health_after
    if ([int]$primitive.shaft_level_delta -ne $expectedDelta -or
        [int]$primitive.shaft_mine_level_after -ne $expectedAfterLevel -or
        [int]$primitive.shaft_health_after -ne $expectedHealthAfter) {
        throw "Native shaft result did not match the transparent deterministic preview."
    }

    $datasetPath = Join-Path $trainingRoot "datasets\live-training-feature-rows.jsonl"
    $datasetRows = if (Test-Path -LiteralPath $datasetPath -PathType Leaf) { @(Get-Content -LiteralPath $datasetPath).Count } else { 0 }
    if ($datasetRows -lt 1) { throw "Verified shaft execution did not append a training row." }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        mine_family = "skull_cavern"
        mine_area = 121
        start_mine_level = $StartMineLevel
        expected_level_delta = $expectedDelta
        observed_level_delta = [int]$primitive.shaft_level_delta
        expected_mine_level_after = $expectedAfterLevel
        observed_mine_level_after = [int]$primitive.shaft_mine_level_after
        expected_health_after = $expectedHealthAfter
        observed_health_after = [int]$primitive.shaft_health_after
        native_dialogue_handled = [bool]$primitive.shaft_native_dialogue_handled
        after_snapshot_fresh = [bool]$execution.after_snapshot_fresh
        state_hash_changed = [bool]$execution.state_hash_changed
        dataset_path = $datasetPath
        dataset_rows = $datasetRows
        execution_path = $executionPath
        after_snapshot_path = $afterPath
    }
    if (-not $summary.after_snapshot_fresh -or -not $summary.state_hash_changed) {
        throw "Shaft execution did not produce a fresh changed after snapshot."
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    if (-not $KeepProcessesRunning) {
        if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
            Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
            Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }

    foreach ($name in $previousEnv.Keys) {
        Set-Item -Path ("Env:" + $name) -Value $previousEnv[$name]
    }
}
