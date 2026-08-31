[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-mastery-claim-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
    throw "Timed out waiting for mastery snapshot. Last status: $lastStatus"
}
function Wait-MasteryReady([int] $SkillId, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.player.mastery_claim
        $option = @($field.value.claimable_options) | Where-Object { [int]$_.skill_id -eq $SkillId } | Select-Object -First 1
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and
            $field.value.service_status -eq "ready" -and
            [int]$field.value.unspent_mastery_levels -eq 1 -and
            $null -ne $option) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Mastery fixture did not become ready for skill=$SkillId."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-mastery-claim"
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
function Invoke-Setup($Snapshot, [string] $FixtureCase, [int] $SkillId) {
    $request = New-Request $Snapshot "debug.setup_mastery_claim" "$RunId.setup.$FixtureCase"
    $request.mastery_fixture_case = $FixtureCase
    $result = Invoke-JsonPost $executeUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Mastery fixture setup failed for $FixtureCase`: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    [pscustomobject]@{ Setup = $result; Snapshot = (Wait-MasteryReady $SkillId 30) }
}
function New-MasteryRequest($Snapshot, [int] $SkillId, [string] $CaseName) {
    $projection = $Snapshot.state.player.mastery_claim.value
    $option = @($projection.claimable_options) | Where-Object { [int]$_.skill_id -eq $SkillId } | Select-Object -First 1
    if ($null -eq $option -or $null -eq $option.action_tile) {
        throw "Typed mastery option or endpoint missing for $CaseName."
    }
    $target = $option.action_tile
    $stats = @($projection.skills | Sort-Object { [int]$_.skill_id } | ForEach-Object { [int]$_.mastery_stat_value })
    $request = New-Request $Snapshot "executor.claim_mastery" "$RunId.execute.$CaseName"
    $request.location_id = "MasteryCave"
    $request.target_location = "MasteryCave"
    $request.target_tile_x = [int]$target.tile_x
    $request.target_tile_y = [int]$target.tile_y
    $request.stand_tile_x = [int]$Snapshot.state.player.tile_x.value
    $request.stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.max_movement_tiles = 512
    $request.mastery_skill_id = $SkillId
    $request.mastery_skill_key = [string]$option.skill_key
    $request.mastery_projection_fingerprint = [string]$projection.projection_fingerprint
    $request.mastery_option_fingerprint = [string]$option.option_fingerprint
    $request.mastery_experience_before = [int]$projection.mastery_experience
    $request.mastery_level_before = [int]$projection.current_mastery_level
    $request.mastery_levels_spent_before = [int]$projection.mastery_levels_spent
    $request.mastery_skill_stat_before = [int]$option.mastery_stat_value
    $request.mastery_all_skill_stats_before_csv = ($stats -join ",")
    $request.mastery_recipe_rewards_json = ConvertTo-Json -InputObject @($option.recipe_rewards) -Depth 32 -Compress
    $request.mastery_direct_rewards_json = ConvertTo-Json -InputObject @($option.direct_rewards) -Depth 32 -Compress
    $request.mastery_grants_trinket_slot = [bool]$option.grants_trinket_slot
    $request.mastery_trinket_slots_before = [int]$projection.trinket_slots
    $request.mastery_action_raw = [string]$target.action_raw
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

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-mastery-claim\" + $RunId)
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

    $forged = Invoke-Setup $snapshot "farming_inventory" 0
    $forgedRequest = New-MasteryRequest $forged.Snapshot 0 "forged-fingerprint"
    $forgedRequest.mastery_projection_fingerprint = ("f" * 64)
    $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
    $forgedPassed = $forgedResult.status -eq "blocked" -and @($forgedResult.block_reasons) -contains "mastery_claim_projection_fingerprint_drifted"
    $cases += [ordered]@{ case = "forged_projection_fingerprint_rejected"; passed = $forgedPassed; result = $forgedResult }

    $claimCases = @(
        [pscustomobject]@{ Fixture = "farming_inventory"; Name = "native_farming_inventory_and_recipe"; Skill = 0; Spent = 1; Direct = 1; Trinket = 0; Complete = $false },
        [pscustomobject]@{ Fixture = "fishing_full_inventory"; Name = "native_fishing_full_inventory_debris_and_recipe"; Skill = 1; Spent = 1; Direct = 1; Trinket = 0; Complete = $false },
        [pscustomobject]@{ Fixture = "foraging_recipes"; Name = "native_foraging_two_recipes"; Skill = 2; Spent = 1; Direct = 0; Trinket = 0; Complete = $false },
        [pscustomobject]@{ Fixture = "mining_recipes"; Name = "native_mining_two_recipes"; Skill = 3; Spent = 1; Direct = 0; Trinket = 0; Complete = $false },
        [pscustomobject]@{ Fixture = "combat_final"; Name = "native_combat_trinket_and_final_fifth_plaque"; Skill = 4; Spent = 5; Direct = 0; Trinket = 1; Complete = $true }
    )
    foreach ($case in $claimCases) {
        $setup = Invoke-Setup (Wait-Snapshot 30) $case.Fixture $case.Skill
        $request = New-MasteryRequest $setup.Snapshot $case.Skill $case.Name
        $result = Invoke-JsonPost $executeUrl $request
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [int]$result.mastery_skill_stat_after -eq 1 -and [int]$result.mastery_levels_spent_after -eq $case.Spent -and
            [int]$result.mastery_direct_reward_total_delta -eq $case.Direct -and [int]$result.mastery_trinket_slots_after -eq $case.Trinket -and
            [bool]$result.mastery_all_plaques_completed_after -eq $case.Complete
        $cases += [ordered]@{ case = $case.Name; passed = $passed; result = $result }
    }

    $finalSnapshot = Wait-Snapshot 30
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_mastery_claim_smoke.v1"
        evidence_id = "EVD-319"
        run_id = $RunId
        status = if ($passedCount -eq 6) { "passed" } else { "failed" }
        expected_case_count = 6
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/6"; artifact = $runDirectory } |
        ConvertTo-Json -Depth 4
    if ($passedCount -ne 6) { throw "Runtime mastery claim smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
