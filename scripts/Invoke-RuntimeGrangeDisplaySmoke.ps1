param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-grange-display-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.grange_display.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for grange display snapshot."
}
function Wait-Grange([string] $Url, [bool] $Judged, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-World $Url 15
        $context = $snapshot.state.player.grange_display.value
        if ($context.festival_id -eq "festival_fall16" -and
            [bool]$context.grange_judged -eq $Judged -and
            ($context.gate_status -eq "ready" -or $context.gate_status -like "complete_*")) {
            return $snapshot
        }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for active fall16 grange context (judged=$Judged)."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-grange-display"
        queue_item_id = $QueueItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Select-AdjacentInteraction($Snapshot) {
    $px = [int]$Snapshot.state.player.tile_x.value
    $py = [int]$Snapshot.state.player.tile_y.value
    $row = @($Snapshot.state.player.grange_display.value.interaction_tiles | Where-Object {
        ([Math]::Abs([int]$_.tile_x - $px) + [Math]::Abs([int]$_.tile_y - $py)) -eq 1
    }) | Select-Object -First 1
    if ($null -eq $row) { throw "Fixture player is not adjacent to a projected grange interaction tile." }
    return $row
}
function New-GrangeRequest($Snapshot, [string] $QueueItemId) {
    $context = $Snapshot.state.player.grange_display.value
    $operation = $context.next_operation
    if ($null -eq $operation -or $operation.status -ne "ready") { throw "No ready grange operation." }
    $interaction = Select-AdjacentInteraction $Snapshot
    $request = New-BaseRequest $Snapshot "executor.manage_grange_display" $QueueItemId
    $request.location_id = [string]$context.festival_location_id
    $request.max_movement_tiles = 512
    $request.grange_projection_fingerprint = [string]$context.projection_fingerprint
    $request.grange_interaction_tile_x = [int]$interaction.tile_x
    $request.grange_interaction_tile_y = [int]$interaction.tile_y
    $request.grange_stand_tile_x = [int]$Snapshot.state.player.tile_x.value
    $request.grange_stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.grange_judged = [bool]$context.grange_judged
    $request.grange_objective = [string]$operation.objective
    $request.grange_operation = [string]$operation.operation
    $request.grange_display_slot_index = [int]$operation.display_slot_index
    $request.grange_inventory_slot_index = [int]$operation.inventory_slot_index
    $request.grange_inventory_stack_before = [int]$operation.inventory_stack_before
    $request.grange_inventory_stack_after = [int]$operation.inventory_stack_after
    $request.grange_sink_inventory_slot_index = [int]$operation.sink_inventory_slot_index
    $request.qualified_item_id = [string]$operation.qualified_item_id
    $request.item_id = [string]$operation.item_id
    $request.grange_item_runtime_type = [string]$operation.runtime_type
    $request.grange_item_quality = [int]$operation.quality
    $request.grange_actual_sell_price = [int]$operation.actual_sell_price
    $request.grange_item_points = [int]$operation.item_points
    $request.grange_scoring_group = [string]$operation.scoring_group
    $request.grange_score_before = [int]$operation.score_before
    $request.grange_score_after = [int]$operation.score_after
    $request.grange_occupied_slots_before = [int]$operation.occupied_slots_before
    $request.grange_occupied_slots_after = [int]$operation.occupied_slots_after
    $request.grange_best_available_score = [int]$context.best_available_score
    $request.grange_first_place_score = [int]$context.first_place_score
    $request.native_contract = [string]$context.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is in use." }
}
if (Get-Process StardewModdingAPI -ErrorAction SilentlyContinue) { throw "StardewModdingAPI is already running." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-grange-display\" + $RunId)
New-Item -ItemType Directory -Force $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force $smokeModsPath | Out-Null
foreach ($name in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item (Join-Path $gameDirectory "Mods\$name") $smokeModsPath -Recurse -Force
}
$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log")
    $snapshot = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $fixtureRequest = New-BaseRequest $snapshot "debug.setup_grange_display" "$RunId.setup.prepare"
    $fixtureRequest.grange_judged = $false
    $fixture = Invoke-Post $executeUrl $fixtureRequest
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Pre-judging grange fixture failed: $($fixture.observed_effect)"
    }
    $cases = @()
    for ($index = 0; $index -lt 12; $index++) {
        $snapshot = Wait-Grange $snapshotUrl $false 60
        $context = $snapshot.state.player.grange_display.value
        if ($context.gate_status -like "complete_*") { break }
        $result = Invoke-Post $executeUrl (New-GrangeRequest $snapshot "$RunId.prepare.$index")
        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            throw "Grange prepare operation $index failed: status=$($result.status); reasons=$($result.block_reasons -join ','); observed=$($result.observed_effect)"
        }
        $cases += $result
    }
    $snapshot = Wait-Grange $snapshotUrl $false 60
    $finalPreparationScore = [int]$snapshot.state.player.grange_display.value.current_projected_score
    if ($finalPreparationScore -lt 90) {
        throw "Fixture best display did not reach first place: $finalPreparationScore"
    }

    $fixtureRequest = New-BaseRequest $snapshot "debug.setup_grange_display" "$RunId.setup.retrieve"
    $fixtureRequest.grange_judged = $true
    $fixture = Invoke-Post $executeUrl $fixtureRequest
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Post-judging grange fixture failed: $($fixture.observed_effect)"
    }
    $snapshot = Wait-Grange $snapshotUrl $true 60
    $retrieve = Invoke-Post $executeUrl (New-GrangeRequest $snapshot "$RunId.retrieve.0")
    if ($retrieve.status -ne "applied" -or $retrieve.primitive_verification_status -ne "verified") {
        throw "Grange retrieval failed: status=$($retrieve.status); reasons=$($retrieve.block_reasons -join ','); observed=$($retrieve.observed_effect)"
    }
    $cases += $retrieve
    $snapshot = Wait-Grange $snapshotUrl $true 60
    if ($snapshot.state.player.grange_display.value.gate_status -ne "complete_grange_items_retrieved") {
        throw "Post-judging grange retrieval did not settle complete."
    }
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_grange_display_smoke.v1"
        status = "passed"
        run_id = $RunId
        passed_case_count = $cases.Count
        preparation_case_count = @($cases | Where-Object { $_.requested_effect -like "operation=place*" }).Count
        retrieval_case_count = @($cases | Where-Object { $_.requested_effect -like "operation=remove*" }).Count
        final_preparation_score = $finalPreparationScore
        cases = $cases
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    [ordered]@{
        status = $summary.status
        run_id = $summary.run_id
        passed_case_count = $summary.passed_case_count
        preparation_case_count = $summary.preparation_case_count
        retrieval_case_count = $summary.retrieval_case_count
        final_preparation_score = $summary.final_preparation_score
        artifact = (Join-Path $artifactDirectory "summary.json")
    } | ConvertTo-Json
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) {
        Stop-Process $game.Id -Force -ErrorAction SilentlyContinue
    }
}
