param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-field-office-survey-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try { $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5; if ($null -ne $result) { return $result } }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.world_progress.island_field_office.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready Field Office snapshot. Last error: $lastError"
}

function Wait-SurveyState(
    [bool] $Left,
    [bool] $Right,
    [bool] $FailedToday,
    [string] $CandidateKind,
    [string] $CandidateStatus,
    [int] $TimeoutSeconds
) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot 10
        $office = $snapshot.state.world_progress.island_field_office.value
        $candidate = @($office.survey_candidates) | Select-Object -First 1
        $candidateMatches = [string]::IsNullOrWhiteSpace($CandidateKind) -or
            ($null -ne $candidate -and [string]$candidate.survey_kind -eq $CandidateKind)
        $statusMatches = [string]::IsNullOrWhiteSpace($CandidateStatus) -or
            ($null -ne $candidate -and [string]$candidate.action_status -eq $CandidateStatus)
        if ([bool]$office.plants_restored_left -eq $Left -and
            [bool]$office.plants_restored_right -eq $Right -and
            [bool]$office.has_failed_survey_today -eq $FailedToday -and $candidateMatches -and $statusMatches) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for survey state left=$Left right=$Right failed=$FailedToday candidate=$CandidateKind status=$CandidateStatus."
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-field-office-survey-smoke"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Setup-SurveyFixture([string] $Kind, [int] $WalnutsFound, [string] $FixtureCase, [string] $CaseName) {
    $snapshot = Wait-WorldSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_field_office_survey" ("setup-" + $CaseName)
    $request.field_office_survey_kind = $Kind
    $request.field_office_golden_walnuts_found_before = $WalnutsFound
    $request.field_office_survey_fixture_case = $FixtureCase
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Field Office survey fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $left = $Kind -eq "purple_starfish"
    $failed = $FixtureCase -in @("failed_today", "day_reset")
    $expectedStatus = if ($failed) { "field_office_survey_failed_today" } else { "ready" }
    Wait-SurveyState $left $false $failed $Kind $expectedStatus 30
}

function New-CorrectSurveyRequest($Before, $Candidate, [string] $QueueItemId) {
    $office = $Before.state.world_progress.island_field_office.value
    $endpoint = @($office.survey_action_tiles) | Where-Object { $_.action_raw -eq "FieldOfficeSurvey" } | Select-Object -First 1
    if ($null -eq $endpoint) { throw "Field Office survey endpoint missing for $QueueItemId." }
    $request = New-BaseRequest $Before "executor.answer_field_office_survey" $QueueItemId
    $request.location_id = [string]$office.location_id; $request.target_location = [string]$office.location_id
    $request.target_tile_x = [int]$endpoint.tile_x; $request.target_tile_y = [int]$endpoint.tile_y
    $request.stand_tile_x = [int]$Before.state.player.tile_x.value; $request.stand_tile_y = [int]$Before.state.player.tile_y.value
    $request.max_movement_tiles = 512
    $request.field_office_survey_action_raw = [string]$endpoint.action_raw
    $request.field_office_survey_kind = [string]$Candidate.survey_kind
    $request.field_office_survey_answer = [int]$Candidate.answer
    $request.field_office_survey_answer_minimum = [int]$Candidate.answer_minimum
    $request.field_office_survey_answer_maximum = [int]$Candidate.answer_maximum
    $request.field_office_survey_prompt_question_key = [string]$Candidate.prompt_question_key
    $request.field_office_survey_prompt_response_key = [string]$Candidate.prompt_response_key
    $request.field_office_survey_answer_question_key = [string]$Candidate.answer_question_key
    $request.field_office_survey_answer_response_key = [string]$Candidate.answer_response_key
    $request.field_office_survey_plant_restored_before = [bool]$Candidate.plant_restored_before
    $request.field_office_survey_plant_restored_after = [bool]$Candidate.plant_restored_after
    $request.field_office_survey_failed_today_before = [bool]$Candidate.failed_survey_today_before
    $request.field_office_survey_failed_today_after = [bool]$Candidate.failed_survey_today_after
    $request.field_office_collected_nut_key = [string]$Candidate.expected_collected_nut_key
    $request.field_office_collected_nut_before = [bool]$Candidate.collected_nut_before
    $request.field_office_survey_walnut_debris_count_before = [int]$Candidate.walnut_debris_count_before
    $request.field_office_survey_walnut_debris_count_after = [int]$Candidate.walnut_debris_count_after
    $request.field_office_survey_walnut_debris_spawn_count = [int]$Candidate.walnut_debris_spawn_count
    $request.field_office_survey_golden_walnuts_found_after = [int]$Candidate.golden_walnuts_found_after
    $request.field_office_survey_golden_walnuts_found_delta = [int]$Candidate.golden_walnuts_found_delta
    $request.field_office_survey_output_delivery = [string]$Candidate.output_delivery
    $request.field_office_finale_ready_after = [bool]$Candidate.expected_finale_ready_after
    $request.field_office_survey_expected_finale_trigger_after = [bool]$Candidate.expected_finale_trigger_after
    $request.field_office_plants_restored_left_before = [bool]$office.plants_restored_left
    $request.field_office_plants_restored_right_before = [bool]$office.plants_restored_right
    $request.field_office_finale_received_before = [bool]$office.finale_received_or_pending
    $request.field_office_survey_donated_piece_count_before = [int]$office.donated_piece_count
    $request.field_office_golden_walnuts_found_before = [int]$office.golden_walnuts_found
    $request.field_office_projection_status = [string]$office.projection_status
    $request.native_contract = "FieldOfficeSurvey_then_Survey_Yes_then_exact_Correct_response_then_native_plant_nut_debris_and_finale"
    $request
}

