param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-treasure-totem-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
                $snapshot.state.player.treasure_totem.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Treasure Totem snapshot."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-treasure-totem"
        queue_item_id = $ItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function New-TreasureTotemRequest($Snapshot, [string] $ItemId) {
    $context = $Snapshot.state.player.treasure_totem.value
    $row = @($context.rows | Where-Object { $_.qualified_item_id -eq "(O)TreasureTotem" -and $_.stack_before -gt 0 }) | Select-Object -First 1
    if ($null -eq $row -or $context.native_use_gate_status -ne "ready") {
        throw "Treasure Totem projection is not ready: $($context.native_use_gate_status)"
    }
    $spawn = $context.spawn_projection; $ring = $context.ring_contract; $center = $context.center_tile
    $request = New-BaseRequest $Snapshot "executor.use_treasure_totem" $ItemId
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.inventory_slot_index = [int]$row.inventory_slot_index
    $request.item_id = [string]$row.item_id; $request.qualified_item_id = [string]$row.qualified_item_id
    $request.expected_stack_before = [int]$row.stack_before; $request.expected_stack_after = [int]$row.stack_after
    $request.treasure_totem_projection_fingerprint = [string]$context.projection_fingerprint
    $request.treasure_totem_center_tile_x = [int]$center.tile_x
    $request.treasure_totem_center_tile_y = [int]$center.tile_y
    $request.treasure_totem_ring_candidate_count = [int]$spawn.ring_candidate_count
    $request.treasure_totem_expected_spawn_count = [int]$spawn.expected_spawn_count
    $request.treasure_totem_expected_spawn_tiles_json = [string]$spawn.expected_spawn_tiles_json
    $request.treasure_totem_existing_artifact_spot_count_before = [int]$spawn.existing_artifact_spot_count_before
    $request.treasure_totem_existing_artifact_spot_count_after = [int]$spawn.existing_artifact_spot_count_after
    $request.treasure_totems_used_before = [int]$spawn.treasure_totems_used_before
    $request.treasure_totems_used_after = [int]$spawn.treasure_totems_used_after
    $request.treasure_totem_ring_scan_radius = [int]$ring.scan_radius
    $request.treasure_totem_rounded_radius = [int]$ring.rounded_radius
    $request.treasure_totem_artifact_spot_qualified_item_id = [string]$ring.artifact_spot_qualified_item_id
    $request.treasure_totem_initial_sound = [string]$ring.initial_sound
    $request.native_contract = [string]$context.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"; $smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"; $snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is in use." } }
if (Get-Process StardewModdingAPI -ErrorAction SilentlyContinue) { throw "StardewModdingAPI is already running." }
$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-treasure-totem\" + $RunId); New-Item -ItemType Directory -Force $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId; New-Item -ItemType Directory -Force $smokeModsPath | Out-Null
foreach ($name in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) { Copy-Item (Join-Path $gameDirectory "Mods\$name") $smokeModsPath -Recurse -Force }
$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$savedEnvironment = @{}; foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath; $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"; $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log")
    $snapshot = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $fixture = Invoke-Post $executeUrl (New-BaseRequest $snapshot "debug.setup_treasure_totem" "$RunId.fixture")
    if ($fixture.status -ne "applied") { throw "Treasure Totem fixture failed: $($fixture.observed_effect)" }
    $snapshot = Wait-World $snapshotUrl 60
    $result = Invoke-Post $executeUrl (New-TreasureTotemRequest $snapshot "$RunId.use")
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Treasure Totem use failed: status=$($result.status); verification=$($result.primitive_verification_status); observed=$($result.observed_effect)"
    }
    $after = Wait-World $snapshotUrl 60
    if ([int]$after.state.player.treasure_totem.value.spawn_projection.treasure_totems_used_before -ne
        [int]$snapshot.state.player.treasure_totem.value.spawn_projection.treasure_totems_used_after) {
        throw "Treasure Totem counter was not visible in the fresh after snapshot."
    }
    $summary = [ordered]@{ schema_version = "stardewai.runtime_treasure_totem_smoke.v1"; status = "passed"; run_id = $RunId; passed_case_count = 1; total_case_count = 1; cases = @($result) }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) { Stop-Process $game.Id -Force -ErrorAction SilentlyContinue }
}
