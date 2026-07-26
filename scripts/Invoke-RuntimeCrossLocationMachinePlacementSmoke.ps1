param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-cross-location-machine-placement-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-cross-location-machine-placement-smoke",
    [int] $BackendPort = 5131,
    [int] $StartupTimeoutSeconds = 180,
    [string] $TargetLocationId = "FarmHouse",
    [int] $FixtureTileX = 60,
    [int] $FixtureTileY = 15,
    [string] $QualifiedItemId = "(BC)12",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [string] $Url,
        $Body,
        [int] $TimeoutSeconds = 180
    )
    $json = $Body | ConvertTo-Json -Depth 96
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param(
        [string] $Url,
        [int] $TimeoutSeconds = 30
    )
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Health {
    param(
        [string] $Url,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($health.status -eq "ok") {
                return $health
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
    param(
        [string] $Url,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 45
            $saveReady =
                $snapshot.save_id.status -in @("available", "derived")
            $locationReady =
                $snapshot.state.player.location_id.status -in @(
                    "available",
                    "derived"
                )
            $placementReady =
                $snapshot.state.player.machine_placement.status -in @(
                    "available",
                    "derived"
                ) -and
                -not ([string](
                    $snapshot.state.player.machine_placement.value.
                        projection_status
                )).StartsWith(
                    "unavailable",
                    [StringComparison]::Ordinal
                )
            $routeReady =
                $snapshot.state.locations.route_graph.status -in @(
                    "available",
                    "derived"
                ) -and
                $snapshot.state.locations.route_connectors.status -in @(
                    "available",
                    "derived"
                )
            $lastStatus =
                "save=$saveReady;location=$locationReady;" +
                "placement=$placementReady;route=$routeReady"
            if ($saveReady -and
                $locationReady -and
                $placementReady -and
                $routeReady) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw (
        "Timed out waiting for cross-location machine snapshot. " +
        "Last status: $lastStatus"
    )
}

function Candidate-Parameter {
    param($Candidate, [string] $Name)
    $parameter = @($Candidate.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -First 1
    if ($null -eq $parameter) {
        return ""
    }

    return [string]$parameter.value
}

function Queue-Parameter {
    param($QueueItem, [string] $Name)
    $parameter = @($QueueItem.normalized_command.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -First 1
    if ($null -eq $parameter) {
        return ""
    }

    return [string]$parameter.value
}

function Find-PlacementRow {
    param(
        $Snapshot,
        [int] $SlotIndex,
        [string] $ItemId
    )
    return @(
        $Snapshot.state.player.machine_placement.value.rows
    ) | Where-Object {
        [int]$_.inventory_slot_index -eq $SlotIndex -and
        [string]$_.qualified_item_id -eq $ItemId
    } | Select-Object -First 1
}

function Find-Machine {
    param(
        $Snapshot,
        [string] $LocationId,
        [int] $X,
        [int] $Y
    )
    return @($Snapshot.state.farm.machines.value) |
        Where-Object {
            [string]$_.location_id -eq $LocationId -and
            [int]$_.tile_x -eq $X -and
            [int]$_.tile_y -eq $Y
        } |
        Select-Object -First 1
}

function Read-SetupSlot {
    param($SetupResult)
    foreach ($text in @(
        @($SetupResult.primitive_verification_reasons) +
        @([string]$SetupResult.observed_effect)
    )) {
        if ([string]$text -match "inventory_slot_index=(-?\d+)") {
            return [int]$Matches[1]
        }
    }
    return -1
}

function Invoke-LiveLoop {
    param(
        [string] $StageRoot,
        [string] $SnapshotPath,
        [string] $BackendUrl,
        [string] $SnapshotUrl,
        [string] $ExecutorUrl,
        [string] $SavesPath,
        [string] $CandidateId,
        [string] $LogPath
    )
    $loopProject = Join-Path $ProjectRoot (
        "tools\StardewAI.LiveTrainingLoop\" +
        "StardewAI.LiveTrainingLoop.csproj"
    )
    & dotnet run --no-restore --project $loopProject -- `
        --root $StageRoot `
        --backend-url $BackendUrl `
        --bridge-snapshot-url $SnapshotUrl `
        --executor-url $ExecutorUrl `
        --snapshot-file $SnapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $SavesPath `
        --iterations 2 `
        --required-verified-actions 2 `
        --skip-training `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "farm.process_machines" `
        --daily-plan-candidate-kind "route_connector_tile" `
        --daily-plan-candidate-id $CandidateId `
        --after-snapshot-wait-ms 750 `
        --continue-after-blocked-queue-items *>&1 |
        Set-Content -LiteralPath $LogPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw (
            "LiveTrainingLoop cross-location run failed with exit " +
            "$LASTEXITCODE. See $LogPath"
        )
    }
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=training_machine"
$executorUrl = "http://127.0.0.1:8767"
$backendUrl = "http://127.0.0.1:$BackendPort"
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

$runDirectory = Join-Path $ProjectRoot (
    Join-Path $OutputDirectory $RunId
)
$loopRoot = Join-Path $runDirectory "live-loop"
$ledgerDirectory = Join-Path $runDirectory "strategy-ledger"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
$sourceSnapshotPath = Join-Path $runDirectory "source-snapshot.json"
New-Item -ItemType Directory -Force -Path $runDirectory |
    Out-Null
New-Item -ItemType Directory -Force -Path $ledgerDirectory |
    Out-Null

& (Join-Path $ProjectRoot `
    "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot |
    Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot |
    Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH =
        $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID =
        $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE =
        $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_STRATEGY_LEDGER_DIR =
        $env:STARDEWAI_STRATEGY_LEDGER_DIR
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
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
    $env:STARDEWAI_STRATEGY_LEDGER_DIR = $ledgerDirectory
    $env:ASPNETCORE_URLS = $backendUrl
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @(
            "run",
            "--no-restore",
            "--project",
            (Join-Path $ProjectRoot `
                "src\StardewAI.Backend\StardewAI.Backend.csproj"),
            "--no-launch-profile"
        ) `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-Health -Url "$backendUrl/health" -TimeoutSeconds 60 |
        Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -PassThru
    $executorHealth = Wait-Health `
        -Url "$executorUrl/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $initialSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "cross-location-machine-placement-fixture"
        queue_item_id =
            "cross-location-machine-placement-fixture.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_machine_placement_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $FixtureTileX
        target_tile_y = $FixtureTileY
        qualified_item_id = $QualifiedItemId
    }
    $setupResult = Invoke-JsonPost `
        -Url "$executorUrl/api/v1/training/execute" `
        -Body $setupRequest
    Write-JsonFile `
        (Join-Path $runDirectory "setup-result.json") `
        $setupResult
    if ($setupResult.status -ne "applied" -or
        $setupResult.primitive_verification_status -ne "verified") {
        throw "Cross-location machine fixture setup failed."
    }

    Start-Sleep -Milliseconds 750
    $sourceSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl -TimeoutSeconds 60
    $slotIndex = Read-SetupSlot -SetupResult $setupResult
    $placementRow = Find-PlacementRow `
        -Snapshot $sourceSnapshot `
        -SlotIndex $slotIndex `
        -ItemId $QualifiedItemId
    $sourceLocationId =
        [string]$sourceSnapshot.state.player.location_id.value
    if ($slotIndex -lt 0 -or
        $null -eq $placementRow -or
        $sourceLocationId -ne "Farm") {
        throw (
            "Fixture did not expose the expected Farm inventory machine: " +
            "slot=$slotIndex;location=$sourceLocationId"
        )
    }
    $stackBefore = [int]$placementRow.stack
    Write-JsonFile $sourceSnapshotPath $sourceSnapshot

    $ingest = Invoke-JsonPost `
        -Url "$backendUrl/api/v1/snapshots" `
        -Body $sourceSnapshot
    $availability = Invoke-JsonPost `
        -Url "$backendUrl/api/v1/planner/options/availability" `
        -Body ([ordered]@{
            state_hash = $sourceSnapshot.state_hash
            candidate_option_ids = @("farm.process_machines")
            candidates = @()
            include_executor_calibration_options = $true
        })
    Write-JsonFile `
        (Join-Path $runDirectory "source-availability.json") `
        $availability
    $remoteCandidate = @(
        $availability.options |
            Where-Object {
                $_.option_id -eq "farm.process_machines"
            } |
            ForEach-Object { $_.event_candidates } |
            Where-Object {
                $_.kind -eq "route_connector_tile" -and
                [bool]$_.available -and
                (Candidate-Parameter `
                    -Candidate $_ `
                    -Name "continuation.machine_location_id") -eq
                    $TargetLocationId -and
                (Candidate-Parameter `
                    -Candidate $_ `
                    -Name "continuation.machine_inventory_slot_index") -eq
                    [string]$slotIndex -and
                (Candidate-Parameter `
                    -Candidate $_ `
                    -Name "continuation.machine_qualified_item_id") -eq
                    $QualifiedItemId
            }
    ) | Select-Object -First 1
    if ($null -eq $remoteCandidate) {
        throw (
            "No typed remote machine placement candidate for " +
            "$TargetLocationId."
        )
    }
    $candidateId = [string]$remoteCandidate.candidate_id

    Invoke-LiveLoop `
        -StageRoot $loopRoot `
        -SnapshotPath $sourceSnapshotPath `
        -BackendUrl $backendUrl `
        -SnapshotUrl $snapshotUrl `
        -ExecutorUrl $executorUrl `
        -SavesPath $savesPath `
        -CandidateId $candidateId `
        -LogPath (Join-Path $runDirectory "live-loop.log")

    $artifactRoot = Join-Path $loopRoot (
        "runs\$RunId\live-snapshots"
    )
    $firstQueue = Get-Content -LiteralPath (
        Join-Path $artifactRoot "compiled-queue-0001.json"
    ) -Raw | ConvertFrom-Json
    $firstExecution = Get-Content -LiteralPath (
        Join-Path $artifactRoot "execution-0001.json"
    ) -Raw | ConvertFrom-Json
    $secondQueue = Get-Content -LiteralPath (
        Join-Path $artifactRoot "compiled-queue-0002.json"
    ) -Raw | ConvertFrom-Json
    $secondExecution = Get-Content -LiteralPath (
        Join-Path $artifactRoot "execution-0002.json"
    ) -Raw | ConvertFrom-Json
    $report = Get-Content -LiteralPath (
        Join-Path $loopRoot (
            "runs\$RunId\live-training-loop-report.json"
        )
    ) -Raw | ConvertFrom-Json

    $routeItem = @($firstQueue.items) |
        Where-Object {
            $_.option_id -eq "executor.traverse_connector"
        } |
        Select-Object -First 1
    $routeExecution = @($firstExecution.step_results) |
        Where-Object {
            $_.option_id -eq "executor.traverse_connector"
        } |
        Select-Object -First 1
    $placeExecution = @($secondExecution.step_results) |
        Where-Object {
            $_.option_id -eq "executor.place_machine"
        } |
        Select-Object -Last 1
    $placeItem = if ($null -eq $placeExecution) {
        $null
    }
    else {
        $placeExecution.effective_queue_item
    }
    if ($null -eq $routeItem -or
        $null -eq $routeExecution -or
        $routeExecution.status -ne "applied" -or
        $routeExecution.primitive_verification_status -ne "verified" -or
        (Queue-Parameter `
            -QueueItem $routeItem `
            -Name "continuation.machine_location_id") -ne
            $TargetLocationId -or
        (Queue-Parameter `
            -QueueItem $routeItem `
            -Name "continuation.machine_inventory_slot_index") -ne
            [string]$slotIndex -or
        (Queue-Parameter `
            -QueueItem $routeItem `
            -Name "continuation.machine_qualified_item_id") -ne
            $QualifiedItemId) {
        throw "Initial connector did not preserve machine placement identity."
    }
    if ($null -eq $placeExecution -or
        $placeExecution.status -ne "applied" -or
        $placeExecution.primitive_verification_status -ne "verified" -or
        -not [bool]$secondExecution.objective_continuation_completed) {
        throw (
            "Fresh-snapshot target-map native placement did not complete."
        )
    }

    $targetX = [int](Queue-Parameter `
        -QueueItem $placeItem -Name "target_tile_x")
    $targetY = [int](Queue-Parameter `
        -QueueItem $placeItem -Name "target_tile_y")
    $finalSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl -TimeoutSeconds 60
    Write-JsonFile `
        (Join-Path $runDirectory "final-snapshot.json") `
        $finalSnapshot
    $targetMachine = Find-Machine `
        -Snapshot $finalSnapshot `
        -LocationId $TargetLocationId `
        -X $targetX -Y $targetY
    $afterRow = Find-PlacementRow `
        -Snapshot $finalSnapshot `
        -SlotIndex $slotIndex `
        -ItemId $QualifiedItemId
    $stackAfter = if ($null -eq $afterRow) {
        0
    }
    else {
        [int]$afterRow.stack
    }
    $finalLocationId =
        [string]$finalSnapshot.state.player.location_id.value
    $passed =
        $null -ne $targetMachine -and
        [string]$targetMachine.qualified_item_id -eq
            $QualifiedItemId -and
        $finalLocationId -eq $TargetLocationId -and
        $stackAfter -eq ($stackBefore - 1) -and
        [int]$report.verified_actions -eq 2
    if (-not $passed) {
        throw "Cross-location machine placement final state failed."
    }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        candidate_id = $candidateId
        source_location_id = $sourceLocationId
        target_location_id = $TargetLocationId
        first_connector_kind = Queue-Parameter `
            -QueueItem $routeItem -Name "connector_kind"
        first_connector_expected_target = Queue-Parameter `
            -QueueItem $routeItem `
            -Name "expected_target_location"
        exact_target_tile = "$targetX,$targetY"
        machine_qualified_item_id = $QualifiedItemId
        inventory_slot_index = $slotIndex
        inventory_stack_before = $stackBefore
        inventory_stack_after = $stackAfter
        connector_verified = $true
        continuation_identity_preserved = $true
        fresh_snapshot_native_place_verified = $true
        objective_continuation_completed =
            [bool]$secondExecution.objective_continuation_completed
        target_machine_present_after = $null -ne $targetMachine
        source_state_hash = $sourceSnapshot.state_hash
        final_state_hash = $finalSnapshot.state_hash
        state_hash_changed =
            $sourceSnapshot.state_hash -ne
                $finalSnapshot.state_hash
        verified_outer_iterations = [int]$report.verified_actions
        backend_ingest_state_hash = $ingest.state_hash
        executor_health = $executorHealth
        game_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
    }
    Write-JsonFile `
        (Join-Path $runDirectory "summary.json") `
        $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id `
            -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and
        $gameProcess -and
        -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id `
            -Force -ErrorAction SilentlyContinue
    }
}