function Invoke-CorrectSurveyCase(
    [string] $CaseName,
    [string] $Kind,
    [int] $WalnutsFound,
    [string] $FixtureCase,
    $BeforeSnapshot
) {
    $before = if ($null -ne $BeforeSnapshot) { $BeforeSnapshot } else { Setup-SurveyFixture $Kind $WalnutsFound $FixtureCase $CaseName }
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $office = $before.state.world_progress.island_field_office.value
    $candidate = @($office.survey_candidates) | Where-Object { $_.survey_kind -eq $Kind } | Select-Object -First 1
    if ($null -eq $candidate -or $candidate.action_status -ne "ready") { throw "Transparent survey candidate was not ready for $CaseName." }
    $request = New-CorrectSurveyRequest $before $candidate ("answer-" + $CaseName)
    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $left = [bool]$office.plants_restored_left -or $Kind -eq "purple_flower"
    $right = [bool]$office.plants_restored_right -or $Kind -eq "purple_starfish"
    $nextKind = if ($left -and -not $right) { "purple_starfish" } else { "" }
    $nextStatus = if ([string]::IsNullOrWhiteSpace($nextKind)) { "" } else { "ready" }
    $after = Wait-SurveyState $left $right $false $nextKind $nextStatus 45
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $afterOffice = $after.state.world_progress.island_field_office.value
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
        [bool]$afterOffice.plants_restored_left -eq $left -and [bool]$afterOffice.plants_restored_right -eq $right -and
        -not [bool]$afterOffice.has_failed_survey_today -and
        [int]$afterOffice.golden_walnuts_found -eq [int]$candidate.golden_walnuts_found_after
    $summary = [ordered]@{
        case = $CaseName; passed = $passed; status = $result.status; verification = $result.primitive_verification_status
        survey_kind = $Kind; answer = [int]$candidate.answer; walnuts_found = $WalnutsFound
        walnut_debris_before = [int]$candidate.walnut_debris_count_before
        walnut_debris_after = [int]$candidate.walnut_debris_count_after
        walnut_debris_spawn_count = [int]$candidate.walnut_debris_spawn_count
        golden_walnuts_found_before = [int]$candidate.golden_walnuts_found_before
        golden_walnuts_found_after = [int]$candidate.golden_walnuts_found_after
        finale_ready_after = [bool]$afterOffice.finale_ready; finale_trigger_expected = [bool]$candidate.expected_finale_trigger_after
        block_reasons = @($result.block_reasons)
    }
    [ordered]@{ summary = $summary; after = $after }
}

