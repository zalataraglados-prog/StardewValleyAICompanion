param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-darts-game-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 300
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

function Wait-DartsSnapshot([int] $TimeoutSeconds, [string] $RequiredGate = "") {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $field = $snapshot.state.player.darts_game
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
    throw "Timed out waiting for Darts snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-darts-game-smoke"
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-darts-game-smoke\" + $RunId)
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
    $loaded = Wait-DartsSnapshot 150

    $setup = New-BaseRequest $loaded "debug.setup_darts_game" "setup"
    $setup.darts_limited_nut_dropped_before = 0
    $setupResult = Invoke-JsonPost $executorUrl $setup
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Darts fixture setup failed: $($setupResult | ConvertTo-Json -Depth 32 -Compress)"
    }

    $cases = @()
    for ($index = 0; $index -lt 3; $index++) {
        $before = Wait-DartsSnapshot 30 "ready"
        $context = $before.state.player.darts_game.value
        $endpoint = @($context.interaction_tiles) | Select-Object -First 1
        if ($null -eq $endpoint) { throw "Darts interaction endpoint missing for round $index." }
        $request = New-BaseRequest $before "executor.play_darts" ("play-round-" + ($index + 1))
        $request.location_id = "IslandSouthEastCave"; $request.target_location = "IslandSouthEastCave"
        $request.target_tile_x = [int]$endpoint.tile_x; $request.target_tile_y = [int]$endpoint.tile_y
        $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
        $request.max_movement_tiles = 512; $request.darts_projection_fingerprint = [string]$context.projection_fingerprint
        $request.darts_action_raw = [string]$endpoint.action_raw; $request.darts_action_token = [string]$endpoint.action_token
        $request.darts_yes_response_key = "Yes"; $request.darts_limited_nut_key = "Darts"; $request.darts_limited_nut_limit = 3
        $request.darts_limited_nut_dropped_before = [int]$context.limited_nut_dropped_before
        $request.darts_limited_nut_dropped_after = [int]$context.limited_nut_dropped_after
        $request.darts_starting_dart_count = [int]$context.starting_dart_count; $request.darts_starting_points = 301
        $request.darts_perfect_victory_max_throws = 6; $request.darts_perfect_score_plan = [string]$context.perfect_score_plan
        $request.darts_charge_release_threshold = [double]$context.charge_release_threshold
        $request.native_contract = [string]$context.native_contract

        $result = Invoke-JsonPost $executorUrl $request
        $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ("round-" + ($index + 1) + "-result.json")) -Encoding utf8
        $immediateAfter = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
        $immediateAfter | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ("round-" + ($index + 1) + "-immediate-snapshot.json")) -Encoding utf8
        $after = Wait-DartsSnapshot 30
        $roundPassed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [int]$after.state.player.darts_game.value.limited_nut_dropped_before -eq ($index + 1)
        $cases += [ordered]@{
            round = $index + 1; status = $result.status; verification = $result.primitive_verification_status
            starting_darts = [int]$context.starting_dart_count; dropped_before = [int]$context.limited_nut_dropped_before
            dropped_after = [int]$after.state.player.darts_game.value.limited_nut_dropped_before
            observed_effect = $result.observed_effect; block_reasons = @($result.block_reasons); passed = $roundPassed
        }
        if (-not $roundPassed) {
            throw "Runtime Darts round $($index + 1) failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
        }
    }

    $final = Wait-DartsSnapshot 30 "complete_three_darts_walnuts_dropped"
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq 3) { "passed" } else { "failed" }; evidence_id = "EVD-306"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = 3; passed_case_count = $passedCount; final_gate = [string]$final.state.player.darts_game.value.gate_status; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 64
    if ($passedCount -ne 3) { throw "Runtime Darts smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
