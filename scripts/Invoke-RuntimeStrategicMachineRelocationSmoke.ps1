param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-strategic-machine-relocation-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-strategic-machine-relocation-smoke",
    [int] $BackendPort = 5130,
    [int] $StartupTimeoutSeconds = 180,
    [int] $SourceTileX = 56,
    [int] $SourceTileY = 15,
    [int] $PeerOneTileX = 50,
    [int] $PeerOneTileY = 15,
    [int] $PeerTwoTileX = 52,
    [int] $PeerTwoTileY = 15,
    [string] $TargetLocationId = "Farm",
    [int] $TargetPeerOneTileX = 29,
    [int] $TargetPeerOneTileY = 29,
    [int] $TargetPeerTwoTileX = 30,
    [int] $TargetPeerTwoTileY = 29,
    [string] $MachineQualifiedItemId = "(BC)12",
    [string] $MachineItemId = "12",
    [string] $InputQualifiedItemId = "(O)262",
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $saveReadable = $snapshot.save_id.status -in @(
                "available",
                "derived"
            )
            $machinesReadable =
                $snapshot.state.farm.machines.status -eq "available"
            $placementReadable =
                $snapshot.state.player.machine_placement.status -eq
                    "available"
            $lastStatus =
                "save=$($snapshot.save_id.status)" +
                ";machines=$($snapshot.state.farm.machines.status)" +
                ";placement=" +
                "$($snapshot.state.player.machine_placement.status)"
            if ($saveReadable -and
                $machinesReadable -and
                $placementReadable) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw (
        "Timed out waiting for strategic machine snapshot. " +
        "Last status: $lastStatus"
    )
}

function Find-Machine {
    param(
        $Snapshot,
        [int] $X,
        [int] $Y,
        [string] $LocationId = "Farm"
    )
    foreach ($machine in @($Snapshot.state.farm.machines.value)) {
        if ([string]$machine.location_id -eq $LocationId -and
            [int]$machine.tile_x -eq $X -and
            [int]$machine.tile_y -eq $Y) {
            return $machine
        }
    }
    return $null
}

function Find-LoadableInput {
    param(
        $Machine,
        [string] $QualifiedItemId
    )
    foreach ($input in @($Machine.loadable_inputs)) {
        if ([string]$input.qualified_item_id -eq $QualifiedItemId) {
            return $input
        }
    }
    return $null
}

function Inventory-Count {
    param(
        $Snapshot,
        [string] $QualifiedItemId
    )
    $count = 0
    foreach ($item in @($Snapshot.state.player.inventory.value)) {
        if ([string]$item.qualified_item_id -eq $QualifiedItemId) {
            $count += [int]$item.stack
        }
    }
    return $count
}

function Find-Debris {
    param(
        $Snapshot,
        [string] $QualifiedItemId
    )
    foreach ($debris in @($Snapshot.state.farm.debris.value)) {
        if ([string]$debris.qualified_item_id -eq
                $QualifiedItemId -and
            @($debris.chunks).Count -gt 0) {
            return $debris
        }
    }
    return $null
}

function Candidate-Parameter {
    param(
        $Candidate,
        [string] $Name
    )
    foreach ($parameter in @($Candidate.parameters)) {
        if ([string]$parameter.name -eq $Name) {
            return [string]$parameter.value
        }
    }
    return ""
}

function Queue-Parameter {
    param(
        $QueueItem,
        [string] $Name
    )
    foreach ($parameter in @(
        $QueueItem.normalized_command.parameters
    )) {
        if ([string]$parameter.name -eq $Name) {
            return [string]$parameter.value
        }
    }
    return ""
}

function Invoke-SetupMachine {
    param(
        [string] $ExecutorUrl,
        [string] $SavesPath,
        [string] $ExecutionRunId,
        [string] $Step,
        [int] $X,
        [int] $Y,
        [string] $LocationId = "Farm"
    )
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $ExecutionRunId
        queue_id = "strategic-machine-relocation-fixture"
        queue_item_id =
            "strategic-machine-relocation-fixture.$Step"
        before_state_hash = "fixture"
        option_id = "debug.setup_machine_input_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $X
        target_tile_y = $Y
        location_id = $LocationId
        expected_shop_id = $MachineItemId
        qualified_item_id = $InputQualifiedItemId
        quantity = 1
    }
    return Invoke-JsonPost `
        -Url "$ExecutorUrl/api/v1/training/execute" `
        -Body $request
}

