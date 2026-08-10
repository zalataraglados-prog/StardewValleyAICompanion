[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-junimo-kart-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-junimo-kart",
    [int] $StartupTimeoutSeconds = 150,
    [int] $ExecutionTimeoutSeconds = 3300,
    [ValidateRange(1, 100)]
    [int] $MaxAttempts = 8,
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
        queue_id = "runtime-junimo-kart"
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
    STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS = $env:STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS
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
    $env:STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS = $ExecutionTimeoutSeconds.ToString()
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $windowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle $windowStyle -PassThru
    $initial = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds

    $fixtureRequest = New-Request $initial "debug.setup_junimo_kart_quest" "$RunId.fixture"
    $fixture = Invoke-JsonPost $executeUrl $fixtureRequest
    Write-Json (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Junimo Kart fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $ready = Wait-Snapshot $snapshotUrl 60
    Write-Json (Join-Path $runDirectory "ready-snapshot.json") $ready
    $arcade = @($ready.state.current_location.arcade_action_tiles.value | Where-Object {
        $_.action_type -eq "Arcade_Minecart" -and $_.unlocked
    }) | Select-Object -First 1
    if ($null -eq $arcade) { throw "Transparent Saloon snapshot has no unlocked Arcade_Minecart tile." }

    $interactRequest = New-Request $ready "executor.interact" "$RunId.interact"
    $interactRequest.target_tile_x = [int]$arcade.tile_x
    $interactRequest.target_tile_y = [int]$arcade.tile_y
    $interactRequest.interaction_kind = "map_action"
    $interactRequest.expected_action_type = "Arcade_Minecart"
    $interact = Invoke-JsonPost $executeUrl $interactRequest
    Write-Json (Join-Path $runDirectory "interact-result.json") $interact
    if ($interact.status -ne "applied" -or $interact.primitive_verification_status -ne "verified") {
        throw "Arcade interaction failed: $(@($interact.block_reasons) -join ',')"
    }

    $dialogueSnapshot = Wait-Snapshot $snapshotUrl 30
    $dialogueRequest = New-Request $dialogueSnapshot "executor.choose_dialogue_response" "$RunId.endless"
    $dialogueRequest.expected_dialogue_key = "MinecartGame"
    $dialogueRequest.dialogue_response_key = "Endless"
    $dialogueRequest.minigame_id = "MineCart"
    $dialogueRequest.minigame_mode = 2
    $dialogue = Invoke-JsonPost $executeUrl $dialogueRequest
    Write-Json (Join-Path $runDirectory "dialogue-result.json") $dialogue
    if ($dialogue.status -ne "applied" -or $dialogue.primitive_verification_status -ne "verified") {
        throw "Endless dialogue failed: $(@($dialogue.block_reasons) -join ',')"
    }

    $minigameSnapshot = Wait-Snapshot $snapshotUrl 30
    $playRequest = New-Request $minigameSnapshot "executor.play_junimo_kart" "$RunId.play"
    $playRequest.quest_family = "special_order"
    $playRequest.quest_key = "QiChallenge3"
    $playRequest.quest_runtime_type = "SpecialOrder"
    $playRequest.quest_objective_index = 0
    $playRequest.quest_expected_current_count = 0
    $playRequest.quest_expected_target_count = 50000
    $playRequest.minigame_id = "MineCart"
    $playRequest.minigame_mode = 2
    $playRequest.minigame_target_score = 50000
    $playRequest.minigame_max_attempts = $MaxAttempts
    $play = Invoke-JsonPost $executeUrl $playRequest $ExecutionTimeoutSeconds
    Write-Json (Join-Path $runDirectory "play-result.json") $play
    $after = Wait-Snapshot $snapshotUrl 60
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after
    $objective = @($after.state.quests.special_orders.value | Where-Object { $_.quest_key -eq "QiChallenge3" }).objectives |
        Where-Object { $_.runtime_type -eq "JKScoreObjective" } |
        Select-Object -First 1
    $observedProgress = if ($null -ne $objective) {
        [int]$objective.current_count
    } elseif ($null -ne $play.quest_progress_after) {
        [int]$play.quest_progress_after
    } else {
        $null
    }
    $passed = $play.status -eq "applied" -and
        $play.primitive_verification_status -eq "verified" -and
        $null -ne $observedProgress -and
        $observedProgress -ge 50000
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_junimo_kart_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        fixture_status = $fixture.status
        interact_status = $interact.status
        dialogue_status = $dialogue.status
        play_status = $play.status
        play_verification = $play.primitive_verification_status
        objective_after = $observedProgress
        target = 50000
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
