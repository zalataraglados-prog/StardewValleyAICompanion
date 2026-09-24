param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-community-center-lifecycle-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [int] $BackendPort = 8795,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 240)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Json {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            if ($null -ne $value) {
                return $value
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-LifecycleSnapshot {
    param(
        [scriptblock] $Predicate,
        [string] $Description,
        [int] $TimeoutSeconds = 120
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 15
            $progress = $snapshot.state.world_progress.community_center
            $story = $snapshot.state.player.story_event
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $progress.status -in @("available", "derived") -and
                $story.status -in @("available", "derived") -and
                (& $Predicate $snapshot $progress.value $story.value)) {
                return $snapshot
            }
            $lastState =
                "location=$($snapshot.state.player.location_id.value);" +
                "stage=$($progress.value.lifecycle.stage);" +
                "event=$($story.value.event_id);" +
                "event_active=$($story.value.active)"
        }
        catch {
            $lastState = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Description. Last state: $lastState"
}

function New-BaseRequest {
    param($Snapshot, [string] $OptionId, [string] $QueueItemId)
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "community-center-lifecycle-fixture"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $isolatedSavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-StoryFixture {
    param($Snapshot, [string] $Profile, [string] $ExpectedEventId)
    $request = New-BaseRequest `
        -Snapshot $Snapshot `
        -OptionId "debug.setup_story_event" `
        -QueueItemId ("fixture-" + $Profile)
    $request.story_event_boundary_kind = $Profile
    $result = Invoke-JsonPost -Url $nativeExecutorUrl -Body $request
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory ($Profile + "-fixture.json")) `
        -Value $result
    if ($result.status -ne "applied" -or
        $result.primitive_verification_status -ne "verified") {
        throw "Community Center story fixture $Profile failed."
    }
    $minimumCommandIndex = if ($ExpectedEventId -eq "191393") {
        14
    }
    else {
        4
    }
    return Wait-LifecycleSnapshot `
        -Description ("active event " + $ExpectedEventId) `
        -Predicate {
            param($candidate, $progress, $story)
            $eventProjection = if ($ExpectedEventId -eq "611439") {
                $progress.lifecycle.initial_unlock_event
            }
            elseif ($ExpectedEventId -eq "112") {
                $progress.lifecycle.junimo_text_event
            }
            else {
                $progress.lifecycle.final_ceremony_event
            }
            [bool]$story.active -and
                [string]$story.event_id -eq $ExpectedEventId -and
                [bool]$story.dialogue_menu_open -and
                [int]$story.current_command_index -ge $minimumCommandIndex -and
                [bool]$eventProjection.event_active
        }
}

function Invoke-LifecycleLocationFixture {
    param($Snapshot, [string] $CaseName)
    $request = New-BaseRequest `
        -Snapshot $Snapshot `
        -OptionId "debug.setup_community_center_lifecycle" `
        -QueueItemId ("fixture-" + $CaseName)
    $request.community_center_fixture_case = $CaseName
    $result = Invoke-JsonPost -Url $nativeExecutorUrl -Body $request
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory ($CaseName + "-fixture.json")) `
        -Value $result
    if ($result.status -ne "applied" -or
        $result.primitive_verification_status -ne "verified") {
        throw "Community Center lifecycle fixture $CaseName failed."
    }
    return Wait-LifecycleSnapshot `
        -Description ("lifecycle fixture " + $CaseName) `
        -Predicate {
            param($candidate, $progress, $story)
            -not [bool]$story.active -and
                (($CaseName -eq "first_note_location" -and
                    [string]$candidate.state.player.location_id.value -eq
                        "CommunityCenter" -and
                    [string]$progress.lifecycle.stage -eq
                        "first_junimo_note_pending") -or
                 ($CaseName -eq "sleep_location" -and
                    [string]$candidate.state.player.location_id.value -like
                        "FarmHouse*" -and
                    [string]$progress.lifecycle.stage -eq
                        "area_mail_settlement_pending"))
        }
}

function Invoke-DonationFixture {
    param($Snapshot)
    $request = New-BaseRequest `
        -Snapshot $Snapshot `
        -OptionId "debug.setup_community_center_donation" `
        -QueueItemId "fixture-complete-all-areas"
    $request.community_center_fixture_case = "complete_all_areas"
    $request.inventory_slot_index = 11
    $result = Invoke-JsonPost -Url $nativeExecutorUrl -Body $request
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "complete-all-areas-fixture.json") `
        -Value $result
    if ($result.status -ne "applied" -or
        $result.primitive_verification_status -ne "verified") {
        throw "Community Center final donation fixture failed."
    }
    $bundleReason = @($result.primitive_verification_reasons) |
        Where-Object { $_ -like "bundle=*" } | Select-Object -First 1
    $ingredientReason = @($result.primitive_verification_reasons) |
        Where-Object { $_ -like "ingredient=*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($bundleReason) -or
        [string]::IsNullOrWhiteSpace($ingredientReason)) {
        throw "Final donation fixture did not report its exact target."
    }
    return [ordered]@{
        snapshot = Wait-LifecycleSnapshot `
            -Description "final Community Center donation candidate" `
            -Predicate {
                param($candidate, $progress, $story)
                -not [bool]$story.active -and
                    [string]$candidate.state.player.location_id.value -eq
                        "CommunityCenter" -and
                    [string]$progress.lifecycle.stage -eq
                        "bundle_donation_active"
            }
        bundle_id = [int]($bundleReason.Substring("bundle=".Length))
        ingredient_index = [int](
            $ingredientReason.Substring("ingredient=".Length))
    }
}

function Invoke-LoopStep {
    param(
        [string] $Name,
        $Snapshot,
        [string] $OptionId,
        [string] $CandidateKind,
        [string] $CandidateId,
        [string] $TransitionKind = "",
        [string[]] $CandidateParameters = @()
    )
    $stepRunId = $RunId
    $stepDirectory = Join-Path $artifactDirectory $Name
    $stepLoopRoot = Join-Path $stepDirectory "loop"
    New-Item -ItemType Directory -Force -Path $stepDirectory | Out-Null
    $sourcePath = Join-Path $stepDirectory "source-snapshot.json"
    Write-JsonFile -Path $sourcePath -Value $Snapshot

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @(
        $loopDll,
        "--root", $stepLoopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $executorRoot,
        "--snapshot-file", $sourcePath,
        "--no-manifest",
        "--skip-training",
        "--run-id", $stepRunId,
        "--save-isolation-path", $isolatedSavesPath,
        "--iterations", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", $OptionId,
        "--emit-queue-execution-receipt",
        "--after-snapshot-wait-ms", "1500",
        "--continue-after-blocked-queue-items"
    )) {
        $arguments.Add([string]$value)
    }
    if (-not [string]::IsNullOrWhiteSpace($CandidateKind)) {
        $arguments.Add("--daily-plan-candidate-kind")
        $arguments.Add($CandidateKind)
    }
    if (-not [string]::IsNullOrWhiteSpace($CandidateId)) {
        $arguments.Add("--daily-plan-candidate-id")
        $arguments.Add($CandidateId)
    }
    foreach ($parameter in $CandidateParameters) {
        $arguments.Add("--daily-plan-candidate-parameter")
        $arguments.Add($parameter)
    }

    $loopOutput = & dotnet $arguments
    $loopOutput | Set-Content `
        -LiteralPath (Join-Path $stepDirectory "loop.stdout.log") `
        -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Lifecycle loop step $Name failed with exit $LASTEXITCODE."
    }

    $snapshotDirectory = Join-Path $stepLoopRoot (
        "runs\" + $stepRunId + "\live-snapshots")
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $beforePath = Join-Path $snapshotDirectory "before-snapshot-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $afterPath = Join-Path $snapshotDirectory "after-snapshot-0001.json"
    foreach ($path in @($queuePath, $beforePath, $executionPath, $afterPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Lifecycle loop step $Name did not produce $path."
        }
    }
    $execution = Get-Content -LiteralPath $executionPath -Raw |
        ConvertFrom-Json
    if ($execution.status -ne "applied" -or
        -not [bool]$execution.after_snapshot_fresh) {
        throw "Lifecycle loop step $Name did not produce fresh applied execution."
    }

    $admission = $null
    if (-not [string]::IsNullOrWhiteSpace($TransitionKind)) {
        $admissionPath = Join-Path $stepDirectory "lifecycle-admission.json"
        $bootstrapArguments = @(
            $bootstrapDll,
            "build-community-center-lifecycle-receipt",
            "--queue", $queuePath,
            "--before-snapshot", $beforePath,
            "--execution-receipt", $executionPath,
            "--after-snapshot", $afterPath,
            "--transition-kind", $TransitionKind,
            "--run-id", $stepRunId,
            "--executor-version", "runtime_test_harness_executor.v1",
            "--output", $admissionPath
        )
        & dotnet $bootstrapArguments | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Lifecycle admission $TransitionKind failed for $Name."
        }
        $admission = Get-Content -LiteralPath $admissionPath -Raw |
            ConvertFrom-Json
        if ($admission.status -ne "ready" -or
            -not [bool]$admission.training_label_eligible) {
            throw "Lifecycle admission $TransitionKind was blocked for $Name."
        }
    }

    return [ordered]@{
        name = $Name
        run_id = $stepRunId
        option_id = $OptionId
        transition_kind = $TransitionKind
        execution_status = [string]$execution.status
        step_count = @($execution.step_results).Count
        admission_status = if ($null -eq $admission) {
            "not_applicable"
        }
        else {
            [string]$admission.status
        }
        training_label_eligible = if ($null -eq $admission) {
            $false
        }
        else {
            [bool]$admission.training_label_eligible
        }
        after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$runtimeSavesPath = Join-Path $RuntimeRoot "saves"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$backendUrl = "http://127.0.0.1:$BackendPort"
$executorRoot = "http://127.0.0.1:8767"
$nativeExecutorUrl = $executorRoot + "/api/v1/training/execute"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) {
    throw "SMAPI executable not found: $smapi"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $runtimeSavesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated runtime save exists under $runtimeSavesPath."
    }
    $SaveSlot = $slot.Name
}
$sourceSavePath = Join-Path $runtimeSavesPath $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Runtime save slot not found: $sourceSavePath"
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen `
            -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Community Center lifecycle smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot (
    "artifacts\runtime-community-center-lifecycle\" + $RunId)
$isolatedSavesPath = Join-Path $artifactDirectory "isolated-saves"
$isolatedSavePath = Join-Path $isolatedSavesPath $SaveSlot
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $isolatedSavesPath | Out-Null
Copy-Item -LiteralPath $sourceSavePath -Destination $isolatedSavePath -Recurse
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
dotnet build (Join-Path $ProjectRoot `
    "src\StardewAI.Backend\StardewAI.Backend.csproj") `
    -c Release --no-restore --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Backend build failed." }
dotnet build (Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --no-restore --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop build failed." }
dotnet build (Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj") `
    -c Release --no-restore --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "GoalConditionedBootstrap build failed." }
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

