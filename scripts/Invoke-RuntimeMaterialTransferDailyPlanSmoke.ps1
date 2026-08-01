param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-material-transfer-daily-plan-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-material-transfer-daily-plan-smoke",
    [int] $BackendPort = 5131,
    [int] $StartupTimeoutSeconds = 180,
    [int] $TargetTileX = -1,
    [int] $TargetTileY = -1,
    [int] $TransferQuantity = 2,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 64
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") { return $response }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-MaterialGraph {
    param([string] $Url, [int] $TimeoutSeconds, [string] $DifferentFromStateHash = "")
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $field = $snapshot.state.farm.material_inventory_graph
            $hashChanged = [string]::IsNullOrWhiteSpace($DifferentFromStateHash) -or
                [string]$snapshot.state_hash -ne $DifferentFromStateHash
            $lastStatus = "field=$($field.status);schema=$($field.value.schema_version);hash_changed=$hashChanged"
            if ($field.status -in @("available", "derived") -and
                $field.value.schema_version -eq "material_inventory_graph.v1" -and
                $hashChanged) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for material inventory graph. Last status: $lastStatus"
}

function Find-Node {
    param($Graph, [string] $NodeId)
    @($Graph.inventory_nodes) | Where-Object { [string]$_.node_id -eq $NodeId } | Select-Object -First 1
}

function Find-ItemSlot {
    param($Node, [string] $QualifiedItemId, [int] $Quality)
    @($Node.slots) | Where-Object {
        [string]$_.qualified_item_id -eq $QualifiedItemId -and [int]$_.quality -eq $Quality
    } | Sort-Object slot_index | Select-Object -First 1
}

function Get-NodeQuantity {
    param($Node, [string] $QualifiedItemId, [int] $Quality)
    $quantity = 0
    foreach ($slot in @($Node.slots)) {
        if ([string]$slot.qualified_item_id -eq $QualifiedItemId -and [int]$slot.quality -eq $Quality) {
            $quantity += [int]$slot.stack
        }
    }
    return $quantity
}

