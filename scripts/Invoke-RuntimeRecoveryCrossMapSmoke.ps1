param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-recovery-cross-map-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-recovery-cross-map-smoke",
    [int] $BackendPort = 5108,
    [int] $StartupTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 180)
    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") {
                return $response
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            $location = Read-FieldValue $snapshot "player" "location_id"
            $time = Read-FieldValue $snapshot "time" "time"
            if (-not [string]::IsNullOrWhiteSpace([string]$location) -and $null -ne $time) {
                return $snapshot
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world snapshot. Last error: $lastError"
}

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    if ($null -eq $Snapshot.state) { return $null }
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Find-FarmHouseExitConnector {
    param($Snapshot)
    $warps = Read-FieldValue $Snapshot "current_location" "warps"
    return @($warps | Where-Object {
        ([string]$_.target_location -eq "Farm") -or ([string]$_.target_name -eq "Farm")
    } | Sort-Object y, x | Select-Object -First 1)[0]
}

function New-ExecutionRequest {
    param(
        [string] $QueueItemId,
        [string] $OptionId,
        [string] $BeforeStateHash,
        [string] $SavesPath
    )
    return [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.setup"
        queue_item_id = $QueueItemId
        before_state_hash = $BeforeStateHash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
    }
}

function Invoke-RecoveryPlanIteration {
    param([string] $LoopRoot, [string] $BackendUrl, [string] $SnapshotUrl, [string] $ExecutorUrl, [string] $SavesPath)
    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $LoopRoot `
        --backend-url $BackendUrl `
        --bridge-snapshot-url $SnapshotUrl `
        --executor-url $ExecutorUrl `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $SavesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "recovery.stabilize_day" `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) {
        throw "Recovery LiveTrainingLoop returned exit code $LASTEXITCODE"
    }
}

function Read-LoopArtifacts {
    param([string] $LoopRoot)
    $snapshotRoot = Join-Path $LoopRoot ("runs\" + $RunId + "\live-snapshots")
    $queuePath = Join-Path $snapshotRoot "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotRoot "execution-0001.json"
    $beforePath = Join-Path $snapshotRoot "before-snapshot-0001.json"
    $afterPath = Join-Path $snapshotRoot "after-snapshot-0001.json"
    foreach ($path in @($queuePath, $executionPath, $beforePath, $afterPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing recovery loop artifact: $path"
        }
    }
    return [PSCustomObject]@{
        Queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
        Execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
        Before = Get-Content -LiteralPath $beforePath -Raw | ConvertFrom-Json
        After = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
        QueuePath = $queuePath
        ExecutionPath = $executionPath
        BeforePath = $beforePath
        AfterPath = $afterPath
    }
}

function Assert-SingleVerifiedOption {
    param($Artifacts, [string] $ExpectedOptionId)
    $items = @($Artifacts.Queue.items)
    if ($items.Count -ne 1 -or $items[0].option_id -ne $ExpectedOptionId) {
        throw "Expected exactly one $ExpectedOptionId queue item; observed $($items.option_id -join ',')"
    }
    if ($Artifacts.Execution.option_id -ne $ExpectedOptionId -or
        $Artifacts.Execution.status -ne "applied" -or
        $Artifacts.Execution.primitive_verification_status -ne "verified") {
        throw "$ExpectedOptionId did not execute as applied/verified"
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
foreach ($port in @($BackendPort, 8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening; refusing to attach to an unknown process."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running; refusing to touch an existing game process."
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$routeLoopRoot = Join-Path $runDirectory "route-loop"
$sleepLoopRoot = Join-Path $runDirectory "sleep-loop"
$trainingOutputDirectory = Join-Path $runDirectory "training-output"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

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
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout -RedirectStandardError $backendStderr -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-JsonHealth -Url "$executorUrl/health" -TimeoutSeconds 30 | Out-Null
    $initial = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    if ((Read-FieldValue $initial "player" "location_id") -ne "FarmHouse") {
        throw "Cross-map recovery setup requires the isolated save to start in FarmHouse."
    }

    $connector = Find-FarmHouseExitConnector $initial
    if ($null -eq $connector) { throw "FarmHouse to Farm connector is not transparent in the initial snapshot." }
    $exitRequest = New-ExecutionRequest "$RunId.setup.exit_house" "executor.traverse_connector" $initial.state_hash $savesPath
    $exitRequest.target_tile_x = [int]$connector.x
    $exitRequest.target_tile_y = [int]$connector.y
    $exitRequest.connector_kind = "warp"
    $exitRequest.expected_target_location = "Farm"
    $exitRequest.expected_arrival_tile_x = [int]$connector.target_x
    $exitRequest.expected_arrival_tile_y = [int]$connector.target_y
    $exitResult = Invoke-JsonPost "$executorUrl/api/v1/training/execute" $exitRequest
    if ($exitResult.status -ne "applied" -or $exitResult.primitive_verification_status -ne "verified") {
        throw "Failed to construct Farm recovery start through the native connector."
    }

    $outside = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    if ((Read-FieldValue $outside "player" "location_id") -ne "Farm") { throw "Native setup connector did not arrive at Farm." }
    $timeRequest = New-ExecutionRequest "$RunId.setup.advance_time" "debug.advance_time_to" $outside.state_hash $savesPath
    $timeRequest.target_time = 2200
    $timeResult = Invoke-JsonPost "$executorUrl/api/v1/training/execute" $timeRequest
    if ($timeResult.status -ne "applied" -or $timeResult.primitive_verification_status -ne "verified") {
        throw "Failed to advance the isolated fixture to 2200."
    }
    $beforeRoute = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30

    Invoke-RecoveryPlanIteration $routeLoopRoot $backendUrl $snapshotUrl $executorUrl $savesPath
    $routeArtifacts = Read-LoopArtifacts $routeLoopRoot
    Assert-SingleVerifiedOption $routeArtifacts "executor.traverse_connector"
    if ((Read-FieldValue $routeArtifacts.After "player" "location_id") -ne "FarmHouse") {
        throw "Recovery route did not arrive at FarmHouse."
    }

    Invoke-RecoveryPlanIteration $sleepLoopRoot $backendUrl $snapshotUrl $executorUrl $savesPath
    $sleepArtifacts = Read-LoopArtifacts $sleepLoopRoot
    Assert-SingleVerifiedOption $sleepArtifacts "executor.sleep"
    $startDay = [int](Read-FieldValue $beforeRoute "time" "day")
    $endDay = [int](Read-FieldValue $sleepArtifacts.After "time" "day")
    if ($startDay -eq $endDay) { throw "Recovery sleep completed without a day transition." }

    Write-JsonFile (Join-Path $runDirectory "setup-exit-request.json") $exitRequest
    Write-JsonFile (Join-Path $runDirectory "setup-exit-result.json") $exitResult
    Write-JsonFile (Join-Path $runDirectory "setup-time-result.json") $timeResult
    Write-JsonFile (Join-Path $runDirectory "before-recovery.json") $beforeRoute
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        setup_location = Read-FieldValue $beforeRoute "player" "location_id"
        setup_time = Read-FieldValue $beforeRoute "time" "time"
        route_option_id = $routeArtifacts.Execution.option_id
        route_status = $routeArtifacts.Execution.status
        route_after_location = Read-FieldValue $routeArtifacts.After "player" "location_id"
        sleep_option_id = $sleepArtifacts.Execution.option_id
        sleep_status = $sleepArtifacts.Execution.status
        start_day = $startDay
        end_day = $endDay
        final_location = Read-FieldValue $sleepArtifacts.After "player" "location_id"
        route_before_state_hash = $routeArtifacts.Before.state_hash
        route_after_state_hash = $routeArtifacts.After.state_hash
        sleep_before_state_hash = $sleepArtifacts.Before.state_hash
        sleep_after_state_hash = $sleepArtifacts.After.state_hash
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) { Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue }
        else { Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value }
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) { Stop-Process -Id $gameProcess.Id -Force }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) { Stop-Process -Id $backendProcess.Id -Force }
}
