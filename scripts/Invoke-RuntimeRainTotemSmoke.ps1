param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-rain-totem-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $FestivalFixtureOnly,
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
                $snapshot.state.player.rain_totem.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Rain Totem snapshot."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-rain-totem"
        queue_item_id = $ItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Set-RainTotemFixture($Snapshot, [string] $Location, [bool] $FestivalTomorrow, [string] $ItemId) {
    $request = New-BaseRequest $Snapshot "debug.setup_rain_totem" $ItemId
    $request.location_id = $Location
    $request.rain_totem_tomorrow_is_default_festival = $FestivalTomorrow
    Invoke-Post $executeUrl $request
}
function New-RainTotemRequest($Snapshot, [string] $ItemId, [bool] $ForceNonFestivalClaim = $false) {
    $context = $Snapshot.state.player.rain_totem.value
    $row = @($context.rows | Where-Object { $_.qualified_item_id -eq "(O)681" -and $_.stack_before -gt 0 }) | Select-Object -First 1
    if ($null -eq $row) { throw "Rain Totem inventory row is unavailable." }
    $routing = $context.context_routing; $weather = $context.weather_transition; $animation = $context.animation_contract
    $request = New-BaseRequest $Snapshot "executor.use_rain_totem" $ItemId
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.inventory_slot_index = [int]$row.inventory_slot_index
    $request.item_id = [string]$row.item_id; $request.qualified_item_id = [string]$row.qualified_item_id
    $request.expected_stack_before = [int]$row.stack_before; $request.expected_stack_after = [int]$row.stack_after
    $request.rain_totem_projection_fingerprint = [string]$context.projection_fingerprint
    $request.rain_totem_source_location_context_id = [string]$routing.source_location_context_id
    $request.rain_totem_configured_affected_context_id = [string]$routing.configured_affected_context_id
    $request.rain_totem_affected_location_context_id = [string]$routing.affected_location_context_id
    $request.rain_totem_weather_state_owner_context_id = [string]$routing.weather_state_owner_context_id
    $request.rain_totem_allow_rain_totem = [bool]$routing.allow_rain_totem
    $request.rain_totem_tomorrow_is_default_festival = if ($ForceNonFestivalClaim) { $false } else { [bool]$weather.tomorrow_is_default_festival }
    $request.rain_totem_affected_weather_before = [string]$weather.affected_weather_before
    $request.rain_totem_affected_weather_after = [string]$weather.affected_weather_after
    $request.rain_totem_tomorrow_total_days = [int]$weather.tomorrow_total_days
    $request.rain_totem_effective_tomorrow_weather = [string]$weather.effective_tomorrow_weather
    $request.rain_totem_rain_will_take_effect_tomorrow = [bool]$weather.rain_will_take_effect_tomorrow
    $request.rain_totem_facing_direction = [int]$animation.facing_direction
    $request.rain_totem_animation_duration_ms = [int]$animation.animation_duration_ms
    $request.rain_totem_cloud_sprite_count = [int]$animation.cloud_sprite_count
    $request.rain_totem_item_sprite_count = [int]$animation.item_sprite_count
    $request.rain_totem_cloud_batch_count = [int]$animation.cloud_batch_count
    $request.rain_totem_cloud_delay_step_ms = [int]$animation.cloud_delay_step_ms
    $request.rain_totem_initial_sound = [string]$animation.initial_sound
    $request.rain_totem_delayed_sound = [string]$animation.delayed_sound
    $request.rain_totem_delayed_sound_ms = [int]$animation.delayed_sound_ms
    $request.native_contract = [string]$context.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"; $smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"; $snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is in use." } }
if (Get-Process StardewModdingAPI -ErrorAction SilentlyContinue) { throw "StardewModdingAPI is already running." }
$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-rain-totem\" + $RunId); New-Item -ItemType Directory -Force $artifactDirectory | Out-Null
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
    if ($FestivalFixtureOnly) {
        $fixture = Set-RainTotemFixture $snapshot "Farm" $true "$RunId.festival.fixture"
        $fixture | ConvertTo-Json -Depth 16
        return
    }
    $cases = @()
    foreach ($variant in @(@{ name="default"; location="Farm" }, @{ name="desert_routes_default"; location="Desert" }, @{ name="island"; location="IslandSouth" })) {
        $fixture = Set-RainTotemFixture $snapshot $variant.location $false "$RunId.$($variant.name).fixture"
        if ($fixture.status -ne "applied") { throw "Rain Totem fixture failed for $($variant.name): $($fixture.observed_effect)" }
        $snapshot = Wait-World $snapshotUrl 60
        if ($snapshot.state.player.rain_totem.value.native_use_gate_status -ne "ready") {
            $gate = $snapshot.state.player.rain_totem.value.native_base_use_gate | ConvertTo-Json -Compress
            throw "Rain Totem projection is not ready for $($variant.name): status=$($snapshot.state.player.rain_totem.value.native_use_gate_status); base=$gate"
        }
        $result = Invoke-Post $executeUrl (New-RainTotemRequest $snapshot "$RunId.$($variant.name)")
        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            throw "Rain Totem use failed for $($variant.name): status=$($result.status); verification=$($result.primitive_verification_status); reasons=$($result.block_reasons -join ','); observed=$($result.observed_effect)"
        }
        $cases += $result; $snapshot = Wait-World $snapshotUrl 60
    }
    $fixture = Set-RainTotemFixture $snapshot "Farm" $true "$RunId.festival.fixture"
    if ($fixture.status -ne "applied") { throw "Rain Totem festival fixture failed: reasons=$($fixture.block_reasons -join ','); verification=$($fixture.primitive_verification_reasons -join ','); observed=$($fixture.observed_effect)" }
    $snapshot = Wait-World $snapshotUrl 60
    if ($snapshot.state.player.rain_totem.value.native_use_gate_status -ne "blocked_default_festival_tomorrow") { throw "Festival guard projection did not block." }
    $blocked = Invoke-Post $executeUrl (New-RainTotemRequest $snapshot "$RunId.festival" $true)
    $afterBlocked = Wait-World $snapshotUrl 60
    $remaining = @($afterBlocked.state.player.rain_totem.value.rows | Where-Object { $_.qualified_item_id -eq "(O)681" }) | Select-Object -First 1
    if ($blocked.status -ne "blocked" -or [int]$remaining.stack_before -ne 2) { throw "Festival guard consumed the Rain Totem." }
    $cases += $blocked
    $summary = [ordered]@{ schema_version = "stardewai.runtime_rain_totem_smoke.v1"; status = "passed"; run_id = $RunId; passed_case_count = 4; total_case_count = 4; cases = $cases }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) { Stop-Process $game.Id -Force -ErrorAction SilentlyContinue }
}
