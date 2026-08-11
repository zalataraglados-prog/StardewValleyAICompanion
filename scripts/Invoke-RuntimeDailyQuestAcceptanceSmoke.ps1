[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-daily-quest-acceptance-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-daily-quest-acceptance",
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
        -Body ($Body | ConvertTo-Json -Depth 32) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and
                $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $last = "save_status=$($snapshot.save_id.status)"
        } catch {
            $last = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for snapshot: $last"
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-daily-quest-acceptance"
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

$previousEnvironment = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    SMAPI_MODS_PATH = $env:SMAPI_MODS_PATH
}
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

    $fixture = Invoke-JsonPost $executeUrl (New-Request $initial "debug.setup_daily_quest_acceptance" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Daily quest fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $ready = Wait-Snapshot $snapshotUrl 60
    Write-Json (Join-Path $runDirectory "ready-snapshot.json") $ready
    $offer = $ready.state.quests.daily_quest_offer.value
    if (-not $offer.can_accept -or $offer.status -ne "ready") {
        throw "Transparent daily quest offer is not ready: $(@($offer.blocked_diagnostics) -join ',')"
    }

    $interactRequest = New-Request $ready "executor.interact" "$RunId.interact"
    $interactRequest.target_tile_x = [int]$offer.board_action_tile_x
    $interactRequest.target_tile_y = [int]$offer.board_action_tile_y
    $interactRequest.interaction_kind = "map_action"
    $interactRequest.expected_action_type = "Billboard"
    $interact = Invoke-JsonPost $executeUrl $interactRequest
    Write-Json (Join-Path $runDirectory "interact-result.json") $interact
    if ($interact.status -ne "applied" -or $interact.primitive_verification_status -ne "verified") {
        throw "Daily quest board interaction failed: $(@($interact.block_reasons) -join ',')"
    }

    $menuSnapshot = Wait-Snapshot $snapshotUrl 30
    $acceptRequest = New-Request $menuSnapshot "executor.accept_daily_quest" "$RunId.accept"
    $acceptRequest.quest_candidate_id = "daily_quest_offer:$($offer.offer_fingerprint)"
    $acceptRequest.quest_family = "ordinary_quest"
    $acceptRequest.quest_id = [string]$offer.quest.id
    $acceptRequest.quest_runtime_type = [string]$offer.quest.runtime_type
    $acceptRequest.quest_interaction_kind = "accept_daily"
    $acceptRequest.quest_offer_fingerprint = [string]$offer.offer_fingerprint
    $acceptRequest.quest_offer_title = [string]$offer.quest.title
    $acceptRequest.quest_offer_current_objective = [string]$offer.quest.current_objective
    $accept = Invoke-JsonPost $executeUrl $acceptRequest
    Write-Json (Join-Path $runDirectory "accept-result.json") $accept

    $after = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after
    $acceptedQuest = @($after.state.quests.active_quests.value | Where-Object {
        $_.id -eq $offer.quest.id -and $_.runtime_type -eq $offer.quest.runtime_type
    }) | Select-Object -First 1
    $passed = $accept.status -eq "applied" -and
        $accept.primitive_verification_status -eq "verified" -and
        $null -ne $acceptedQuest -and
        $acceptedQuest.accepted -and
        $acceptedQuest.daily_quest -and
        $acceptedQuest.days_left -eq 2
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_daily_quest_acceptance_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        fixture_status = $fixture.status
        interact_status = $interact.status
        accept_status = $accept.status
        accept_verification = $accept.primitive_verification_status
        quest_present_after = $null -ne $acceptedQuest
        quest_days_left_after = if ($null -ne $acceptedQuest) { $acceptedQuest.days_left } else { $null }
        loaded_mod_allowlist = $loadedModAllowlist
        smoke_mods_path = $smokeModsPath
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 5
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        Set-Item -Path ("Env:" + $name) -Value $previousEnvironment[$name]
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
