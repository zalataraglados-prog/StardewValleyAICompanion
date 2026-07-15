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
    [int] $FishingMaxAttempts = 1,
    [int] $MineFishingLevel = 0,
    [int] $MineFishingMaxAttempts = 12,
    [string] $RunId = ("runtime-fishing-daily-plan-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-fishing-daily-plan-smoke",
    [int] $BackendPort = 5129,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning,
    [switch] $RunSparseMappingFixture
)

$ErrorActionPreference = "Stop"
$fixtureCount = @(
    -not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId),
    -not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId),
    $MineFishingLevel -gt 0
) | Where-Object { $_ }
if ($fixtureCount.Count -gt 1) {
    throw "Configure only one isolated special fishing fixture per run."
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

function Resolve-CompactTrainingAttempts {
    param([string] $SnapshotDir, [string] $DatasetPath)

    $queueRecordsById = @{}
    foreach ($candidateQueuePath in @(Get-ChildItem -LiteralPath $SnapshotDir -Filter "compiled-queue-*.json" | Sort-Object Name)) {
        if ($candidateQueuePath.Name -notmatch '^compiled-queue-(\d{4})\.json$') { continue }
        $candidateQueue = Get-Content -LiteralPath $candidateQueuePath.FullName -Raw | ConvertFrom-Json
        $queueId = [string]$candidateQueue.queue_id
        if ([string]::IsNullOrWhiteSpace($queueId)) { throw "Compiled queue artifact omitted queue_id: $($candidateQueuePath.FullName)" }
        if ($queueRecordsById.ContainsKey($queueId)) { throw "Duplicate compiled queue_id in artifacts: $queueId" }
        $iterationText = $Matches[1]
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
        $stepObservedCatch = @($featureRow.action_features.changed_facts) |
            Where-Object { $_.path -eq "fishing.caught_qualified_item_id" } |
            Select-Object -ExpandProperty after -First 1
        $attemptRecords += [pscustomobject]@{
            iteration = $queueRecord.iteration
            iteration_text = $queueRecord.iteration_text
            queue_path = $queueRecord.queue_path
            execution_path = $queueRecord.execution_path
            row_id = [string]$featureRow.row_id
            queue_id = $queueId
            option_id = [string](@($featureRow.action_features.option_ids) | Select-Object -First 1)
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
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=fishing"
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
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
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

    $gameWindowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle $gameWindowStyle -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -Url $worldSnapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    if ($MineFishingLevel -gt 0) {
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
        start_location = [string](Read-FieldValue $worldSnapshot "player" "location_id")
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
    else {
        $snapshot = Wait-FishingSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    }
    Write-JsonFile $snapshotPath $snapshot

    $attemptIterations = if ($MineFishingLevel -gt 0) {
        [Math]::Max(1, $MineFishingMaxAttempts)
    }
    else {
        [Math]::Max(1, $FishingMaxAttempts)
    }
    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url "http://127.0.0.1:8767" `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations $attemptIterations `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "fishing.catch_fish" `
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

    $winningAttempt = @($attemptRecords) | Where-Object {
        $stepObservedCatch = $_.observed_caught_qualified_item_id
        $requiredCatchMatched = if ($MineFishingLevel -gt 0) { $stepObservedCatch -eq "(O)162" } else { -not [string]::IsNullOrWhiteSpace($stepObservedCatch) }
        $_.option_id -eq "executor.catch_fish" -and
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
        $_.option_id -eq "executor.catch_fish" -and
        $_.status -eq "applied" -and
        [string]$_.effective_queue_id -eq $winningAttempt.queue_id -and
        $_.primitive_verification_status -eq "verified" -and
        (@($_.changed_facts) | Where-Object { $_.path -eq "fishing.caught_qualified_item_id" } | Select-Object -ExpandProperty after -First 1) -eq $winningAttempt.observed_caught_qualified_item_id
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

    $observedCatch = @($catchExecution.changed_facts) |
        Where-Object { $_.path -eq "fishing.caught_qualified_item_id" } |
        Select-Object -ExpandProperty after -First 1
    $compiledExpectedCatch = @($catchItem.normalized_command.parameters) |
        Where-Object { $_.name -eq "expected_qualified_item_id" } |
        Select-Object -ExpandProperty value -First 1
    $compiledDistributionComplete = @($catchItem.normalized_command.parameters) |
        Where-Object { $_.name -eq "outcome_distribution_complete" } |
        Select-Object -ExpandProperty value -First 1
    $compiledPossibleCatchIdsJson = @($catchItem.normalized_command.parameters) |
        Where-Object { $_.name -eq "possible_qualified_item_ids_json" } |
        Select-Object -ExpandProperty value -First 1
    $compiledPossibleCatchIds = @()
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
        if ($observedCatch -ne "(O)162") {
            throw "Mine area-80 smoke requires observed Lava Eel (O)162; observed $observedCatch."
        }
        if (@($catchExecution.primitive_verification_reasons) -notcontains "bobber_bar_success_observed") {
            throw "Lava Eel catch did not report the required BobberBar minigame."
        }
    }
    $datasetText = Get-Content -LiteralPath $datasetPath -Raw
    $datasetContainsObservedCatch = $datasetText.Contains($observedCatch)
    $datasetContainsOutcomeDistribution = $datasetText.Contains('outcome_distribution_json')
    if (-not $datasetContainsObservedCatch -or -not $datasetContainsOutcomeDistribution) {
        throw "Fishing training row omitted the normalized outcome distribution or observed catch label."
    }
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        location_id = $snapshot.state.fishing.location_context.value.location_id
        fixture_kind = if (-not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId)) { "fish_frenzy" } elseif (-not [string]::IsNullOrWhiteSpace($FishPondQualifiedItemId)) { "fish_pond" } elseif ($MineFishingLevel -gt 0) { "mine_area_80" } else { "ordinary" }
        fixture_qualified_item_id = if (-not [string]::IsNullOrWhiteSpace($FishFrenzyQualifiedItemId)) { $FishFrenzyQualifiedItemId } else { $FishPondQualifiedItemId }
        mine_level = if ($MineFishingLevel -gt 0) { $MineFishingLevel } else { $null }
        mine_area = if ($MineFishingLevel -gt 0) { 80 } else { $null }
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
    }
    if (-not $KeepGameRunning -and $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
