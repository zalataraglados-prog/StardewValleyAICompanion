param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [int] $StartVolcanoLevel = 0,
    [int] $MaximumSteps = 320,
    [int] $StartupTimeoutSeconds = 120,
    [string] $RunId = ("runtime-volcano-reach-caldera-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-volcano-reach-caldera",
    [switch] $VisibleGame,
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
            if ($response.status -eq "ok") {
                return $response
            }
            $lastError = "status=$($response.status)"
        }
        catch {
            $lastError = $_.Exception.Message
        }
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
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for isolated save. Last error: $lastError"
}

function Wait-VolcanoSnapshot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=volcano"
            $level = $snapshot.state.volcano.current_level.value
            if ($snapshot.state.volcano.completeness.value.status -eq "complete" -and
                $null -ne $level.level -and
                [int]$level.level -ge 0 -and
                [int]$level.level -le 9) {
                return $snapshot
            }
            $lastError = "volcano_snapshot_not_ready"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for a complete Volcano snapshot. Last error: $lastError"
}

function Read-FieldValue {
    param($Snapshot, [string] $Section, [string] $Field)
    if ($null -eq $Snapshot.state.$Section.$Field) {
        return $null
    }
    return $Snapshot.state.$Section.$Field.value
}

function Read-PlayerLocation {
    param($Snapshot)
    return [string](Read-FieldValue -Snapshot $Snapshot -Section "player" -Field "location_id")
}

function Read-VolcanoLevel {
    param($Snapshot)
    $level = Read-FieldValue -Snapshot $Snapshot -Section "volcano" -Field "current_level"
    if ($null -eq $level -or $null -eq $level.level) {
        return $null
    }
    return [int]$level.level
}

function Read-ExecutionPrimitive {
    param($Execution)
    $steps = @($Execution.step_results)
    if ($steps.Count -gt 0) {
        return $steps[-1]
    }
    return $Execution
}

if ($StartVolcanoLevel -lt 0 -or $StartVolcanoLevel -gt 9) {
    throw "StartVolcanoLevel must be between 0 and 9."
}
if ($MaximumSteps -lt 1) {
    throw "MaximumSteps must be at least 1."
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
$gameProcess = $null
$backendProcess = $null
$stepSummaries = New-Object System.Collections.Generic.List[object]
$visitedLevels = New-Object System.Collections.Generic.List[int]
$calderaReached = $false

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $runtimeSaves -PathType Container)) {
    throw "Isolated saves path not found: $runtimeSaves"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $runtimeSaves -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $runtimeSaves"
    }
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
    STARDEWAI_VOLCANO_CALIBRATION_LOADOUT = $env:STARDEWAI_VOLCANO_CALIBRATION_LOADOUT
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