function Invoke-WrongAndDayResetCases {
    $before = Setup-SurveyFixture "purple_flower" 0 "wrong" "wrong-flower"
    $office = $before.state.world_progress.island_field_office.value
    $candidate = @($office.survey_candidates) | Select-Object -First 1
    $request = New-CorrectSurveyRequest $before $candidate "answer-wrong-flower"
    $request.option_id = "debug.answer_field_office_survey_wrong"
    $request.field_office_survey_answer = 18
    $request.field_office_survey_answer_response_key = "Wrong"
    $request.field_office_survey_plant_restored_after = $false
    $request.field_office_survey_failed_today_after = $true
    $request.field_office_survey_walnut_debris_count_after = [int]$candidate.walnut_debris_count_before
    $request.field_office_survey_walnut_debris_spawn_count = 0
    $request.field_office_survey_golden_walnuts_found_after = [int]$office.golden_walnuts_found
    $request.field_office_survey_golden_walnuts_found_delta = 0
    $request.field_office_survey_output_delivery = "none_wrong_answer"
    $request.field_office_finale_ready_after = $false
    $request.field_office_survey_expected_finale_trigger_after = $false
    $wrongResult = Invoke-JsonPost $executorUrl $request
    $failed = Wait-SurveyState $false $false $true "purple_flower" "field_office_survey_failed_today" 45
    $failedCandidate = @($failed.state.world_progress.island_field_office.value.survey_candidates) | Select-Object -First 1
    $wrongSummary = [ordered]@{
        case = "wrong-answer-day-lock"; passed = $wrongResult.status -eq "applied" -and
            $wrongResult.primitive_verification_status -eq "verified" -and
            $failedCandidate.action_status -eq "field_office_survey_failed_today"
        status = $wrongResult.status; verification = $wrongResult.primitive_verification_status
        action_status = [string]$failedCandidate.action_status; block_reasons = @($wrongResult.block_reasons)
    }

    $dayRequest = New-BaseRequest $failed "debug.field_office_survey_day_update" "day-update-after-wrong"
    $dayResult = Invoke-JsonPost $executorUrl $dayRequest
    $reset = Wait-SurveyState $false $false $false "purple_flower" "ready" 30
    $resetCandidate = @($reset.state.world_progress.island_field_office.value.survey_candidates) | Select-Object -First 1
    $daySummary = [ordered]@{
        case = "native-day-update-reset"; passed = $dayResult.status -eq "applied" -and
            $dayResult.primitive_verification_status -eq "verified" -and $resetCandidate.action_status -eq "ready"
        status = $dayResult.status; verification = $dayResult.primitive_verification_status
        action_status = [string]$resetCandidate.action_status; block_reasons = @($dayResult.block_reasons)
    }
    @($wrongSummary, $daySummary)
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-field-office-survey-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")
$savedEnvironment = @{}; foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-WorldSnapshot 120 | Out-Null

    $cases = @()
    $cases += (Invoke-CorrectSurveyCase "flower-standard" "purple_flower" 0 "standard" $null).summary
    $cases += (Invoke-CorrectSurveyCase "starfish-standard" "purple_starfish" 0 "standard" $null).summary
    $sameDayBefore = Setup-SurveyFixture "purple_flower" 0 "same_day" "same-day"
    $sameDayFlower = Invoke-CorrectSurveyCase "same-day-flower" "purple_flower" 0 "same_day" $sameDayBefore
    $cases += $sameDayFlower.summary
    $sameDayStarfish = Invoke-CorrectSurveyCase "same-day-starfish" "purple_starfish" 0 "same_day" $sameDayFlower.after
    $cases += $sameDayStarfish.summary
    $cases += Invoke-WrongAndDayResetCases
    $cases += (Invoke-CorrectSurveyCase "flower-at-walnut-cap" "purple_flower" 130 "cap" $null).summary
    $cases += (Invoke-CorrectSurveyCase "starfish-at-walnut-cap" "purple_starfish" 130 "cap" $null).summary
    $cases += (Invoke-CorrectSurveyCase "finale-from-starfish" "purple_starfish" 0 "finale" $null).summary

    $expectedCount = 9; $passedCount = @($cases | Where-Object { $_.passed }).Count
    $finalSnapshot = Wait-WorldSnapshot 30
    $finalSnapshot | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory "final-full-snapshot.json") -Encoding utf8
    $summary = [ordered]@{
        status = if ($passedCount -eq $expectedCount) { "passed" } else { "failed" }; evidence_id = "EVD-303"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = $expectedCount; passed_case_count = $passedCount; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne $expectedCount) { throw "Runtime Field Office survey smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