function Invoke-SetupIdleMachine {
    param(
        [string] $ExecutorUrl,
        [string] $SavesPath,
        [string] $ExecutionRunId,
        [string] $Step,
        [int] $X,
        [int] $Y,
        [string] $LocationId
    )
    return Invoke-JsonPost `
        -Url "$ExecutorUrl/api/v1/training/execute" `
        -Body ([ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $ExecutionRunId
            queue_id = "strategic-machine-relocation-fixture"
            queue_item_id =
                "strategic-machine-relocation-fixture.$Step"
            before_state_hash = "fixture"
            option_id = "debug.setup_idle_machine_target"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $SavesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $X
            target_tile_y = $Y
            location_id = $LocationId
            expected_shop_id = $MachineItemId
        })
}

function Invoke-LoadMachine {
    param(
        [string] $ExecutorUrl,
        [string] $SnapshotUrl,
        [string] $SavesPath,
        [string] $ExecutionRunId,
        [string] $Step,
        [int] $X,
        [int] $Y,
        [string] $LocationId = "Farm"
    )
    $snapshot = Wait-WorldSnapshot `
        -Url $SnapshotUrl -TimeoutSeconds 60
    $machine = Find-Machine -Snapshot $snapshot -X $X -Y $Y `
        -LocationId $LocationId
    $input = Find-LoadableInput `
        -Machine $machine `
        -QualifiedItemId $InputQualifiedItemId
    if ($null -eq $machine -or $null -eq $input) {
        throw "Fixture machine at $X,$Y had no transparent input."
    }
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $ExecutionRunId
        queue_id = "strategic-machine-relocation-fixture"
        queue_item_id =
            "strategic-machine-relocation-fixture.$Step"
        before_state_hash = $snapshot.state_hash
        option_id = "executor.load_machine_input"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $X
        target_tile_y = $Y
        input_slot_index = [int]$input.slot_index
        location_id = $LocationId
        qualified_item_id = $InputQualifiedItemId
    }
    return Invoke-JsonPost `
        -Url "$ExecutorUrl/api/v1/training/execute" `
        -Body $request
}