function Read-QueueParameter {
    param($QueueItem, [string] $Name)
    @($QueueItem.normalized_command.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -ExpandProperty value -First 1
}

function Invoke-TransferDirection {
    param(
        [string] $Label,
        $BeforeSnapshot,
        [string] $SourceNodeId,
        [string] $DestinationNodeId,
        [int] $SourceSlotIndex,
        [string] $QualifiedItemId,
        [int] $Quality,
        [int] $Quantity,
        [int] $ExpectedSourceStack,
        [string] $LoopRoot,
        [string] $BackendUrl,
        [string] $SnapshotUrl,
        [string] $SavesPath,
        [string] $RunDirectory
    )

    $directionRunId = $RunId
    $arguments = @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"),
        "--",
        "--root", $LoopRoot,
        "--backend-url", $BackendUrl,
        "--bridge-snapshot-url", $SnapshotUrl,
        "--executor-url", "http://127.0.0.1:8767",
        "--no-manifest",
        "--run-id", $directionRunId,
        "--save-isolation-path", $SavesPath,
        "--iterations", "1",
        "--train-every", "1",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "inventory.transfer_item",
        "--daily-plan-candidate-parameter", "source_node_id=$SourceNodeId",
        "--daily-plan-candidate-parameter", "destination_node_id=$DestinationNodeId",
        "--daily-plan-candidate-parameter", "source_slot_index=$SourceSlotIndex",
        "--daily-plan-candidate-parameter", "qualified_item_id=$QualifiedItemId",
        "--daily-plan-candidate-parameter", "quality=$Quality",
        "--daily-plan-candidate-parameter", "quantity=$Quantity",
        "--daily-plan-candidate-parameter", "expected_source_stack=$ExpectedSourceStack",
        "--after-snapshot-wait-ms", "250",
        "--after-snapshot-poll-ms", "100",
        "--max-queue-item-attempts", "8"
    )
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop $Label returned exit code $LASTEXITCODE" }

    $snapshotDir = Join-Path $LoopRoot "runs\$directionRunId\live-snapshots"
    $rankingPath = Join-Path $snapshotDir "ranking-response-0001.json"
    $dailyPlanPath = Join-Path $snapshotDir "daily-plan-response-0001.json"
    $queuePath = Join-Path $snapshotDir "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDir "execution-0001.json"
    $episodePath = Join-Path $snapshotDir "plan-execution-episode-0001.json"
    $datasetPath = Join-Path $LoopRoot "datasets\live-training-feature-rows.jsonl"
    foreach ($path in @($rankingPath, $dailyPlanPath, $queuePath, $executionPath, $episodePath, $datasetPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "$Label artifact missing: $path" }
    }

    $ranking = Get-Content -LiteralPath $rankingPath -Raw | ConvertFrom-Json
    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $episode = Get-Content -LiteralPath $episodePath -Raw | ConvertFrom-Json
    $candidate = @($ranking.ranked_event_candidates) |
        Where-Object { $_.option_id -eq "inventory.transfer_item" -and $_.kind -eq "transfer_inventory_item" } |
        Select-Object -First 1
    $planStep = @($dailyPlan.plan.steps) | Where-Object { $_.kind -eq "transfer_material" } | Select-Object -First 1
    $queueItem = @($queue.items) | Where-Object { $_.option_id -eq "executor.transfer_material" } | Select-Object -First 1
    $transferExecution = @($execution.step_results) |
        Where-Object { $_.option_id -eq "executor.transfer_material" } |
        Select-Object -Last 1

    if ($null -eq $candidate -or -not [bool]$candidate.available) { throw "$Label high-level candidate was not available." }
    if ($null -eq $planStep) { throw "$Label daily plan did not contain transfer_material." }
    if ($null -eq $queueItem) { throw "$Label queue did not contain executor.transfer_material." }
    if ($null -eq $transferExecution -or
        $transferExecution.status -ne "applied" -or
        $transferExecution.primitive_verification_status -ne "verified") {
        Write-JsonFile (Join-Path $RunDirectory "$Label-execution-rejected.json") $execution
        throw "$Label native transfer was not applied and verified."
    }
    if ($transferExecution.material_transfer_native_menu_opened -ne $true -or
        $transferExecution.material_transfer_native_lock_released -ne $true -or
        [int]$transferExecution.material_transfer_click_count -ne $Quantity) {
        throw "$Label native menu lifecycle or click count was not verified."
    }
    if ($episode.option_id -ne "executor.transfer_material" -or
        $episode.material_transfer_native_menu_opened -ne $true -or
        $episode.material_transfer_native_lock_released -ne $true -or
        $null -eq $episode.material_transfer_intent -or
        $null -eq $episode.material_transfer_projection) {
        throw "$Label episode did not preserve material-transfer execution evidence."
    }
    $datasetRows = @(Get-Content -LiteralPath $datasetPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($datasetRows.Count -lt 1) { throw "$Label training dataset did not record a row." }

    $afterSnapshot = Wait-MaterialGraph -Url $SnapshotUrl -TimeoutSeconds 30 -DifferentFromStateHash ([string]$BeforeSnapshot.state_hash)
    $beforeGraph = $BeforeSnapshot.state.farm.material_inventory_graph.value
    $afterGraph = $afterSnapshot.state.farm.material_inventory_graph.value
    $beforeSource = Find-Node $beforeGraph $SourceNodeId
    $beforeDestination = Find-Node $beforeGraph $DestinationNodeId
    $afterSource = Find-Node $afterGraph $SourceNodeId
    $afterDestination = Find-Node $afterGraph $DestinationNodeId
    $sourceBefore = Get-NodeQuantity $beforeSource $QualifiedItemId $Quality
    $sourceAfter = Get-NodeQuantity $afterSource $QualifiedItemId $Quality
    $destinationBefore = Get-NodeQuantity $beforeDestination $QualifiedItemId $Quality
    $destinationAfter = Get-NodeQuantity $afterDestination $QualifiedItemId $Quality
    if ($sourceBefore - $sourceAfter -ne $Quantity -or $destinationAfter - $destinationBefore -ne $Quantity) {
        throw "$Label transparent before/after quantity delta did not equal $Quantity."
    }
    if ([int]$transferExecution.material_transfer_source_stack_before -ne $ExpectedSourceStack -or
        [int]$transferExecution.material_transfer_source_stack_after -ne ($ExpectedSourceStack - $Quantity) -or
        [int]$transferExecution.material_transfer_destination_quantity_before -ne $destinationBefore -or
        [int]$transferExecution.material_transfer_destination_quantity_after -ne $destinationAfter) {
        throw "$Label executor evidence did not match transparent before/after snapshots."
    }

    Write-JsonFile (Join-Path $RunDirectory "$Label-before-snapshot.json") $BeforeSnapshot
    Write-JsonFile (Join-Path $RunDirectory "$Label-after-snapshot.json") $afterSnapshot
    Write-JsonFile (Join-Path $RunDirectory "$Label-execution.json") $transferExecution
    Copy-Item -LiteralPath $episodePath -Destination (Join-Path $RunDirectory "$Label-episode.json") -Force
    Copy-Item -LiteralPath $datasetPath -Destination (Join-Path $RunDirectory "$Label-dataset.jsonl") -Force

    return [pscustomobject]@{
        Label = $Label
        AfterSnapshot = $afterSnapshot
        CandidateId = [string]$candidate.candidate_id
        QueueItemId = [string]$queueItem.queue_item_id
        SourceQuantityBefore = $sourceBefore
        SourceQuantityAfter = $sourceAfter
        DestinationQuantityBefore = $destinationBefore
        DestinationQuantityAfter = $destinationAfter
        NativeBranch = [string]$transferExecution.material_transfer_projection.native_branch
        MenuOpened = [bool]$transferExecution.material_transfer_native_menu_opened
        LockReleased = [bool]$transferExecution.material_transfer_native_lock_released
        ClickCount = [int]$transferExecution.material_transfer_click_count
        EpisodePath = $episodePath
        DatasetPath = $datasetPath
    }
}

if ($TransferQuantity -le 0) { throw "TransferQuantity must be positive." }
$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }

