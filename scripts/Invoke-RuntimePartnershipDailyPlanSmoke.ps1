param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-partnership-daily-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 5132,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw([string] $Url, [string] $Json, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body $Json -TimeoutSec $TimeoutSeconds
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
            if ($null -ne $value) { return $value }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([int] $TimeoutSeconds = 180) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $lastStatus = "save=$($snapshot.save_id.status);player=$($snapshot.state.player.location_id.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.player.location_id.status -in @("available", "derived")) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Find-Friendship($Snapshot, [string] $NpcName) {
    @($Snapshot.state.npcs.friendships.value | Where-Object { [string]$_.npc_name -eq $NpcName }) |
        Select-Object -First 1
}

function Wait-Friendship([string] $NpcName, [string] $Status, [int] $MinimumTotalDays = -1, [int] $TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $friendship = Find-Friendship $snapshot $NpcName
            $totalDays = [int]$snapshot.state.time.total_days.value
            $lastStatus = "npc=$NpcName;status=$($friendship.status);days=$totalDays;location=$($snapshot.state.player.location_id.value)"
            if ($null -ne $friendship -and [string]$friendship.status -eq $Status -and
                ($MinimumTotalDays -lt 0 -or $totalDays -ge $MinimumTotalDays)) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for friendship state. Last status: $lastStatus"
}

function Wait-DayAdvance([int] $MinimumTotalDays, [int] $TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $totalDays = [int]$snapshot.state.time.total_days.value
            $lastStatus = "days=$totalDays;location=$($snapshot.state.player.location_id.value)"
            if ($totalDays -ge $MinimumTotalDays) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for day $MinimumTotalDays. Last status: $lastStatus"
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function New-ExecutionRequest([string] $OptionId, [string] $QueueItemId, [string] $StateHash, $Extra = @{}) {
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-partnership-matrix"
        queue_item_id = $QueueItemId
        before_state_hash = $StateHash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
    foreach ($key in $Extra.Keys) { $request[$key] = $Extra[$key] }
    $request
}

function Invoke-NativeSleep([string] $CaseDirectory, [int] $Ordinal, $BeforeSnapshot) {
    $prepare = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
        "debug.prepare_partnership_sleep" "$RunId.prepare-sleep.$Ordinal" ([string]$BeforeSnapshot.state_hash))
    Write-Json (Join-Path $CaseDirectory "prepare-sleep-$Ordinal-result.json") $prepare
    if ($prepare.status -ne "applied" -or $prepare.primitive_verification_status -ne "verified") {
        throw "Partnership sleep preparation failed on ordinal $Ordinal."
    }

    $homeSnapshot = Wait-WorldSnapshot 30
    $beforeTotalDays = [int]$homeSnapshot.state.time.total_days.value
    $sleep = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
        "executor.sleep" "$RunId.sleep.$Ordinal" ([string]$homeSnapshot.state_hash)) 180
    Write-Json (Join-Path $CaseDirectory "sleep-$Ordinal-result.json") $sleep
    if ($sleep.status -ne "applied" -or $sleep.primitive_verification_status -ne "verified") {
        throw "Native sleep failed on partnership ordinal ${Ordinal}: $(@($sleep.block_reasons) -join ',')"
    }

    $after = Wait-DayAdvance ($beforeTotalDays + 1) 60
    Write-Json (Join-Path $CaseDirectory "post-sleep-$Ordinal-snapshot.json") $after
    [ordered]@{
        ordinal = $Ordinal
        total_days_before = $beforeTotalDays
        total_days_after = [int]$after.state.time.total_days.value
        friendship_status_after = [string](Find-Friendship $after "Abigail").status
        sleep_status = [string]$sleep.status
        sleep_verification_status = [string]$sleep.primitive_verification_status
    }
}

