param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-monster-musk-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.monster_musk.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Monster Musk snapshot."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-monster-musk"
        queue_item_id = $ItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function New-MonsterMuskRequest($Snapshot, [string] $ItemId) {
    $context = $Snapshot.state.player.monster_musk.value
    $row = @($context.rows | Where-Object { $_.qualified_item_id -eq "(O)879" -and $_.stack_before -gt 0 }) | Select-Object -First 1
    if ($null -eq $row -or $context.native_use_gate_status -ne "ready") { throw "Monster Musk projection is not ready." }
    $buff = $context.buff_contract; $active = $context.active_buff; $spawn = $context.spawn_semantics; $animation = $context.animation_contract
    $request = New-BaseRequest $Snapshot "executor.use_monster_musk" $ItemId
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.inventory_slot_index = [int]$row.inventory_slot_index
    $request.item_id = [string]$row.item_id; $request.qualified_item_id = [string]$row.qualified_item_id
    $request.expected_stack_before = [int]$row.stack_before; $request.expected_stack_after = [int]$row.stack_after
    $request.monster_musk_projection_fingerprint = [string]$context.projection_fingerprint
    $request.monster_musk_buff_id = [string]$buff.id
    $request.monster_musk_buff_active_before = [bool]$active.active
    $request.monster_musk_buff_remaining_before_ms = [int]$active.remaining_ms
    $request.monster_musk_buff_total_before_ms = [int]$active.total_ms
    $request.monster_musk_buff_duration_ms = [int]$buff.duration_ms
    $request.monster_musk_buff_max_duration_ms = [int]$buff.max_duration_ms
    $request.monster_musk_buff_is_debuff = [bool]$buff.is_debuff
    $request.monster_musk_buff_icon_sprite_index = [int]$buff.icon_sprite_index
    $request.monster_musk_buff_icon_texture = [string]$buff.icon_texture
    $request.monster_musk_buff_glow_color = [string]$buff.glow_color
    $request.monster_musk_buff_effects_empty = [bool]$buff.effects_empty
    $request.monster_musk_buff_actions_on_apply_count = [int]$buff.actions_on_apply_count
    $request.monster_musk_buff_reapply_semantics = [string]$buff.reapply_semantics
    $request.monster_musk_ordinary_mine_spawn_multiplier = [int]$spawn.ordinary_mine_multiplier
    $request.monster_musk_volcano_spawn_multiplier = [int]$spawn.volcano_multiplier
    $request.monster_musk_repellent_buff_id = [string]$spawn.repellent_buff_id
    $request.monster_musk_facing_direction = [int]$animation.facing_direction
    $request.monster_musk_freeze_pause_ms = [int]$animation.freeze_pause_ms
    $request.monster_musk_callback_delay_ms = [int]$animation.callback_delay_ms
    $request.monster_musk_followup_animation_ms = [int]$animation.followup_animation_ms
    $request.monster_musk_sprite_count = [int]$animation.sprite_count
    $request.monster_musk_sprite_delays_ms = [string]$animation.sprite_delays_ms
    $request.monster_musk_sprite_motion_x_domain = [string]$animation.sprite_motion_x_domain
    $request.monster_musk_initial_sound = [string]$animation.initial_sound
    $request.monster_musk_callback_sound = [string]$animation.callback_sound
    $request.native_contract = [string]$context.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"; $smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"; $snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is in use." } }
if (Get-Process StardewModdingAPI -ErrorAction SilentlyContinue) { throw "StardewModdingAPI is already running." }
$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-monster-musk\" + $RunId); New-Item -ItemType Directory -Force $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId; New-Item -ItemType Directory -Force $smokeModsPath | Out-Null
foreach ($name in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) { Copy-Item (Join-Path $gameDirectory "Mods\$name") $smokeModsPath -Recurse -Force }
$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath; $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"; $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log")
    $snapshot = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $fixture = Invoke-Post $executeUrl (New-BaseRequest $snapshot "debug.setup_monster_musk" "$RunId.fixture")
    if ($fixture.status -ne "applied") { throw "Monster Musk fixture failed." }
    $first = Invoke-Post $executeUrl (New-MonsterMuskRequest (Wait-World $snapshotUrl 60) "$RunId.apply")
    if ($first.status -ne "applied" -or $first.primitive_verification_status -ne "verified") { throw "Monster Musk initial apply failed: $($first.observed_effect)" }
    $second = Invoke-Post $executeUrl (New-MonsterMuskRequest (Wait-World $snapshotUrl 60) "$RunId.refresh")
    if ($second.status -ne "applied" -or $second.primitive_verification_status -ne "verified") { throw "Monster Musk refresh failed: $($second.observed_effect)" }
    $summary = [ordered]@{ schema_version = "stardewai.runtime_monster_musk_smoke.v1"; status = "passed"; run_id = $RunId; passed_case_count = 2; total_case_count = 2; cases = @($first, $second) }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) { Stop-Process $game.Id -Force -ErrorAction SilentlyContinue }
}
