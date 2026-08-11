[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-quest-reward-claim-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-quest-reward-claim",
    [int] $StartupTimeoutSeconds = 150,
    [switch] $VisibleGame,
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
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot."
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-quest-reward-claim"
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
    $windowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle $windowStyle -PassThru
    $initial = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $initial

    for ($menuPass = 0; $menuPass -lt 8 -and $initial.state.menus.active_menu.value.is_open; $menuPass++) {
        $close = Invoke-JsonPost $executeUrl (New-Request $initial "executor.close_menu" "$RunId.initial-close.$menuPass")
        Write-Json (Join-Path $runDirectory "initial-close-result-$menuPass.json") $close
        if ($close.status -ne "applied") { throw "Initial menu close failed: $(@($close.block_reasons) -join ',')" }
        Start-Sleep -Seconds 1
        $initial = Wait-Snapshot $snapshotUrl 30
    }
    if ($initial.state.menus.active_menu.value.is_open) { throw "Initial menu did not close." }

    $fixture = Invoke-JsonPost $executeUrl (New-Request $initial "debug.setup_quest_reward" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied") { throw "Quest reward fixture failed: $(@($fixture.block_reasons) -join ',')" }

    $ready = $null
    $stableClearPasses = 0
    for ($menuPass = 0; $menuPass -lt 4 -and $stableClearPasses -lt 2; $menuPass++) {
        Start-Sleep -Seconds 1
        $ready = Wait-Snapshot $snapshotUrl 30
        if (-not $ready.state.menus.active_menu.value.is_open) {
            $stableClearPasses++
            continue
        }
        $stableClearPasses = 0
        $close = Invoke-JsonPost $executeUrl (New-Request $ready "executor.close_menu" "$RunId.close.$menuPass")
        Write-Json (Join-Path $runDirectory "close-result-$menuPass.json") $close
        if ($close.status -ne "applied") { throw "Fixture follow-up menu close failed: $(@($close.block_reasons) -join ',')" }
    }
    $ready = Wait-Snapshot $snapshotUrl 30
    if ($ready.state.menus.active_menu.value.is_open) { throw "Fixture menu did not remain clear." }
    Write-Json (Join-Path $runDirectory "ready-snapshot.json") $ready
    $claim = @($ready.state.quests.claimable_rewards.value | Where-Object { $_.quest.id -eq "stardewai.runtime.reward" }) | Select-Object -First 1
    if ($null -eq $claim -or -not $claim.claimable) { throw "Transparent claimable reward fixture is missing." }
    $moneyBefore = [int]$ready.state.player.money.value

    $claimRequest = New-Request $ready "executor.claim_quest_reward" "$RunId.claim"
    $claimRequest.quest_candidate_id = "quest_reward:$($claim.reward_fingerprint)"
    $claimRequest.quest_family = "ordinary"
    $claimRequest.quest_id = [string]$claim.quest.id
    $claimRequest.quest_runtime_type = [string]$claim.quest.runtime_type
    $claimRequest.quest_reward_fingerprint = [string]$claim.reward_fingerprint
    $claimRequest.quest_money_reward_expected = [int]$claim.quest.money_reward
    $claimRequest.quest_expected_money_before = $moneyBefore
    $result = Invoke-JsonPost $executeUrl $claimRequest
    Write-Json (Join-Path $runDirectory "claim-result.json") $result

    $after = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after
    $moneyAfter = [int]$after.state.player.money.value
    $stillClaimable = @($after.state.quests.claimable_rewards.value | Where-Object { $_.reward_fingerprint -eq $claim.reward_fingerprint }).Count -gt 0
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        $moneyAfter -eq ($moneyBefore + [int]$claim.quest.money_reward) -and
        -not $stillClaimable
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_quest_reward_claim_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        fixture_status = $fixture.status
        claim_status = $result.status
        claim_verification = $result.primitive_verification_status
        money_before = $moneyBefore
        reward = [int]$claim.quest.money_reward
        money_after = $moneyAfter
        still_claimable_after = $stillClaimable
        loaded_mod_allowlist = $loadedModAllowlist
        smoke_mods_path = $smokeModsPath
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
