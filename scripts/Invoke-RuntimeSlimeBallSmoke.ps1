[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-slime-ball-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-slime-ball",
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "schema=$($snapshot.schema_version);save_id=$($snapshot.save_id.status)"
        } catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot. Last status: $lastStatus"
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-slime-ball"
        queue_item_id = $ItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Close-InitialMenus($Snapshot) {
    $current = $Snapshot
    for ($pass = 0; $pass -lt 8 -and $current.state.menus.active_menu.value.is_open; $pass++) {
        $close = Invoke-JsonPost $executeUrl (New-Request $current "executor.close_menu" "$RunId.initial-close.$pass")
        if ($close.status -ne "applied") { throw "Initial menu close failed: $(@($close.block_reasons) -join ',')" }
        Start-Sleep -Seconds 1
        $current = Wait-Snapshot $snapshotUrl 30
    }
    if ($current.state.menus.active_menu.value.is_open) { throw "Initial menu did not close." }
    return $current
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$loadedModAllowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
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
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $current = Close-InitialMenus (Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds)

    $fixture = Invoke-JsonPost $executeUrl (New-Request $current "debug.setup_slime_ball" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture.json") $fixture
    if ($fixture.status -ne "applied") { throw "Slime Ball fixture failed: $(@($fixture.block_reasons) -join ',')" }
    Start-Sleep -Milliseconds 500

    $ready = Wait-Snapshot $snapshotUrl 30
    $ball = @($ready.state.current_location.objects.value | Where-Object { $null -ne $_.slime_ball_collection }) | Select-Object -First 1
    if ($null -eq $ball) { throw "Slime Ball transparent projection missing." }
    $projection = $ball.slime_ball_collection
    if ($projection.status -ne "ready") { throw "Slime Ball projection is not ready: $($projection.status)" }
    $stand = @($projection.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    $safe = $ready.state.player.safe_item_context.value
    if ($null -eq $stand -or $safe.safe_slot_kind -ne "empty") { throw "Slime Ball stand or empty slot unavailable." }

    $request = New-Request $ready "farming.collect_slime_ball" "$RunId.collect"
    $request.location_id = [string]$ready.state.player.location_id.value
    $request.target_tile_x = [int]$ball.tile_x
    $request.target_tile_y = [int]$ball.tile_y
    $request.stand_tile_x = [int]$stand.tile_x
    $request.stand_tile_y = [int]$stand.tile_y
    $request.target_runtime_type = [string]$projection.target_runtime_type
    $request.item_id = [string]$projection.canonical_item_id
    $request.qualified_item_id = [string]$projection.canonical_qualified_item_id
    $request.required_fragility = [int]$projection.required_fragility
    $request.slime_ball_seed_days_played = [int]$projection.day_seed_days_played
    $request.slime_ball_seed_unique_game_id = [long]$projection.day_seed_unique_game_id
    $request.slime_ball_expected_slime_quantity = [int]$projection.expected_slime_quantity
    $request.slime_ball_expected_petrified_slime_quantity = [int]$projection.expected_petrified_slime_quantity
    $request.slime_ball_expected_location_action_return = [bool]$projection.expected_native_location_action_return
    $request.safe_slot_index = [int]$safe.safe_slot_index
    $request.restore_slot_index = [int]$safe.current_tool_index
    $request.interaction_kind = [string]$projection.interaction_kind
    $request.expected_action_type = [string]$projection.expected_action_type
    $request.native_contract = [string]$projection.native_contract
    $request.max_movement_tiles = 512
    $result = Invoke-JsonPost $executeUrl $request
    Write-Json (Join-Path $runDirectory "collection.json") $result
    Start-Sleep -Milliseconds 500
    $after = Wait-Snapshot $snapshotUrl 30
    $afterBall = @($after.state.current_location.objects.value | Where-Object {
        [int]$_.tile_x -eq [int]$ball.tile_x -and [int]$_.tile_y -eq [int]$ball.tile_y
    }) | Select-Object -First 1
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
        $null -eq $afterBall -and [int]$after.state.player.current_tool_index.value -eq [int]$safe.current_tool_index
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_slime_ball_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        target_tile = "$($ball.tile_x),$($ball.tile_y)"
        expected_slime_quantity = [int]$projection.expected_slime_quantity
        expected_petrified_slime_quantity = [int]$projection.expected_petrified_slime_quantity
        execution_status = $result.status
        verification = $result.primitive_verification_status
        object_present_after = $null -ne $afterBall
        loaded_mod_allowlist = $loadedModAllowlist
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
