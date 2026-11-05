param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $FishingLocation = "Beach",
    [string] $FishFrenzyQualifiedItemId = "",
    [int] $FishFrenzyTileX = 44,
    [int] $FishFrenzyTileY = 29,
    [string] $FishPondQualifiedItemId = "",
    [int] $FishPondTileX = 65,
    [int] $FishPondTileY = 18,
    [string] $CrabPotQualifiedItemId = "",
    [int] $CrabPotTileX = 39,
    [int] $CrabPotTileY = 51,
    [switch] $IncludeCrabPotCounterfactual,
    [int] $CrabPotCounterfactualTileX = 39,
    [int] $CrabPotCounterfactualTileY = 56,
    [ValidateRange(0, 28)] [int] $MasterAnglerTerminalBoundaryDay = 0,
    [int] $FishingMaxAttempts = 1,
    [int] $MineFishingLevel = 0,
    [int] $MineFishingMaxAttempts = 12,
    [ValidateSet("none", "ordinary_quest", "special_order")]
    [string] $TaskFamily = "none",
    [string] $TaskId = "stardewai.runtime.fishing-source",
    [string] $TaskQualifiedItemId = "",
    [int] $TaskTargetCount = 99,
    [string] $RunId = ("runtime-fishing-daily-plan-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-fishing-daily-plan-smoke",
    [int] $BackendPort = 5129,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning,
    [switch] $IsolateMods,
    [switch] $IsolateSave,
    [switch] $SetupLocationFixture,
    [switch] $NoBuild,
    [switch] $StandaloneCollectionTeacher,
    [switch] $StandaloneSkillTeacher,
    [ValidateSet(0, 15, 25)] [int] $SkillTerminalBoundaryLevel = 0,
    [ValidateRange(1, 28)] [int] $SkillTerminalBoundaryDay = 7,
    [string] $RequirementInventory = (Join-Path $ProjectRoot "experiments\local-data\output\authoritative-requirement-inventory-v1.json"),
    [string] $AcquisitionLowering = (Join-Path $ProjectRoot "experiments\local-data\output\acquisition-route-option-lowering-v1.json"),
    [string] $MasterAnglerWindows = (Join-Path $ProjectRoot "experiments\local-data\output\master-angler-stage-one-window-index-v1.json"),
    [string] $RouteTimingCalibration = "I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json",
    [switch] $RunSparseMappingFixture
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
. (Join-Path $PSScriptRoot "Deploy.Common.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeFishingCollectionEvidence.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeSkillCollectionEvidence.ps1")
$isCrabPotFixture = -not [string]::IsNullOrWhiteSpace($CrabPotQualifiedItemId)
if ($StandaloneSkillTeacher) {
    if ($StandaloneCollectionTeacher -or $MasterAnglerTerminalBoundaryDay -gt 0 -or
        -not $IsolateSave -or -not $IsolateMods -or -not $isCrabPotFixture -or
        -not $IncludeCrabPotCounterfactual -or $FishingLocation -ne "Farm" -or
        $TaskFamily -ne "none" -or $SkillTerminalBoundaryLevel -notin @(15, 25) -or $FishingMaxAttempts -ne 1) {
        throw "Skills threshold capture requires one isolated native crab-pot comparison, a declared 15/25 boundary and no collection/global override."
    }
}
elseif ($SkillTerminalBoundaryLevel -ne 0) {
    throw "Skills terminal boundary requires the explicit standalone Skills source mode."
}
if ($MasterAnglerTerminalBoundaryDay -gt 0 -and (-not $isCrabPotFixture -or -not $IncludeCrabPotCounterfactual)) {
    throw "Terminal save boundary capture requires the declared crab-pot and counterfactual boundaries."
}
if ($IncludeCrabPotCounterfactual -and -not $isCrabPotFixture) {
    throw "Crab-pot counterfactual requires a declared crab-pot capture boundary."
}
if ($isCrabPotFixture -and ((-not $StandaloneCollectionTeacher -and -not $StandaloneSkillTeacher) -or -not $IsolateSave -or -not $IsolateMods -or $FishingLocation -ne "Farm")) {
    throw "Crab-pot collection capture requires standalone Teacher, save/mod isolation and explicit Farm location."
}
$fixtureCount = @(
    -not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId),
    -not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId),
    $MineFishingLevel -gt 0
    $SetupLocationFixture.IsPresent
    $isCrabPotFixture
) | Where-Object { $_ }
if ($fixtureCount.Count -gt 1) {
    throw "Configure only one isolated special fishing fixture per run."
}
if ($SetupLocationFixture -and -not $IsolateSave) {
    throw "Location fishing fixtures require an isolated save copy."
}
if ($StandaloneCollectionTeacher) {
    if (-not $IsolateSave -or -not $IsolateMods -or $TaskFamily -ne "none") {
        throw "Standalone collection Teacher capture requires save/mod isolation and no task override."
    }
    foreach ($path in @($RequirementInventory, $AcquisitionLowering, $MasterAnglerWindows, $RouteTimingCalibration)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Teacher authority input is missing: $path" }
    }
}

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 180)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
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

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
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

function Wait-FishingSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $requestTimeoutSeconds = [Math]::Max(5, [Math]::Min($TimeoutSeconds, 180))
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds $requestTimeoutSeconds
            $context = $snapshot.state.fishing.location_context.value
            $tiles = @($snapshot.state.fishing.fishable_tiles.value)
            $rods = @($snapshot.state.fishing.rod_contexts.value)
            $completeRods = @($rods | Where-Object { $_.complete -eq $true -and $_.special_catch_sources_complete -eq $true })
            $lastStatus = "save_id=$($snapshot.save_id.status);can_fish=$($context.can_fish_here);tiles=$($tiles.Count);complete_rods=$($completeRods.Count)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $context.can_fish_here -eq $true -and
                $tiles.Count -gt 0 -and
                $completeRods.Count -gt 0) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "The isolated save did not reach a fully transparent fishable state. Put this test save on a fishable map with a rod before retrying. Last status: $lastStatus"
}

function Read-CollectionTaskProgress {
    param($Snapshot)
    if ($TaskFamily -eq "special_order") {
        $order = $Snapshot.state.quests.special_orders.value |
            Where-Object { [string]$_.quest_key -eq $TaskId } |
            Select-Object -First 1
        if ($null -eq $order) {
            if ($TaskId -in @($Snapshot.state.quests.completed_special_orders.value)) {
                return $TaskTargetCount
            }
            return $null
        }
        if (@($order.objectives).Count -eq 0) { return $null }
        return [int]@($order.objectives)[0].current_count
    }
    $quest = $Snapshot.state.quests.active_quests.value |
        Where-Object {
            [string]$_.id -eq $TaskId -and
            [string]$_.runtime_type -eq "ResourceCollectionQuest"
        } |
        Select-Object -First 1
    if ($null -eq $quest) { return $null }
    return [int]$quest.per_type_fields.number_collected
}

