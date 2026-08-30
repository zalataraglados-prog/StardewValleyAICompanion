param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-calico-jack-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try { $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5; if ($null -ne $result) { return $result } }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-CalicoSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.player.calico_jack.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready Calico Jack snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-calico-jack-smoke"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Setup-CalicoSeed([int] $Seed, [int] $Bet, [int] $Coins, [string] $CaseName) {
    $snapshot = Wait-CalicoSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_calico_jack" ("setup-" + $CaseName + "-" + $Seed)
    $request.calico_times_played_seed = $Seed
    $request.calico_bet = $Bet
    $request.calico_club_coins_before = $Coins
    $request.calico_fixture_case = if ($Bet -eq 1000) { "high_stakes" } else { "low_stakes" }
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Calico Jack fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $ready = Wait-CalicoSnapshot 10
        $context = $ready.state.player.calico_jack.value
        if ([int]$context.next_times_played_seed -eq $Seed -and [string]$context.gate_status -eq "ready") { return $ready }
        Start-Sleep -Milliseconds 300
    }
    throw "Calico Jack seed $Seed did not become ready."
}

function Find-CalicoCase(
    [string] $CaseName,
    [int] $Bet,
    [int] $Coins,
    [scriptblock] $Predicate,
    [System.Collections.Generic.HashSet[int]] $UsedSeeds
) {
    foreach ($seed in 1..512) {
        if ($UsedSeeds.Contains($seed)) { continue }
        $snapshot = Setup-CalicoSeed $seed $Bet $Coins $CaseName
        $context = $snapshot.state.player.calico_jack.value
        if (& $Predicate $context) {
            [void]$UsedSeeds.Add($seed)
            return $snapshot
        }
    }
    throw "No deterministic seed found for Calico Jack case $CaseName."
}

function New-CalicoExecutionRequest($Before, [string] $CaseName) {
    $context = $Before.state.player.calico_jack.value
    $next = $context.next_round
    $bet = [int]$context.recommended_bet
    $endpoint = @($context.interaction_tiles) | Where-Object { [int]$_.bet -eq $bet } | Select-Object -First 1
    if ($null -eq $endpoint) { throw "Recommended Calico Jack table missing for $CaseName." }
    $request = New-BaseRequest $Before "executor.play_calico_jack" ("play-" + $CaseName)
    $request.location_id = "Club"; $request.target_location = "Club"
    $request.target_tile_x = [int]$endpoint.tile_x; $request.target_tile_y = [int]$endpoint.tile_y
    $request.stand_tile_x = [int]$Before.state.player.tile_x.value; $request.stand_tile_y = [int]$Before.state.player.tile_y.value
    $request.max_movement_tiles = 512
    $request.calico_projection_fingerprint = [string]$context.projection_fingerprint
    $request.calico_action_raw = [string]$endpoint.action_raw; $request.calico_action_token = [string]$endpoint.action_token
    $request.calico_table_kind = [string]$endpoint.table_kind; $request.calico_bet = $bet
    $request.calico_dialogue_key = [string]$endpoint.dialogue_key; $request.calico_play_response_key = [string]$endpoint.play_response_key
    $request.calico_club_coins_before = [int]$context.club_coins; $request.calico_target_club_coins = [int]$context.target_club_coins
    $request.calico_remaining_club_coin_demand = [int]$context.remaining_club_coin_demand
    $request.calico_target_item_id = [string]$context.target_qualified_item_id
    $request.calico_times_played_seed = [int]$next.times_played_seed; $request.calico_days_played_seed = [int]$context.days_played_seed
    $request.calico_unique_game_id_seed = [string]$context.unique_game_id_seed
    $request.calico_daily_luck = [double]$context.daily_luck; $request.calico_luck_level = [int]$context.luck_level
    $request.calico_player_cards_json = @($next.player_cards) | ConvertTo-Json -Compress
    $request.calico_dealer_cards_json = @($next.dealer_cards_including_hidden) | ConvertTo-Json -Compress
    $request.calico_recommended_first_action = [string]$next.recommended_first_action
    $request.calico_projected_next_hit_card = [int]$next.projected_next_hit_card
    $request.calico_coin_delta_per_low_bet = [int]$next.coin_delta_per_low_bet
    $request.calico_expected_coin_delta = [int]$next.coin_delta_per_low_bet * $bet / 100
    $request.calico_projected_outcome = [string]$next.projected_outcome
    $request.calico_decision_policy = "exact_seed_replay_hidden_card_and_future_draw_max_coin_delta"
    $request.calico_exit_policy = "quit_after_one_native_settlement"
    $request.native_contract = "ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit"
    $request
}

function Invoke-CalicoCase($Before, [string] $CaseName) {
    $beforeContext = $Before.state.player.calico_jack.value
    $request = New-CalicoExecutionRequest $Before $CaseName
    $Before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $after = Wait-CalicoSnapshot 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $afterContext = $after.state.player.calico_jack.value
    $expectedCoins = [int]$beforeContext.club_coins + [int]$request.calico_expected_coin_delta
    [ordered]@{
        case = $CaseName
        passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [int]$afterContext.club_coins -eq $expectedCoins -and
            [int]$afterContext.next_times_played_seed -eq ([int]$beforeContext.next_times_played_seed + 1)
        status = $result.status; verification = $result.primitive_verification_status
        seed = [int]$beforeContext.next_times_played_seed; bet = [int]$request.calico_bet
        first_action = [string]$request.calico_recommended_first_action
        projected_outcome = [string]$request.calico_projected_outcome
        expected_coin_delta = [int]$request.calico_expected_coin_delta
        club_coins_after = [int]$afterContext.club_coins; block_reasons = @($result.block_reasons)
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-calico-jack-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")
$savedEnvironment = @{}; foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-CalicoSnapshot 120 | Out-Null

    $used = [System.Collections.Generic.HashSet[int]]::new()
    $highWin = Find-CalicoCase "high-win" 1000 5000 { param($context) [int]$context.recommended_bet -eq 1000 -and [int]$context.next_round.coin_delta_per_low_bet -gt 0 } $used
    $cases = @((Invoke-CalicoCase $highWin "high-win"))
    $lowLoss = Find-CalicoCase "low-loss" 100 500 { param($context) [int]$context.recommended_bet -eq 100 -and [int]$context.next_round.coin_delta_per_low_bet -lt 0 } $used
    $cases += Invoke-CalicoCase $lowLoss "low-loss"
    $firstHit = Find-CalicoCase "first-hit" 100 500 { param($context) [int]$context.recommended_bet -eq 100 -and [string]$context.next_round.recommended_first_action -eq "hit" } $used
    $cases += Invoke-CalicoCase $firstHit "first-hit"
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $cases.Count) { "passed" } else { "failed" }
        evidence_id = "EVD-304"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = $cases.Count; passed_case_count = $passedCount; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne $cases.Count) { throw "Runtime Calico Jack smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
