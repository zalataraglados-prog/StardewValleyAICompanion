param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-horse-flute-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.horse_flute.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Horse Flute snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-horse-flute"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function New-HorseFluteRequest($Snapshot, [string] $QueueItemId) {
    $context = $Snapshot.state.player.horse_flute.value
    $row = @($context.rows | Where-Object { [string]$_.qualified_item_id -eq "(O)911" -and [int]$_.stack_before -gt 0 }) | Select-Object -First 1
    if ($null -eq $row) { throw "Transparent Horse Flute inventory row missing." }
    if ([string]$context.native_use_gate_status -ne "ready") { throw "Horse Flute projection not ready: $($context.native_use_gate_status)" }
    $horse = $context.owned_horse
    $request = New-Request $Snapshot "executor.use_horse_flute" $QueueItemId
    $request["location_id"] = [string]$Snapshot.state.player.location_id.value
    $request["inventory_slot_index"] = [int]$row.inventory_slot_index
    $request["expected_stack_before"] = [int]$row.stack_before
    $request["expected_stack_after"] = [int]$row.stack_after
    $request["qualified_item_id"] = [string]$row.qualified_item_id
    $request["horse_warp_restrictions"] = [int]$context.horse_warp_restrictions
    $request["horse_warp_restriction_names"] = if (@($context.horse_warp_restriction_names).Count -eq 0) { "none" } else { @($context.horse_warp_restriction_names) -join "," }
    $request["owned_horse_id"] = [string]$horse.horse_id
    $request["owned_horse_location_id"] = [string]$horse.location_id
    $request["owned_horse_tile_x"] = [int]$horse.tile_x
    $request["owned_horse_tile_y"] = [int]$horse.tile_y
    $request["owned_horse_nearby"] = [bool]$horse.is_nearby
    $request["team_event_stable_horse_id"] = [string]$context.team_event_stable_binding.stable_horse_id
    $request["team_event_stable_location_id"] = [string]$context.team_event_stable_binding.stable_location_id
    $request["team_event_stable_tile_x"] = [int]$context.team_event_stable_binding.stable_tile_x
    $request["team_event_stable_tile_y"] = [int]$context.team_event_stable_binding.stable_tile_y
    $request["team_event_stable_matches_owned_horse"] = [bool]$context.team_event_stable_binding.matches_owned_horse
    $request["horse_flute_expected_result"] = [string]$context.expected_result
    $request["horse_flute_use_delay_ms"] = [int]$context.use_delay_ms
    $request["horse_flute_freeze_pause_ms"] = [int]$context.freeze_pause_ms
    $request["horse_flute_music_duck_ms"] = [int]$context.music_duck_ms
    $request["horse_flute_expected_facing_direction"] = [int]$context.facing_direction
    $request["native_contract"] = [string]$context.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI executable not found: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Horse Flute smoke requires unused port $port." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-horse-flute\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
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
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru

    $snapshot = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $fixture = Invoke-Post $executeUrl (New-Request $snapshot "debug.setup_horse_flute" "$RunId.fixture")
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") { throw "Horse Flute fixture failed: $(@($fixture.block_reasons) -join ',')" }

    $beforeSummon = Wait-World $snapshotUrl 60
    $summon = Invoke-Post $executeUrl (New-HorseFluteRequest $beforeSummon "$RunId.summon")
    if ($summon.status -ne "applied" -or $summon.primitive_verification_status -ne "verified") { throw "Horse Flute summon branch failed: $($summon.observed_effect)" }

    $beforeNoop = Wait-World $snapshotUrl 60
    $noop = Invoke-Post $executeUrl (New-HorseFluteRequest $beforeNoop "$RunId.adjacent-noop")
    if ($noop.status -ne "applied" -or $noop.primitive_verification_status -ne "verified") { throw "Horse Flute adjacent no-op branch failed: $($noop.observed_effect)" }

    $cases = @(
        [ordered]@{ branch = "summon_after_1500ms"; status = "passed"; observed_effect = [string]$summon.observed_effect },
        [ordered]@{ branch = "already_adjacent_no_warp"; status = "passed"; observed_effect = [string]$noop.observed_effect })
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_horse_flute_smoke.v1"
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        passed_case_count = 2
        total_case_count = 2
        cases = $cases
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue }
}