function Resolve-CompactTrainingAttempts {
    param([string] $SnapshotDir, [string] $DatasetPath)

    $queueRecordsById = @{}
    foreach ($candidateQueuePath in @(Get-ChildItem -LiteralPath $SnapshotDir -Filter "*compiled-queue-*.json" | Sort-Object Name)) {
        if ($candidateQueuePath.Name -notmatch '^(?:compiled-queue-(?<ordinal>\d{4})|continuation-compiled-queue-(?<ordinal>\d{4})-item-\d{4})\.json$') { continue }
        $candidateQueue = Get-Content -LiteralPath $candidateQueuePath.FullName -Raw | ConvertFrom-Json
        $queueId = [string]$candidateQueue.queue_id
        if ([string]::IsNullOrWhiteSpace($queueId)) { throw "Compiled queue artifact omitted queue_id: $($candidateQueuePath.FullName)" }
        if ($queueRecordsById.ContainsKey($queueId)) { throw "Duplicate compiled queue_id in artifacts: $queueId" }
        $iterationText = $Matches['ordinal']
        $executionPath = Join-Path $SnapshotDir "execution-$iterationText.json"
        $queueRecordsById[$queueId] = [pscustomobject]@{
            iteration = [int]$iterationText
            iteration_text = $iterationText
            queue_path = $candidateQueuePath.FullName
            execution_path = $executionPath
        }
    }
    if ($queueRecordsById.Count -eq 0) { throw "No compiled queue artifacts were written." }

    $attemptRecords = @()
    foreach ($datasetLine in [System.IO.File]::ReadLines($DatasetPath)) {
        if ([string]::IsNullOrWhiteSpace($datasetLine)) { continue }
        $featureRow = $datasetLine | ConvertFrom-Json
        $queueId = [string]$featureRow.queue_id
        if ([string]::IsNullOrWhiteSpace($queueId)) { throw "Compact JSONL training row omitted queue_id." }
        if (-not $queueRecordsById.ContainsKey($queueId)) { throw "Compact JSONL training row did not map to a compiled queue artifact by queue_id: $queueId" }
        $queueRecord = $queueRecordsById[$queueId]
        if (-not (Test-Path -LiteralPath $queueRecord.execution_path)) {
            throw "Compact JSONL training row mapped to missing aggregate execution artifact: $($queueRecord.execution_path)"
        }
        $optionId = [string](@($featureRow.action_features.option_ids) | Select-Object -First 1)
        $stepObservedCatch = Read-RuntimeFishingObservedCatch -OptionId $optionId -Facts $featureRow.action_features.changed_facts
        $attemptRecords += [pscustomobject]@{
            iteration = $queueRecord.iteration
            iteration_text = $queueRecord.iteration_text
            queue_path = $queueRecord.queue_path
            execution_path = $queueRecord.execution_path
            row_id = [string]$featureRow.row_id
            queue_id = $queueId
            option_id = $optionId
            primitive_verification_reasons = @($featureRow.action_features.primitive_verification_reasons)
            primitive_verified = (@($featureRow.action_features.features.boolean) | Where-Object { $_.name -eq "execution.primitive_verified" -and $_.value -eq $true } | Select-Object -First 1)
            observed_caught_qualified_item_id = [string]$stepObservedCatch
        }
    }
    if ($attemptRecords.Count -eq 0) { throw "No compact JSONL training rows were written." }
    return @($attemptRecords)
}

function Invoke-SparseCompactMappingFixture {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("stardewai-compact-row-map-" + [Guid]::NewGuid().ToString("N"))
    $snapshotDir = Join-Path $fixtureRoot "live-snapshots"
    New-Item -ItemType Directory -Path $snapshotDir | Out-Null
    try {
        foreach ($iteration in @(1, 2, 5)) {
            Write-JsonFile (Join-Path $snapshotDir ("compiled-queue-{0:D4}.json" -f $iteration)) ([ordered]@{ queue_id = "queue-$iteration" })
            Write-JsonFile (Join-Path $snapshotDir ("execution-{0:D4}.json" -f $iteration)) ([ordered]@{ status = "fixture" })
        }
        $datasetPath = Join-Path $fixtureRoot "live-training-feature-rows.jsonl"
        $rows = @(
            '{"row_id":"row-1","queue_id":"queue-1","action_features":{"option_ids":["executor.catch_fish"],"changed_facts":[],"primitive_verification_reasons":[],"features":{"boolean":[]}}}',
            '{"row_id":"row-2","queue_id":"queue-2","action_features":{"option_ids":["executor.catch_fish"],"changed_facts":[],"primitive_verification_reasons":[],"features":{"boolean":[]}}}',
            '{"row_id":"row-3","queue_id":"queue-5","action_features":{"option_ids":["executor.catch_fish"],"changed_facts":[{"path":"fishing.caught_qualified_item_id","after":"(O)162"}],"primitive_verification_reasons":[],"features":{"boolean":[{"name":"execution.primitive_verified","value":true}]}}}'
        )
        $rows | Set-Content -LiteralPath $datasetPath -Encoding utf8
        $attempts = @(Resolve-CompactTrainingAttempts -SnapshotDir $snapshotDir -DatasetPath $datasetPath)
        $thirdAttempt = $attempts[2]
        if ($attempts.Count -ne 3 -or $attempts[0].iteration -ne 1 -or $attempts[1].iteration -ne 2 -or $thirdAttempt.iteration -ne 5) {
            throw "Sparse compact row fixture failed: row 3 mapped to iteration $($thirdAttempt.iteration), expected 5."
        }
        [ordered]@{
            status = "passed"
            row_3_iteration = $thirdAttempt.iteration
            row_3_execution_path = $thirdAttempt.execution_path
        } | ConvertTo-Json -Depth 4
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
    }
}

if ($RunSparseMappingFixture) {
    Invoke-SparseCompactMappingFixture
    return
}

function Test-MineArea80Prerequisites {
    param($Snapshot, [int] $MineLevel)
    $mineHandler = @($Snapshot.state.fishing.special_catch_sources.value.location_get_fish_override.handlers) | Where-Object {
        $_.handler -eq "mine_shaft_fishing"
    } | Select-Object -First 1
    $selectedRod = @($Snapshot.state.fishing.rod_inventory.value) | Where-Object { $_.selected -eq $true } | Select-Object -First 1
    $inventoryCapacity = $Snapshot.state.player.inventory_capacity.value
    $energy = $Snapshot.state.player.energy.value
    return $Snapshot.state.fishing.location_context.value.location_id -eq "UndergroundMine$MineLevel" -and
        $null -ne $mineHandler -and
        $mineHandler.mine_area -eq 80 -and
        $mineHandler.special_fish_qualified_item_id -eq "(O)162" -and
        $mineHandler.has_curiosity_lure -eq $true -and
        [string]$mineHandler.bait_internal_name -like "*Lava Eel*" -and
        $mineHandler.specific_bait_name_condition_complete -eq $true -and
        $mineHandler.specific_bait_name_condition_matched -eq $true -and
        $null -ne $selectedRod -and
        -not [string]::IsNullOrWhiteSpace([string]$selectedRod.qualified_item_id) -and
        $selectedRod.slot_index -ge 0 -and
        $selectedRod.upgrade_level -eq 4 -and
        $selectedRod.attachment_slot_count -ge 3 -and
        $selectedRod.has_curiosity_lure -eq $true -and
        @(@($selectedRod.tackle) | Where-Object { $_.qualified_item_id -eq "(O)695" }).Count -ge 1 -and
        $selectedRod.bait.qualified_item_id -eq "(O)SpecificBait" -and
        [string]$selectedRod.bait.internal_name -like "*Lava Eel*" -and
        $selectedRod.bait.preserved_parent_sheet_index -eq "162" -and
        $inventoryCapacity.max_items -ge 36 -and
        $inventoryCapacity.has_empty_slot -eq $true -and
        $inventoryCapacity.empty_slots -gt 0 -and
        $energy -ge 200
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 15
            $location = Read-FieldValue $snapshot "player" "location_id"
            if (-not [string]::IsNullOrWhiteSpace([string]$location)) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready snapshot. Last error: $lastError"
}

