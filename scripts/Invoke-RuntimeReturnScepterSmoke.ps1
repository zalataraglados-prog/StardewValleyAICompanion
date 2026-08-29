param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-return-scepter-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.return_scepter.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Return Scepter snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-return-scepter"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function New-ReturnScepterRequest($Snapshot, [string] $QueueItemId) {
    $context = $Snapshot.state.player.return_scepter.value
    $row = @($context.rows | Where-Object { [string]$_.qualified_item_id -eq "(T)ReturnScepter" -and [int]$_.stack_before -eq 1 }) | Select-Object -First 1
    if ($null -eq $row) { throw "Transparent Return Scepter row missing." }
    if ([string]$context.native_use_gate_status -ne "ready") { throw "Return Scepter projection not ready: $($context.native_use_gate_status)" }
    $destination = $context.destination
    $animation = $context.animation_contract
    $request = New-Request $Snapshot "executor.use_return_scepter" $QueueItemId
    $request["location_id"] = [string]$Snapshot.state.player.location_id.value
    $request["inventory_slot_index"] = [int]$row.inventory_slot_index
    $request["expected_stack_before"] = [int]$row.stack_before
    $request["expected_stack_after"] = [int]$row.stack_after
    $request["qualified_item_id"] = [string]$row.qualified_item_id
    $request["return_scepter_projection_fingerprint"] = [string]$context.projection_fingerprint
    $request["return_scepter_home_location_id"] = [string]$destination.home_location_id
    $request["return_scepter_home_runtime_type"] = [string]$destination.home_runtime_type
    $request["return_scepter_destination_location_id"] = [string]$destination.destination_location_id
    $request["return_scepter_front_door_tile_x"] = [int]$destination.front_door_tile_x
    $request["return_scepter_front_door_tile_y"] = [int]$destination.front_door_tile_y
    $request["return_scepter_home_is_cabin"] = [bool]$destination.home_is_cabin
    $request["return_scepter_already_at_destination"] = [bool]$destination.already_at_destination
    $request["return_scepter_instant_use"] = [bool]$animation.instant_use
    $request["return_scepter_facing_direction"] = [int]$animation.facing_direction
    $request["return_scepter_callback_delay_ms"] = [int]$animation.callback_delay_ms
    $request["return_scepter_freeze_pause_ms"] = [int]$animation.freeze_pause_ms
    $request["return_scepter_poof_sprite_count"] = [int]$animation.poof_sprite_count
    $request["return_scepter_trail_sprite_count"] = [int]$animation.trail_sprite_count
    $request["return_scepter_trail_delay_step_ms"] = [int]$animation.trail_delay_step_ms
    $request["return_scepter_trail_max_delay_ms"] = [int]$animation.trail_max_delay_ms
    $request["return_scepter_sound"] = [string]$animation.sound
    $request["native_contract"] = [string]$context.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI executable not found: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Return Scepter smoke requires unused port $port." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-return-scepter\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
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
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru

    $snapshot = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $fixture = Invoke-Post $executeUrl (New-Request $snapshot "debug.setup_return_scepter" "$RunId.fixture")
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Return Scepter fixture failed: blocks=$(@($fixture.block_reasons) -join ','); reasons=$(@($fixture.primitive_verification_reasons) -join ','); observed=$($fixture.observed_effect)"
    }
    $before = Wait-World $snapshotUrl 60
    $result = Invoke-Post $executeUrl (New-ReturnScepterRequest $before "$RunId.use")
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Return Scepter use failed: blocks=$(@($result.block_reasons) -join ','); reasons=$(@($result.primitive_verification_reasons) -join ','); observed=$($result.observed_effect)"
    }
    $after = Wait-World $snapshotUrl 60
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_return_scepter_smoke.v1"
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        home_location_id = [string]$before.state.player.return_scepter.value.destination.home_location_id
        home_runtime_type = [string]$before.state.player.return_scepter.value.destination.home_runtime_type
        destination = "Farm:" + [string]$before.state.player.return_scepter.value.destination.front_door_tile_x + "," + [string]$before.state.player.return_scepter.value.destination.front_door_tile_y
        observed_effect = [string]$result.observed_effect
        final_location = [string]$after.state.player.location_id.value
        final_tile = [string]$after.state.player.tile_x.value + "," + [string]$after.state.player.tile_y.value
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue }
}
