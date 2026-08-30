[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-adventure-guild-reward-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "save=$($snapshot.save_id.status)"
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for Adventure Guild reward snapshot. Last status: $lastStatus"
}
function Wait-RewardReady([int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.quests.adventure_guild_reward
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and
            $field.value.status -eq "ready" -and
            [int]$field.value.pending_goal_count -eq 1 -and
            [bool]$field.value.inventory_capacity_sufficient) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Adventure Guild reward fixture did not become ready."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-adventure-guild-reward"
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
function Invoke-Setup($Snapshot, [string] $CaseName) {
    $request = New-Request $Snapshot "debug.setup_adventure_guild_reward" "$RunId.setup.$CaseName"
    $request.adventure_guild_reward_fixture_case = "single_item"
    $result = Invoke-JsonPost $executeUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Adventure Guild reward fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    [pscustomobject]@{ Setup = $result; Snapshot = (Wait-RewardReady 30) }
}
function New-RewardRequest($Snapshot, [string] $CaseName) {
    $projection = $Snapshot.state.quests.adventure_guild_reward.value
    $request = New-Request $Snapshot "executor.claim_adventure_guild_reward" "$RunId.execute.$CaseName"
    $request.location_id = [string]$projection.location_id
    $request.target_location = [string]$projection.location_id
    $request.target_tile_x = [int]$projection.action_tile_x
    $request.target_tile_y = [int]$projection.action_tile_y
    $request.stand_tile_x = [int]$projection.stand_tile_x
    $request.stand_tile_y = [int]$projection.stand_tile_y
    $request.max_movement_tiles = 512
    $request.adventure_guild_reward_batch_fingerprint = [string]$projection.batch_fingerprint
    $request.adventure_guild_reward_goals_json = ConvertTo-Json -InputObject @($projection.goals) -Depth 32 -Compress
    $request.adventure_guild_reward_pending_goal_count = [int]$projection.pending_goal_count
    $request.adventure_guild_reward_item_count = [int]$projection.reward_item_count
    $request.adventure_guild_reward_dialogue_count = [int]$projection.reward_dialogue_count
    $request.adventure_guild_reward_inventory_max_items = [int]$projection.inventory_max_items
    $request.adventure_guild_reward_inventory_occupied_slots = [int]$projection.inventory_occupied_slots
    $request.adventure_guild_reward_inventory_capacity_sufficient = [bool]$projection.inventory_capacity_sufficient
    $request.adventure_guild_reward_action_tile_index = [int]$projection.action_tile_index
    $request.native_contract = [string]$projection.native_contract
    $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
if (-not (Test-Path -LiteralPath (Join-Path $savesPath $SaveSlot) -PathType Container)) { throw "Isolated save not found: $SaveSlot" }
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach or start."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-adventure-guild-reward\" + $RunId)
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
    "STARDEWAI_DEDICATED_HOST_MODE", "STARDEWAI_DEDICATED_HOST_RUN_ID", "STARDEWAI_DEDICATED_HOST_ACTOR_ID",
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
    $env:STARDEWAI_DEDICATED_HOST_MODE = "1"
    $env:STARDEWAI_DEDICATED_HOST_RUN_ID = $RunId
    $env:STARDEWAI_DEDICATED_HOST_ACTOR_ID = "ai_host.main"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $snapshot = Wait-Snapshot $StartupTimeoutSeconds
    $cases = @()

    $forged = Invoke-Setup $snapshot "forged-fingerprint"
    $forgedRequest = New-RewardRequest $forged.Snapshot "forged-fingerprint"
    $forgedRequest.adventure_guild_reward_batch_fingerprint = ("f" * 64)
    $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
    $forgedPassed = $forgedResult.status -eq "blocked" -and
        @($forgedResult.block_reasons) -contains "adventure_guild_reward_batch_projection_drifted"
    $cases += [ordered]@{ case = "forged_batch_fingerprint_rejected"; passed = $forgedPassed; result = $forgedResult }

    $capacity = Invoke-Setup (Wait-Snapshot 30) "capacity-drift"
    $capacityRequest = New-RewardRequest $capacity.Snapshot "capacity-drift"
    $capacityRequest.adventure_guild_reward_inventory_occupied_slots = [int]$capacityRequest.adventure_guild_reward_inventory_occupied_slots + 1
    $capacityResult = Invoke-JsonPost $executeUrl $capacityRequest
    $capacityPassed = $capacityResult.status -eq "blocked" -and
        @($capacityResult.block_reasons) -contains "adventure_guild_reward_inventory_capacity_drifted"
    $cases += [ordered]@{ case = "inventory_capacity_state_drift_rejected"; passed = $capacityPassed; result = $capacityResult }

    $native = Invoke-Setup (Wait-Snapshot 30) "native-claim"
    $nativeRequest = New-RewardRequest $native.Snapshot "native-claim"
    $nativeResult = Invoke-JsonPost $executeUrl $nativeRequest
    $nativeAfter = Wait-Snapshot 30
    $nativeProjection = $nativeAfter.state.quests.adventure_guild_reward.value
    $nativePassed = $nativeResult.status -eq "applied" -and
        $nativeResult.primitive_verification_status -eq "verified" -and
        [int]$nativeResult.adventure_guild_reward_claimed_goal_count -eq 1 -and
        [int]$nativeResult.adventure_guild_reward_collected_item_count -eq 1 -and
        [int]$nativeProjection.pending_goal_count -eq 0
    $cases += [ordered]@{ case = "native_complete_batch_claim"; passed = $nativePassed; result = $nativeResult }

    Write-Json (Join-Path $runDirectory "full-snapshot.json") $nativeAfter
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_adventure_guild_reward_smoke.v1"
        evidence_id = "EVD-317"
        run_id = $RunId
        status = if ($passedCount -eq 3) { "passed" } else { "failed" }
        expected_case_count = 3
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/3"; artifact = $runDirectory } |
        ConvertTo-Json -Depth 4
    if ($passedCount -ne 3) { throw "Runtime Adventure Guild reward smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
