[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-pot-of-gold-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-pot-of-gold",
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

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
        queue_id = "runtime-pot-of-gold"
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
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild | Out-Null

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
    $initial = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $initial

    for ($menuPass = 0; $menuPass -lt 8 -and $initial.state.menus.active_menu.value.is_open; $menuPass++) {
        $close = Invoke-JsonPost $executeUrl (New-Request $initial "executor.close_menu" "$RunId.initial-close.$menuPass")
        if ($close.status -ne "applied") { throw "Initial menu close failed: $(@($close.block_reasons) -join ',')" }
        Start-Sleep -Seconds 1
        $initial = Wait-Snapshot $snapshotUrl 30
    }
    if ($initial.state.menus.active_menu.value.is_open) { throw "Initial menu did not close." }

    $fixtureRequest = New-Request $initial "debug.setup_pot_of_gold" "$RunId.fixture"
    $fixtureRequest.debug_fill_inventory = $true
    $fixture = Invoke-JsonPost $executeUrl $fixtureRequest
    Write-Json (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied") { throw "Pot of Gold fixture failed: $(@($fixture.block_reasons) -join ',')" }

    Start-Sleep -Seconds 1
    $ready = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "ready-snapshot.json") $ready
    $reward = $ready.state.current_location.pot_of_gold_reward.value
    if ($reward.status -ne "ready" -or -not $reward.exact_object_present) {
        throw "Transparent Pot of Gold projection is not ready: $($reward.status)"
    }
    $stand = @($reward.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    if ($null -eq $stand) { throw "No transparent Pot of Gold stand tile." }

    $claimRequest = New-Request $ready "rewards.claim_pot_of_gold" "$RunId.claim"
    $claimRequest.location_id = "Forest"
    $claimRequest.target_tile_x = [int]$reward.target_tile_x
    $claimRequest.target_tile_y = [int]$reward.target_tile_y
    $claimRequest.stand_tile_x = [int]$stand.tile_x
    $claimRequest.stand_tile_y = [int]$stand.tile_y
    $claimRequest.target_runtime_type = [string]$reward.target_runtime_type
    $claimRequest.qualified_item_id = [string]$reward.qualified_item_id
    $claimRequest.quantity = [int]$reward.expected_coin_quantity
    $claimRequest.expected_output_items_json = [string]$reward.expected_output_items_json
    $claimRequest.reward_branch = [string]$reward.reward_branch
    $claimRequest.interaction_kind = [string]$reward.interaction_kind
    $claimRequest.expected_action_type = [string]$reward.expected_action_type
    $claimRequest.native_contract = [string]$reward.native_contract
    $claimRequest.max_movement_tiles = 512
    $result = Invoke-JsonPost $executeUrl $claimRequest
    Write-Json (Join-Path $runDirectory "claim-result.json") $result

    $after = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after
    $afterReward = $after.state.current_location.pot_of_gold_reward.value
    $coinDebris = @($after.state.current_location.debris.value | Where-Object { $_.qualified_item_id -eq "(O)GoldCoin" }).Count
    $hatDebris = @($after.state.current_location.debris.value | Where-Object { $_.qualified_item_id -eq "(H)LeprechuanHat" }).Count
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        -not $afterReward.exact_object_present -and
        $coinDebris -eq [int]$reward.expected_coin_quantity -and
        $hatDebris -eq 1
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_pot_of_gold_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        fixture_status = $fixture.status
        claim_status = $result.status
        claim_verification = $result.primitive_verification_status
        expected_coin_quantity = [int]$reward.expected_coin_quantity
        coin_debris_after = $coinDebris
        hat_debris_after = $hatDebris
        pot_present_after = [bool]$afterReward.exact_object_present
        inventory_was_full = $true
        loaded_mod_allowlist = $loadedModAllowlist
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 5
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
