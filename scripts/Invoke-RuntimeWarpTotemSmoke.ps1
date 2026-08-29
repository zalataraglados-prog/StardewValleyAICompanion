param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-warp-totem-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
                $snapshot.state.player.warp_totem.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Warp Totem snapshot."
}
function Wait-WarpTotemFixture([string] $Url, [string] $ItemId, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-World $Url 15
        $row = @($snapshot.state.player.warp_totem.value.rows | Where-Object {
            $_.qualified_item_id -eq "(O)$ItemId" -and $_.stack_before -eq 2 -and
            $_.native_use_gate_status -eq "ready"
        }) | Select-Object -First 1
        if ($snapshot.state.player.location_id.value -eq "FarmHouse" -and $null -ne $row) {
            return $snapshot
        }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for Warp Totem fixture item (O)$ItemId in FarmHouse."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-warp-totem"
        queue_item_id = $ItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function New-WarpTotemRequest($Snapshot, [string] $ItemId, [string] $QueueItemId) {
    $context = $Snapshot.state.player.warp_totem.value
    $row = @($context.rows | Where-Object {
        $_.qualified_item_id -eq "(O)$ItemId" -and $_.stack_before -gt 0 -and
        $_.native_use_gate_status -eq "ready"
    }) | Select-Object -First 1
    if ($null -eq $row) { throw "Warp Totem (O)$ItemId has no ready inventory row." }
    $route = $row.destination_route
    $animation = $context.native_animation_contract
    $request = New-BaseRequest $Snapshot "executor.use_warp_totem" $QueueItemId
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.inventory_slot_index = [int]$row.inventory_slot_index
    $request.item_id = [string]$row.item_id
    $request.qualified_item_id = [string]$row.qualified_item_id
    $request.expected_stack_before = [int]$row.stack_before
    $request.expected_stack_after = [int]$row.stack_after
    $request.warp_totem_projection_fingerprint = [string]$context.projection_fingerprint
    $request.warp_totem_base_destination_location_id = [string]$route.base_destination_location_id
    $request.warp_totem_requested_destination_tile_x = [int]$route.requested_destination_tile_x
    $request.warp_totem_requested_destination_tile_y = [int]$route.requested_destination_tile_y
    $request.warp_totem_effective_destination_location_id = [string]$route.effective_destination_location_id
    $request.warp_totem_effective_destination_tile_x = [int]$route.effective_destination_tile_x
    $request.warp_totem_effective_destination_tile_y = [int]$route.effective_destination_tile_y
    $request.warp_totem_destination_route_mode = [string]$route.destination_route_mode
    $request.warp_totem_farm_destination_source = [string]$route.farm_destination_source
    $request.warp_totem_passive_festival_route_json = [string]$route.passive_festival_route_json
    $request.warp_totem_active_festival_id = [string]$route.active_festival_id
    $request.warp_totem_active_festival_start_time = [int]$route.active_festival_start_time
    $request.warp_totem_active_festival_end_time = [int]$route.active_festival_end_time
    $request.warp_totem_active_festival_entry_tile_x = [int]$route.active_festival_entry_tile_x
    $request.warp_totem_active_festival_entry_tile_y = [int]$route.active_festival_entry_tile_y
    $request.warp_totem_active_festival_entry_facing = [int]$route.active_festival_entry_facing
    $request.warp_totem_festival_prestart_warp_cancelled = [bool]$route.festival_prestart_warp_cancelled
    $request.warp_totem_festival_ready_check_required = [bool]$route.festival_ready_check_required
    $request.warp_totem_facing_direction = [int]$animation.facing_direction
    $request.warp_totem_animation_duration_ms = [int]$animation.animation_duration_ms
    $request.warp_totem_totem_callback_delay_ms = [int]$animation.totem_callback_delay_ms
    $request.warp_totem_initial_item_sprite_count = [int]$animation.initial_item_sprite_count
    $request.warp_totem_sprinkle_sprite_count = [int]$animation.sprinkle_sprite_count
    $request.warp_totem_poof_sprite_count = [int]$animation.poof_sprite_count
    $request.warp_totem_trail_sprite_count = [int]$animation.trail_sprite_count
    $request.warp_totem_initial_sound = [string]$animation.initial_sound
    $request.warp_totem_warp_sound = [string]$animation.warp_sound
    $request.warp_totem_glow_color_rgba = [string]$row.glow_color_rgba
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-warp-totem\" + $RunId)
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
    $cases = @()
    foreach ($variant in @(
        @{ item_id = "688"; name = "farm" },
        @{ item_id = "689"; name = "mountain" },
        @{ item_id = "690"; name = "beach" },
        @{ item_id = "261"; name = "desert" },
        @{ item_id = "886"; name = "island" }
    )) {
        $fixtureRequest = New-BaseRequest $snapshot "debug.setup_warp_totem" $variant.item_id
        $fixtureRequest.item_id = $variant.item_id
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Warp Totem fixture failed for $($variant.name): $($fixture.observed_effect)"
        }
        $snapshot = Wait-WarpTotemFixture $snapshotUrl $variant.item_id 60
        $result = Invoke-Post $executeUrl (New-WarpTotemRequest $snapshot $variant.item_id "$RunId.$($variant.name)")
        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            throw "Warp Totem use failed for $($variant.name): status=$($result.status); verification=$($result.primitive_verification_status); reasons=$($result.block_reasons -join ','); observed=$($result.observed_effect)"
        }
        $cases += $result
        $snapshot = Wait-World $snapshotUrl 60
    }
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_warp_totem_smoke.v1"
        status = "passed"
        run_id = $RunId
        passed_case_count = $cases.Count
        total_case_count = 5
        cases = $cases
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) {
        Stop-Process $game.Id -Force -ErrorAction SilentlyContinue
    }
}