foreach ($port in @($BackendPort, 8765, 8767)) {
    $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($null -ne $listener) { throw "Port $port is already listening; refusing to attach to an unknown process." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running; refusing to touch an existing game process."
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}
$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) { throw "Isolated save slot not found: $slotPath" }

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

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
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout -RedirectStandardError $backendStderr -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30 | Out-Null
    $initialSnapshot = Wait-MaterialGraph -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    if (($TargetTileX -lt 0) -xor ($TargetTileY -lt 0)) {
        throw "TargetTileX and TargetTileY must either both be supplied or both be omitted."
    }
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.setup"
        queue_item_id = "$RunId.setup.material_inventory"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_material_transfer_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Material transfer fixture setup failed."
    }
    if ($TargetTileX -lt 0) {
        if ([string]$setupResult.observed_effect -notmatch "target=(-?\d+),(-?\d+)") {
            throw "Dynamic material transfer fixture did not report its selected target."
        }
        $TargetTileX = [int]$Matches[1]
        $TargetTileY = [int]$Matches[2]
    }
    $fixtureSnapshot = Wait-MaterialGraph -Url $snapshotUrl -TimeoutSeconds 30 -DifferentFromStateHash ([string]$initialSnapshot.state_hash)
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "fixture-snapshot.json") $fixtureSnapshot

    $graph = $fixtureSnapshot.state.farm.material_inventory_graph.value
    $playerNode = @($graph.inventory_nodes) | Where-Object { $_.inventory_kind -eq "player_inventory" } | Select-Object -First 1
    $access = @($graph.access_points) | Where-Object {
        $_.access_kind -eq "placed_chest" -and
        [string]$_.location_id -eq "Farm" -and
        [int]$_.tile_x -eq $TargetTileX -and
        [int]$_.tile_y -eq $TargetTileY -and
        [string]$_.special_chest_type -eq "None"
    } | Select-Object -First 1
    if ($null -eq $playerNode -or $null -eq $access) { throw "Fixture player node or ordinary chest access point missing." }
    $chestNode = Find-Node $graph ([string]$access.node_id)
    $chestSlot = Find-ItemSlot $chestNode "(O)388" 4
    if ($null -eq $chestSlot -or [int]$chestSlot.stack -lt $TransferQuantity) {
        throw "Fixture chest does not contain enough (O)388 quality 4 material."
    }

    $withdraw = Invoke-TransferDirection `
        -Label "chest-to-player" `
        -BeforeSnapshot $fixtureSnapshot `
        -SourceNodeId ([string]$chestNode.node_id) `
        -DestinationNodeId ([string]$playerNode.node_id) `
        -SourceSlotIndex ([int]$chestSlot.slot_index) `
        -QualifiedItemId "(O)388" -Quality 4 -Quantity $TransferQuantity `
        -ExpectedSourceStack ([int]$chestSlot.stack) `
        -LoopRoot (Join-Path $loopRoot "chest-to-player") `
        -BackendUrl $backendUrl -SnapshotUrl $snapshotUrl -SavesPath $savesPath -RunDirectory $runDirectory

    $withdrawGraph = $withdraw.AfterSnapshot.state.farm.material_inventory_graph.value
    $playerAfterWithdraw = Find-Node $withdrawGraph ([string]$playerNode.node_id)
    $returnSlot = Find-ItemSlot $playerAfterWithdraw "(O)388" 4
    if ($null -eq $returnSlot -or [int]$returnSlot.stack -lt $TransferQuantity) {
        throw "Withdrawn material was not available for the return transfer."
    }
    $deposit = Invoke-TransferDirection `
        -Label "player-to-chest" `
        -BeforeSnapshot $withdraw.AfterSnapshot `
        -SourceNodeId ([string]$playerNode.node_id) `
        -DestinationNodeId ([string]$chestNode.node_id) `
        -SourceSlotIndex ([int]$returnSlot.slot_index) `
        -QualifiedItemId "(O)388" -Quality 4 -Quantity $TransferQuantity `
        -ExpectedSourceStack ([int]$returnSlot.stack) `
        -LoopRoot (Join-Path $loopRoot "player-to-chest") `
        -BackendUrl $backendUrl -SnapshotUrl $snapshotUrl -SavesPath $savesPath -RunDirectory $runDirectory

    $finalGraph = $deposit.AfterSnapshot.state.farm.material_inventory_graph.value
    $finalChest = Find-Node $finalGraph ([string]$chestNode.node_id)
    $finalPlayer = Find-Node $finalGraph ([string]$playerNode.node_id)
    $initialChestQuantity = Get-NodeQuantity $chestNode "(O)388" 4
    $finalChestQuantity = Get-NodeQuantity $finalChest "(O)388" 4
    if ($initialChestQuantity -ne $finalChestQuantity) { throw "Round-trip did not restore the fixture chest quantity." }

    $finalChestSlot = Find-ItemSlot $finalChest "(O)388" 4
    $finalPlayerQuantity = Get-NodeQuantity $finalPlayer "(O)388" 4
    $staleExpectedStack = [int]$finalChestSlot.stack - 1
    $negativeRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.negative"
        queue_item_id = "$RunId.negative.stale_source"
        before_state_hash = $deposit.AfterSnapshot.state_hash
        option_id = "executor.transfer_material"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        location_id = "Farm"
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        stand_tile_x = [int]$deposit.AfterSnapshot.state.player.tile_x.value
        stand_tile_y = [int]$deposit.AfterSnapshot.state.player.tile_y.value
        max_movement_tiles = 512
        material_transfer_intent = [ordered]@{
            schema_version = "material_transfer_intent.v1"
            source_node_id = [string]$finalChest.node_id
            destination_node_id = [string]$finalPlayer.node_id
            source_slot_index = [int]$finalChestSlot.slot_index
            qualified_item_id = "(O)388"
            quality = 4
            quantity = 1
            expected_source_stack = $staleExpectedStack
        }
        material_transfer_projection = [ordered]@{
            schema_version = "material_transfer_projection.v1"
            status = "projected"
            native_branch = "negative_stale_projection_must_not_execute"
            source_stack_after = $staleExpectedStack - 1
            destination_quantity_before = $finalPlayerQuantity
            destination_quantity_after = $finalPlayerQuantity + 1
            destination_slot_changes = @()
            blocking_reasons = @()
        }
    }
    $negativeResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $negativeRequest
    $negativeSnapshot = Wait-MaterialGraph -Url $snapshotUrl -TimeoutSeconds 30
    $negativeGraph = $negativeSnapshot.state.farm.material_inventory_graph.value
    $negativeChest = Find-Node $negativeGraph ([string]$finalChest.node_id)
    $negativePlayer = Find-Node $negativeGraph ([string]$finalPlayer.node_id)
    if ($negativeResult.status -ne "blocked" -or
        -not (@($negativeResult.block_reasons) -contains "material_transfer_source_projection_drifted") -or
        [int]$negativeResult.material_transfer_click_count -ne 0 -or
        (Get-NodeQuantity $negativeChest "(O)388" 4) -ne $finalChestQuantity -or
        (Get-NodeQuantity $negativePlayer "(O)388" 4) -ne $finalPlayerQuantity) {
        throw "Stale material-transfer projection did not fail closed without quantity mutation."
    }
    Write-JsonFile (Join-Path $runDirectory "negative-stale-request.json") $negativeRequest
    Write-JsonFile (Join-Path $runDirectory "negative-stale-result.json") $negativeResult
    Write-JsonFile (Join-Path $runDirectory "negative-stale-after-snapshot.json") $negativeSnapshot

    $evidence = [ordered]@{
        schema_version = "runtime_e3_material_transfer_evidence.v1"
        evidence_class = "E3_runtime_output"
        option_id = "inventory.transfer_item"
        execution_option_id = "executor.transfer_material"
        high_level_chain = "inventory.transfer_item->daily_plan->executor.transfer_material->native_ItemGrabMenu"
        isolated_save_path = $savesPath
        save_slot = $SaveSlot
        debug_fixture_excluded_from_action_evidence = $true
        directions = @(
            [ordered]@{ label = $withdraw.Label; candidate_id = $withdraw.CandidateId; queue_item_id = $withdraw.QueueItemId; source_before = $withdraw.SourceQuantityBefore; source_after = $withdraw.SourceQuantityAfter; destination_before = $withdraw.DestinationQuantityBefore; destination_after = $withdraw.DestinationQuantityAfter; native_branch = $withdraw.NativeBranch; menu_opened = $withdraw.MenuOpened; lock_released = $withdraw.LockReleased; click_count = $withdraw.ClickCount },
            [ordered]@{ label = $deposit.Label; candidate_id = $deposit.CandidateId; queue_item_id = $deposit.QueueItemId; source_before = $deposit.SourceQuantityBefore; source_after = $deposit.SourceQuantityAfter; destination_before = $deposit.DestinationQuantityBefore; destination_after = $deposit.DestinationQuantityAfter; native_branch = $deposit.NativeBranch; menu_opened = $deposit.MenuOpened; lock_released = $deposit.LockReleased; click_count = $deposit.ClickCount }
        )
        round_trip_chest_quantity_before = $initialChestQuantity
        round_trip_chest_quantity_after = $finalChestQuantity
        stale_projection_failure_closed = $true
        stale_projection_click_count = [int]$negativeResult.material_transfer_click_count
        result = "verified"
    }
    Write-JsonFile (Join-Path $runDirectory "e3-evidence.json") $evidence
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        runtime_root = $RuntimeRoot
        fixture_target_tile = "$TargetTileX,$TargetTileY"
        hidden = $true
        silent = $true
        high_level_option = "inventory.transfer_item"
        native_executor = "executor.transfer_material"
        directions_verified = 2
        native_menu_lifecycles_verified = 2
        training_episode_count = 2
        training_dataset_count = 2
        round_trip_restored = $true
        stale_projection_failure_closed = $true
        e3_evidence_path = (Join-Path $runDirectory "e3-evidence.json")
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($key in $previousEnv.Keys) { Set-Item -Path "env:$key" -Value $previousEnv[$key] }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