function Find-RoutePath {
    param($Snapshot, [string] $StartLocation, [string] $TargetLocation)
    $graph = Read-FieldValue $Snapshot "locations" "route_graph"
    if ($null -eq $graph -or $null -eq $graph.edges) { return @() }
    $edges = @($graph.edges | Where-Object {
        $_.resolved -eq $true -and
        -not [string]::IsNullOrWhiteSpace([string]$_.from_location) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.target_location) -and
        $null -ne $_.from_x -and $null -ne $_.from_y
    })
    $queue = New-Object System.Collections.Queue
    $queue.Enqueue($StartLocation)
    $visited = @{ $StartLocation = $true }
    $previousLocation = @{}
    $previousEdge = @{}
    while ($queue.Count -gt 0) {
        $location = [string]$queue.Dequeue()
        if ($location -eq $TargetLocation) { break }
        foreach ($edge in @($edges | Where-Object { ([string]$_.from_location) -eq $location } | Sort-Object from_y, from_x, target_location)) {
            $next = [string]$edge.target_location
            if ($visited.ContainsKey($next)) { continue }
            $visited[$next] = $true
            $previousLocation[$next] = $location
            $previousEdge[$next] = $edge
            $queue.Enqueue($next)
        }
    }
    if (-not $visited.ContainsKey($TargetLocation)) { return @() }
    $path = @()
    $cursor = $TargetLocation
    while ($cursor -ne $StartLocation) {
        $edge = $previousEdge[$cursor]
        $path = @($edge) + $path
        $cursor = $previousLocation[$cursor]
    }
    return $path
}

