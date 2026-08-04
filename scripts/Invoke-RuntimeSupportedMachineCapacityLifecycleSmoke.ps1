param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-supported-machine-capacity-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-supported-machine-capacity",
    [int] $BackendPort = 5128,
    [int] $StartupTimeoutSeconds = 180,
    [int] $TargetTileX = 60,
    [int] $TargetTileY = 15,
    [string] $RecipeName = "Keg",
    [string] $MachineQualifiedItemId = "(BC)12",
    [string] $MachineItemId = "12",
    [string] $ProcessInputQualifiedItemId = "(O)262",
    [int] $ProcessInputQuantity = 20,
    [ValidateSet("none", "ordinary_quest", "special_order")]
    [string] $TaskFamily = "none",
    [string] $TaskId = "960217",
    [string] $TaskOutputQualifiedItemId = "(O)346",
    [ValidateSet("empty", "inventory")]
    [string] $FixtureCapacityMode = "empty",
    [int] $CompletionTimeoutSeconds = 120,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
$lifecycleTargetX = $TargetTileX
$lifecycleTargetY = $TargetTileY
$taskMode = $TaskFamily -ne "none"
$lifecycleGoal = if ($taskMode) {
    "goal.grandpa_max_score_year3"
} else {
    "goal.economy.earn_money"
}

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 64
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw {
    param([string] $Url, [string] $Json, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $Json -TimeoutSec $TimeoutSeconds
}

function Wait-Health {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ([string]$health.status -eq "ok") {
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

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    if ($null -eq $Snapshot.state) { return $null }
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url
            $saveReadable = $snapshot.save_id.status -in @(
                "available", "derived")
            $crafting = $snapshot.state.player.machine_crafting
            $placement = $snapshot.state.player.machine_placement
            $machines = $snapshot.state.farm.machines
            $readable =
                $null -ne $crafting -and
                $crafting.status -in @("available", "derived") -and
                $null -ne $placement -and
                $placement.status -in @("available", "derived") -and
                $null -ne $machines -and
                $machines.status -in @(
                    "available", "derived", "partial")
            $lastStatus =
                "save=$saveReadable;machine_fields=$readable"
            if ($saveReadable -and $readable) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw (
        "Timed out waiting for transparent machine snapshot. " +
        "Last status: $lastStatus")
}

function Find-MachineAtTarget {
    param($Snapshot)
    foreach ($machine in @(Read-FieldValue $Snapshot "farm" "machines")) {
        if ([string]$machine.location_id -eq "Farm" -and
            [int]$machine.tile_x -eq $lifecycleTargetX -and
            [int]$machine.tile_y -eq $lifecycleTargetY -and
            [string]$machine.qualified_item_id -eq
                $MachineQualifiedItemId) {
            return $machine
        }
    }
    return $null
}

function Read-QueueParameter {
    param($QueueItem, [string] $Name)
    foreach ($parameter in @(
        $QueueItem.normalized_command.parameters)) {
        if ([string]$parameter.name -eq $Name) {
            return [string]$parameter.value
        }
    }
    return ""
}

function Get-InventoryCount {
    param($Snapshot, [string] $QualifiedItemId)
    $count = 0
    foreach ($item in @(Read-FieldValue $Snapshot "player" "inventory")) {
        if ([string]$item.qualified_item_id -eq $QualifiedItemId) {
            $count += [int]$item.stack
        }
    }
    return $count
}

function Wait-LifecycleState {
    param(
        [string] $Url,
        [ValidateSet("fixture", "crafted", "placed", "processing")]
        [string] $Stage,
        [int] $TimeoutSeconds = 60
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 20
        $machine = Find-MachineAtTarget -Snapshot $snapshot
        $inventoryMachineCount = Get-InventoryCount `
            -Snapshot $snapshot `
            -QualifiedItemId $MachineQualifiedItemId
        $processing = $null -ne $machine -and (
            [int]$machine.minutes_until_ready -gt 0 -or
            [bool]$machine.ready_for_harvest)
        $matched = switch ($Stage) {
            "fixture" {
                $null -eq $machine -and
                $inventoryMachineCount -eq 0
            }
            "crafted" {
                $null -eq $machine -and
                $inventoryMachineCount -ge 1
            }
            "placed" {
                $null -ne $machine -and
                $inventoryMachineCount -eq 0 -and
                -not $processing
            }
            "processing" { $processing }
        }
        $lastStatus =
            "machine_present=$($null -ne $machine)" +
            ";inventory_machine_count=$inventoryMachineCount" +
            ";processing=$processing"
        if ($matched) { return $snapshot }
        Start-Sleep -Milliseconds 500
    }
    throw "Lifecycle stage '$Stage' timed out. Last status: $lastStatus"
}

function Wait-TaskMachineReady {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url `
            -TimeoutSeconds 20
        $machine = Find-MachineAtTarget -Snapshot $snapshot
        $ready = $null -ne $machine -and
            [bool]$machine.ready_for_harvest -and
            [string]$machine.held_item.qualified_item_id -eq
                $TaskOutputQualifiedItemId
        $lastStatus = if ($null -eq $machine) {
            "machine_absent"
        } else {
            "ready=$($machine.ready_for_harvest);" +
            "minutes=$($machine.minutes_until_ready);" +
            "output=$($machine.held_item.qualified_item_id)"
        }
        if ($ready) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Task machine ready wait timed out. Last status: $lastStatus"
}

function Get-LatestSupportLedger {
    param($Snapshot)
    $snapshotJson = $Snapshot |
        ConvertTo-Json -Depth 96 -Compress
    Invoke-JsonPostRaw `
        -Url "$backendUrl/api/v1/snapshots" `
        -Json $snapshotJson | Out-Null
    $encodedHash = [uri]::EscapeDataString(
        [string]$Snapshot.state_hash)
    Invoke-JsonGet `
        -Url (
            "$backendUrl/api/v1/strategy/commitments/latest" +
            "?stateHash=$encodedHash")
}

function Assert-SupportIntent {
    param(
        $Ledger,
        [string] $ExpectedStatus,
        [string] $ExpectedStage,
        [switch] $RequireTarget
    )
    $intent = @($Ledger.machine_support_intents) |
        Where-Object {
            [string]$_.qualified_item_id -eq
                $MachineQualifiedItemId
        } |
        Select-Object -First 1
    if ($null -eq $intent) {
        throw "Machine support intent was not persisted."
    }
    if ([string]$intent.status -ne $ExpectedStatus -or
        (-not [string]::IsNullOrWhiteSpace($ExpectedStage) -and
         [string]$intent.stage -ne $ExpectedStage)) {
        throw (
            "Unexpected support intent state: status=" +
            $intent.status + ";stage=" + $intent.stage)
    }
    if ($RequireTarget -and (
        [string]$intent.target_location_id -ne "Farm" -or
        [int]$intent.target_tile_x -ne $lifecycleTargetX -or
        [int]$intent.target_tile_y -ne $lifecycleTargetY)) {
        throw "Machine support intent lost its exact placement target."
    }
    if ($taskMode -and (
        [string]$intent.demand_class -ne
            "priority_task_requirement" -or
        [string]$intent.support_kind -ne
            "machine_capacity_active_collection_task")) {
        throw "Machine support intent lost its exact task demand class."
    }
    return $intent
}

function Invoke-LifecycleStage {
    param(
        [int] $Iteration,
        [string] $ExpectedExecutorOptionId,
        [string] $CandidateOptionId =
            "farm.establish_supported_machine_capacity"
    )
    & dotnet $loopDll `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorUrl `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --goal $lifecycleGoal `
        --iterations 1 `
        --train-every 1 `
        --skip-training `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options `
            $CandidateOptionId `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) {
        throw (
            "LiveTrainingLoop stage $Iteration failed with exit " +
            "code $LASTEXITCODE.")
    }

    $snapshotDir = Join-Path $loopRoot (
        "runs\$RunId\live-snapshots")
    $dailyPlanPath = Join-Path $snapshotDir (
        "daily-plan-response-{0:D4}.json" -f $Iteration)
    $queuePath = Join-Path $snapshotDir (
        "compiled-queue-{0:D4}.json" -f $Iteration)
    $executionPath = Join-Path $snapshotDir (
        "execution-{0:D4}.json" -f $Iteration)
    foreach ($path in @(
        $dailyPlanPath, $queuePath, $executionPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required lifecycle artifact missing: $path"
        }
    }
    $dailyPlan = Get-Content $dailyPlanPath -Raw |
        ConvertFrom-Json
    $queue = Get-Content $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content $executionPath -Raw |
        ConvertFrom-Json
    $step = @($execution.step_results) |
        Where-Object {
            [string]$_.option_id -eq $ExpectedExecutorOptionId
        } |
        Select-Object -First 1
    if ($null -eq $step -or
        [string]$step.status -ne "applied" -or
        [string]$step.primitive_verification_status -ne "verified") {
        Write-JsonFile `
            (Join-Path $runDirectory (
                "stage-{0:D2}-rejected.json" -f $Iteration)) `
            $execution
        throw (
            "Stage $Iteration did not verify " +
            "$ExpectedExecutorOptionId.")
    }
    if ([string]$queue.status -notin @("pending", "partial") -or
        [string]$dailyPlan.action_queue.status -ne
            [string]$queue.status) {
        throw "Stage $Iteration did not compile a runnable daily plan."
    }
    return [pscustomobject]@{
        DailyPlanPath = $dailyPlanPath
        QueuePath = $queuePath
        ExecutionPath = $executionPath
        QueueId = [string]$queue.queue_id
        QueueItemId = [string]$step.queue_item_id
        PrimitiveKind = [string]$step.primitive_kind
        QuestProgressBefore = $step.quest_progress_before
        QuestProgressAfter = $step.quest_progress_after
    }
}

foreach ($port in @($BackendPort, 8765, 8767)) {
    $listener = Get-NetTCPConnection -State Listen `
        -LocalPort $port -ErrorAction SilentlyContinue
    if ($null -ne $listener) {
        throw "Runtime lifecycle smoke requires unused port $port."
    }
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=training_machine"
$executorUrl = "http://127.0.0.1:8767"
$executeUrl = "$executorUrl/api/v1/training/execute"
$loopDll = Join-Path $ProjectRoot (
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\" +
    "StardewAI.LiveTrainingLoop.dll")
if (-not (Test-Path $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (
    Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$strategyLedgerRoot = Join-Path $runDirectory (
    "strategy-commitments")
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $runDirectory |
    Out-Null

& (Join-Path $ProjectRoot `
    "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& dotnet build (Join-Path $ProjectRoot (
    "tools\StardewAI.LiveTrainingLoop\" +
    "StardewAI.LiveTrainingLoop.csproj")) `
    -c Release --no-restore --nologo | Out-Null
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path $loopDll -PathType Leaf)) {
    throw "LiveTrainingLoop Release build failed."
}

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
    $env:STARDEWAI_STRATEGY_LEDGER_DIR =
        $strategyLedgerRoot
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @(
            "run", "--no-restore", "--project",
            (Join-Path $ProjectRoot (
                "src\StardewAI.Backend\" +
                "StardewAI.Backend.csproj")),
            "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-Health -Url "$backendUrl/health" `
        -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -PassThru
    $executorHealth = Wait-Health `
        -Url "$executorUrl/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $initial = Wait-WorldSnapshot -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-supported-machine-capacity"
        queue_item_id =
            "runtime-supported-machine-capacity.setup"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_machine_lifecycle_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        recipe_name = $RecipeName
        output_qualified_item_id = $MachineQualifiedItemId
        process_input_qualified_item_id =
            $ProcessInputQualifiedItemId
        process_input_quantity = $ProcessInputQuantity
        process_additional_items_json = "[]"
        crafting_source = "native_personal_crafting_menu"
    }
    $setupResult = Invoke-JsonPost -Url $executeUrl `
        -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory `
        "setup-result.json") $setupResult
    if ([string]$setupResult.status -ne "applied" -or
        [string]$setupResult.primitive_verification_status -ne
            "verified") {
        throw "Machine lifecycle fixture failed."
    }

    $fixtureSnapshot = Wait-LifecycleState -Url $snapshotUrl `
        -Stage fixture
    Write-JsonFile (Join-Path $runDirectory `
        "fixture-snapshot.json") $fixtureSnapshot

    $inventoryFixtureResult = $null
    if ($FixtureCapacityMode -eq "inventory") {
        $inventoryFixtureRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-supported-machine-capacity"
            queue_item_id =
                "runtime-supported-machine-capacity.setup-inventory"
            before_state_hash = [string]$fixtureSnapshot.state_hash
            option_id = "debug.setup_machine_placement_target"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $TargetTileX
            target_tile_y = $TargetTileY
            qualified_item_id = $MachineQualifiedItemId
        }
        $inventoryFixtureResult = Invoke-JsonPost -Url $executeUrl `
            -Body $inventoryFixtureRequest
        Write-JsonFile (Join-Path $runDirectory `
            "inventory-fixture-result.json") $inventoryFixtureResult
        if ([string]$inventoryFixtureResult.status -ne "applied" -or
            [string]$inventoryFixtureResult.primitive_verification_status -ne
                "verified") {
            throw "Inventory machine fixture failed."
        }
        $fixtureSnapshot = Wait-LifecycleState -Url $snapshotUrl `
            -Stage crafted
        Write-JsonFile (Join-Path $runDirectory `
            "inventory-fixture-snapshot.json") $fixtureSnapshot
    }

    $taskSetupResult = $null
    if ($taskMode) {
        $taskSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-supported-machine-capacity"
            queue_item_id =
                "runtime-supported-machine-capacity.setup-task"
            before_state_hash = [string]$fixtureSnapshot.state_hash
            option_id = "debug.setup_collection_task_fixture"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            quest_family = $TaskFamily
            quest_id = $TaskId
            qualified_item_id = $TaskOutputQualifiedItemId
            quest_expected_target_count = 1
        }
        $taskSetupResult = Invoke-JsonPost -Url $executeUrl `
            -Body $taskSetupRequest
        Write-JsonFile (Join-Path $runDirectory `
            "task-setup-result.json") $taskSetupResult
        if ([string]$taskSetupResult.status -ne "applied" -or
            [string]$taskSetupResult.primitive_verification_status -ne
                "verified") {
            throw "Machine lifecycle task fixture failed."
        }
        $fixtureSnapshot = Wait-WorldSnapshot -Url $snapshotUrl `
            -TimeoutSeconds 30
        Write-JsonFile (Join-Path $runDirectory `
            "task-fixture-snapshot.json") $fixtureSnapshot
    }

    $craftStage = $null
    $craftIntent = $null
    $placementIteration = 1
    if ($FixtureCapacityMode -eq "empty") {
        $craftStage = Invoke-LifecycleStage -Iteration 1 `
            -ExpectedExecutorOptionId "executor.craft_machine_item"
        $craftedSnapshot = Wait-LifecycleState -Url $snapshotUrl `
            -Stage crafted
        $craftLedger = Get-LatestSupportLedger `
            -Snapshot $craftedSnapshot
        $craftIntent = Assert-SupportIntent -Ledger $craftLedger `
            -ExpectedStatus active -ExpectedStage craft_selected
        Write-JsonFile (Join-Path $runDirectory `
            "craft-selected-ledger.json") $craftLedger
        $placementIteration = 2
    }

    $placementStage = Invoke-LifecycleStage `
        -Iteration $placementIteration `
        -ExpectedExecutorOptionId "executor.place_machine"
    $placementQueue = Get-Content $placementStage.QueuePath -Raw |
        ConvertFrom-Json
    $placementQueueItem = @($placementQueue.items) |
        Where-Object {
            [string]$_.option_id -eq "executor.place_machine"
        } |
        Select-Object -First 1
    if ($null -eq $placementQueueItem) {
        throw "Placement stage lost its compiled executor item."
    }
    $lifecycleTargetX = [int](Read-QueueParameter `
        -QueueItem $placementQueueItem -Name "target_tile_x")
    $lifecycleTargetY = [int](Read-QueueParameter `
        -QueueItem $placementQueueItem -Name "target_tile_y")
    $placedSnapshot = Wait-LifecycleState -Url $snapshotUrl `
        -Stage placed
    $placementLedger = Get-LatestSupportLedger `
        -Snapshot $placedSnapshot
    $placementIntent = Assert-SupportIntent `
        -Ledger $placementLedger -ExpectedStatus active `
        -ExpectedStage placement_bound -RequireTarget
    Write-JsonFile (Join-Path $runDirectory `
        "placement-bound-ledger.json") $placementLedger

    $loadCandidateOption = if ($taskMode) {
        "farm.fulfill_machine_task_demand"
    } else {
        "farm.establish_supported_machine_capacity"
    }
    $loadIteration = $placementIteration + 1
    $loadStage = Invoke-LifecycleStage -Iteration $loadIteration `
        -ExpectedExecutorOptionId "executor.load_machine_input" `
        -CandidateOptionId $loadCandidateOption
    if ($taskMode -and [int]$loadStage.QuestProgressAfter -ne 0) {
        throw "Task progress changed during machine input loading."
    }
    $processingSnapshot = Wait-LifecycleState -Url $snapshotUrl `
        -Stage processing
    $processingJson = $processingSnapshot |
        ConvertTo-Json -Depth 96 -Compress
    Invoke-JsonPostRaw -Url "$backendUrl/api/v1/snapshots" `
        -Json $processingJson | Out-Null
    $completedLedger = Get-LatestSupportLedger `
        -Snapshot $processingSnapshot
    $completedIntent = Assert-SupportIntent `
        -Ledger $completedLedger -ExpectedStatus completed `
        -ExpectedStage placement_bound -RequireTarget
    if ([string]$completedIntent.completion_reason -ne
        "exact_target_machine_processing_observed") {
        throw "Support intent completion reason was not exact."
    }
    Write-JsonFile (Join-Path $runDirectory `
        "completed-ledger.json") $completedLedger

    $collectStage = $null
    if ($taskMode) {
        $readySnapshot = Wait-TaskMachineReady -Url $snapshotUrl `
            -TimeoutSeconds $CompletionTimeoutSeconds
        Write-JsonFile (Join-Path $runDirectory `
            "ready-snapshot.json") $readySnapshot
        $collectStage = Invoke-LifecycleStage `
            -Iteration ($loadIteration + 1) `
            -ExpectedExecutorOptionId `
                "executor.collect_machine_output" `
            -CandidateOptionId `
                "farm.fulfill_machine_task_demand"
        if ([int]$collectStage.QuestProgressBefore -ne 0 -or
            [int]$collectStage.QuestProgressAfter -ne 1) {
            throw "Native machine collection did not advance the task."
        }
    }

    $datasetPath = Join-Path $loopRoot (
        "datasets\live-training-feature-rows.jsonl")
    if (-not (Test-Path $datasetPath -PathType Leaf)) {
        throw "Lifecycle training dataset was not written."
    }
    $rows = @(Get-Content $datasetPath | ForEach-Object {
        $_ | ConvertFrom-Json
    })
    $expectedExecutors = @()
    if ($FixtureCapacityMode -eq "empty") {
        $expectedExecutors += "executor.craft_machine_item"
    }
    $expectedExecutors += @(
        "executor.place_machine",
        "executor.load_machine_input")
    if ($taskMode) {
        $expectedExecutors += "executor.collect_machine_output"
    }
    foreach ($expectedExecutor in $expectedExecutors) {
        $matching = @($rows | Where-Object {
            @($_.action_features.option_ids) -contains
                $expectedExecutor
        })
        if ($matching.Count -eq 0) {
            throw (
                "Training feature row missing executor: " +
                $expectedExecutor)
        }
    }

    $stageQueueIds = @()
    if ($null -ne $craftStage) {
        $stageQueueIds += [string]$craftStage.QueueId
    }
    $stageQueueIds += [string]$placementStage.QueueId
    $stageQueueIds += [string]$loadStage.QueueId
    if ($taskMode) {
        $stageQueueIds += [string]$collectStage.QueueId
    }

    $summary = [ordered]@{
        status = "passed"
        evidence_id = $(if ($taskMode) {
            "EVD-217"
        } else {
            "EVD-215"
        })
        run_id = $RunId
        save_slot = $SaveSlot
        option_id =
            "farm.establish_supported_machine_capacity"
        target_location_id = "Farm"
        fixture_clear_tile = "$TargetTileX,$TargetTileY"
        bound_target_tile =
            "$lifecycleTargetX,$lifecycleTargetY"
        machine_qualified_item_id = $MachineQualifiedItemId
        process_input_qualified_item_id =
            $ProcessInputQualifiedItemId
        process_input_quantity = $ProcessInputQuantity
        fixture_capacity_mode = $FixtureCapacityMode
        task_family = $TaskFamily
        task_id = $(if ($taskMode) { $TaskId } else { "" })
        task_output_qualified_item_id = $(if ($taskMode) {
            $TaskOutputQualifiedItemId
        } else {
            ""
        })
        support_intent_id = [string]$completedIntent.intent_id
        craft_selected_verified =
            $FixtureCapacityMode -eq "empty" -and
            [string]$craftIntent.stage -eq "craft_selected"
        direct_inventory_placement_verified =
            $FixtureCapacityMode -eq "inventory" -and
            $null -eq $craftStage -and
            [string]$placementIntent.stage -eq "placement_bound"
        placement_bound_verified =
            [string]$placementIntent.stage -eq "placement_bound"
        processing_started = $true
        task_progress_after_load = $(if ($taskMode) {
            [int]$loadStage.QuestProgressAfter
        } else {
            $null
        })
        task_progress_after_collect = $(if ($taskMode) {
            [int]$collectStage.QuestProgressAfter
        } else {
            $null
        })
        completion_reason =
            [string]$completedIntent.completion_reason
        training_feature_row_count = $rows.Count
        training_feature_executors = $expectedExecutors
        stage_queue_ids = $stageQueueIds
        dataset_path = $datasetPath
        executor_health = $executorHealth
        backend_process_id = $backendProcess.Id
        smapi_process_id = $gameProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") `
        $summary
    $summary | ConvertTo-Json -Depth 12
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if ($backendProcess -and
        -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force `
            -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and $gameProcess -and
        -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force `
            -ErrorAction SilentlyContinue
    }
}
