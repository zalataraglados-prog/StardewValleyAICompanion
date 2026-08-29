param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fair-wheel-spin-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [int] $MaxAttempts = 12,
    [switch] $RequireBothOutcomes,
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
                $snapshot.state.player.fair_wheel_spin.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for fair wheel snapshot."
}
function Wait-FairWheel([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-World $Url 15
        $context = $snapshot.state.player.fair_wheel_spin.value
        if ($context.festival_id -eq "festival_fall16" -and $context.gate_status -eq "ready" -and
            [int]$context.festival_score -eq 1000 -and [int]$context.wager_star_tokens -eq 466) { return $snapshot }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for a bounded Fall 16 fair wheel context."
}
function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-fair-wheel-spin"
        queue_item_id = $QueueItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Select-WheelEndpoint($Snapshot) {
    $px = [int]$Snapshot.state.player.tile_x.value
    $py = [int]$Snapshot.state.player.tile_y.value
    $row = @($Snapshot.state.player.fair_wheel_spin.value.interaction_tiles | Where-Object {
        [int]$_.stand_tile_x -eq $px -and [int]$_.stand_tile_y -eq $py -and
        [int]$_.tile_index -in @(308, 309)
    }) | Select-Object -First 1
    if ($null -eq $row) { throw "Fixture player is not on a projected Fair wheel stand." }
    return $row
}
function New-FairWheelRequest($Snapshot, [string] $QueueItemId) {
    $context = $Snapshot.state.player.fair_wheel_spin.value
    $endpoint = Select-WheelEndpoint $Snapshot
    $distribution = $context.base_zero_luck_distribution
    $request = New-BaseRequest $Snapshot "executor.spin_fair_wheel" $QueueItemId
    $request.location_id = [string]$context.festival_location_id
    $request.max_movement_tiles = 512
    $request.fair_wheel_projection_fingerprint = [string]$context.projection_fingerprint
    $request.fair_wheel_interaction_tile_x = [int]$endpoint.tile_x
    $request.fair_wheel_interaction_tile_y = [int]$endpoint.tile_y
    $request.fair_wheel_stand_tile_x = [int]$endpoint.stand_tile_x
    $request.fair_wheel_stand_tile_y = [int]$endpoint.stand_tile_y
    $request.fair_wheel_festival_score_before = [int]$context.festival_score
    $request.fair_wheel_stardrop_price_star_tokens = [int]$context.stardrop_price_star_tokens
    $request.fair_wheel_projected_unclaimed_grange_tokens = [int]$context.projected_unclaimed_grange_tokens
    $request.fair_wheel_remaining_star_token_demand = [int]$context.remaining_star_token_demand
    $request.fair_wheel_selected_color = [string]$context.selected_color
    $request.fair_wheel_wager_star_tokens = [int]$context.wager_star_tokens
    $request.fair_wheel_luck_level = [int]$context.effective_luck_level
    $request.fair_wheel_base_green_wins = [int]$distribution.green_wins
    $request.fair_wheel_base_orange_wins = [int]$distribution.orange_wins
    $request.fair_wheel_base_outcome_count = [int]$distribution.constructor_outcomes
    $request.fair_wheel_prestart_duration_ms = [int]$context.prestart_duration_ms
    $request.fair_wheel_result_duration_ms = [int]$context.result_duration_ms
    $request.fair_wheel_dialogue_key = [string]$context.dialogue_key
    $request.fair_wheel_response_key = [string]$context.response_key
    $request.fair_wheel_wager_policy = [string]$context.wager_policy
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-fair-wheel-spin\" + $RunId)
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
    $attempts = @()
    $sawWin = $false
    $sawLoss = $false
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $fixtureRequest = New-BaseRequest $snapshot "debug.setup_fair_wheel_spin" "$RunId.setup.$attempt"
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Fair wheel fixture failed: $($fixture.observed_effect)"
        }
        $snapshot = Wait-FairWheel $snapshotUrl 60
        $result = Invoke-Post $executeUrl (New-FairWheelRequest $snapshot "$RunId.spin.$attempt") 90
        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            throw "Fair wheel spin failed: status=$($result.status); reasons=$($result.block_reasons -join ','); observed=$($result.observed_effect)"
        }
        $outcome = if ([string]$result.observed_effect -match ";outcome=win") { "win" } elseif ([string]$result.observed_effect -match ";outcome=loss") { "loss" } else { "unknown" }
        if ($outcome -eq "win") { $sawWin = $true }
        if ($outcome -eq "loss") { $sawLoss = $true }
        $attempts += [ordered]@{ attempt = $attempt; outcome = $outcome; result = $result }
        $snapshot = Wait-World $snapshotUrl 60
        if (-not $RequireBothOutcomes -or ($sawWin -and $sawLoss)) { break }
    }
    if ($RequireBothOutcomes -and -not ($sawWin -and $sawLoss)) {
        throw "Did not observe both native wheel outcomes in $MaxAttempts attempts."
    }
    $snapshotPath = Join-Path $artifactDirectory "full-snapshot.json"
    $snapshot | ConvertTo-Json -Depth 100 | Set-Content $snapshotPath -Encoding utf8
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_fair_wheel_spin_smoke.v1"
        status = "passed"
        run_id = $RunId
        attempt_count = $attempts.Count
        saw_win = $sawWin
        saw_loss = $sawLoss
        attempts = $attempts
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    [ordered]@{
        status = $summary.status
        run_id = $summary.run_id
        attempt_count = $summary.attempt_count
        saw_win = $summary.saw_win
        saw_loss = $summary.saw_loss
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