function Wait-LocationSnapshot {
    param([string] $Url, [string] $ExpectedLocation, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastLocation = ""
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $lastLocation = [string](Read-FieldValue $snapshot "player" "location_id")
            if ($lastLocation -eq $ExpectedLocation) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for location $ExpectedLocation; last location was $lastLocation. Last error: $lastError"
}

function Invoke-FishingSetupRoute {
    param($Snapshot, [string] $TargetLocation, [string] $RunId, [string] $SavePath, [string] $SnapshotUrl)
    $startLocation = [string](Read-FieldValue $Snapshot "player" "location_id")
    if ($startLocation -eq $TargetLocation) {
        return [pscustomobject]@{ Snapshot = $Snapshot; Segments = @() }
    }
    $path = @(Find-RoutePath -Snapshot $Snapshot -StartLocation $startLocation -TargetLocation $TargetLocation)
    if ($path.Count -eq 0) { throw "No transparent route from $startLocation to fishing location $TargetLocation" }

    $segments = @()
    $current = $Snapshot
    foreach ($edge in $path) {
        $currentLocation = [string](Read-FieldValue $current "player" "location_id")
        if ($currentLocation -ne [string]$edge.from_location) {
            throw "Fishing setup route drifted: expected $($edge.from_location), observed $currentLocation"
        }
        $request = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-setup-route"
            queue_item_id = "route.$($edge.from_location).to.$($edge.target_location).$([guid]::NewGuid().ToString('N'))"
            before_state_hash = $current.state_hash
            option_id = "executor.traverse_connector"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $SavePath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            max_crops = 512
            target_tile_x = [int]$edge.from_x
            target_tile_y = [int]$edge.from_y
            connector_kind = [string]$edge.kind
            expected_target_location = [string]$edge.target_location
            expected_arrival_tile_x = if ($null -ne $edge.target_x) { [int]$edge.target_x } else { $null }
            expected_arrival_tile_y = if ($null -ne $edge.target_y) { [int]$edge.target_y } else { $null }
        }
        $result = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request -TimeoutSeconds 600
        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            throw "Fishing setup route segment failed: $($edge.from_location) -> $($edge.target_location); status=$($result.status); reasons=$(@($result.block_reasons) -join ',')"
        }
        $current = Wait-LocationSnapshot -Url $SnapshotUrl -ExpectedLocation ([string]$edge.target_location) -TimeoutSeconds 90
        $segments += [pscustomobject]@{
            from_location = [string]$edge.from_location
            target_location = [string]$edge.target_location
            connector_kind = [string]$edge.kind
            target_tile_x = [int]$edge.from_x
            target_tile_y = [int]$edge.from_y
            result_status = $result.status
            primitive_verification_status = $result.primitive_verification_status
            primitive_verification_reasons = @($result.primitive_verification_reasons)
        }
    }
    return [pscustomobject]@{ Snapshot = $current; Segments = $segments }
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = if ($StandaloneCollectionTeacher -or $StandaloneSkillTeacher) {
    "http://127.0.0.1:8765/api/v1/snapshot?profile=collection_teacher"
}
else { "http://127.0.0.1:8765/api/v1/snapshot?profile=fishing" }
$worldSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=route"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$snapshotPath = Join-Path $runDirectory "fishing-snapshot.json"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
if (Test-Path -LiteralPath $runDirectory) { throw "Run directory already exists: $runDirectory" }
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$sourceSavePath = Join-Path $savesPath $SaveSlot
$sourceSaveHash = Get-DirectoryContentHash -Path $sourceSavePath
if ($IsolateSave) {
    $savesPath = Join-Path $runDirectory "isolated-saves"
    New-Item -ItemType Directory -Path $savesPath | Out-Null
    Copy-Item -LiteralPath $sourceSavePath -Destination (Join-Path $savesPath $SaveSlot) -Recurse
    if ((Get-DirectoryContentHash -Path (Join-Path $savesPath $SaveSlot)) -cne $sourceSaveHash) {
        throw "Isolated fishing save does not match its source."
    }
}
foreach ($port in @($BackendPort, 8765, 8767)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Fishing smoke port is already in use: $port"
    }
}
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild:$NoBuild | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild:$NoBuild | Out-Null
$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
foreach ($project in @("src\StardewAI.Backend", "tools\StardewAI.LiveTrainingLoop")) {
    if (-not $NoBuild) {
        & dotnet build (Join-Path $ProjectRoot $project) -c Release --disable-build-servers -m:1 `
            -p:BuildInParallel=false -p:UseSharedCompilation=false -p:EnableModZip=false "-p:GamePath=$runtimeGameDir"
        if ($LASTEXITCODE -ne 0) { throw "Fishing smoke dependency build failed: $project" }
    }
}
foreach ($component in @(
    @{ Dll = $backendDll; Source = "src\StardewAI.Backend" },
    @{ Dll = $loopDll; Source = "tools\StardewAI.LiveTrainingLoop" }
)) {
    Assert-StardewAIBuildOutputFresh -OutputPath $component.Dll -InputRoots @(
        (Join-Path $ProjectRoot "src\StardewAI.Contracts"),
        (Join-Path $ProjectRoot "src\StardewAI.Core"),
        (Join-Path $ProjectRoot $component.Source),
        (Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap")
    ) -InputFiles @((Join-Path $ProjectRoot "Directory.Build.props"), (Join-Path $ProjectRoot "Directory.Build.targets"))
}
$terminalSaveBoundaryPath = ""
$skillSaveBoundaryPath = ""
if ($StandaloneSkillTeacher) {
    $requestPath = Join-Path $runDirectory "skill-terminal-save-boundary-request.json"
    $skillSaveBoundaryPath = Join-Path $runDirectory "skill-terminal-save-boundary.json"
    Write-JsonFile $requestPath ([ordered]@{
        schema_version = "skill_terminal_save_boundary_request.v1"
        source_save_file = Join-Path $sourceSavePath $SaveSlot
        isolated_save_file = Join-Path (Join-Path $savesPath $SaveSlot) $SaveSlot
        isolation_root = $savesPath
        target_scoring_level = $SkillTerminalBoundaryLevel
        decision_spring_day_year_two = $SkillTerminalBoundaryDay
        declared_local_prerequisite_only = $true
        formal_product_training_authorized = $false
    })
    & dotnet (Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll") `
        prepare-skill-terminal-save-boundary --request $requestPath --output $skillSaveBoundaryPath
    if ($LASTEXITCODE -ne 0) { throw "Skills save boundary preparation failed before game launch." }
}
if ($MasterAnglerTerminalBoundaryDay -gt 0) {
    $requestPath = Join-Path $runDirectory "terminal-save-boundary-request.json"
    $terminalSaveBoundaryPath = Join-Path $runDirectory "terminal-save-boundary.json"
    Write-JsonFile $requestPath ([ordered]@{
        schema_version = "master_angler_terminal_save_boundary_request.v1"
        source_save_file = Join-Path $sourceSavePath $SaveSlot
        isolated_save_file = Join-Path (Join-Path $savesPath $SaveSlot) $SaveSlot
        isolation_root = $savesPath
        requirement_inventory_path = $RequirementInventory
        acquisition_lowering_path = $AcquisitionLowering
        missing_qualified_item_id = $CrabPotQualifiedItemId
        decision_spring_day_year_two = $MasterAnglerTerminalBoundaryDay
        declared_local_prerequisite_only = $true
        formal_product_training_authorized = $false
    })
    & dotnet (Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll") `
        prepare-master-angler-terminal-save-boundary --request $requestPath --output $terminalSaveBoundaryPath
    if ($LASTEXITCODE -ne 0) { throw "Terminal save boundary preparation failed before game launch." }
}
$gameArguments = @{}
if ($IsolateMods) {
    $modsPath = Join-Path $runDirectory "isolated-mods"
    New-Item -ItemType Directory -Path $modsPath | Out-Null
    foreach ($name in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
        Copy-Item -LiteralPath (Join-Path $runtimeGameDir "Mods\$name") -Destination (Join-Path $modsPath $name) -Recurse
    }
    $gameArguments.ArgumentList = @('--mods-path', ('"' + $modsPath + '"'))
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
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
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = Join-Path $runDirectory "runtime-receipts"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @('"' + $backendDll + '"') `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameWindowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle $gameWindowStyle -PassThru @gameArguments
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -Url $worldSnapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $worldStartLocation = [string](Read-FieldValue $worldSnapshot "player" "location_id")
    if ($isCrabPotFixture) {
        if ($IncludeCrabPotCounterfactual) {
            $counterfactualBoundary = Initialize-RuntimeCrabPotCaptureBoundary -RunId $RunId -SavePath $savesPath `
                -SnapshotUrl $worldSnapshotUrl -Snapshot $worldSnapshot -QualifiedItemId $CrabPotQualifiedItemId `
                -TileX $CrabPotCounterfactualTileX -TileY $CrabPotCounterfactualTileY -OutputRoot $runDirectory -Identity counterfactual
            $worldSnapshot = $counterfactualBoundary.Snapshot
        }
        $primaryBoundary = Initialize-RuntimeCrabPotCaptureBoundary -RunId $RunId -SavePath $savesPath `
            -SnapshotUrl $worldSnapshotUrl -Snapshot $worldSnapshot -QualifiedItemId $CrabPotQualifiedItemId `
            -TileX $CrabPotTileX -TileY $CrabPotTileY -OutputRoot $runDirectory -Identity primary
        $CrabPotTileX = $primaryBoundary.TileX
        $CrabPotTileY = $primaryBoundary.TileY
        $worldSnapshot = $primaryBoundary.Snapshot
        if ($IncludeCrabPotCounterfactual -and $counterfactualBoundary.TileX -eq $CrabPotTileX -and $counterfactualBoundary.TileY -eq $CrabPotTileY) {
            throw "Crab-pot counterfactual must remain a distinct native-legal pot."
        }
        $setupRoute = [pscustomobject]@{
            Snapshot = $worldSnapshot
            Segments = @()
            SkipReason = "explicit ready-pot boundary; not evidence of travel, placement, baiting or natural production"
        }
    }
    elseif ($SetupLocationFixture) {
        $fixture = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body ([ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-fixture"
            queue_item_id = "runtime-fishing-fixture.location"
            before_state_hash = $worldSnapshot.state_hash
            option_id = "debug.setup_location_fishing"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            location_id = $FishingLocation
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
        })
        Write-JsonFile (Join-Path $runDirectory "location-fixture-result.json") $fixture
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Existing native fishing-location fixture failed: $(@($fixture.block_reasons) -join ',')"
        }
        $setupRoute = [pscustomobject]@{
            Snapshot = $worldSnapshot
            Segments = @()
            SkipReason = "explicit boundary fixture; not evidence of travel or equipment acquisition"
        }
    }
    elseif ($MineFishingLevel -gt 0) {
        $setupRoute = [pscustomobject]@{
            Snapshot = $worldSnapshot
            Segments = @()
            SkipReason = "native Game1.enterMine fixture transition handles MineShaft entry"
        }
    }
    else {
        $setupRoute = Invoke-FishingSetupRoute `
            -Snapshot $worldSnapshot `
            -TargetLocation $FishingLocation `
            -RunId $RunId `
            -SavePath $savesPath `
            -SnapshotUrl $worldSnapshotUrl
    }
    Write-JsonFile (Join-Path $runDirectory "fishing-setup-route.json") ([ordered]@{
        start_location = $worldStartLocation
        target_location = $FishingLocation
        final_location = [string](Read-FieldValue $setupRoute.Snapshot "player" "location_id")
        skip_reason = [string]$setupRoute.SkipReason
        segments = @($setupRoute.Segments)
    })
    $fishFrenzySetup = $null
    $fishPondSetup = $null
    $fishPondBeforeCount = $null
    $mineFishingSetup = $null
    if (-not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId)) {
        $fishFrenzySetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-fixture"
            queue_item_id = "runtime-fishing-fixture.fish-frenzy"
            before_state_hash = $setupRoute.Snapshot.state_hash
            option_id = "debug.setup_fish_frenzy"
            target_tile_x = $FishFrenzyTileX
            target_tile_y = $FishFrenzyTileY
            qualified_item_id = $FishFrenzyQualifiedItemId
            save_isolation_path = $savesPath
        }
        $fishFrenzySetup = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $fishFrenzySetupRequest
        Write-JsonFile (Join-Path $runDirectory "fish-frenzy-setup-result.json") $fishFrenzySetup
        if ($fishFrenzySetup.status -ne "applied" -or $fishFrenzySetup.primitive_verification_status -ne "verified") {
            throw "Fish frenzy isolated fixture setup failed: status=$($fishFrenzySetup.status); reasons=$(@($fishFrenzySetup.block_reasons) -join ',')"
        }

        Start-Sleep -Milliseconds 250
        $snapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
        $frenzy = $snapshot.state.fishing.special_catch_sources.value.fish_frenzy
        if (-not $frenzy.active -or $frenzy.qualified_item_id -ne $FishFrenzyQualifiedItemId -or
            $frenzy.center_tile_x -ne $FishFrenzyTileX -or $frenzy.center_tile_y -ne $FishFrenzyTileY) {
            throw "Transparent fishing snapshot did not expose the configured fish frenzy fixture."
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId)) {
        $fishPondSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-fixture"
            queue_item_id = "runtime-fishing-fixture.fish-pond"
            before_state_hash = $setupRoute.Snapshot.state_hash
            option_id = "debug.setup_fish_pond"
            target_tile_x = $FishPondTileX
            target_tile_y = $FishPondTileY
            qualified_item_id = $FishPondQualifiedItemId
            save_isolation_path = $savesPath
        }
        $fishPondSetup = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $fishPondSetupRequest
        Write-JsonFile (Join-Path $runDirectory "fish-pond-setup-result.json") $fishPondSetup
        if ($fishPondSetup.status -ne "applied" -or $fishPondSetup.primitive_verification_status -ne "verified") {
            throw "Fish pond isolated fixture setup failed: status=$($fishPondSetup.status); reasons=$(@($fishPondSetup.block_reasons) -join ',')"
        }
        $actualPondTopLeft = @($fishPondSetup.changed_facts) | Where-Object {
            $_.path -eq "current_location.fish_pond.top_left_tile"
        } | Select-Object -ExpandProperty after -First 1
        if ([string]::IsNullOrWhiteSpace($actualPondTopLeft) -or $actualPondTopLeft -notmatch '^(\d+),(\d+)$') {
            throw "Fish pond fixture did not report its actual legal placement."
        }
        $actualFishPondTileX = [int]$Matches[1]
        $actualFishPondTileY = [int]$Matches[2]

        Start-Sleep -Milliseconds 250
        $snapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
        $pond = @($snapshot.state.fishing.special_catch_sources.value.fish_ponds) | Where-Object {
            $_.tile_x -eq $actualFishPondTileX -and $_.tile_y -eq $actualFishPondTileY
        } | Select-Object -First 1
        if ($null -eq $pond -or -not $pond.catch_available -or $pond.fish_qualified_item_id -ne $FishPondQualifiedItemId) {
            throw "Transparent fishing snapshot did not expose the configured occupied fish pond fixture."
        }
        $fishPondBeforeCount = [int]$pond.fish_count
    }
    elseif ($MineFishingLevel -gt 0) {
        $mineFishingSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-fixture"
            queue_item_id = "runtime-fishing-fixture.mine-area-80"
            before_state_hash = $setupRoute.Snapshot.state_hash
            option_id = "debug.setup_mine_fishing_floor"
            mine_level = $MineFishingLevel
            save_isolation_path = $savesPath
        }
        $mineFishingSetup = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $mineFishingSetupRequest
        Write-JsonFile (Join-Path $runDirectory "mine-fishing-setup-result.json") $mineFishingSetup
        if ($mineFishingSetup.status -ne "applied" -or $mineFishingSetup.primitive_verification_status -ne "verified") {
            throw "Mine fishing isolated fixture setup failed: status=$($mineFishingSetup.status); reasons=$(@($mineFishingSetup.block_reasons) -join ',')"
        }

        Start-Sleep -Milliseconds 250
        $snapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
        if (-not (Test-MineArea80Prerequisites -Snapshot $snapshot -MineLevel $MineFishingLevel)) {
            throw "Transparent fishing snapshot did not expose the configured MineShaft lava-area fixture."
        }
    }
    elseif ($isCrabPotFixture) {
        $snapshot = Wait-WorldSnapshot -Url ($snapshotUrl + "&fresh=1") -TimeoutSeconds $StartupTimeoutSeconds
        $pot = @($snapshot.state.current_location.objects.value) | Where-Object {
            $_.tile_x -eq $CrabPotTileX -and $_.tile_y -eq $CrabPotTileY
        } | Select-Object -First 1
        if ($null -eq $pot -or $pot.crab_pot_collect_status -ne "ready" -or
            $pot.crab_pot_output_qualified_item_id -ne $CrabPotQualifiedItemId) {
            throw "Transparent collection profile did not expose the declared ready-pot boundary."
        }
        if ($MasterAnglerTerminalBoundaryDay -gt 0) {
            $progress = Read-FieldValue $snapshot "world_progress" "fish_collection_progress"
            $achievements = $snapshot.state.world_progress.achievements
            if ($progress.eligible_species_count -ne 72 -or $progress.caught_eligible_species_count -ne 71 -or
                $progress.missing_species_count -ne 1 -or @($progress.missing_item_ids).Count -ne 1 -or
                [string]$progress.missing_item_ids[0] -ne $CrabPotQualifiedItemId.Replace("(O)", "") -or
                (Read-FieldValue $snapshot "time" "year") -ne 2 -or (Read-FieldValue $snapshot "time" "season") -ne "spring" -or
                (Read-FieldValue $snapshot "time" "day") -ne $MasterAnglerTerminalBoundaryDay -or
                (Read-FieldValue $snapshot "time" "total_days") -ne (111 + $MasterAnglerTerminalBoundaryDay) -or
                $achievements.status -notin @("available", "derived") -or @($achievements.value) -contains 26) {
                throw "Loaded game did not expose the declared 71/72 boundary without achievement 26."
            }
        }
    }
    else {
        $snapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    }
    $taskSetupResult = $null
    $taskProgressBefore = $null
    if ($TaskFamily -ne "none") {
        if ([string]::IsNullOrWhiteSpace($TaskQualifiedItemId)) {
            if ($MineFishingLevel -gt 0) {
                $TaskQualifiedItemId = "(O)167"
            }
            else {
                throw "TaskQualifiedItemId is required outside the MineShaft fishing fixture."
            }
        }
        if ($TaskTargetCount -le 0) {
            throw "TaskTargetCount must be positive."
        }
        $taskSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-fixture"
            queue_item_id = "runtime-fishing-fixture.collection-task"
            before_state_hash = $snapshot.state_hash
            option_id = "debug.setup_collection_task_fixture"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            quest_id = $TaskId
            quest_family = $TaskFamily
            quest_expected_target_count = $TaskTargetCount
            qualified_item_id = $TaskQualifiedItemId
        }
        $taskSetupResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $taskSetupRequest `
            -TimeoutSeconds 120
        Write-JsonFile (Join-Path $runDirectory "task-setup-result.json") $taskSetupResult
        if ($taskSetupResult.status -ne "applied" -or
            $taskSetupResult.primitive_verification_status -ne "verified") {
            throw "Collection task fixture failed: $(@($taskSetupResult.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 300
        $snapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
        $taskProgressBefore = Read-CollectionTaskProgress -Snapshot $snapshot
        if ($taskProgressBefore -ne 0) {
            throw "Collection task fixture did not publish progress 0/$TaskTargetCount."
        }
    }
    Write-JsonFile $snapshotPath $snapshot

    $attemptIterations = if ($MineFishingLevel -gt 0) {
        [Math]::Max(1, $MineFishingMaxAttempts)
    }
    else {
        [Math]::Max(1, $FishingMaxAttempts)
    }
    $dailyPlanCandidateOptions = if ($TaskFamily -eq "none") {
        "fishing.catch_fish"
    }
    else {
        "quest.advance"
    }
    $skillTeacherQueue = $null
    $loopSnapshotPath = $snapshotPath
    $dailyPlanArguments = @("--use-daily-plan")
    $planningArguments = if ($StandaloneSkillTeacher) {
        $skillTeacherQueue = Initialize-RuntimeSkillTeacherQueue `
            -BootstrapDll (Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll") `
            -OutputRoot $runDirectory -SnapshotUrl $snapshotUrl -TargetScoringLevel $SkillTerminalBoundaryLevel
        $loopSnapshotPath = $skillTeacherQueue.SnapshotPath
        $dailyPlanArguments = @()
        @("--precompiled-queue", $skillTeacherQueue.QueuePath, "--max-attempts", "1",
            "--required-verified-actions", "1", "--max-queue-item-attempts", "1", "--emit-queue-execution-receipt",
            "--execution-snapshot-profile", "collection_teacher", "--snapshot-file-profile", "collection_teacher",
            "--goal", "goal.skills.raise_skill_levels")
    }
    elseif ($StandaloneCollectionTeacher) {
        @(
            "--standalone-collection-teacher",
            "--execution-snapshot-profile", "collection_teacher",
            "--snapshot-file-profile", "collection_teacher",
            "--stage-one-collection-requirement-inventory", $RequirementInventory,
            "--stage-one-collection-acquisition-lowering", $AcquisitionLowering,
            "--master-angler-stage-one-window-index", $MasterAnglerWindows,
            "--route-timing-calibration", $RouteTimingCalibration
        )
    }
    else { @("--daily-plan-max-candidates", "1", "--daily-plan-candidate-options", $dailyPlanCandidateOptions) }
    & dotnet $loopDll `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url "http://127.0.0.1:8767" `
        --snapshot-file $loopSnapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations $attemptIterations `
        --skip-training `
        --sleep-ms 0 `
        @dailyPlanArguments `
        @planningArguments `
        --after-snapshot-wait-ms 1000
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop returned exit code $LASTEXITCODE" }

    $runRoot = Join-Path $loopRoot (Join-Path "runs" $RunId)
    $snapshotDir = Join-Path $runRoot "live-snapshots"
    $datasetPath = Join-Path $loopRoot "datasets\live-training-feature-rows.jsonl"
    $executionFiles = @(Get-ChildItem -LiteralPath $snapshotDir -Filter "execution-*.json" | Where-Object { $_.Name -match '^execution-\d{4}\.json$' } | Sort-Object Name)
    if ($executionFiles.Count -eq 0) { throw "No aggregate execution artifacts were written." }
    $requiredPaths = @($executionFiles | ForEach-Object { $_.FullName })
    $requiredPaths += $datasetPath
    foreach ($requiredPath in $requiredPaths) {
        if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required fishing smoke artifact missing: $requiredPath" }
    }

    $attemptRecords = @(Resolve-CompactTrainingAttempts -SnapshotDir $snapshotDir -DatasetPath $datasetPath)
    $observedCaughtIdsInAttemptOrder = @($attemptRecords | Where-Object { -not [string]::IsNullOrWhiteSpace($_.observed_caught_qualified_item_id) } | ForEach-Object { $_.observed_caught_qualified_item_id })

    $captureOptionId = if ($isCrabPotFixture) { "executor.collect_crab_pot" } else { "executor.catch_fish" }
    $winningAttempt = @($attemptRecords) | Where-Object {
        $stepObservedCatch = $_.observed_caught_qualified_item_id
        $requiredCatchMatched = if ($TaskFamily -ne "none") {
            $stepObservedCatch -eq $TaskQualifiedItemId
        }
        elseif ($isCrabPotFixture) {
            $stepObservedCatch -eq $CrabPotQualifiedItemId
        }
        elseif ($MineFishingLevel -gt 0) {
            $stepObservedCatch -eq "(O)162"
        }
        else {
            -not [string]::IsNullOrWhiteSpace($stepObservedCatch)
        }
        $_.option_id -eq $captureOptionId -and
        $null -ne $_.primitive_verified -and
        $requiredCatchMatched
    } | Select-Object -First 1
    if ($null -eq $winningAttempt) {
        Write-JsonFile (Join-Path $runDirectory "execution-rejected.json") ([ordered]@{
            execution_paths = @($executionFiles.FullName)
            total_attempts = $attemptRecords.Count
            observed_caught_qualified_item_ids = $observedCaughtIdsInAttemptOrder
        })
        throw "Fishing executor did not produce the required observed verified catch result."
    }
    $executionPath = $winningAttempt.execution_path
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $catchExecution = @($execution.step_results) | Where-Object {
        $_.option_id -eq $captureOptionId -and
        $_.status -eq "applied" -and
        [string]$_.effective_queue_id -eq $winningAttempt.queue_id -and
        $_.primitive_verification_status -eq "verified" -and
        (Read-RuntimeFishingObservedCatch -OptionId $_.option_id -Facts $_.changed_facts) -eq $winningAttempt.observed_caught_qualified_item_id
    } | Select-Object -First 1
    if ($null -eq $catchExecution) {
        throw "Winning aggregate execution artifact did not contain the compact JSONL verified catch row."
    }

    $catchItem = $catchExecution.effective_queue_item
    $queuePath = $winningAttempt.queue_path
    $dailyPlanPath = $catchExecution.effective_before_snapshot_path
    if ($queuePath -match 'compiled-queue-(\d{4})\.json$') {
        $candidateDailyPlanPath = Join-Path $snapshotDir "daily-plan-response-$($Matches[1]).json"
        if (Test-Path -LiteralPath $candidateDailyPlanPath) {
            $dailyPlanPath = $candidateDailyPlanPath
        }
    }

    $observedCatch = Read-RuntimeFishingObservedCatch -OptionId $catchExecution.option_id -Facts $catchExecution.changed_facts
    $nativeCatchReceiptPath = ""
    $skillNativeReceiptPath = ""
    $compiledPossibleCatchIds = @()
    $compiledExpectedCatch = ""
    $compiledDistributionComplete = ""
    $observedCatchInCompiledDistribution = $false
    if ($StandaloneSkillTeacher) {
        $skillNativeReceiptPath = Build-RuntimeSkillThresholdSource `
            -BootstrapDll (Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll") `
            -OutputRoot $runDirectory -RunId $RunId -PreferencePath $skillTeacherQueue.PreferencePath `
            -TargetScoringLevel $SkillTerminalBoundaryLevel -Step $catchExecution
    }
    elseif ($isCrabPotFixture) {
        $nativeCatchReceiptPath = Build-RuntimeMasterAnglerCatchSource `
            -BootstrapDll (Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll") `
            -OutputRoot $runDirectory -RunId $RunId -RequirementInventory $RequirementInventory `
            -AcquisitionLowering $AcquisitionLowering -Step $catchExecution
    }
    else {
        $compiledExpectedCatch = @($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "expected_qualified_item_id" } |
            Select-Object -ExpandProperty value -First 1
        $compiledDistributionComplete = @($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "outcome_distribution_complete" } |
            Select-Object -ExpandProperty value -First 1
        $compiledPossibleCatchIdsJson = @($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "possible_qualified_item_ids_json" } |
            Select-Object -ExpandProperty value -First 1
        foreach ($possibleCatchId in ($compiledPossibleCatchIdsJson | ConvertFrom-Json)) {
            $compiledPossibleCatchIds += [string] $possibleCatchId
        }
        $observedCatchInCompiledDistribution = -not [string]::IsNullOrWhiteSpace($observedCatch) -and $compiledPossibleCatchIds -contains $observedCatch
        if (-not [string]::IsNullOrWhiteSpace($compiledExpectedCatch)) {
            throw "Fishing mechanical action incorrectly constrained the stochastic result to $compiledExpectedCatch."
        }
        if ($compiledDistributionComplete -ne "True" -or -not $observedCatchInCompiledDistribution) {
            throw "Observed catch $observedCatch was not covered by the compiler-approved complete outcome distribution."
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId) -and
        ($compiledPossibleCatchIds.Count -ne 1 -or $compiledPossibleCatchIds[0] -ne $FishFrenzyQualifiedItemId -or $observedCatch -ne $FishFrenzyQualifiedItemId)) {
        throw "Fish frenzy priority was not preserved from transparent candidate distribution through observed runtime output."
    }
    if (-not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId) -and
        ($compiledPossibleCatchIds.Count -ne 1 -or $compiledPossibleCatchIds[0] -ne $FishPondQualifiedItemId -or $observedCatch -ne $FishPondQualifiedItemId)) {
        throw "Fish pond priority was not preserved from transparent candidate distribution through observed runtime output."
    }
    $fishPondAfterCount = $null
    if (-not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId)) {
        if (@($catchExecution.primitive_verification_reasons) -notcontains "special_catch_without_bobber_bar_observed") {
            throw "Fish pond catch unexpectedly required or reported a BobberBar minigame."
        }
        $afterSnapshot = Get-Content -LiteralPath $catchExecution.after_snapshot_path -Raw | ConvertFrom-Json
        $afterPond = @($afterSnapshot.state.fishing.special_catch_sources.value.fish_ponds) | Where-Object {
            $_.tile_x -eq $actualFishPondTileX -and $_.tile_y -eq $actualFishPondTileY
        } | Select-Object -First 1
        if ($null -eq $afterPond) {
            throw "Post-execution transparent snapshot omitted the configured fish pond."
        }
        $fishPondAfterCount = [int]$afterPond.fish_count
        if ($fishPondAfterCount -ne ($fishPondBeforeCount - 1)) {
            throw "Fish pond occupant count did not decrease by one after the native catch."
        }
    }
    if ($MineFishingLevel -gt 0) {
        $expectedMineCatchIds = @("(O)162", "(O)CaveJelly", "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172")
        $missingMineCatchIds = @($expectedMineCatchIds | Where-Object { $compiledPossibleCatchIds -notcontains $_ })
        $unexpectedMineCatchIds = @($compiledPossibleCatchIds | Where-Object { $expectedMineCatchIds -notcontains $_ })
        if ($missingMineCatchIds.Count -gt 0 -or $unexpectedMineCatchIds.Count -gt 0) {
            throw "Mine area-80 compiled distribution was incomplete or contained unexpected results. Missing=$($missingMineCatchIds -join ','); unexpected=$($unexpectedMineCatchIds -join ',')"
        }
        $requiredMineCatch = if ($TaskFamily -ne "none") {
            $TaskQualifiedItemId
        }
        else {
            "(O)162"
        }
        if ($observedCatch -ne $requiredMineCatch) {
            throw "Mine area-80 smoke requires observed $requiredMineCatch; observed $observedCatch."
        }
        if ($requiredMineCatch -eq "(O)162" -and
            @($catchExecution.primitive_verification_reasons) -notcontains "bobber_bar_success_observed") {
            throw "Lava Eel catch did not report the required BobberBar minigame."
        }
        if ($requiredMineCatch -ne "(O)162" -and
            @($catchExecution.primitive_verification_reasons) -notcontains "special_catch_without_bobber_bar_observed") {
            throw "Mine trash catch did not report the native no-BobberBar path."
        }
    }
    $taskProgressAfter = $null
    $compiledQuestFamily = ""
    $compiledQuestId = ""
    $compiledQuestSourceStep = ""
    $compiledQuestTargetStep = ""
    if ($TaskFamily -ne "none") {
        $compiledQuestFamily = [string](@($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "quest_family" } |
            Select-Object -ExpandProperty value -First 1)
        $compiledQuestId = [string](@($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "quest_id" } |
            Select-Object -ExpandProperty value -First 1)
        $compiledQuestSourceStep = [string](@($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "quest_acquisition_source_step" } |
            Select-Object -ExpandProperty value -First 1)
        $compiledQuestTargetStep = [string](@($catchItem.normalized_command.parameters) |
            Where-Object { $_.name -eq "quest_acquisition_target_step" } |
            Select-Object -ExpandProperty value -First 1)
        if ($compiledQuestFamily -ne $TaskFamily -or
            $compiledQuestId -ne $TaskId -or
            $compiledQuestSourceStep -ne "True" -or
            $compiledQuestTargetStep -ne "False") {
            throw "Fishing catch was not compiled as the requested task acquisition source."
        }
        $finalTaskSnapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $taskProgressAfter = Read-CollectionTaskProgress -Snapshot $finalTaskSnapshot
        if ($null -eq $taskProgressAfter -or $taskProgressAfter -le $taskProgressBefore) {
            throw "Task-attached fishing run did not advance native progress from $taskProgressBefore."
        }
    }
    $datasetText = Get-Content -LiteralPath $datasetPath -Raw
    $datasetContainsObservedCatch = $datasetText.Contains($observedCatch)
    $datasetContainsOutcomeDistribution = $datasetText.Contains('outcome_distribution_json')
    if (-not $datasetContainsObservedCatch -or (-not $isCrabPotFixture -and -not $datasetContainsOutcomeDistribution)) {
        throw "Fishing training row omitted the normalized outcome distribution or observed catch label."
    }
    $teacherPreferencePath = if ($StandaloneSkillTeacher) { $skillTeacherQueue.PreferencePath } else {
        Join-Path $snapshotDir ("stage-one-collection-preference-{0:D4}.json" -f $winningAttempt.iteration)
    }
    $teacherPreference = if (($StandaloneCollectionTeacher -or $StandaloneSkillTeacher) -and (Test-Path -LiteralPath $teacherPreferencePath)) {
        Get-Content -LiteralPath $teacherPreferencePath -Raw | ConvertFrom-Json
    }
    else { $null }
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        source_save_path = $sourceSavePath
        source_save_sha256_before = $sourceSaveHash
        isolated_save = $IsolateSave.IsPresent
        isolated_mods = $IsolateMods.IsPresent
        setup_location_fixture = $SetupLocationFixture.IsPresent
        standalone_collection_teacher = $StandaloneCollectionTeacher.IsPresent
        standalone_skill_teacher = $StandaloneSkillTeacher.IsPresent
        skill_save_boundary_path = $skillSaveBoundaryPath
        skill_native_threshold_receipt_path = $skillNativeReceiptPath
        terminal_save_boundary_path = $terminalSaveBoundaryPath
        teacher_preference_path = if ($null -ne $teacherPreference) { $teacherPreferencePath } else { "" }
        teacher_preference_status = if ($null -ne $teacherPreference) { $teacherPreference.status } else { "not_recorded" }
        teacher_strict_pair_count = if ($StandaloneSkillTeacher) { @($teacherPreference.pairwisePreferences).Count } elseif ($null -ne $teacherPreference) { @($teacherPreference.pairwise_preferences).Count } else { 0 }
        teacher_comparison_verified = $false
        skills_independent_current_comparison_verified = $StandaloneSkillTeacher.IsPresent -and -not [string]::IsNullOrWhiteSpace($skillNativeReceiptPath)
        training_skipped = $true
        formal_training_authorized = $false
        location_id = Read-FieldValue $snapshot "player" "location_id"
        fixture_kind = if ($isCrabPotFixture) { "ready_crab_pot_boundary" } elseif ($SetupLocationFixture) { "location_boundary" } elseif (-not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId)) { "fish_frenzy" } elseif (-not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId)) { "fish_pond" } elseif ($MineFishingLevel -gt 0) { "mine_area_80" } else { "ordinary" }
        fixture_qualified_item_id = if ($isCrabPotFixture) { $CrabPotQualifiedItemId } elseif (-not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId)) { $FishFrenzyQualifiedItemId } else { $FishPondQualifiedItemId }
        crab_pot_actual_target_tile = if ($isCrabPotFixture) { "$CrabPotTileX,$CrabPotTileY" } else { "" }
        crab_pot_counterfactual_tile = if ($IncludeCrabPotCounterfactual) { "$($counterfactualBoundary.TileX),$($counterfactualBoundary.TileY)" } else { "" }
        native_catch_receipt_path = $nativeCatchReceiptPath
        outcome_distribution_verification_status = if ($isCrabPotFixture) { "not_applicable_exact_native_crab_pot_output" } else { "complete_rod_distribution_verified" }
        mine_level = if ($MineFishingLevel -gt 0) { $MineFishingLevel } else { $null }
        mine_area = if ($MineFishingLevel -gt 0) { 80 } else { $null }
        task_family = $TaskFamily
        task_id = if ($TaskFamily -eq "none") { "" } else { $TaskId }
        task_qualified_item_id = if ($TaskFamily -eq "none") { "" } else { $TaskQualifiedItemId }
        task_target_count = if ($TaskFamily -eq "none") { $null } else { $TaskTargetCount }
        task_setup_status = if ($null -eq $taskSetupResult) { "not_requested" } else { $taskSetupResult.status }
        task_progress_before = $taskProgressBefore
        task_progress_after = $taskProgressAfter
        compiled_quest_family = $compiledQuestFamily
        compiled_quest_id = $compiledQuestId
        compiled_quest_acquisition_source_step = $compiledQuestSourceStep
        compiled_quest_acquisition_target_step = $compiledQuestTargetStep
        fish_pond_occupant_count_before = $fishPondBeforeCount
        fish_pond_occupant_count_after = $fishPondAfterCount
        fish_pond_actual_top_left_tile = if ($null -ne $fishPondSetup) { "$actualFishPondTileX,$actualFishPondTileY" } else { "" }
        setup_route_segment_count = @($setupRoute.Segments).Count
        setup_route_path = @($setupRoute.Segments | ForEach-Object { "$($_.from_location)->$($_.target_location)" })
        setup_route_skip_reason = [string]$setupRoute.SkipReason
        action_queue_status = $execution.status
        compiled_catch_queue_item_id = $catchItem.queue_item_id
        compiled_expected_qualified_item_id = $compiledExpectedCatch
        compiled_outcome_distribution_complete = $compiledDistributionComplete
        compiled_possible_qualified_item_ids = $compiledPossibleCatchIds
        verified_execution_status = $catchExecution.status
        verified_execution_reasons = @($catchExecution.primitive_verification_reasons)
        observed_caught_qualified_item_id = $observedCatch
        observed_caught_qualified_item_ids_in_attempt_order = $observedCaughtIdsInAttemptOrder
        observed_catch_in_compiled_distribution = $observedCatchInCompiledDistribution
        attempt_iterations = $attemptIterations
        total_attempts = $attemptRecords.Count
        aggregate_execution_count = $attemptRecords.Count
        aggregate_execution_file_count = $executionFiles.Count
        winning_iteration = $winningAttempt.iteration
        winning_execution_path = $executionPath
        dataset_contains_observed_catch = $datasetContainsObservedCatch
        dataset_contains_outcome_distribution = $datasetContainsOutcomeDistribution
        dataset_path = $datasetPath
        daily_plan_path = $dailyPlanPath
        queue_path = $queuePath
        execution_path = $executionPath
        executor_health = $executorHealth
        smapi_process_id = $gameProcess.Id
        backend_process_id = $backendProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
        $backendProcess.WaitForExit(10000) | Out-Null
    }
    if (-not $KeepGameRunning -and $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
    $sourceSaveHashAfter = Get-DirectoryContentHash -Path $sourceSavePath
    Write-JsonFile (Join-Path $runDirectory "isolation-audit.json") ([ordered]@{
        source_save_path = $sourceSavePath
        source_save_sha256_before = $sourceSaveHash
        source_save_sha256_after = $sourceSaveHashAfter
        source_save_unchanged = $sourceSaveHashAfter -ceq $sourceSaveHash
        isolated_save = $IsolateSave.IsPresent
        isolated_mods = $IsolateMods.IsPresent
        standalone_collection_teacher = $StandaloneCollectionTeacher.IsPresent
        standalone_skill_teacher = $StandaloneSkillTeacher.IsPresent
        skill_save_boundary_path = $skillSaveBoundaryPath
        terminal_save_boundary_path = $terminalSaveBoundaryPath
        game_kept_running = $KeepGameRunning.IsPresent
        game_process_exited = $null -eq $gameProcess -or $gameProcess.HasExited
        backend_process_exited = $null -eq $backendProcess -or $backendProcess.HasExited
        training_skipped = $true
        formal_training_authorized = $false
    })
    if ($IsolateSave -and $sourceSaveHashAfter -cne $sourceSaveHash) {
        throw "Original fishing save changed during isolated capture."
    }
}
