param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fair-slingshot-game-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Invoke-Post([string] $Url, $Body, [int] $TimeoutSeconds = 300) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.fair_slingshot_game.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for fair slingshot game snapshot."
}
function Wait-FairSlingshot([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-World $Url 15
        $context = $snapshot.state.player.fair_slingshot_game.value
        if ($context.festival_id -eq "festival_fall16" -and $context.gate_status -eq "ready") {
            return $snapshot
        }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for a ready fall16 fair slingshot game context."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-fair-slingshot-game"
        queue_item_id = $QueueItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Select-AdjacentInteraction($Snapshot) {
    $px = [int]$Snapshot.state.player.tile_x.value
    $py = [int]$Snapshot.state.player.tile_y.value
    $row = @($Snapshot.state.player.fair_slingshot_game.value.interaction_tiles | Where-Object {
        ([Math]::Abs([int]$_.tile_x - $px) + [Math]::Abs([int]$_.tile_y - $py)) -eq 1
    }) | Select-Object -First 1
    if ($null -eq $row) { throw "Fixture player is not adjacent to a projected fair slingshot interaction tile." }
    return $row
}
function New-FairSlingshotRequest($Snapshot, [string] $QueueItemId) {
    $context = $Snapshot.state.player.fair_slingshot_game.value
    $interaction = Select-AdjacentInteraction $Snapshot
    $request = New-BaseRequest $Snapshot "executor.play_fair_slingshot_game" $QueueItemId
    $request.location_id = [string]$context.festival_location_id
    $request.max_movement_tiles = 512
    $request.fair_slingshot_projection_fingerprint = [string]$context.projection_fingerprint
    $request.fair_slingshot_interaction_tile_x = [int]$interaction.tile_x
    $request.fair_slingshot_interaction_tile_y = [int]$interaction.tile_y
    $request.fair_slingshot_stand_tile_x = [int]$Snapshot.state.player.tile_x.value
    $request.fair_slingshot_stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.fair_slingshot_money_before = [int]$context.player_money
    $request.fair_slingshot_entry_fee_money = [int]$context.entry_fee_money
    $request.fair_slingshot_festival_score_before = [int]$context.festival_score
    $request.fair_slingshot_stardrop_price_star_tokens = [int]$context.stardrop_price_star_tokens
    $request.fair_slingshot_projected_unclaimed_grange_tokens = [int]$context.projected_unclaimed_grange_tokens
    $request.fair_slingshot_remaining_star_token_demand = [int]$context.remaining_star_token_demand
    $request.fair_slingshot_prestart_duration_ms = [int]$context.prestart_duration_ms
    $request.fair_slingshot_game_duration_ms = [int]$context.game_duration_ms
    $request.fair_slingshot_post_game_delay_ms = [int]$context.post_game_delay_ms
    $request.fair_slingshot_results_duration_ms = [int]$context.results_duration_ms
    $request.fair_slingshot_target_count = @($context.target_sequence).Count
    $request.fair_slingshot_dialogue_key = [string]$context.dialogue_key
    $request.fair_slingshot_play_response_key = [string]$context.play_response_key
    $request.fair_slingshot_execution_strategy = [string]$context.execution_strategy
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-fair-slingshot-game\" + $RunId)
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
    $fixtureRequest = New-BaseRequest $snapshot "debug.setup_fair_slingshot_game" "$RunId.setup"
    $fixture = Invoke-Post $executeUrl $fixtureRequest
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Fair slingshot fixture failed: $($fixture.observed_effect)"
    }
    $snapshot = Wait-FairSlingshot $snapshotUrl 60
    $result = Invoke-Post $executeUrl (New-FairSlingshotRequest $snapshot "$RunId.play") 300
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Fair slingshot game failed: status=$($result.status); reasons=$($result.block_reasons -join ','); observed=$($result.observed_effect)"
    }
    $finalSnapshot = Wait-World $snapshotUrl 60
    $snapshotPath = Join-Path $artifactDirectory "full-snapshot.json"
    $finalSnapshot | ConvertTo-Json -Depth 100 | Set-Content $snapshotPath -Encoding utf8
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_fair_slingshot_game_smoke.v1"
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
