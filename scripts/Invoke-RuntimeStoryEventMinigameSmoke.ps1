param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-story-event-minigame-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 240) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
            if ($null -ne $result) { return $result }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-StorySnapshot([int] $TimeoutSeconds, [scriptblock] $Predicate) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $field = $snapshot.state.player.story_event
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $field.status -in @("available", "derived") -and
                [string]$field.value.projection_status -eq "complete_locked_base_1.6.15" -and
                (& $Predicate $snapshot $field.value)) {
                return $snapshot
            }
            $lastState = "event=" + [string]$field.value.event_id +
                ";minigame=" + [string]$field.value.active_minigame_type +
                ";support=" + [string]$field.value.active_minigame_support_status +
                ";question=" + [string]$field.value.dialogue_question_key
        }
        catch { $lastState = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for story minigame snapshot. Last state: $lastState"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-story-event-minigame-smoke"
        queue_item_id = $QueueItemId
        before_state_hash = $Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function New-MinigameRequest($Snapshot, [string] $QueueItemId, [int] $ResponseIndex = -1) {
    $story = $Snapshot.state.player.story_event.value
    $request = New-BaseRequest $Snapshot "executor.advance_story_event_minigame" $QueueItemId
    $request.story_event_projection_fingerprint = [string]$story.projection_fingerprint
    $request.story_event_id = [string]$story.event_id
    $request.story_event_location_id = [string]$story.location_id
    $request.story_event_command_index = [int]$story.current_command_index
    $request.story_event_command_raw = [string]$story.current_command_raw
    $request.story_event_boundary_kind = [string]$story.boundary_kind
    $request.story_event_minigame_native_contract = [string]$story.active_minigame_native_contract
    $request.story_event_minigame_type = [string]$story.active_minigame_type
    $request.story_event_minigame_id = [string]$story.active_minigame_id
    $request.story_event_minigame_owner_kind = [string]$story.active_minigame_owner_kind
    $request.story_event_minigame_execution_mode = [string]$story.active_minigame_execution_mode
    if ($ResponseIndex -ge 0) {
        $response = @($story.dialogue_responses | Where-Object { [int]$_.index -eq $ResponseIndex }) | Select-Object -First 1
        if ($null -eq $response) { throw "Story minigame response index $ResponseIndex is not projected." }
        $request.story_event_question_key = [string]$story.dialogue_question_key
        $request.story_event_response_index = [int]$response.index
        $request.story_event_response_key = [string]$response.response_key
    }
    return $request
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach or start."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-story-event-minigame-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS"
)
$savedEnvironment = @{}
foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 60 | Out-Null
    $idle = Wait-StorySnapshot 180 { param($snapshot, $story) -not [bool]$story.active -and [string]$story.active_minigame_type -eq "" }

    $setupPassive = New-BaseRequest $idle "debug.setup_story_event" "setup-passive"
    $setupPassive.story_event_boundary_kind = "passive_minigame_fixture"
    $setupPassiveResult = Invoke-JsonPost $executorUrl $setupPassive
    if ($setupPassiveResult.status -ne "applied") { throw "Passive fixture setup failed." }
    $passiveBefore = Wait-StorySnapshot 30 {
        param($snapshot, $story)
        [string]$story.event_id -eq "EVD323Passive" -and
            [string]$story.active_minigame_type -eq "StardewValley.Minigames.FantasyBoardGame" -and
            [bool]$story.active_minigame_supported
    }
    $passiveBefore | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "passive-before.json") -Encoding utf8
    $passiveResult = Invoke-JsonPost $executorUrl (New-MinigameRequest $passiveBefore "passive") 180
    $passiveResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "passive-result.json") -Encoding utf8
    $idleAfterPassive = Wait-StorySnapshot 30 { param($snapshot, $story) -not [bool]$story.active -and [string]$story.active_minigame_type -eq "" }

    $setupChoice = New-BaseRequest $idleAfterPassive "debug.setup_story_event" "setup-choice"
    $setupChoice.story_event_boundary_kind = "choice_minigame_fixture"
    $setupChoiceResult = Invoke-JsonPost $executorUrl $setupChoice
    if ($setupChoiceResult.status -ne "applied") { throw "Choice fixture setup failed." }
    $choiceBefore = Wait-StorySnapshot 30 {
        param($snapshot, $story)
        [string]$story.event_id -eq "EVD323Choice" -and
            [string]$story.active_minigame_type -eq "StardewValley.Minigames.FantasyBoardGame" -and
            [string]$story.dialogue_question_key -eq "EVD323Question" -and @($story.dialogue_responses).Count -eq 2
    }
    $choiceBefore | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "choice-before.json") -Encoding utf8
    $choiceResult = Invoke-JsonPost $executorUrl (New-MinigameRequest $choiceBefore "choice" 1) 180
    $choiceResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "choice-result.json") -Encoding utf8
    $choiceAfter = Wait-StorySnapshot 30 { param($snapshot, $story) -not [bool]$story.active -and [string]$story.active_minigame_type -eq "" }

    $cases = @(
        [ordered]@{ name = "fantasy_board_passive_message"; result = $passiveResult },
        [ordered]@{ name = "fantasy_board_second_response"; result = $choiceResult }
    )
    $passedCases = @($cases | Where-Object {
        $_.result.status -eq "applied" -and $_.result.primitive_verification_status -eq "verified" -and
        [string]$_.result.observed_effect -match "boundary=minigame_completed"
    }).Count
    $passed = $passedCases -eq 2 -and -not [bool]$choiceAfter.state.player.story_event.value.active
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-323"
        run_id = $RunId
        save_slot = $SaveSlot
        expected_case_count = 2
        passed_case_count = $passedCases
        cases = @($cases | ForEach-Object {
            [ordered]@{
                name = $_.name
                status = $_.result.status
                verification = $_.result.primitive_verification_status
                observed_effect = $_.result.observed_effect
                block_reasons = @($_.result.block_reasons)
            }
        })
    }
    $summary | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 64
    if (-not $passed) { throw "Runtime story-event minigame smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
