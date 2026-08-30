param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-slots-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec 180
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

function Wait-SlotsSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.player.slots.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready Slots snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-slots-smoke"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Setup-Slots([int] $Bet, [int] $Coins, [int] $TimesPlayed, [string] $CaseName) {
    $snapshot = Wait-SlotsSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_slots" ("setup-" + $CaseName)
    $request.slots_bet = $Bet; $request.slots_club_coins_before = $Coins
    $request.slots_times_played_before = $TimesPlayed
    $request.slots_fixture_case = if ($Bet -eq 10) { "low_bet" } else { "high_bet" }
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Slots fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $ready = Wait-SlotsSnapshot 10
        $context = $ready.state.player.slots.value
        if ([int]$context.times_played -eq $TimesPlayed -and [int]$context.recommended_bet -eq $Bet -and
            [string]$context.gate_status -eq "ready") { return $ready }
        Start-Sleep -Milliseconds 300
    }
    throw "Slots case $CaseName did not become ready."
}

function New-SlotsExecutionRequest($Before, [string] $CaseName) {
    $context = $Before.state.player.slots.value
    $endpoint = @($context.interaction_tiles) | Select-Object -First 1
    if ($null -eq $endpoint) { throw "Slots endpoint missing for $CaseName." }
    $request = New-BaseRequest $Before "executor.play_slots" ("play-" + $CaseName)
    $request.location_id = "Club"; $request.target_location = "Club"
    $request.target_tile_x = [int]$endpoint.tile_x; $request.target_tile_y = [int]$endpoint.tile_y
    $request.stand_tile_x = [int]$Before.state.player.tile_x.value; $request.stand_tile_y = [int]$Before.state.player.tile_y.value
    $request.max_movement_tiles = 512
    $request.slots_projection_fingerprint = [string]$context.projection_fingerprint
    $request.slots_action_raw = [string]$endpoint.action_raw; $request.slots_action_token = [string]$endpoint.action_token
    $request.slots_bet = [int]$context.recommended_bet; $request.slots_club_coins_before = [int]$context.club_coins
    $request.slots_target_club_coins = [int]$context.target_club_coins
    $request.slots_remaining_club_coin_demand = [int]$context.remaining_club_coin_demand
    $request.slots_target_item_id = [string]$context.target_qualified_item_id
    $request.slots_times_played_before = [int]$context.times_played
    $request.slots_daily_luck = [double]$context.daily_luck; $request.slots_luck_level = [int]$context.luck_level
    $request.slots_luck_multiplier = [double]$context.luck_multiplier
    $request.slots_expected_payout_multiplier = [double]$context.expected_payout_multiplier
    $request.slots_expected_net_coin_delta = [double]$context.expected_net_coin_delta
    $request.slots_payout_rows_json = @($context.payout_rows) | ConvertTo-Json -Depth 16 -Compress
    $request.slots_rng_contract = [string]$context.rng_contract
    $request.slots_exit_policy = [string]$context.exit_policy
    $request.native_contract = [string]$context.native_contract
    $request
}

function Invoke-SlotsCase([int] $Bet, [int] $Coins, [int] $TimesPlayed, [string] $CaseName) {
    $before = Setup-Slots $Bet $Coins $TimesPlayed $CaseName
    $request = New-SlotsExecutionRequest $before $CaseName
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $after = Wait-SlotsSnapshot 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $contextAfter = $after.state.player.slots.value
    [ordered]@{
        case = $CaseName
        passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [int]$contextAfter.times_played -eq ($TimesPlayed + 1) -and [string]$contextAfter.active_spin -eq ""
        status = $result.status; verification = $result.primitive_verification_status; bet = $Bet
        club_coins_before = $Coins; club_coins_after = [int]$contextAfter.club_coins
        observed_effect = [string]$result.observed_effect; block_reasons = @($result.block_reasons)
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-slots-smoke\" + $RunId)
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
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-SlotsSnapshot 120 | Out-Null

    $cases = @(
        (Invoke-SlotsCase 10 50 42 "low-bet"),
        (Invoke-SlotsCase 100 1000 84 "high-bet")
    )
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $cases.Count) { "passed" } else { "failed" }
        evidence_id = "EVD-308"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = $cases.Count; passed_case_count = $passedCount; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne $cases.Count) { throw "Runtime Slots smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
