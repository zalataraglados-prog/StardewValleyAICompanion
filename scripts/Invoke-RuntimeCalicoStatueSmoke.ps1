[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-calico-statue-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $snapshotUrl 30
            if ($snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "save=$($snapshot.save_id.status)"
        } catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for Calico Statue snapshot. Last status: $lastStatus"
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-calico-statue"
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
        Start-Sleep -Milliseconds 500
        $current = Wait-Snapshot 30
    }
    return $current
}

function Wait-ReadyEffect([int] $EffectId, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $projectionField = $snapshot.state.mining.calico_statue
        $projection = $projectionField.value
        if ($projectionField.status -in @("available", "derived") -and
            $projection.gate_status -eq "ready" -and
            [int]$projection.projected_effect_id -eq $EffectId -and
            @($projection.stand_tiles | Where-Object { $_.available }).Count -gt 0) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Calico Statue effect $EffectId did not become ready."
}

function New-ActivationRequest($Snapshot, [int] $EffectId) {
    $projection = $Snapshot.state.mining.calico_statue.value
    $stand = @($projection.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    if ($null -eq $stand) { throw "No reachable Calico Statue stand for effect $EffectId." }
    $effect = $projection.projected_effect
    $request = New-Request $Snapshot "executor.activate_calico_statue" "$RunId.activate.$EffectId"
    $request.location_id = [string]$projection.location_id
    $request.target_location = [string]$projection.location_id
    $request.target_tile_x = [int]$projection.target_tile_x
    $request.target_tile_y = [int]$projection.target_tile_y
    $request.stand_tile_x = [int]$stand.tile_x
    $request.stand_tile_y = [int]$stand.tile_y
    $request.max_movement_tiles = 512
    $request.calico_statue_projection_fingerprint = [string]$projection.projection_fingerprint
    $request.calico_statue_accepted_effect_id = [int]$projection.projected_effect_id
    $request.calico_statue_effect_key = [string]$effect.effect_key
    $request.calico_statue_strategy_polarity = [string]$effect.strategy_polarity
    $request.calico_statue_exact_effect = [string]$effect.exact_effect
    $request.calico_statue_calico_egg_reward = [int]$effect.calico_egg_reward
    $request.calico_statue_current_effects_csv = [string]$projection.current_effects_csv
    $request.calico_statue_expected_effects_after_csv = [string]$projection.expected_effects_after_csv
    $request.calico_statue_total_activated_before = [int]$projection.total_activated_today_before
    $request.calico_statue_next_activation_number = [int]$projection.next_activation_number
    $request.calico_statue_rating_before = [int]$projection.rating_before
    $request.calico_statue_expected_rating_after = [int]$projection.expected_rating_after
    $request.calico_statue_average_daily_luck = [double]$projection.average_daily_luck
    $request.calico_statue_days_played = [int]$projection.days_played
    $request.calico_statue_unique_game_id_half = [string]$projection.unique_game_id_half
    $request.calico_statue_use_legacy_random = [bool]$projection.use_legacy_random
    $request.calico_statue_mine_level = [int]$projection.mine_level
    $request.calico_statue_festival_day = [int]$projection.desert_festival_day
    $request.calico_statue_tile_index_before = [int]$projection.target_tile_index_before
    $request.calico_statue_tile_index_after = [int]$projection.target_tile_index_after
    $request.calico_statue_eggs_before = [int]$projection.calico_eggs_before
    $request.calico_statue_health_before = [int]$projection.health_before
    $request.calico_statue_max_health = [int]$projection.max_health
    $request.calico_statue_stamina_before = [double]$projection.stamina_before
    $request.calico_statue_max_stamina = [double]$projection.max_stamina
    $request.interaction_kind = [string]$projection.interaction_kind
    $request.expected_action_type = [string]$projection.expected_action_type
    $request.native_contract = [string]$projection.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-calico-statue\" + $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach or start."
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
    $initial = Close-InitialMenus (Wait-Snapshot $StartupTimeoutSeconds)
    $results = @()
    foreach ($effectId in 0..17) {
        $fixtureRequest = New-Request $initial "debug.setup_calico_statue" "$RunId.fixture.$effectId"
        $fixtureRequest.calico_statue_fixture_effect_id = $effectId
        $fixture = Invoke-JsonPost $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Calico Statue fixture $effectId failed: $($fixture | ConvertTo-Json -Depth 32 -Compress)"
        }
        $ready = Wait-ReadyEffect $effectId 60
        $result = Invoke-JsonPost $executeUrl (New-ActivationRequest $ready $effectId)
        $after = Wait-Snapshot 30
        $projectionAfter = $after.state.mining.calico_statue.value
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [string]$projectionAfter.gate_status -eq "complete_current_floor_statue_already_activated"
        $results += [ordered]@{
            effect_id = $effectId
            effect_key = [string]$ready.state.mining.calico_statue.value.projected_effect.effect_key
            status = [string]$result.status
            verification = [string]$result.primitive_verification_status
            passed = $passed
            observed_effect = [string]$result.observed_effect
            block_reasons = @($result.block_reasons)
        }
        Write-Json (Join-Path $runDirectory ("effect-$effectId-result.json")) $result
        $initial = $after
    }

    $passedCount = @($results | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_calico_statue_smoke.v1"
        evidence_id = "EVD-309"
        run_id = $RunId
        status = if ($passedCount -eq 18) { "passed" } else { "failed" }
        expected_case_count = 18
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $results
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne 18) { throw "Runtime Calico Statue smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
