param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-prairie-king-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 300) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
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

function Wait-PrairieKingSnapshot([int] $TimeoutSeconds, [string] $RequiredGate = "") {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 15
            $field = $snapshot.state.player.prairie_king
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
    throw "Timed out waiting for Prairie King snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-prairie-king-smoke"
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-prairie-king-smoke\" + $RunId)
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
    $loaded = Wait-PrairieKingSnapshot 150

    $setup = New-BaseRequest $loaded "debug.setup_prairie_king" "setup"
    $setupResult = Invoke-JsonPost $executorUrl $setup
    $setupResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "fixture-result.json") -Encoding utf8
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Prairie King fixture setup failed: $($setupResult | ConvertTo-Json -Depth 32 -Compress)"
    }

    $before = Wait-PrairieKingSnapshot 30 "ready"
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "before-snapshot.json") -Encoding utf8
    $context = $before.state.player.prairie_king.value
    $endpoint = @($context.interaction_tiles) | Select-Object -First 1
    if ($null -eq $endpoint) { throw "Prairie King interaction endpoint missing." }

    $request = New-BaseRequest $before "executor.play_prairie_king" "play"
    $request.location_id = "Saloon"; $request.target_location = "Saloon"
    $request.target_tile_x = [int]$endpoint.tile_x; $request.target_tile_y = [int]$endpoint.tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.max_movement_tiles = 512; $request.minigame_id = "PrairieKing"
    $request.prairie_king_projection_fingerprint = [string]$context.projection_fingerprint
    $request.prairie_king_action_raw = [string]$endpoint.action_raw; $request.prairie_king_action_token = [string]$endpoint.action_token
    $request.prairie_king_dialogue_key = [string]$context.dialogue_key; $request.prairie_king_dialogue_response_key = [string]$context.dialogue_response_key
    $request.prairie_king_completed_before = [long]$context.completed_before
    $request.prairie_king_completed_without_dying_before = [long]$context.completed_without_dying_before
    $request.prairie_king_completion_goal = [string]$context.completion_goal
    $request.prairie_king_equivalent_duration_ticks = [int]$context.equivalent_duration_ticks
    $request.prairie_king_equivalent_acceleration = [int]$context.equivalent_acceleration
    $request.prairie_king_equivalent_contract = [string]$context.equivalent_contract

    $result = Invoke-JsonPost $executorUrl $request 300
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "play-result.json") -Encoding utf8
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "simulated_equivalent") {
        throw "Prairie King execution failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $after = Wait-PrairieKingSnapshot 30 "complete_prairie_king_without_dying"
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "after-snapshot.json") -Encoding utf8
    $afterContext = $after.state.player.prairie_king.value
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "simulated_equivalent" -and
        [long]$afterContext.completed_before -eq ([long]$context.completed_before + 1) -and
        [long]$afterContext.completed_without_dying_before -eq ([long]$context.completed_without_dying_before + 1)
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }; evidence_id = "EVD-307"; run_id = $RunId; save_slot = $SaveSlot
        invocation_policy = [string]$context.invocation_policy; native_proxy_policy = [string]$context.native_proxy_policy
        execution_status = $result.status; primitive_verification = $result.primitive_verification_status
        completed_before = [long]$context.completed_before; completed_after = [long]$afterContext.completed_before
        completed_without_dying_before = [long]$context.completed_without_dying_before
        completed_without_dying_after = [long]$afterContext.completed_without_dying_before
        final_gate = [string]$afterContext.gate_status; observed_effect = $result.observed_effect
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 64
    if (-not $passed) { throw "Runtime Prairie King smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
