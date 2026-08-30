[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-geode-processing-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 300,
    [string[]] $GeodeQualifiedItemIds = @("(O)275", "(O)535", "(O)536", "(O)537", "(O)749", "(O)MysteryBox", "(O)GoldenMysteryBox", "(O)791"),
    [switch] $SkipForgedCase,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}
function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 300) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}
function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Wait-Snapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $snapshotUrl 30
            if ($snapshot.save_id.status -in @("available", "derived")) { return $snapshot }
            $lastStatus = "save=$($snapshot.save_id.status)"
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for geode processing snapshot. Last status: $lastStatus"
}
function Wait-GeodeReady([string] $QualifiedItemId, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.player.geode_processing
        $input = @($field.value.inventory_inputs | Where-Object { $_.qualified_item_id -eq $QualifiedItemId }) | Select-Object -First 1
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and
            $field.value.base_service_status -eq "ready" -and $null -ne $input -and $input.status -eq "available") {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Geode processing fixture did not become ready for $QualifiedItemId."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-geode-processing"
        queue_item_id = $ItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Invoke-Setup($Snapshot, [string] $QualifiedItemId, [int] $Geodes, [int] $Mystery, [bool] $Golden) {
    $setup = New-Request $Snapshot "debug.setup_geode_processing" ("$RunId.setup." + $QualifiedItemId.Replace("(O)", ""))
    $setup.geode_qualified_item_id = $QualifiedItemId; $setup.geode_stack_before = 2; $setup.geode_money_before = 1000
    $setup.geodes_cracked_before = $Geodes; $setup.mystery_boxes_opened_before = $Mystery
    $setup.golden_coconut_cracked_before = $Golden; $setup.geode_got_mystery_book_mail_before = $true
    $setup.geode_artifact_found_mail_before = $false
    $result = Invoke-JsonPost $executeUrl $setup
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Geode fixture setup failed for $QualifiedItemId`: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    Wait-GeodeReady $QualifiedItemId 30
}
function New-GeodeRequest($Snapshot, [string] $QualifiedItemId, [string] $CaseName) {
    $projection = $Snapshot.state.player.geode_processing.value
    $input = @($projection.inventory_inputs | Where-Object { $_.qualified_item_id -eq $QualifiedItemId }) | Select-Object -First 1
    $target = @($projection.counter_action_tiles) | Select-Object -First 1
    if ($null -eq $input -or $null -eq $target) { throw "Typed geode input or Blacksmith endpoint missing for $CaseName." }
    $request = New-Request $Snapshot "executor.crack_geode" ("$RunId.execute.$CaseName")
    $request.location_id = "Blacksmith"; $request.target_location = "Blacksmith"
    $request.target_tile_x = [int]$target.tile_x; $request.target_tile_y = [int]$target.tile_y
    $request.stand_tile_x = [int]$Snapshot.state.player.tile_x.value; $request.stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.max_movement_tiles = 512; $request.geode_purpose = "isolated EVD-315 native smoke"
    $request.geode_qualified_item_id = [string]$input.qualified_item_id; $request.geode_slot_index = [int]$input.slot_index
    $request.geode_input_quality = [int]$input.quality; $request.geode_stack_before = [int]$input.stack_before
    $request.geode_free_slots_before = [int]$projection.free_inventory_slots; $request.geode_money_before = [int]$projection.money_before
    $request.geode_price_gold = [int]$projection.price_gold; $request.geodes_cracked_before = [int]$projection.geodes_cracked_before
    $request.mystery_boxes_opened_before = [int]$projection.mystery_boxes_opened_before
    $request.golden_coconut_cracked_before = [bool]$projection.golden_coconut_cracked_before
    $request.golden_walnuts_before = [int]$projection.golden_walnuts_before
    $request.golden_walnuts_found_before = [int]$projection.golden_walnuts_found_before
    $request.geode_archaeology_found_count = [int]$projection.archaeology_found_count
    $request.geode_save_id_half = [long]$projection.predictor_context.save_id_half
    $request.geode_player_id_half = [long]$projection.predictor_context.player_id_half
    $request.geode_season = [string]$projection.predictor_context.season
    $request.geode_deepest_mine_level = [int]$projection.predictor_context.deepest_mine_level
    $request.geode_skill_1_level = [int]$projection.predictor_context.skill_1_unmodified_level
    $request.geode_farming_mastery_unlocked = [bool]$projection.predictor_context.farming_mastery_unlocked
    $request.geode_qi_beans_rule_active = [bool]$projection.predictor_context.qi_beans_rule_active
    $request.geode_got_mystery_book_mail_before = [bool]$projection.predictor_context.got_mystery_book_mail
    $request.geode_artifact_found_mail_before = [bool]$projection.predictor_context.artifact_found_mail
    $request.geode_prediction_kind = [string]$input.kind
    $request.geode_expected_output_qid = if ($null -eq $input.expected_output) { "" } else { [string]$input.expected_output.qualified_item_id }
    $request.geode_expected_output_stack = if ($null -eq $input.expected_output) { 0 } else { [int]$input.expected_output.stack }
    $request.geode_expected_output_quality = if ($null -eq $input.expected_output) { 0 } else { [int]$input.expected_output.quality }
    $request.geode_accepted_outputs_json = ConvertTo-Json -InputObject @($input.accepted_outputs) -Depth 32 -Compress
    $request.geode_expected_mail_additions_json = ConvertTo-Json -InputObject @($input.expected_mail_additions) -Depth 8 -Compress
    $request.geode_projection_fingerprint = [string]$projection.projection_fingerprint
    $request.geode_action_raw = [string]$target.action_raw; $request.geode_action_token = [string]$target.action_token
    $request.native_contract = [string]$projection.native_contract
    $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"; $snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }; $SaveSlot = $slot.Name
}
if (-not (Test-Path -LiteralPath (Join-Path $savesPath $SaveSlot) -PathType Container)) { throw "Isolated save not found: $SaveSlot" }
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-geode-processing\" + $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$loadedModAllowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName; $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_DEDICATED_HOST_MODE", "STARDEWAI_DEDICATED_HOST_RUN_ID", "STARDEWAI_DEDICATED_HOST_ACTOR_ID",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$previousEnvironment = @{}; foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath; $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_DEDICATED_HOST_MODE = "1"; $env:STARDEWAI_DEDICATED_HOST_RUN_ID = $RunId; $env:STARDEWAI_DEDICATED_HOST_ACTOR_ID = "ai_host.main"
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"; $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Wait-Snapshot $StartupTimeoutSeconds; $cases = @()
    if (-not $SkipForgedCase) {
        $forgedBefore = Invoke-Setup $initial "(O)535" 42 15 $false
        $forgedRequest = New-GeodeRequest $forgedBefore "(O)535" "forged-counter"
        $forgedRequest.geodes_cracked_before = [int]$forgedRequest.geodes_cracked_before + 1
        $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
        $forgedPassed = $forgedResult.status -eq "blocked" -and
            @($forgedResult.block_reasons) -contains "geode_processing_counter_or_golden_coconut_state_drifted"
        $cases += [ordered]@{ case = "forged_counter_rejected"; passed = $forgedPassed; result = $forgedResult }
        $initial = Wait-Snapshot 30
    }
    foreach ($qid in $GeodeQualifiedItemIds) {
        $before = Invoke-Setup $initial $qid 42 15 $false
        $caseName = $qid.Replace("(O)", "").ToLowerInvariant(); $request = New-GeodeRequest $before $qid $caseName
        $result = Invoke-JsonPost $executeUrl $request
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified"
        $cases += [ordered]@{ case = "native_$caseName"; passed = $passed; prediction_kind = $request.geode_prediction_kind; result = $result }
        $initial = Wait-Snapshot 30
    }
    $finalSnapshot = Wait-Snapshot 30; Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $expectedCount = $GeodeQualifiedItemIds.Count + $(if ($SkipForgedCase) { 0 } else { 1 })
    $summary = [ordered]@{ schema_version = "stardewai.runtime_geode_processing_smoke.v1"; evidence_id = "EVD-315"
        run_id = $RunId; status = if ($passedCount -eq $expectedCount) { "passed" } else { "failed" }; expected_case_count = $expectedCount
        passed_case_count = $passedCount; loaded_mod_allowlist = $loadedModAllowlist; cases = $cases }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/$expectedCount"; artifact = $runDirectory } | ConvertTo-Json -Depth 4
    if ($passedCount -ne $expectedCount) { throw "Runtime geode processing smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue; $gameProcess.WaitForExit(10000) | Out-Null
    }
}
