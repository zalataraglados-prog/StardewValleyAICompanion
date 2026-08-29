param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fair-strength-game-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Invoke-Post([string] $Url, $Body, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.fair_strength_game.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for fair strength game snapshot."
}
function Wait-FairStrength([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-World $Url 15
        $context = $snapshot.state.player.fair_strength_game.value
        if ($context.festival_id -eq "festival_fall16" -and $context.gate_status -eq "ready" -and
            [int]$context.remaining_star_token_demand -eq 1) { return $snapshot }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for an exact-one-token fall16 fair strength game context."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-fair-strength-game"
        queue_item_id = $QueueItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Select-StrengthEndpoint($Snapshot) {
    $px = [int]$Snapshot.state.player.tile_x.value
    $py = [int]$Snapshot.state.player.tile_y.value
    $row = @($Snapshot.state.player.fair_strength_game.value.interaction_tiles | Where-Object {
        [int]$_.stand_tile_x -eq $px -and [int]$_.stand_tile_y -eq $py -and
        [int]$_.stand_tile_x -eq 29 -and [int]$_.tile_index -eq 540
    }) | Select-Object -First 1
    if ($null -eq $row) { throw "Fixture player is not on the projected x=29 Fair strength stand." }
    return $row
}
function New-FairStrengthRequest($Snapshot, [string] $QueueItemId) {
    $context = $Snapshot.state.player.fair_strength_game.value
    $endpoint = Select-StrengthEndpoint $Snapshot
    $request = New-BaseRequest $Snapshot "executor.play_fair_strength_game" $QueueItemId
    $request.location_id = [string]$context.festival_location_id
    $request.max_movement_tiles = 512
    $request.fair_strength_projection_fingerprint = [string]$context.projection_fingerprint
    $request.fair_strength_interaction_tile_x = [int]$endpoint.tile_x
    $request.fair_strength_interaction_tile_y = [int]$endpoint.tile_y
    $request.fair_strength_stand_tile_x = [int]$endpoint.stand_tile_x
    $request.fair_strength_stand_tile_y = [int]$endpoint.stand_tile_y
    $request.fair_strength_festival_score_before = [int]$context.festival_score
    $request.fair_strength_stardrop_price_star_tokens = [int]$context.stardrop_price_star_tokens
    $request.fair_strength_projected_unclaimed_grange_tokens = [int]$context.projected_unclaimed_grange_tokens
    $request.fair_strength_remaining_star_token_demand = [int]$context.remaining_star_token_demand
    $request.fair_strength_entry_fee_money = [int]$context.entry_fee_money
    $request.fair_strength_expected_reward_star_tokens = [int]$context.expected_reward_star_tokens
    $request.fair_strength_perfect_power_minimum = [double]$context.perfect_power_minimum
    $request.fair_strength_power_maximum = [double]$context.power_maximum
    $request.fair_strength_required_player_tile_x = [int]$context.required_player_tile_x
    $request.fair_strength_swing_start_frame = [int]$context.swing_animation.start_frame
    $request.fair_strength_swing_interval_ms = [double]$context.swing_animation.interval_ms
    $request.fair_strength_swing_frame_count = [int]$context.swing_animation.frame_count
    $request.fair_strength_perfect_result_delay_ms = [double]$context.perfect_result_delay_ms
    $request.fair_strength_execution_strategy = [string]$context.execution_strategy
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-fair-strength-game\" + $RunId)
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
    $fixtureRequest = New-BaseRequest $snapshot "debug.setup_fair_strength_game" "$RunId.setup"
    $fixture = Invoke-Post $executeUrl $fixtureRequest
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Fair strength fixture failed: $($fixture.observed_effect)"
    }
    $snapshot = Wait-FairStrength $snapshotUrl 60
    $result = Invoke-Post $executeUrl (New-FairStrengthRequest $snapshot "$RunId.play") 180
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Fair strength game failed: status=$($result.status); reasons=$($result.block_reasons -join ','); observed=$($result.observed_effect)"
    }
    $finalSnapshot = Wait-World $snapshotUrl 60
    $snapshotPath = Join-Path $artifactDirectory "full-snapshot.json"
    $finalSnapshot | ConvertTo-Json -Depth 100 | Set-Content $snapshotPath -Encoding utf8
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_fair_strength_game_smoke.v1"
        status = "passed"
        run_id = $RunId
        primitive_verification_status = $result.primitive_verification_status
        observed_effect = $result.observed_effect
        result = $result
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    [ordered]@{
        status = $summary.status
        run_id = $summary.run_id
        primitive_verification_status = $summary.primitive_verification_status
        observed_effect = $summary.observed_effect
        artifact = (Join-Path $artifactDirectory "summary.json")
        snapshot = $snapshotPath
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