try {
    & dotnet build (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj") --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Backend build failed with exit code $LASTEXITCODE."
    }
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "LiveTrainingLoop build failed with exit code $LASTEXITCODE."
    }
    & (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
    & (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

    $env:STARDEWAI_TEST_SAVES = $runtimeSaves
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $runtimeSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_VOLCANO_CALIBRATION_LOADOUT = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $gameStart = @{
        FilePath = $smapiExe
        WorkingDirectory = $runtimeGameDir
        PassThru = $true
    }
    if (-not $VisibleGame) {
        $gameStart.WindowStyle = "Hidden"
    }
    $gameProcess = Start-Process @gameStart
    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30 | Out-Null
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-volcano-reach-caldera"
        queue_item_id = "runtime-volcano-reach-caldera.setup"
        before_state_hash = $worldSnapshot.state_hash
        option_id = "debug.setup_volcano_floor"
        mine_level = $StartVolcanoLevel
        save_isolation_path = $runtimeSaves
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Volcano fixture failed: status=$($setupResult.status); reasons=$(@($setupResult.block_reasons) -join ',')"
    }

    $beforeSetupSnapshot = Wait-VolcanoSnapshot -TimeoutSeconds $StartupTimeoutSeconds
    Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $beforeSetupSnapshot
    if ((Read-VolcanoLevel $beforeSetupSnapshot) -ne $StartVolcanoLevel) {
        throw "Volcano fixture loaded the wrong start level."
    }

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @($backendDll, "--urls", "http://127.0.0.1:5108") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendLog `
        -RedirectStandardError $backendErrorLog `
        -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:5108/health" -TimeoutSeconds 60 | Out-Null

    for ($step = 1; $step -le $MaximumSteps; $step++) {
        $before = Wait-VolcanoSnapshot -TimeoutSeconds $StartupTimeoutSeconds
        $beforeLevel = Read-VolcanoLevel $before
        if ($null -eq $beforeLevel) {
            throw "Current Volcano level was unavailable before step $step."
        }
        if ($visitedLevels.Count -eq 0 -or $visitedLevels[-1] -ne $beforeLevel) {
            $visitedLevels.Add($beforeLevel)
        }

        $artifactRunId = $RunId + "-step-" + $step.ToString("D4")
        $arguments = @(
            $loopDll,
            "--root", $trainingRoot,
            "--backend-url", "http://127.0.0.1:5108",
            "--bridge-snapshot-url", "http://127.0.0.1:8765/api/v1/snapshot?profile=volcano",
            "--executor-url", "http://127.0.0.1:8767",
            "--no-manifest",
            "--run-id", $RunId,
            "--artifact-run-id", $artifactRunId,
            "--save-isolation-path", $runtimeSaves,
            "--iterations", "1",
            "--required-verified-actions", "1",
            "--skip-training",
            "--sleep-ms", "0",
            "--max-crops", "64",
            "--use-parameterized-action",
            "--action-option-id", "volcano.reach_caldera",
            "--action-parameter", "target_volcano_level=9",
            "--action-parameter", "target_location=Caldera",
            "--max-queue-item-attempts", "1"
        )
        $loopOutputPath = Join-Path $runDirectory ("loop-step-" + $step.ToString("D4") + ".json")
        & dotnet @arguments | Set-Content -LiteralPath $loopOutputPath -Encoding utf8
        $loopExitCode = $LASTEXITCODE

        $snapshotDirectory = Join-Path $trainingRoot (Join-Path "runs" (Join-Path $artifactRunId "live-snapshots"))
        $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
        if (-not (Test-Path -LiteralPath $executionPath -PathType Leaf)) {
            throw "Step $step did not write execution-0001.json; loop_exit_code=$loopExitCode"
        }

        $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
        $primitive = Read-ExecutionPrimitive $execution
        $afterPath = [string]$execution.after_snapshot_path
        if ([string]::IsNullOrWhiteSpace($afterPath) -or -not (Test-Path -LiteralPath $afterPath -PathType Leaf)) {
            throw "Step $step did not write a readable after snapshot."
        }
        $after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
        $afterLevel = Read-VolcanoLevel $after
        $afterLocation = Read-PlayerLocation $after
        $optionId = [string]$primitive.option_id
        $primitiveStatus = [string]$primitive.status
        $verification = [string]$primitive.primitive_verification_status
        $expectedOptions = @(
            "executor.move_to_tile",
            "executor.traverse_connector",
            "executor.cool_volcano_lava",
            "executor.break_volcano_stone",
            "executor.break_volcano_container",
            "executor.combat_volcano_monster"
        )

        $stepSummary = [ordered]@{
            step = $step
            before_level = $beforeLevel
            after_level = $afterLevel
            after_location = $afterLocation
            state_hash_before = [string]$before.state_hash
            state_hash_after = [string]$after.state_hash
            state_hash_changed = [bool]$execution.state_hash_changed
            after_snapshot_fresh = [bool]$execution.after_snapshot_fresh
            option_id = $optionId
            primitive_kind = [string]$primitive.primitive_kind
            status = $primitiveStatus
            verification = $verification
            block_reasons = @($primitive.block_reasons)
            actual_ticks = $primitive.actual_ticks
            execution_path = $executionPath
            after_snapshot_path = $afterPath
        }
        $stepSummaries.Add([pscustomobject]$stepSummary)
        Write-JsonFile (Join-Path $runDirectory ("step-summary-" + $step.ToString("D4") + ".json")) $stepSummary

        if ($loopExitCode -ne 0 -or $primitiveStatus -ne "applied" -or $verification -ne "verified") {
            throw "Step $step failed: option=$optionId status=$primitiveStatus verification=$verification reasons=$(@($primitive.block_reasons) -join ',')"
        }
        if ($optionId -notin $expectedOptions) {
            throw "Step $step selected an unexpected cross-family option: $optionId"
        }
        if (-not [bool]$execution.after_snapshot_fresh) {
            throw "Step $step produced a stale after snapshot."
        }
        if ([string]::Equals($afterLocation, "Caldera", [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($optionId -ne "executor.traverse_connector" -or $beforeLevel -ne 9) {
                throw "Caldera was reached through an unexpected option or source level."
            }
            $calderaReached = $true
            break
        }
        if ($null -eq $afterLevel -or $afterLevel -lt 0 -or $afterLevel -gt 9) {
            throw "Step $step left the Volcano family without reaching Caldera."
        }
        if ($afterLevel -lt $beforeLevel -or $afterLevel -gt ($beforeLevel + 1)) {
            throw "Step $step crossed an invalid Volcano level boundary: before=$beforeLevel after=$afterLevel"
        }
    }

    if (-not $calderaReached) {
        throw "MaximumSteps exhausted before Caldera was reached."
    }

    $datasetPath = Join-Path $trainingRoot "datasets\live-training-feature-rows.jsonl"
    $datasetRows = if (Test-Path -LiteralPath $datasetPath -PathType Leaf) {
        @(Get-Content -LiteralPath $datasetPath).Count
    }
    else {
        0
    }
    $stepSummaryArray = $stepSummaries.ToArray()
    $visitedLevelArray = $visitedLevels.ToArray()
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        start_volcano_level = $StartVolcanoLevel
        caldera_reached = $calderaReached
        visited_levels = @($visitedLevelArray)
        distinct_level_count = @($visitedLevelArray | Sort-Object -Unique).Count
        executed_step_count = $stepSummaryArray.Count
        verified_step_count = @($stepSummaryArray | Where-Object { $_.status -eq "applied" -and $_.verification -eq "verified" }).Count
        primitive_counts = @($stepSummaryArray | Group-Object option_id | ForEach-Object {
            [ordered]@{ option_id = $_.Name; count = $_.Count }
        })
        all_after_snapshots_fresh = @($stepSummaryArray | Where-Object { -not $_.after_snapshot_fresh }).Count -eq 0
        all_state_hashes_changed = @($stepSummaryArray | Where-Object { -not $_.state_hash_changed }).Count -eq 0
        dataset_path = $datasetPath
        dataset_rows = $datasetRows
        game_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
        steps = @($stepSummaryArray)
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 24
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
        if ($null -eq $previousEnv[$name]) {
            Remove-Item -Path ("Env:" + $name) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("Env:" + $name) -Value $previousEnv[$name]
        }
    }
}
