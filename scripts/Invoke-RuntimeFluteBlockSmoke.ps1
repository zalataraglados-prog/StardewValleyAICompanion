[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-flute-block-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path }
function Get-Json([string] $Url, [int] $Timeout = 30) { Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $Timeout }
function Post-Json([string] $Url, $Body) { Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 48) -TimeoutSec 120 }
function Wait-Snapshot([string] $Url, [int] $Timeout) {
    $deadline = (Get-Date).AddSeconds($Timeout)
    while ((Get-Date) -lt $deadline) {
        try { $value = Get-Json $Url; if ($value.schema_version -eq "snapshot.v1" -and $value.save_id.status -in @("available", "derived")) { return $value } } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot."
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path "artifacts\runtime-flute-block" $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$mods = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeMods = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeMods | Out-Null
foreach ($mod in $mods) {
    $target = Join-Path $smokeMods $mod
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path (Join-Path (Join-Path $gameDirectory "Mods") $mod) "*") -Destination $target -Recurse -Force
}
function Request($Snapshot, [string] $Option, [string] $Item) {
    [ordered]@{ schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-flute-block"; queue_item_id = $Item;
        before_state_hash = [string]$Snapshot.state_hash; option_id = $Option; execution_mode = "training_singleplayer"; actor = "training_farmer.main";
        save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O") }
}

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$old = @{}; foreach ($name in $names) { $old[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath; $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"; $env:SMAPI_MODS_PATH = $smokeMods
    $process = Start-Process -FilePath $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $current = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds
    for ($i = 0; $i -lt 8 -and $current.state.menus.active_menu.value.is_open; $i++) {
        $close = Post-Json $executeUrl (Request $current "executor.close_menu" "$RunId.close.$i")
        if ($close.status -ne "applied") { throw "Initial menu close failed." }
        Start-Sleep -Seconds 1; $current = Wait-Snapshot $snapshotUrl 30
    }
    $fixture = Post-Json $executeUrl (Request $current "debug.setup_flute_block" "$RunId.fixture")
    if ($fixture.status -ne "applied") { throw "Flute Block fixture failed: $(@($fixture.block_reasons) -join ',')" }
    Start-Sleep -Milliseconds 500
    $ready = Wait-Snapshot $snapshotUrl 30
    $row = @($ready.state.current_location.objects.value | Where-Object { $null -ne $_.flute_block_tuning }) | Select-Object -First 1
    if ($null -eq $row) { throw "Flute Block projection missing." }
    $p = $row.flute_block_tuning
    $stand = @($p.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    $safe = $ready.state.player.safe_item_context.value
    $request = Request $ready "world.tune_flute_block" "$RunId.tune"
    $request.location_id = [string]$ready.state.player.location_id.value; $request.target_tile_x = [int]$row.tile_x; $request.target_tile_y = [int]$row.tile_y
    $request.stand_tile_x = [int]$stand.tile_x; $request.stand_tile_y = [int]$stand.tile_y; $request.target_runtime_type = [string]$p.target_runtime_type
    $request.item_id = [string]$p.canonical_item_id; $request.qualified_item_id = [string]$p.canonical_qualified_item_id
    $request.safe_slot_index = [int]$safe.safe_slot_index; $request.flute_block_safe_slot_kind = [string]$safe.safe_slot_kind; $request.restore_slot_index = [int]$safe.current_tool_index
    $request.flute_block_current_pitch_raw = [string]$p.current_pitch_raw; $request.flute_block_current_pitch = [int]$p.current_pitch_parsed; $request.flute_block_next_pitch = [int]$p.next_pitch
    $request.flute_block_pitch_min = [int]$p.pitch_min_inclusive; $request.flute_block_pitch_max = [int]$p.pitch_max_inclusive; $request.flute_block_pitch_step = [int]$p.pitch_step
    $request.flute_block_pitch_state_count = [int]$p.pitch_state_count; $request.flute_block_sound_cue = [string]$p.sound_cue
    $request.flute_block_expected_shake_timer = [int]$p.expected_shake_timer_immediately_after_action; $request.flute_block_expected_scale_y = [double]$p.expected_scale_y_immediately_after_action
    $request.flute_block_expected_location_action_return = [bool]$p.expected_native_location_action_return; $request.interaction_kind = [string]$p.interaction_kind
    $request.expected_action_type = [string]$p.expected_action_type; $request.native_contract = [string]$p.native_contract; $request.max_movement_tiles = 512
    $result = Post-Json $executeUrl $request
    $after = Wait-Snapshot $snapshotUrl 30
    $afterRow = @($after.state.current_location.objects.value | Where-Object { [int]$_.tile_x -eq [int]$row.tile_x -and [int]$_.tile_y -eq [int]$row.tile_y }) | Select-Object -First 1
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $result.training_impact_scope -eq "player_command_only_executor_evidence" -and
        [string]$afterRow.flute_block_tuning.current_pitch_raw -eq [string]$p.next_pitch -and [string]$afterRow.qualified_item_id -eq "(O)464" -and
        [int]$after.state.player.current_tool_index.value -eq [int]$safe.current_tool_index
    $summary = [ordered]@{ schema_version = "stardewai.runtime_flute_block_smoke.v1"; run_id = $RunId; status = if ($passed) { "passed" } else { "failed" };
        execution_status = $result.status; verification = $result.primitive_verification_status; training_impact_scope = $result.training_impact_scope;
        pitch_before = [int]$p.current_pitch_parsed; pitch_after = [int]$afterRow.flute_block_tuning.current_pitch_parsed; observed_effect = $result.observed_effect;
        loaded_mod_allowlist = $mods; output_directory = $runDirectory }
    $summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 20
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $old[$name]) }
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
