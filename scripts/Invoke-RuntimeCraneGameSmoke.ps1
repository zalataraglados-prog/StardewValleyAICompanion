param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-crane-game-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 240
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try { $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10; if ($null -ne $result) { return $result } }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-CraneSnapshot([int] $TimeoutSeconds, [string] $RequiredGate = "") {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $field = $snapshot.state.player.crane_game
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $field.status -in @("available", "derived") -and
                ([string]::IsNullOrWhiteSpace($RequiredGate) -or [string]$field.value.gate_status -eq $RequiredGate)) {
                return $snapshot
            }
            $lastError = "gate=" + [string]$field.value.gate_status
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Crane Game snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-crane-game-smoke"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-crane-game-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_SUPPRESS_LOCAL_RENDER", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")
$savedEnvironment = @{}; foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"; $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 45 | Out-Null
    $loaded = Wait-CraneSnapshot 150

    $setup = New-BaseRequest $loaded "debug.setup_crane_game" "setup"
    $setup.crane_money_before = 10000
    $setupResult = Invoke-JsonPost $executorUrl $setup
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Crane Game fixture setup failed: $($setupResult | ConvertTo-Json -Depth 32 -Compress)"
    }
    $before = Wait-CraneSnapshot 30 "ready"
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "before-snapshot.json") -Encoding utf8
    $context = $before.state.player.crane_game.value
    $endpoint = @($context.interaction_tiles) | Select-Object -First 1
    if ($null -eq $endpoint) { throw "Crane Game interaction endpoint missing." }
    $request = New-BaseRequest $before "executor.play_crane_game" "play-one-session"
    $request.location_id = "MovieTheater"; $request.target_location = "MovieTheater"
    $request.target_tile_x = [int]$endpoint.tile_x; $request.target_tile_y = [int]$endpoint.tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.max_movement_tiles = 512; $request.crane_projection_fingerprint = [string]$context.projection_fingerprint
    $request.crane_action_raw = [string]$endpoint.action_raw; $request.crane_action_token = [string]$endpoint.action_token
    $request.crane_yes_response_key = "Yes"; $request.crane_fee_gold = 500; $request.crane_money_before = [int]$context.money
    $request.crane_empty_slots_before = [int]$context.inventory_empty_slots; $request.crane_attempts = 3
    $request.crane_timer_ticks_per_attempt = 900; $request.crane_selection_policy = [string]$context.selection_policy
    $request.crane_exit_policy = "finish_three_attempts_then_collect_all_native_rewards"
    $request.native_contract = [string]$context.native_contract

    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "result.json") -Encoding utf8
    $after = Wait-CraneSnapshot 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "after-snapshot.json") -Encoding utf8
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
        [int]$after.state.player.money.value -eq ([int]$context.money - 500) -and
        [string]$after.state.player.crane_game.value.active_session -eq ""
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }; evidence_id = "EVD-305"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = 1; passed_case_count = if ($passed) { 1 } else { 0 }
        case = [ordered]@{ status = $result.status; verification = $result.primitive_verification_status; money_before = [int]$context.money; money_after = [int]$after.state.player.money.value; observed_effect = $result.observed_effect; block_reasons = @($result.block_reasons) }
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if (-not $passed) { throw "Runtime Crane Game smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
