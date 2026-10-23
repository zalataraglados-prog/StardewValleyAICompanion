[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-island-gourmand-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 300
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
            if ($null -ne $result) { return $result }
        } catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-GourmandSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 15
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.world_progress.island_gourmand.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Gourmand snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId
        queue_id = "runtime-island-gourmand-smoke"; queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"
        save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Setup-GourmandFixture([bool] $Talked, [string] $CaseName) {
    $snapshot = Wait-GourmandSnapshot 60
    $request = New-BaseRequest $snapshot "debug.setup_island_gourmand" ("setup-" + $CaseName)
    $request.gourmand_request_index_before = 0
    $request.gourmand_talked_before = $Talked
    $request.gourmand_golden_walnuts_found_before = 0
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Gourmand fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $after = Wait-GourmandSnapshot 30
    $row = $after.state.world_progress.island_gourmand.value
    if ([int]$row.requests_fulfilled -ne 0 -or
        [bool]$row.talked_to_gourmand_received_or_pending -ne $Talked -or
        [int]$row.ready_matching_crop_count -ne 1 -or
        [string]$row.required_crop_item_id -ne "254") {
        throw "Gourmand fixture transparency mismatch for $CaseName."
    }
    $after
}

function New-GourmandRequest($Snapshot, [string] $CaseName) {
    $row = $Snapshot.state.world_progress.island_gourmand.value
    $walnuts = $Snapshot.state.world_progress.golden_walnuts.value
    $action = @($row.action_tiles) | Where-Object { $_.action -eq "Gourmand" } | Select-Object -First 1
    if ($null -eq $action) { throw "Gourmand action tile missing for $CaseName." }
    $completing = [bool]$row.talked_to_gourmand_received_or_pending
    $stage = if ($completing) { "complete_request" } else { "introduce_request" }
    $delta = if ($completing) { [Math]::Min(5, 130 - [int]$walnuts.found) } else { 0 }
    $request = New-BaseRequest $Snapshot "executor.advance_gourmand_request" ("execute-" + $CaseName)
    $request.location_id = [string]$row.location_id; $request.target_location = [string]$row.location_id
    $request.target_tile_x = [int]$action.tile_x; $request.target_tile_y = [int]$action.tile_y
    $request.stand_tile_x = [int]$Snapshot.state.player.tile_x.value
    $request.stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.max_movement_tiles = 512
    $request.gourmand_action = "Gourmand"
    $request.gourmand_schema_version = [string]$row.schema_version
    $request.gourmand_projection_status = [string]$row.projection_status
    $request.gourmand_interaction_stage = $stage
    $request.gourmand_request_index_before = [int]$row.requests_fulfilled
    $request.gourmand_request_index_after = [int]$row.requests_fulfilled + $(if ($completing) { 1 } else { 0 })
    $request.gourmand_required_crop_item_id = [string]$row.required_crop_item_id
    $request.gourmand_required_crop_qualified_item_id = [string]$row.required_crop_qualified_item_id
    $request.gourmand_ready_matching_crop_count = [int]$row.ready_matching_crop_count
    $request.gourmand_talked_before = $completing; $request.gourmand_talked_after = $true
    $request.gourmand_expected_collected_nut_key = if ($completing) { "IslandGourmand1" } else { "" }
    $request.gourmand_walnut_debris_count_before = [int]$row.walnut_debris_count
    $request.gourmand_walnut_debris_count_after = 0
    $request.gourmand_expected_walnut_debris_spawn_count = if ($completing) { 5 } else { 0 }
    $request.gourmand_golden_walnuts_found_before = [int]$walnuts.found
    $request.gourmand_golden_walnuts_found_delta = $delta
    $request.gourmand_golden_walnuts_found_after = [int]$walnuts.found + $delta
    $request.gourmand_question_key = if ($completing) { "Gourmand" } else { "" }
    $request.gourmand_response_key = if ($completing) { "Yes" } else { "" }
    $request.gourmand_output_delivery = if ($completing) { "native_five_debris_then_magnet_pickup" } else { "none_introduction_only" }
    $request.native_contract = "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward"
    $request
}

function Invoke-GourmandCase([bool] $Talked, [string] $CaseName) {
    $before = Setup-GourmandFixture $Talked $CaseName
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $request = New-GourmandRequest $before $CaseName
    $request | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-request.json")) -Encoding utf8
    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $after = Wait-GourmandSnapshot 60
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $row = $after.state.world_progress.island_gourmand.value
    $expectedIndex = if ($Talked) { 1 } else { 0 }
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        [bool]$row.talked_to_gourmand_received_or_pending -and
        [int]$row.requests_fulfilled -eq $expectedIndex -and
        [int]$row.walnut_debris_count -eq 0 -and
        [int]$after.state.world_progress.golden_walnuts.value.found -eq $(if ($Talked) { 5 } else { 0 })
    [ordered]@{
        case = $CaseName; passed = $passed; stage = [string]$request.gourmand_interaction_stage
        status = [string]$result.status; verification = [string]$result.primitive_verification_status
        requests_fulfilled = [int]$row.requests_fulfilled
        golden_walnuts_found = [int]$after.state.world_progress.golden_walnuts.value.found
        actual_ticks = $result.actual_ticks; block_reasons = @($result.block_reasons)
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach or start."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-island-gourmand-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_SUPPRESS_LOCAL_RENDER", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")
$savedEnvironment = @{}; foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath; $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"; $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"; $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 60 | Out-Null
    Wait-GourmandSnapshot 180 | Out-Null

    $cases = @(
        (Invoke-GourmandCase $false "introduction"),
        (Invoke-GourmandCase $true "first-request-completion"))
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $cases.Count) { "passed" } else { "failed" }
        run_id = $RunId; save_slot = $SaveSlot; expected_case_count = $cases.Count
        passed_case_count = $passedCount; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
    if ($passedCount -ne $cases.Count) { throw "Runtime Island Gourmand smoke failed: $artifactDirectory" }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