function Invoke-PartnershipCase([string] $CaseName, [string] $ExpectedKind, [string] $ExpectedActionKind, [string] $NpcName) {
    $caseDirectory = Join-Path $artifactDirectory $CaseName
    $loopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null

    $initial = Wait-WorldSnapshot 180
    $setup = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
        "debug.setup_partnership_fixture" "$RunId.fixture.$CaseName" ([string]$initial.state_hash) `
        @{ partnership_fixture_case = $CaseName })
    Write-Json (Join-Path $caseDirectory "setup-result.json") $setup
    if ($setup.status -ne "applied" -or $setup.primitive_verification_status -ne "verified") {
        throw "Partnership fixture failed for ${CaseName}: $(@($setup.block_reasons) -join ',')"
    }

    Start-Sleep -Milliseconds 750
    $before = Wait-Friendship $NpcName $(if ($CaseName -eq "marriage") { "Dating" } else { "Friendly" })
    $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
    Write-Json $snapshotPath $before
    Invoke-JsonPostRaw "$backendUrl/api/v1/snapshots" (Get-Content -LiteralPath $snapshotPath -Raw) | Out-Null

    $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" ([ordered]@{
        state_hash = [string]$before.state_hash
        candidate_option_ids = @("social.advance_partnership")
        candidates = @()
        include_executor_calibration_options = $true
    })
    Write-Json (Join-Path $caseDirectory "availability.json") $availability
    $option = @($availability.options | Where-Object { $_.option_id -eq "social.advance_partnership" }) | Select-Object -First 1
    $candidate = @($option.social_candidates | Where-Object {
        [bool]$_.available -and [string]$_.kind -eq $ExpectedKind
    }) | Select-Object -First 1
    if ($null -eq $candidate) {
        $diagnostics = @($option.social_candidates | ForEach-Object {
            "$($_.kind):$(@($_.block_reasons) -join ',')"
        }) -join ";"
        throw "No available $ExpectedKind candidate for ${CaseName}. diagnostics=$diagnostics"
    }

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $loopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorBaseUrl `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "social.advance_partnership" `
        --daily-plan-candidate-kind $ExpectedKind `
        --daily-plan-candidate-id ([string]$candidate.candidate_id) `
        --after-snapshot-wait-ms 1000 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop failed for $CaseName with exit code $LASTEXITCODE" }

    $runRoot = Join-Path $loopRoot (Join-Path "runs" $RunId)
    $snapshotDirectory = Join-Path $runRoot "live-snapshots"
    $dailyPlanPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $datasetPath = Join-Path $loopRoot "datasets\live-training-feature-rows.jsonl"
    foreach ($requiredPath in @($dailyPlanPath, $queuePath, $executionPath, $datasetPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required partnership artifact missing: $requiredPath"
        }
    }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $socialQueue = @($queue.items | Where-Object { $_.option_id -eq "executor.social_interact" }) | Select-Object -First 1
    $stepResults = if ($null -ne $execution.step_results) { @($execution.step_results) } else { @($execution) }
    $socialResult = @($stepResults | Where-Object { $_.option_id -eq "executor.social_interact" }) | Select-Object -First 1
    if ($null -eq $socialQueue -or $null -eq $socialResult) {
        throw "Partnership plan did not compile and execute shared social primitive for $CaseName."
    }

    $afterStatus = if ($ExpectedActionKind -eq "bouquet") { "Dating" } else { "Engaged" }
    $after = Wait-Friendship $NpcName $afterStatus
    Write-Json (Join-Path $caseDirectory "after-snapshot.json") $after
    $beforeFriendship = Find-Friendship $before $NpcName
    $afterFriendship = Find-Friendship $after $NpcName
    $datasetText = Get-Content -LiteralPath $datasetPath -Raw
    $transitionPassed = [string]$dailyPlan.action_queue.status -eq "pending" -and
        [string]$queue.status -eq "pending" -and
        [string]$socialResult.status -eq "applied" -and
        [string]$socialResult.primitive_verification_status -eq "verified" -and
        $socialResult.social_native_handled -eq $true -and
        [string]$socialResult.social_action_kind -eq $ExpectedActionKind -and
        [string]$socialResult.partnership_friendship_status_after -eq $afterStatus -and
        [string]$afterFriendship.status -eq $afterStatus -and
        $null -eq $socialResult.social_gift_stack_after -and
        $datasetText.Contains("executor.social_interact") -and
        $datasetText.Contains([string]$candidate.candidate_id)
    if (-not $transitionPassed) {
        throw "Native partnership transition verification failed for $CaseName."
    }

    $wedding = $null
    if ($CaseName -eq "marriage") {
        $weddingDate = [int]$afterFriendship.wedding_date_total_days
        $sleepEvidence = @()
        $current = $after
        for ($ordinal = 1; $ordinal -le 7; $ordinal++) {
            $currentFriendship = Find-Friendship $current $NpcName
            if ([string]$currentFriendship.status -eq "Married") { break }
            $sleepEvidence += Invoke-NativeSleep $caseDirectory $ordinal $current
            $current = Wait-WorldSnapshot 30
        }
        $settled = Wait-Friendship $NpcName "Married" $weddingDate 60
        Write-Json (Join-Path $caseDirectory "wedding-settled-snapshot.json") $settled
        $settledFriendship = Find-Friendship $settled $NpcName
        $weddingPassed = [string]$settled.state.player.spouse.value -eq $NpcName -and
            [string]$settledFriendship.status -eq "Married" -and
            [int]$settledFriendship.wedding_date_total_days -eq [int]$settled.state.time.total_days.value -and
            @($sleepEvidence).Count -ge 3
        if (-not $weddingPassed) { throw "Native cross-day wedding settlement failed." }
        $wedding = [ordered]@{
            passed = $weddingPassed
            scheduled_total_days = $weddingDate
            settled_total_days = [int]$settled.state.time.total_days.value
            final_status = [string]$settledFriendship.status
            spouse = [string]$settled.state.player.spouse.value
            sleep_count = @($sleepEvidence).Count
            sleeps = @($sleepEvidence)
        }
    }

    [ordered]@{
        case = $CaseName
        passed = $transitionPassed
        npc_name = $NpcName
        candidate_id = [string]$candidate.candidate_id
        candidate_kind = [string]$candidate.kind
        action_kind = [string]$socialResult.social_action_kind
        friendship_status_before = [string]$beforeFriendship.status
        friendship_status_after = [string]$afterFriendship.status
        spouse_after = [string]$socialResult.partnership_spouse_after
        roommate_marriage_after = $socialResult.partnership_roommate_marriage_after
        wedding_date_total_days_after = $socialResult.partnership_wedding_date_total_days_after
        native_handled = $socialResult.social_native_handled
        primitive_verification_status = [string]$socialResult.primitive_verification_status
        wedding_settlement = $wedding
        daily_plan_path = $dailyPlanPath
        queue_path = $queuePath
        execution_path = $executionPath
        dataset_path = $datasetPath
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorBaseUrl = "http://127.0.0.1:8767"
$executorUrl = "$executorBaseUrl/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-partnership-daily-plan\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$trainingOutputDirectory = Join-Path $artifactDirectory "runtime-training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS")
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $windowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle $windowStyle -PassThru
    Wait-Json "$executorBaseUrl/health" 30 | Out-Null
    Wait-WorldSnapshot 180 | Out-Null

    $bouquet = Invoke-PartnershipCase "bouquet" "partnership_bouquet_current" "bouquet" "Abigail"
    $marriage = Invoke-PartnershipCase "marriage" "partnership_propose_marriage_current" "propose_marriage" "Abigail"
    $roommate = Invoke-PartnershipCase "roommate" "partnership_propose_roommate_current" "propose_roommate" "Krobus"
    $caseResults = @($bouquet, $marriage, $roommate)
    $transitionPassedCount = @($caseResults | Where-Object { $_.passed }).Count
    $weddingPassed = $marriage.wedding_settlement.passed -eq $true
    $passedCount = $transitionPassedCount + $(if ($weddingPassed) { 1 } else { 0 })
    $summary = [ordered]@{
        status = if ($passedCount -eq 4) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        expected_case_count = 4
        passed_case_count = $passedCount
        transitions = $caseResults
        cross_day_wedding = $marriage.wedding_settlement
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 96
    if ($passedCount -ne 4) { throw "Runtime partnership matrix failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
