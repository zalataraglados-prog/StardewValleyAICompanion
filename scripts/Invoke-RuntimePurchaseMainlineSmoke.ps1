param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-purchase-mainline-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-purchase-mainline-smoke",
    [int] $BackendPort = 5132,
    [int] $StartupTimeoutSeconds = 150,
    [int] $PurchaseWindowTimeoutSeconds = 240,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = ""
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 4
            if ($response.status -eq "ok" -or
                $response.schema_version -eq "snapshot.v1") {
                return $response
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 6
            $location = [string](Read-FieldValue $snapshot "player" "location_id")
            if (-not [string]::IsNullOrWhiteSpace($location)) {
                return $snapshot
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world-ready snapshot."
}

function Wait-PurchaseWindow {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastTime = -1
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 10
        $lastTime = [int](Read-FieldValue $snapshot "time" "time")
        if ($lastTime -ge 900 -and $lastTime -lt 1600) {
            return $snapshot
        }
        if ($lastTime -ge 1600) {
            throw "Isolated save started too late for deterministic shop coverage: $lastTime"
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for deterministic shop hours; last time was $lastTime."
}

function Read-QueueOptionIds {
    param([string] $SnapshotDirectory)
    $ids = New-Object System.Collections.Generic.List[string]
    Get-ChildItem -LiteralPath $SnapshotDirectory -Filter "*compiled-queue-*.json" |
        Sort-Object Name |
        ForEach-Object {
            $queue = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
            foreach ($item in @($queue.items)) {
                $id = [string]$item.option_id
                if (-not [string]::IsNullOrWhiteSpace($id)) { $ids.Add($id) }
            }
        }
    return @($ids)
}

function Read-CompletedPurchaseExecution {
    param([string] $SnapshotDirectory)
    foreach ($path in @(Get-ChildItem -LiteralPath $SnapshotDirectory `
            -Filter "execution-*.json" | Sort-Object Name -Descending)) {
        $execution = Get-Content -LiteralPath $path.FullName -Raw | ConvertFrom-Json
        if ($execution.objective_continuation_completed -eq $true) {
            $buy = @($execution.step_results | Where-Object {
                [string]$_.option_id -eq "executor.buy_shop_item" -and
                [string]$_.status -eq "applied" -and
                [string]$_.primitive_verification_status -eq "verified"
            } | Select-Object -First 1)[0]
            if ($null -ne $buy) {
                return [pscustomobject]@{ Execution = $execution; Buy = $buy; Path = $path.FullName }
            }
        }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"
$loopDll = Join-Path $ProjectRoot (
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\" +
    "StardewAI.LiveTrainingLoop.dll")

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slot exists under $savesPath" }
    $SaveSlot = $slot.Name
}
$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) {
    throw "Isolated save slot not found: $slotPath"
}
foreach ($port in @($BackendPort, 8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port `
            -ErrorAction SilentlyContinue)) {
        throw "Runtime purchase smoke requires unused port $port."
    }
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
$gameStdout = Join-Path $runDirectory "game.stdout.log"
$gameStderr = Join-Path $runDirectory "game.stderr.log"

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& dotnet build (Join-Path $ProjectRoot (
    "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj")) `
    -c Release --no-restore --nologo | Out-Null
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $loopDll -PathType Leaf)) {
    throw "LiveTrainingLoop Release build failed."
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_TRAINING_OUTPUT_DIR = $env:STARDEWAI_TRAINING_OUTPUT_DIR
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}
$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile") -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout -RedirectStandardError $backendStderr -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -RedirectStandardOutput $gameStdout `
        -RedirectStandardError $gameStderr -PassThru
    Wait-JsonHealth -Url "$executorUrl/health" -TimeoutSeconds 45 | Out-Null
    Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds |
        Out-Null
    $before = Wait-PurchaseWindow -Url $snapshotUrl `
        -TimeoutSeconds $PurchaseWindowTimeoutSeconds
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $before

    & dotnet $loopDll `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorUrl `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --goal daily.closed_loop `
        --max-attempts 24 `
        --skip-training `
        --sleep-ms 100 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options economy.buy_supplies `
        --continue-after-blocked-queue-items `
        --max-queue-item-attempts 12 `
        --after-snapshot-wait-ms 750 `
        --stop-after-objective-complete
    if ($LASTEXITCODE -ne 0) {
        throw "Purchase LiveTrainingLoop failed with exit code $LASTEXITCODE."
    }

    $loopRunDirectory = Join-Path $loopRoot "runs\$RunId"
    $snapshotDirectory = Join-Path $loopRunDirectory "live-snapshots"
    $reportPath = Join-Path $loopRunDirectory "live-training-loop-report.json"
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "LiveTrainingLoop report is missing: $reportPath"
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $queueOptionIds = Read-QueueOptionIds -SnapshotDirectory $snapshotDirectory
    $completed = Read-CompletedPurchaseExecution -SnapshotDirectory $snapshotDirectory
    $passed =
        $report.objective_completed -eq $true -and
        $null -eq $report.active_objective_continuation -and
        $queueOptionIds -contains "executor.traverse_connector" -and
        $queueOptionIds -contains "executor.interact" -and
        $queueOptionIds -contains "executor.buy_shop_item" -and
        $null -ne $completed
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-219"
        run_id = $RunId
        save_slot = $SaveSlot
        high_level_option_id = "economy.buy_supplies"
        objective_completed = [bool]$report.objective_completed
        active_objective_continuation = $report.active_objective_continuation
        queue_option_ids = @($queueOptionIds | Select-Object -Unique)
        exact_buy_applied_and_verified = $null -ne $completed
        completed_execution_path = if ($null -eq $completed) { "" } else { $completed.Path }
        report_path = $reportPath
        snapshot_directory = $snapshotDirectory
        before_location = Read-FieldValue $before "player" "location_id"
        before_time = Read-FieldValue $before "time" "time"
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if (-not $passed) { throw "High-level purchase mainline smoke failed." }
}
catch {
    Write-JsonFile (Join-Path $runDirectory "failure.json") ([ordered]@{
        status = "failed"
        run_id = $RunId
        error = $_.Exception.Message
        backend_stdout = $backendStdout
        backend_stderr = $backendStderr
        game_stdout = $gameStdout
        game_stderr = $gameStderr
    })
    throw
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else { Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value }
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and
        -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
        $backendProcess.WaitForExit(10000) | Out-Null
    }
}
