param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-artifact-spot-daily-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-artifact-spot-daily-plan",
    [int] $BackendPort = 5134,
    [int] $StartupTimeoutSeconds = 180,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([Parameter(Mandatory = $true)] [string] $Path, [Parameter(Mandatory = $true)] $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([Parameter(Mandatory = $true)] [string] $Url, [Parameter(Mandatory = $true)] $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 64
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw {
    param([Parameter(Mandatory = $true)] [string] $Url, [Parameter(Mandatory = $true)] [string] $Json, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $Json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([Parameter(Mandatory = $true)] [string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([Parameter(Mandatory = $true)] [string] $Url, [Parameter(Mandatory = $true)] [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
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
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $saveReady = $snapshot.save_id.status -in @("available", "derived")
            $timeReady = $snapshot.in_game_time.status -in @("available", "derived")
            $locationReady = $null -ne (Read-FieldValue $snapshot "player" "location_id")
            $objectsReady = $null -ne $snapshot.state.current_location.objects -and
                $snapshot.state.current_location.objects.status -in @("available", "derived")
            $lastStatus = "save=$saveReady;time=$timeReady;location=$locationReady;objects=$objectsReady;completeness=$($snapshot.completeness)"
            if ($saveReady -and $timeReady -and $locationReady -and $objectsReady) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a loaded isolated world. Last status: $lastStatus"
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

function Find-TargetArtifactSpot {
    param($Snapshot)
    return @(Read-FieldValue $Snapshot "current_location" "objects") |
        Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY -and
            [string]$_.qualified_item_id -eq "(O)590" -and
            [string]$_.clear_kind -eq "artifact_spot"
        } |
        Select-Object -First 1
}

function Wait-ArtifactSpotSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $spot = Find-TargetArtifactSpot -Snapshot $snapshot
            $locationId = [string](Read-FieldValue $snapshot "player" "location_id")
            $lastStatus = "location=$locationId;spot=$($null -ne $spot);projection=$([string]$spot.clear_obstacle_executor_status);output=$([string]$spot.clear_output_projection_status);completeness=$($snapshot.completeness)"
            if ($null -ne $spot -and
                [string]$spot.clear_obstacle_executor_status -eq "ready" -and
                [string]$spot.clear_output_projection_status -eq "exact") {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a transparent native artifact spot. Last status: $lastStatus"
}

function Read-QueueParameter {
    param($QueueItem, [string] $Name)
    foreach ($parameter in @($QueueItem.normalized_command.parameters)) {
        if ([string]$parameter.name -eq $Name) {
            return [string]$parameter.value
        }
    }
    return ""
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$snapshotPath = Join-Path $runDirectory "artifact-spot-snapshot.json"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
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
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-artifact-spot-daily-plan"
        queue_item_id = "runtime-artifact-spot-daily-plan.setup"
        before_state_hash = [string]$initialSnapshot.state_hash
        option_id = "debug.setup_clear_obstacle"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        max_crops = 1
        rule_key = "artifact_spot"
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Artifact-spot fixture setup was not applied and verified."
    }

    Wait-ArtifactSpotSnapshot -Url $snapshotUrl -TimeoutSeconds 60 | Out-Null
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri $snapshotUrl `
        -Headers @{ "Accept" = "application/json" } `
        -OutFile $snapshotPath `
        -TimeoutSec 60
    $snapshotJson = Get-Content -LiteralPath $snapshotPath -Raw -Encoding UTF8
    $readySnapshot = $snapshotJson | ConvertFrom-Json
    $targetSpot = Find-TargetArtifactSpot -Snapshot $readySnapshot
    if ($null -eq $targetSpot -or
        [string]$targetSpot.clear_obstacle_executor_status -ne "ready" -or
        [string]$targetSpot.clear_output_projection_status -ne "exact") {
        throw "The raw snapshot no longer contains the exact ready native artifact spot."
    }
    Invoke-JsonPostRaw -Url "$backendUrl/api/v1/snapshots" -Json $snapshotJson -TimeoutSeconds 30 | Out-Null

    $availabilityRequest = [ordered]@{
        state_hash = [string]$readySnapshot.state_hash
        candidate_option_ids = @("foraging.excavate_artifact_spots")
        candidates = @()
        include_executor_calibration_options = $false
    }
    $availability = Invoke-JsonPost -Url "$backendUrl/api/v1/planner/options/availability" -Body $availabilityRequest -TimeoutSeconds 30
    Write-JsonFile (Join-Path $runDirectory "availability.json") $availability
    $targetCandidates = @(
        $availability.options |
        Where-Object { [string]$_.option_id -eq "foraging.excavate_artifact_spots" } |
        ForEach-Object { $_.event_candidates } |
        Where-Object {
            [string]$_.kind -eq "clear_obstacle_tile" -and
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY -and
            [bool]$_.available
        })
    if ($targetCandidates.Count -ne 1) {
        throw "Expected one available high-level artifact-spot candidate; found $($targetCandidates.Count)."
    }
    $targetCandidateId = [string]$targetCandidates[0].candidate_id

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url "http://127.0.0.1:8767" `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --skip-training `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "foraging.excavate_artifact_spots" `
        --daily-plan-candidate-kind "clear_obstacle_tile" `
        --daily-plan-candidate-id $targetCandidateId `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) {
        throw "LiveTrainingLoop returned exit code $LASTEXITCODE."
    }

    $runRoot = Join-Path $loopRoot (Join-Path "runs" $RunId)
    $reportPath = Join-Path $runRoot "live-training-loop-report.json"
    $dailyPlanPath = Join-Path $runRoot "live-snapshots\daily-plan-response-0001.json"
    $queuePath = Join-Path $runRoot "live-snapshots\compiled-queue-0001.json"
    $executionPath = Join-Path $runRoot "live-snapshots\execution-0001.json"
    foreach ($requiredPath in @($reportPath, $dailyPlanPath, $queuePath, $executionPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Expected high-level runtime artifact is missing: $requiredPath"
        }
    }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $clearItem = @($queue.items) |
        Where-Object {
            [string]$_.option_id -eq "executor.clear_obstacle" -and
            [int](Read-QueueParameter $_ "target_tile_x") -eq $TargetTileX -and
            [int](Read-QueueParameter $_ "target_tile_y") -eq $TargetTileY -and
            [string](Read-QueueParameter $_ "required_tool_kind") -eq "hoe"
        } |
        Select-Object -First 1
    if ($null -eq $clearItem) {
        throw "High-level DailyPlan did not compile the exact artifact spot into executor.clear_obstacle."
    }
    $clearExecution = @($execution.step_results) |
        Where-Object {
            [string]$_.queue_item_id -eq [string]$clearItem.queue_item_id -and
            [string]$_.option_id -eq "executor.clear_obstacle" -and
            [string]$_.status -eq "applied" -and
            [string]$_.primitive_verification_status -eq "verified"
        } |
        Select-Object -First 1
    if ($null -eq $clearExecution) {
        throw "The compiled artifact-spot executor item did not produce an applied/verified native receipt."
    }

    $afterSnapshot = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 30
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $afterSnapshot
    if ($null -ne (Find-TargetArtifactSpot -Snapshot $afterSnapshot)) {
        throw "The native artifact spot remains present after the verified execution."
    }

    $summary = [ordered]@{
        status = "passed"
        evidence_id = "EVD-334"
        run_id = $RunId
        save_slot = $SaveSlot
        source_option_id = "foraging.excavate_artifact_spots"
        selected_candidate_id = $targetCandidateId
        selected_candidate_kind = "clear_obstacle_tile"
        compiled_option_id = [string]$clearItem.option_id
        compiled_queue_item_id = [string]$clearItem.queue_item_id
        target_location = [string](Read-FieldValue $readySnapshot "player" "location_id")
        target_tile = "$TargetTileX,$TargetTileY"
        target_qualified_item_id = [string]$targetSpot.qualified_item_id
        projected_output_items_json = [string]$targetSpot.clear_output_items_json
        projected_artifact_spots_dug_after = [int]$targetSpot.artifact_spots_dug_expected_after
        daily_plan_status = [string]$dailyPlan.action_queue.status
        action_queue_status = [string]$queue.status
        execution_status = [string]$clearExecution.status
        execution_verification = [string]$clearExecution.primitive_verification_status
        execution_reasons = @($clearExecution.primitive_verification_reasons)
        target_present_after = $false
        bridge_state_hash_ready = [string]$readySnapshot.state_hash
        bridge_state_hash_after = [string]$afterSnapshot.state_hash
        executor_health = $executorHealth
        report_path = $reportPath
        daily_plan_path = $dailyPlanPath
        queue_path = $queuePath
        execution_path = $executionPath
        smapi_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