function Wait-FixtureReady {
    param(
        [string] $SnapshotUrl,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot `
            -Url $SnapshotUrl -TimeoutSeconds 30
        $source = Find-Machine `
            -Snapshot $snapshot -X $SourceTileX -Y $SourceTileY
        $peerOne = Find-Machine `
            -Snapshot $snapshot -X $PeerOneTileX -Y $PeerOneTileY
        $peerTwo = Find-Machine `
            -Snapshot $snapshot -X $PeerTwoTileX -Y $PeerTwoTileY
        $targetPeerOne = Find-Machine `
            -Snapshot $snapshot `
            -X $TargetPeerOneTileX -Y $TargetPeerOneTileY `
            -LocationId $TargetLocationId
        $targetPeerTwo = Find-Machine `
            -Snapshot $snapshot `
            -X $TargetPeerTwoTileX -Y $TargetPeerTwoTileY `
            -LocationId $TargetLocationId
        $inputCount = Inventory-Count `
            -Snapshot $snapshot `
            -QualifiedItemId $InputQualifiedItemId
        $sourceReady =
            $null -ne $source -and
            [bool]$source.removal_safe_now -and
            [string]$source.removal_status -eq
                "safe_idle_native_pickaxe"
        $peerOneBusy =
            $null -ne $peerOne -and
            [int]$peerOne.minutes_until_ready -gt 0
        $peerTwoBusy =
            $null -ne $peerTwo -and
            [int]$peerTwo.minutes_until_ready -gt 0
        $targetPeerOneBusy =
            $null -ne $targetPeerOne -and
            [int]$targetPeerOne.minutes_until_ready -gt 0
        $targetPeerTwoBusy =
            $null -ne $targetPeerTwo -and
            [int]$targetPeerTwo.minutes_until_ready -gt 0
        $targetPeerOnePresent = $null -ne $targetPeerOne
        $targetPeerTwoPresent = $null -ne $targetPeerTwo
        $peerCondition = if ($TargetLocationId -eq "Farm") {
            $peerOneBusy -and $peerTwoBusy
        }
        else {
            $peerOneBusy -and
                $targetPeerOnePresent -and
                $targetPeerTwoPresent
        }
        $lastStatus =
            "source_ready=$sourceReady" +
            ";peer_one_busy=$peerOneBusy" +
            ";peer_two_busy=$peerTwoBusy" +
            ";target_peer_one_busy=$targetPeerOneBusy" +
            ";target_peer_two_busy=$targetPeerTwoBusy" +
            ";target_peer_one_present=$targetPeerOnePresent" +
            ";target_peer_two_present=$targetPeerTwoPresent" +
            ";input_count=$inputCount"
        if ($sourceReady -and
            $peerCondition -and
            $inputCount -eq 0) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Strategic fixture not ready. Last status: $lastStatus"
}

function Invoke-LiveStage {
    param(
        [string] $StageRoot,
        [string] $StageRunId,
        [string] $SnapshotPath,
        [string] $BackendUrl,
        [string] $SnapshotUrl,
        [string] $ExecutorUrl,
        [string] $SavesPath,
        [string] $CandidateKind,
        [string] $CandidateId = "",
        [string] $LogPath
    )
    $loopProject = Join-Path $ProjectRoot (
        "tools\StardewAI.LiveTrainingLoop\" +
        "StardewAI.LiveTrainingLoop.csproj"
    )
    $candidateIdArguments = @()
    if (-not [string]::IsNullOrWhiteSpace($CandidateId)) {
        $candidateIdArguments = @(
            "--daily-plan-candidate-id",
            $CandidateId
        )
    }
    & dotnet run --no-restore --project $loopProject -- `
        --root $StageRoot `
        --backend-url $BackendUrl `
        --bridge-snapshot-url $SnapshotUrl `
        --executor-url $ExecutorUrl `
        --snapshot-file $SnapshotPath `
        --no-manifest `
        --run-id $StageRunId `
        --save-isolation-path $SavesPath `
        --iterations 1 `
        --required-verified-actions 1 `
        --skip-training `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "farm.process_machines" `
        --daily-plan-candidate-kind $CandidateKind `
        @candidateIdArguments `
        --after-snapshot-wait-ms 750 `
        --continue-after-blocked-queue-items *>&1 |
        Set-Content -LiteralPath $LogPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw (
            "LiveTrainingLoop stage $StageRunId failed with exit " +
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
$ledgerDirectory = Join-Path $runDirectory "strategy-ledger"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
$sourceSnapshotPath = Join-Path $runDirectory "source-snapshot.json"
$recoveredSnapshotPath =
    Join-Path $runDirectory "recovered-snapshot.json"
$targetSnapshotPath =
    Join-Path $runDirectory "target-snapshot.json"
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
    Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds |
        Out-Null

    $fixtureResults = @()
    if ($TargetLocationId -ne "Farm") {
        $setupTargetPeerOne = Invoke-SetupIdleMachine `
            -ExecutorUrl $executorUrl `
            -SavesPath $savesPath `
            -ExecutionRunId $RunId `
            -Step "setup-target-peer-one" `
            -X $TargetPeerOneTileX -Y $TargetPeerOneTileY `
            -LocationId $TargetLocationId
        $setupTargetPeerTwo = Invoke-SetupIdleMachine `
            -ExecutorUrl $executorUrl `
            -SavesPath $savesPath `
            -ExecutionRunId $RunId `
            -Step "setup-target-peer-two" `
            -X $TargetPeerTwoTileX -Y $TargetPeerTwoTileY `
            -LocationId $TargetLocationId
        $fixtureResults += @(
            $setupTargetPeerOne,
            $setupTargetPeerTwo
        )
    }
    $setupSource = Invoke-SetupMachine `
        -ExecutorUrl $executorUrl `
        -SavesPath $savesPath `
        -ExecutionRunId $RunId `
        -Step "setup-source" `
        -X $SourceTileX -Y $SourceTileY
    $setupPeerOne = Invoke-SetupMachine `
        -ExecutorUrl $executorUrl `
        -SavesPath $savesPath `
        -ExecutionRunId $RunId `
        -Step "setup-peer-one" `
        -X $PeerOneTileX -Y $PeerOneTileY
    $loadPeerOne = Invoke-LoadMachine `
        -ExecutorUrl $executorUrl `
        -SnapshotUrl $snapshotUrl `
        -SavesPath $savesPath `
        -ExecutionRunId $RunId `
        -Step "load-peer-one" `
        -X $PeerOneTileX -Y $PeerOneTileY
    $fixtureResults += @(
        $setupSource,
        $setupPeerOne,
        $loadPeerOne
    )
    if ($TargetLocationId -eq "Farm") {
        $setupPeerTwo = Invoke-SetupMachine `
            -ExecutorUrl $executorUrl `
            -SavesPath $savesPath `
            -ExecutionRunId $RunId `
            -Step "setup-peer-two" `
            -X $PeerTwoTileX -Y $PeerTwoTileY
        $loadPeerTwo = Invoke-LoadMachine `
            -ExecutorUrl $executorUrl `
            -SnapshotUrl $snapshotUrl `
            -SavesPath $savesPath `
            -ExecutionRunId $RunId `
            -Step "load-peer-two" `
            -X $PeerTwoTileX -Y $PeerTwoTileY
        $fixtureResults += @(
            $setupPeerTwo,
            $loadPeerTwo
        )
    }
    foreach ($fixtureResult in $fixtureResults) {
        if ($fixtureResult.status -ne "applied" -or
            $fixtureResult.primitive_verification_status -ne
                "verified") {
            $fixtureReasons =
                @($fixtureResult.primitive_verification_reasons) `
                -join ","
            throw (
                "Strategic relocation fixture action failed: " +
                "$($fixtureResult.queue_item_id);" +
                "$($fixtureResult.option_id);" +
                "$(@($fixtureResult.block_reasons) -join ',');" +
                "$fixtureReasons;" +
                "$($fixtureResult.observed_effect)"
            )
        }
    }

    $sourceSnapshot = Wait-FixtureReady `
        -SnapshotUrl $snapshotUrl -TimeoutSeconds 90
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
    $relocationCandidate = @(
        $availability.options |
            Where-Object {
                $_.option_id -eq "farm.process_machines"
            } |
            ForEach-Object { $_.event_candidates } |
            Where-Object {
                $_.kind -eq "relocate_machine_item" -and
                [bool]$_.available -and
                (Candidate-Parameter `
                    -Candidate $_ `
                    -Name "relocation_target_location_id") -eq
                    $TargetLocationId
            }
    ) | Select-Object -First 1
    if ($null -eq $relocationCandidate) {
        throw "No positive strategic machine relocation candidate."
    }
    if ([int]$relocationCandidate.tile_x -ne $SourceTileX -or
        [int]$relocationCandidate.tile_y -ne $SourceTileY) {
        throw (
            "Planner selected unexpected relocation source: " +
            "$($relocationCandidate.tile_x)," +
            "$($relocationCandidate.tile_y)"
        )
    }
    $intentId = Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_intent_id"
    $targetX = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_tile_x")
    $targetY = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_tile_y")
    $netBenefit = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "layout_net_benefit_ticks")
    $targetSelectionPolicy = Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_selection_policy"
    $routeConnectorKind = Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_route_connector_kind"
    $routeConnectorCount = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_route_connector_count")
    $routeSegmentsJson = Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_route_segments_json"
    $routeSegments =
        if ([string]::IsNullOrWhiteSpace($routeSegmentsJson)) {
            @()
        }
        else {
            $routeSegmentsJson | ConvertFrom-Json
        }
    $routeEstimatedTicks = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_route_estimated_ticks")
    $timeEstimatePolicy = Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "layout_time_estimate_policy"
    $targetArrivalX = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_arrival_tile_x")
    $targetArrivalY = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_arrival_tile_y")
    $targetStandX = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_stand_tile_x")
    $targetStandY = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_stand_tile_y")
    $targetRouteDistanceTiles = [int](Candidate-Parameter `
        -Candidate $relocationCandidate `
        -Name "relocation_target_route_distance_tiles")
    if ([string]::IsNullOrWhiteSpace($intentId) -or
        $netBenefit -le 0) {
        throw "Strategic candidate lacked a positive typed intent."
    }
    if ($targetSelectionPolicy -ne
            "resolved_route_final_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked" -or
        $routeConnectorCount -lt 1 -or
        $routeSegments.Count -ne $routeConnectorCount -or
        [string]::IsNullOrWhiteSpace($routeConnectorKind) -or
        $routeEstimatedTicks -lt 0 -or
        $timeEstimatePolicy -ne
            "source_approach_plus_resolved_route_static_bfs_plus_target_static_bfs_runtime_rechecked" -or
        $targetRouteDistanceTiles -lt 0 -or
        ([Math]::Abs($targetX - $targetStandX) +
            [Math]::Abs($targetY - $targetStandY)) -ne 1 -or
        $targetRouteDistanceTiles -lt
            ([Math]::Abs($targetStandX - $targetArrivalX) +
             [Math]::Abs($targetStandY - $targetArrivalY))) {
        throw (
            "Strategic candidate lacked a proven static-BFS target " +
            "route and adjacent stand."
        )
    }

    $machineInventoryBefore = Inventory-Count `
        -Snapshot $sourceSnapshot `
        -QualifiedItemId $MachineQualifiedItemId
    $stageOneRoot = Join-Path $runDirectory "stage-remove"
    $stageOneRunId = $RunId
    Invoke-LiveStage `
        -StageRoot $stageOneRoot `
        -StageRunId $stageOneRunId `
        -SnapshotPath $sourceSnapshotPath `
        -BackendUrl $backendUrl `
        -SnapshotUrl $snapshotUrl `
        -ExecutorUrl $executorUrl `
        -SavesPath $savesPath `
        -CandidateKind "relocate_machine_item" `
        -CandidateId ([string]$relocationCandidate.candidate_id) `
        -LogPath (Join-Path $runDirectory "stage-remove.log")
    $stageOneArtifactRoot = Join-Path $stageOneRoot (
        "runs\$stageOneRunId\live-snapshots"
    )
    $stageOneQueue = Get-Content -LiteralPath (
        Join-Path $stageOneArtifactRoot "compiled-queue-0001.json"
    ) -Raw | ConvertFrom-Json
    $stageOneExecution = Get-Content -LiteralPath (
        Join-Path $stageOneArtifactRoot "execution-0001.json"
    ) -Raw | ConvertFrom-Json
    $removeItem = @($stageOneQueue.items) |
        Where-Object {
            $_.option_id -eq "executor.remove_machine"
        } |
        Select-Object -First 1
    $removeExecution = @($stageOneExecution.step_results) |
        Where-Object {
            $_.option_id -eq "executor.remove_machine"
        } |
        Select-Object -First 1
    if ($null -eq $removeItem -or
        $null -eq $removeExecution -or
        $removeExecution.status -ne "applied" -or
        $removeExecution.primitive_verification_status -ne "verified" -or
        (Queue-Parameter `
            -QueueItem $removeItem `
            -Name "relocation_intent_id") -ne $intentId) {
        throw "Strategic daily plan did not verify native removal."
    }

    $recoveryDeadline = (Get-Date).AddSeconds(45)
    $recoveryMode = ""
    $recoveredSnapshot = $null
    $recoveredDebris = $null
    while ((Get-Date) -lt $recoveryDeadline) {
        $candidateSnapshot = Wait-WorldSnapshot `
            -Url $snapshotUrl -TimeoutSeconds 30
        $sourceMachine = Find-Machine `
            -Snapshot $candidateSnapshot `
            -X $SourceTileX -Y $SourceTileY
        $candidateDebris = Find-Debris `
            -Snapshot $candidateSnapshot `
            -QualifiedItemId $MachineQualifiedItemId
        $inventoryCount = Inventory-Count `
            -Snapshot $candidateSnapshot `
            -QualifiedItemId $MachineQualifiedItemId
        if ($null -eq $sourceMachine -and
            $inventoryCount -gt $machineInventoryBefore) {
            $recoveryMode = "native_auto_collected"
            $recoveredSnapshot = $candidateSnapshot
            break
        }
        if ($null -eq $sourceMachine -and
            $null -ne $candidateDebris) {
            $recoveryMode = "debris_visible"
            $recoveredSnapshot = $candidateSnapshot
            $recoveredDebris = $candidateDebris
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if ($null -eq $recoveredSnapshot) {
        throw "Native removal produced neither inventory nor debris."
    }

    $pickupResult = $null
    if ($recoveryMode -eq "debris_visible") {
        $chunk = @($recoveredDebris.chunks)[0]
        $pickupResult = Invoke-JsonPost `
            -Url "$executorUrl/api/v1/training/execute" `
            -Body ([ordered]@{
                schema_version = "training_execution_request.v1"
                run_id = $RunId
                queue_id = "strategic-machine-relocation-recovery"
                queue_item_id =
                    "strategic-machine-relocation-recovery.pickup"
                before_state_hash = $recoveredSnapshot.state_hash
                option_id = "executor.pickup_debris"
                execution_mode = "training_singleplayer"
                actor = "training_farmer.main"
                save_isolation_path = $savesPath
                request_nonce = [guid]::NewGuid().ToString("N")
                created_at = [DateTimeOffset]::UtcNow.ToString("O")
                target_tile_x = [int]$chunk.tile_x
                target_tile_y = [int]$chunk.tile_y
                debris_index = [int]$recoveredDebris.debris_index
                qualified_item_id = $MachineQualifiedItemId
                location_id = "Farm"
            })
        if ($pickupResult.status -ne "applied" -or
            $pickupResult.primitive_verification_status -ne "verified") {
            throw "Native machine debris pickup failed."
        }
        $pickupDeadline = (Get-Date).AddSeconds(45)
        while ((Get-Date) -lt $pickupDeadline) {
            $recoveredSnapshot = Wait-WorldSnapshot `
                -Url $snapshotUrl -TimeoutSeconds 30
            if ((Inventory-Count `
                    -Snapshot $recoveredSnapshot `
                    -QualifiedItemId $MachineQualifiedItemId) -gt
                $machineInventoryBefore) {
                break
            }
            Start-Sleep -Milliseconds 250
        }
    }
    if ((Inventory-Count `
            -Snapshot $recoveredSnapshot `
            -QualifiedItemId $MachineQualifiedItemId) -le
        $machineInventoryBefore) {
        throw "Recovered machine did not enter player inventory."
    }
    Write-JsonFile $recoveredSnapshotPath $recoveredSnapshot

    $placementSnapshotPath = $recoveredSnapshotPath
    $routeExecutions = @()
    $routeCandidatesUsed = @()
    if ($TargetLocationId -ne "Farm") {
        $routeSnapshot = $recoveredSnapshot
        $routeSnapshotPath = $recoveredSnapshotPath
        $visitedLocations = @{}
        for ($routeIndex = 0;
             $routeIndex -lt $routeConnectorCount;
             $routeIndex++) {
            $currentRouteLocation =
                [string]$routeSnapshot.state.player.location_id.value
            if ($currentRouteLocation -eq $TargetLocationId) {
                break
            }
            if ($visitedLocations.ContainsKey($currentRouteLocation)) {
                throw (
                    "Strategic relocation route revisited " +
                    "$currentRouteLocation."
                )
            }
            $visitedLocations[$currentRouteLocation] = $true

            $null = Invoke-JsonPost `
                -Url "$backendUrl/api/v1/snapshots" `
                -Body $routeSnapshot
            $routeAvailability = Invoke-JsonPost `
                -Url "$backendUrl/api/v1/planner/options/availability" `
                -Body ([ordered]@{
                    state_hash = $routeSnapshot.state_hash
                    candidate_option_ids =
                        @("farm.process_machines")
                    candidates = @()
                    include_executor_calibration_options = $true
                })
            Write-JsonFile `
                (Join-Path $runDirectory (
                    "route-availability-{0:D2}.json" -f
                    $routeIndex
                )) `
                $routeAvailability
            $routeCandidate = @(
                $routeAvailability.options |
                    Where-Object {
                        $_.option_id -eq "farm.process_machines"
                    } |
                    ForEach-Object { $_.event_candidates } |
                    Where-Object {
                        $_.kind -eq "route_connector_tile" -and
                        [bool]$_.available -and
                        $_.candidate_id -like
                            "machine-place-route:*" -and
                        (Candidate-Parameter `
                            -Candidate $_ `
                            -Name `
                                "continuation.machine_location_id") -eq
                            $TargetLocationId -and
                        (Candidate-Parameter `
                            -Candidate $_ `
                            -Name `
                                "continuation.relocation_intent_id") -eq
                            $intentId -and
                        [int](Candidate-Parameter `
                            -Candidate $_ `
                            -Name `
                                "machine_route.committed_segment_index") -eq
                            $routeIndex
                    }
            ) | Select-Object -First 1
            if ($null -eq $routeCandidate) {
                throw (
                    "Recovered machine had no committed route segment " +
                    "$routeIndex from $currentRouteLocation to " +
                    "$TargetLocationId."
                )
            }

            $expectedNextLocation = Candidate-Parameter `
                -Candidate $routeCandidate `
                -Name "expected_target_location"
            $routeStageRunId = $RunId
            $routeRoot = Join-Path $runDirectory (
                "stage-route-{0:D2}" -f $routeIndex
            )
            Invoke-LiveStage `
                -StageRoot $routeRoot `
                -StageRunId $routeStageRunId `
                -SnapshotPath $routeSnapshotPath `
                -BackendUrl $backendUrl `
                -SnapshotUrl $snapshotUrl `
                -ExecutorUrl $executorUrl `
                -SavesPath $savesPath `
                -CandidateKind "route_connector_tile" `
                -CandidateId ([string]$routeCandidate.candidate_id) `
                -LogPath (Join-Path $runDirectory (
                    "stage-route-{0:D2}.log" -f $routeIndex
                ))
            $routeArtifactRoot = Join-Path $routeRoot (
                "runs\$routeStageRunId\live-snapshots"
            )
            $routeStageExecution = Get-Content -LiteralPath (
                Join-Path $routeArtifactRoot "execution-0001.json"
            ) -Raw | ConvertFrom-Json
            $routeExecution = @(
                $routeStageExecution.step_results
            ) | Where-Object {
                $_.option_id -eq "executor.traverse_connector"
            } | Select-Object -First 1
            if ($null -eq $routeExecution -or
                $routeExecution.status -ne "applied" -or
                $routeExecution.primitive_verification_status -ne
                    "verified") {
                throw (
                    "Strategic relocation connector segment " +
                    "$routeIndex did not verify."
                )
            }

            $routeSnapshot = Wait-WorldSnapshot `
                -Url $snapshotUrl -TimeoutSeconds 45
            $actualNextLocation =
                [string]$routeSnapshot.state.player.location_id.value
            if ($actualNextLocation -ne $expectedNextLocation) {
                throw (
                    "Strategic route segment $routeIndex reached " +
                    "$actualNextLocation instead of " +
                    "$expectedNextLocation."
                )
            }
            $routeSnapshotPath = Join-Path $runDirectory (
                "route-snapshot-{0:D2}.json" -f ($routeIndex + 1)
            )
            Write-JsonFile $routeSnapshotPath $routeSnapshot
            $routeCandidatesUsed += $routeCandidate
            $routeExecutions += $routeExecution
        }

        if ([string]$routeSnapshot.state.player.location_id.value -ne
            $TargetLocationId -or
            $routeExecutions.Count -ne $routeConnectorCount) {
            throw (
                "Strategic route did not consume the committed " +
                "$routeConnectorCount connector segments."
            )
        }
        Write-JsonFile $targetSnapshotPath $routeSnapshot
        $placementSnapshotPath = $targetSnapshotPath
    }

    $stageTwoRoot = Join-Path $runDirectory "stage-place"
    $stageTwoRunId = $RunId
    Invoke-LiveStage `
        -StageRoot $stageTwoRoot `
        -StageRunId $stageTwoRunId `
        -SnapshotPath $placementSnapshotPath `
        -BackendUrl $backendUrl `
        -SnapshotUrl $snapshotUrl `
        -ExecutorUrl $executorUrl `
        -SavesPath $savesPath `
        -CandidateKind "place_machine_item" `
        -LogPath (Join-Path $runDirectory "stage-place.log")
    $stageTwoArtifactRoot = Join-Path $stageTwoRoot (
        "runs\$stageTwoRunId\live-snapshots"
    )
    $stageTwoQueue = Get-Content -LiteralPath (
        Join-Path $stageTwoArtifactRoot "compiled-queue-0001.json"
    ) -Raw | ConvertFrom-Json
    $stageTwoExecution = Get-Content -LiteralPath (
        Join-Path $stageTwoArtifactRoot "execution-0001.json"
    ) -Raw | ConvertFrom-Json
    $placeItem = @($stageTwoQueue.items) |
        Where-Object {
            $_.option_id -eq "executor.place_machine"
        } |
        Select-Object -First 1
    $placeExecution = @($stageTwoExecution.step_results) |
        Where-Object {
            $_.option_id -eq "executor.place_machine"
        } |
        Select-Object -First 1
    if ($null -eq $placeItem -or
        $null -eq $placeExecution -or
        $placeExecution.status -ne "applied" -or
        $placeExecution.primitive_verification_status -ne "verified" -or
        [int](Queue-Parameter `
            -QueueItem $placeItem -Name "target_tile_x") -ne
            $targetX -or
        [int](Queue-Parameter `
            -QueueItem $placeItem -Name "target_tile_y") -ne
            $targetY -or
        (Queue-Parameter `
            -QueueItem $placeItem `
            -Name "relocation_intent_id") -ne $intentId) {
        throw "Fresh-snapshot plan did not verify exact intent placement."
    }

    $finalSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl -TimeoutSeconds 45
    Write-JsonFile `
        (Join-Path $runDirectory "final-snapshot.json") `
        $finalSnapshot
    $finalIngest = Invoke-JsonPost `
        -Url "$backendUrl/api/v1/snapshots" `
        -Body $finalSnapshot
    $finalLedger = Invoke-JsonGet `
        -Url (
            "$backendUrl/api/v1/strategy/commitments/latest" +
            "?stateHash=$($finalSnapshot.state_hash)"
        )
    Write-JsonFile `
        (Join-Path $runDirectory "final-ledger.json") `
        $finalLedger
    $finalIntent = @($finalLedger.machine_relocation_intents) |
        Where-Object { $_.intent_id -eq $intentId } |
        Select-Object -First 1
    $targetMachine = Find-Machine `
        -Snapshot $finalSnapshot -X $targetX -Y $targetY `
        -LocationId $TargetLocationId
    $sourceMachine = Find-Machine `
        -Snapshot $finalSnapshot -X $SourceTileX -Y $SourceTileY `
        -LocationId "Farm"
    $passed =
        $null -ne $targetMachine -and
        [string]$targetMachine.qualified_item_id -eq
            $MachineQualifiedItemId -and
        $null -eq $sourceMachine -and
        $null -ne $finalIntent -and
        [string]$finalIntent.status -eq "completed" -and
        [string]$finalIntent.completion_reason -eq
            "exact_target_machine_observed"
    if (-not $passed) {
        throw "Strategic machine relocation final state did not close."
    }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        source_location_id = "Farm"
        target_location_id = $TargetLocationId
        source_tile = "$SourceTileX,$SourceTileY"
        peer_tiles = @(
            "$PeerOneTileX,$PeerOneTileY",
            "$PeerTwoTileX,$PeerTwoTileY"
        )
        selected_target_tile = "$targetX,$targetY"
        machine_qualified_item_id = $MachineQualifiedItemId
        relocation_intent_id = $intentId
        layout_net_benefit_ticks = $netBenefit
        target_selection_policy = $targetSelectionPolicy
        target_arrival_tile = "$targetArrivalX,$targetArrivalY"
        target_stand_tile = "$targetStandX,$targetStandY"
        target_route_distance_tiles = $targetRouteDistanceTiles
        route_connector_count = $routeConnectorCount
        route_connector_kind = $routeConnectorKind
        route_estimated_ticks = $routeEstimatedTicks
        route_segments = [object[]] $routeSegments
        time_estimate_policy = $timeEstimatePolicy
        source_candidate_selected = $true
        native_remove_verified = $true
        recovery_mode = $recoveryMode
        native_pickup_verified =
            $recoveryMode -eq "native_auto_collected" -or
            ($pickupResult.status -eq "applied" -and
             $pickupResult.primitive_verification_status -eq "verified")
        fresh_snapshot_exact_place_verified = $true
        cross_location_route_verified =
            $TargetLocationId -eq "Farm" -or
            ($routeExecutions.Count -eq $routeConnectorCount -and
             @($routeExecutions | Where-Object {
                $_.status -ne "applied" -or
                $_.primitive_verification_status -ne "verified"
             }).Count -eq 0)
        route_candidate_id =
            if ($routeCandidatesUsed.Count -eq 0) {
                ""
            }
            else {
                [string]$routeCandidatesUsed[0].candidate_id
            }
        route_candidate_ids = @(
            $routeCandidatesUsed |
                ForEach-Object { [string]$_.candidate_id }
        )
        source_absent_after = $null -eq $sourceMachine
        target_machine_present_after = $null -ne $targetMachine
        intent_status_after = $finalIntent.status
        intent_completion_reason = $finalIntent.completion_reason
        source_state_hash = $sourceSnapshot.state_hash
        recovered_state_hash = $recoveredSnapshot.state_hash
        final_state_hash = $finalSnapshot.state_hash
        all_snapshots_distinct =
            $sourceSnapshot.state_hash -ne
                $recoveredSnapshot.state_hash -and
            $recoveredSnapshot.state_hash -ne
                $finalSnapshot.state_hash
        stage_remove_queue_id = $stageOneQueue.queue_id
        stage_place_queue_id = $stageTwoQueue.queue_id
        executor_health = $executorHealth
        backend_ingest_state_hash = $ingest.state_hash
        final_ingest_state_hash = $finalIngest.state_hash
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