$names = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $names) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$backend = $null
$game = $null
try {
    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process dotnet -ArgumentList @(
        "run",
        "--no-restore",
        "--project",
        (Join-Path $ProjectRoot `
            "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile"
    ) -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "backend.stderr.log") -PassThru
    Wait-Json -Url ($backendUrl + "/health") -TimeoutSeconds 60 | Out-Null

    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $game = Start-Process -FilePath $smapi -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "game.stderr.log") -PassThru
    Wait-Json -Url ($executorRoot + "/health") `
        -TimeoutSeconds 60 | Out-Null
    $loaded = Wait-LifecycleSnapshot `
        -Description "isolated Community Center lifecycle world" `
        -TimeoutSeconds $StartupTimeoutSeconds `
        -Predicate {
            param($candidate, $progress, $story)
            -not [bool]$story.active -and
                [string]$progress.lifecycle.projection_status -eq
                    "complete_locked_base_1.6.15"
        }

    $initialBefore = Invoke-StoryFixture `
        -Snapshot $loaded `
        -Profile "community_center_initial_unlock_event" `
        -ExpectedEventId "611439"
    $initial = Invoke-LoopStep `
        -Name "01-initial-unlock" `
        -Snapshot $initialBefore `
        -OptionId "story.advance_event" `
        -CandidateKind "advance_story_event_automatic" `
        -CandidateId "story-event:611439:advance_story_event_automatic:continue" `
        -TransitionKind "initial_unlock_event"

    $firstNoteBefore = Invoke-LifecycleLocationFixture `
        -Snapshot $initial.after `
        -CaseName "first_note_location"
    $firstNote = Invoke-LoopStep `
        -Name "02-first-junimo-note" `
        -Snapshot $firstNoteBefore `
        -OptionId "community_center.donate_bundle_items" `
        -CandidateKind "read_first_junimo_note" `
        -CandidateId "community-center-read-first-note:area=1" `
        -TransitionKind "first_junimo_note_interaction"

    $junimoTextBefore = Invoke-StoryFixture `
        -Snapshot $firstNote.after `
        -Profile "community_center_junimo_text_event" `
        -ExpectedEventId "112"
    $junimoText = Invoke-LoopStep `
        -Name "03-junimo-text" `
        -Snapshot $junimoTextBefore `
        -OptionId "story.advance_event" `
        -CandidateKind "advance_story_event_automatic" `
        -CandidateId "story-event:112:advance_story_event_automatic:continue" `
        -TransitionKind "junimo_text_unlock_event"

    $donationFixture = Invoke-DonationFixture -Snapshot $junimoText.after
    $donationCandidateId =
        "community-center-donate:$($donationFixture.bundle_id):" +
        "$($donationFixture.ingredient_index):11"
    $donation = Invoke-LoopStep `
        -Name "04-final-donation" `
        -Snapshot $donationFixture.snapshot `
        -OptionId "community_center.donate_bundle_items" `
        -CandidateKind "donate_community_center_item" `
        -CandidateId $donationCandidateId
    if (@($donation.after.state.world_progress.community_center.value.`
            pending_area_mail_flags).Count -eq 0 -or
        -not [bool]$donation.after.state.world_progress.community_center.value.`
            lifecycle.all_areas_complete) {
        throw "Final donation did not leave native room-mail settlement pending."
    }

    $settlementBefore = Invoke-LifecycleLocationFixture `
        -Snapshot $donation.after `
        -CaseName "sleep_location"
    $settlement = Invoke-LoopStep `
        -Name "05-room-mail-settlement" `
        -Snapshot $settlementBefore `
        -OptionId "recovery.stabilize_day" `
        -CandidateKind "recovery_sleep_immediately" `
        -CandidateId "recovery:native_save_boundary" `
        -TransitionKind "room_mail_day_settlement" `
        -CandidateParameters @("control_plane.native_save_boundary=true")

    $postSleepSnapshot = $settlement.after
    $postSleepStory = $null
    $postSleepStoryValue = $postSleepSnapshot.state.player.story_event.value
    if ([bool]$postSleepStoryValue.active) {
        if ([string]$postSleepStoryValue.boundary_kind -ne
            "automatic_progress") {
            throw "Post-sleep story event requires a non-automatic decision."
        }
        $postSleepEventId = [string]$postSleepStoryValue.event_id
        if ([string]::IsNullOrWhiteSpace($postSleepEventId)) {
            throw "Post-sleep story event did not expose an event ID."
        }
        $postSleepStory = Invoke-LoopStep `
            -Name "05a-post-sleep-story-event" `
            -Snapshot $postSleepSnapshot `
            -OptionId "story.advance_event" `
            -CandidateKind "advance_story_event_automatic" `
            -CandidateId (
                "story-event:${postSleepEventId}:" +
                "advance_story_event_automatic:continue")
        $postSleepSnapshot = $postSleepStory.after
    }

    $ceremonyBefore = Invoke-StoryFixture `
        -Snapshot $postSleepSnapshot `
        -Profile "community_center_final_ceremony_event" `
        -ExpectedEventId "191393"
    $ceremonyToDecision = Invoke-LoopStep `
        -Name "06a-final-ceremony-to-decision" `
        -Snapshot $ceremonyBefore `
        -OptionId "story.advance_event" `
        -CandidateKind "advance_story_event_automatic" `
        -CandidateId "story-event:191393:advance_story_event_automatic:continue"
    $ceremonyDecision = $ceremonyToDecision.after.state.player.story_event.value
    if (-not [bool]$ceremonyDecision.active -or
        [string]$ceremonyDecision.event_id -ne "191393" -or
        [string]$ceremonyDecision.boundary_kind -ne "dialogue_decision") {
        throw "Final ceremony did not reach its native dialogue decision."
    }
    $ceremonyResponse = @($ceremonyDecision.dialogue_responses |
        Sort-Object index | Select-Object -First 1)[0]
    if ($null -eq $ceremonyResponse -or
        [int]$ceremonyResponse.index -lt 0 -or
        [string]::IsNullOrWhiteSpace(
            [string]$ceremonyResponse.response_key)) {
        throw "Final ceremony did not expose a typed native response."
    }
    $ceremony = Invoke-LoopStep `
        -Name "06-final-ceremony" `
        -Snapshot $ceremonyToDecision.after `
        -OptionId "story.advance_event" `
        -CandidateKind "advance_story_event_choice" `
        -CandidateId (
            "story-event:191393:advance_story_event_choice:" +
            [string]$ceremonyResponse.index) `
        -TransitionKind "final_ceremony_event"

    $lifecycleSteps = @(
        $initial,
        $firstNote,
        $junimoText,
        $settlement,
        $ceremony
    )
    $summary = [ordered]@{
        schema_version =
            "stardewai.runtime_community_center_lifecycle_smoke.v1"
        status = if (@($lifecycleSteps | Where-Object {
                    -not $_.training_label_eligible
                }).Count -eq 0 -and
                [string]$ceremony.after.state.world_progress.`
                    community_center.value.lifecycle.stage -eq
                    "completion_admitted") {
            "passed"
        }
        else {
            "failed"
        }
        run_id = $RunId
        source_save_slot = $SaveSlot
        isolated_save_root = $isolatedSavesPath
        source_save_preserved = $true
        admitted_lifecycle_transition_count = @($lifecycleSteps |
            Where-Object training_label_eligible).Count
        expected_lifecycle_transition_count = 5
        final_donation = [ordered]@{
            status = $donation.execution_status
            candidate_id = $donationCandidateId
            training_role = "native_prerequisite_not_lifecycle_transition"
        }
        post_sleep_story_event = if ($null -eq $postSleepStory) {
            [ordered]@{
                present = $false
                status = "not_applicable"
            }
        }
        else {
            [ordered]@{
                present = $true
                status = $postSleepStory.execution_status
                training_role =
                    "native_interstitial_not_lifecycle_transition"
            }
        }
        final_ceremony_predecision = [ordered]@{
            status = $ceremonyToDecision.execution_status
            response_index = [int]$ceremonyResponse.index
            response_key = [string]$ceremonyResponse.response_key
            training_role = "native_interstitial_not_lifecycle_transition"
        }
        lifecycle_steps = @($lifecycleSteps | ForEach-Object {
            [ordered]@{
                name = $_.name
                option_id = $_.option_id
                transition_kind = $_.transition_kind
                execution_status = $_.execution_status
                admission_status = $_.admission_status
                training_label_eligible = $_.training_label_eligible
            }
        })
        final_stage = [string]$ceremony.after.state.world_progress.`
            community_center.value.lifecycle.stage
    }
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "summary.json") `
        -Value $summary
    $summary | ConvertTo-Json -Depth 48
    if ($summary.status -ne "passed") {
        throw "Community Center lifecycle smoke failed: $artifactDirectory"
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    if (-not $KeepGameRunning) {
        foreach ($process in @($game, $backend)) {
            if ($null -ne $process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force `
                    -ErrorAction SilentlyContinue
                $process.WaitForExit(10000) | Out-Null
            }
        }
    }
}
