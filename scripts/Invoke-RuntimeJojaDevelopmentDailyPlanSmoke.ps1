param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-joja-development-daily-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($null -ne $value) { return $value }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
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
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Wait-JojaSnapshot([scriptblock] $Predicate, [string] $Description, [int] $TimeoutSeconds = 90) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $joja = $snapshot.state.world_progress.joja_development.value
            $lastStatus = "location=$($snapshot.state.player.location_id.value);route=$($joja.host_route_state);membership=$($joja.actor_membership_received)/$($joja.actor_membership_pending);order=$($joja.project_order_pending)"
            if (& $Predicate $snapshot $joja) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Description. Last status: $lastStatus"
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Read-Parameter($QueueItem, [string] $Name) {
    [string](@($QueueItem.normalized_command.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -ExpandProperty value -First 1)
}

function New-ExecutionRequest([string] $OptionId, [string] $QueueItemId, [string] $StateHash, [hashtable] $Extra = @{}) {
    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-joja-development-matrix"
        queue_item_id = $QueueItemId
        before_state_hash = $StateHash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $isolatedSavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
    foreach ($key in $Extra.Keys) { $request[$key] = $Extra[$key] }
    $request
}

function Find-JojaProject($Joja, [string] $ProjectId) {
    @($Joja.projects | Where-Object { [string]$_.project_id -eq $ProjectId }) | Select-Object -First 1
}

function Invoke-JojaCase($Case) {
    $caseDirectory = Join-Path $artifactDirectory $Case.Name
    $caseLoopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Path $caseDirectory | Out-Null
    $isolatedSavesPath = Join-Path $caseDirectory "isolated-saves"
    $isolatedSaveSlot = Join-Path $isolatedSavesPath $SaveSlot
    $trainingOutputDirectory = Join-Path $caseDirectory "training-output"
    New-Item -ItemType Directory -Path $isolatedSavesPath | Out-Null
    New-Item -ItemType Directory -Path $trainingOutputDirectory | Out-Null
    Copy-Item -LiteralPath $sourceSaveSlot -Destination $isolatedSaveSlot -Recurse

    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $script:isolatedSavesPath = $isolatedSavesPath
    $windowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $caseGameProcess = $null

    try {
        $caseGameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle $windowStyle -PassThru
        Wait-Json "$executorBaseUrl/health" 30 | Out-Null
        Wait-WorldSnapshot | Out-Null

        $initial = Wait-WorldSnapshot
        $setup = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
        "debug.setup_joja_development" "fixture.$($Case.Name)" ([string]$initial.state_hash) `
        @{ joja_fixture_case = $Case.Fixture })
    Write-Json (Join-Path $caseDirectory "setup-result.json") $setup
    if ($setup.status -ne "applied" -or $setup.primitive_verification_status -ne "verified") {
        throw "Joja fixture setup failed for $($Case.Name): $(@($setup.block_reasons) -join ',')"
    }

    $before = Wait-JojaSnapshot {
        param($snapshot, $joja)
        [string]$snapshot.state.player.location_id.value -eq "JojaMart" -and
            [int]$joja.money -eq 100000 -and
            [string]$joja.host_route_state -eq $Case.RouteBefore -and
            [bool]$joja.actor_greeting_received -eq [bool]$Case.GreetingBefore -and
            [bool]$joja.actor_membership_received -eq [bool]$Case.MembershipBefore -and
            -not [bool]$joja.actor_membership_pending -and
            -not [bool]$joja.project_order_pending
    } "ready Joja fixture $($Case.Name)"
    $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
    Write-Json $snapshotPath $before
    Invoke-JsonPostRaw "$backendUrl/api/v1/snapshots" (Get-Content -LiteralPath $snapshotPath -Raw) | Out-Null

    $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" ([ordered]@{
        state_hash = [string]$before.state_hash
        candidate_option_ids = @("joja.advance_development")
        candidates = @()
        include_executor_calibration_options = $true
    })
    Write-Json (Join-Path $caseDirectory "availability.json") $availability
    $candidate = @($availability.options | Where-Object { $_.option_id -eq "joja.advance_development" } |
        ForEach-Object { $_.event_candidates } | Where-Object {
            [bool]$_.available -and [string]$_.candidate_id -eq $Case.CandidateId
        }) | Select-Object -First 1
    if ($null -eq $candidate -or [string]$candidate.kind -ne $Case.CandidateKind) {
        throw "Exact Joja candidate unavailable for $($Case.Name)."
    }

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $caseLoopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorBaseUrl `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $isolatedSavesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "joja.advance_development" `
        --daily-plan-candidate-kind $Case.CandidateKind `
        --daily-plan-candidate-id $Case.CandidateId `
        --after-snapshot-wait-ms 1000 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop failed for $($Case.Name) with exit code $LASTEXITCODE" }

    $runRoot = Join-Path $caseLoopRoot (Join-Path "runs" $RunId)
    $snapshotDirectory = Join-Path $runRoot "live-snapshots"
    $dailyPlanPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $datasetPath = Join-Path $caseLoopRoot "datasets\live-training-feature-rows.jsonl"
    foreach ($requiredPath in @($dailyPlanPath, $queuePath, $executionPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required Joja artifact missing: $requiredPath"
        }
    }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $expectedPrimitive = if ($Case.IsMembership) { "executor.purchase_joja_membership" } else { "executor.purchase_joja_project" }
    $queueItem = @($queue.items | Where-Object { $_.option_id -eq $expectedPrimitive }) | Select-Object -First 1
    $stepResult = @($execution.step_results | Where-Object {
        $_.option_id -eq $expectedPrimitive -and $_.queue_item_id -eq $queueItem.queue_item_id
    }) | Select-Object -First 1
    if ($null -eq $queueItem -or $null -eq $stepResult) {
        throw "Joja DailyPlan did not compile and execute the expected primitive for $($Case.Name)."
    }
    if ($stepResult.status -ne "applied" -or $stepResult.primitive_verification_status -ne "verified") {
        throw "Joja execution failed for $($Case.Name): status=$($stepResult.status); verification=$($stepResult.primitive_verification_status); reasons=$(@($stepResult.block_reasons) -join ',')"
    }
    if (-not (Test-Path -LiteralPath $datasetPath -PathType Leaf)) {
        throw "Verified Joja execution did not produce its dataset: $datasetPath"
    }

    $immediate = Wait-JojaSnapshot {
        param($snapshot, $joja)
        if ($Case.IsMembership) {
            return [bool]$joja.actor_membership_pending -and [int]$joja.money -eq 95000
        }
        $project = Find-JojaProject $joja $Case.ProjectId
        return $null -ne $project -and [bool]$project.complete_or_pending -and
            [bool]$joja.project_order_pending -and [int]$joja.money -eq 100000 - $Case.Price
    } "immediate Joja receipt $($Case.Name)"
    Write-Json (Join-Path $caseDirectory "immediate-after-snapshot.json") $immediate

    $prepare = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
        "debug.prepare_joja_settlement_sleep" "prepare-sleep.$($Case.Name)" ([string]$immediate.state_hash))
    Write-Json (Join-Path $caseDirectory "prepare-sleep-result.json") $prepare
    if ($prepare.status -ne "applied" -or $prepare.primitive_verification_status -ne "verified") {
        throw "Joja settlement sleep preparation failed for $($Case.Name)."
    }
    $prepared = Wait-WorldSnapshot 30
    $dayBefore = [int]$prepared.state.time.total_days.value
    $sleep = Invoke-JsonPost $executorUrl (New-ExecutionRequest `
        "executor.sleep" "sleep.$($Case.Name)" ([string]$prepared.state_hash)) 180
    Write-Json (Join-Path $caseDirectory "sleep-result.json") $sleep
    if ($sleep.status -ne "applied" -or $sleep.primitive_verification_status -ne "verified") {
        throw "Native sleep failed for $($Case.Name): $(@($sleep.block_reasons) -join ',')"
    }

    $settled = Wait-JojaSnapshot {
        param($snapshot, $joja)
        if ([int]$snapshot.state.time.total_days.value -ne $dayBefore + 1) { return $false }
        if ($Case.IsMembership) {
            return [bool]$joja.actor_membership_received -and
                -not [bool]$joja.actor_membership_pending -and
                [string]$joja.host_route_state -eq "joja_locked"
        }
        $project = Find-JojaProject $joja $Case.ProjectId
        return $null -ne $project -and [bool]$project.complete_or_pending -and
            [bool]$project.cc_mail_received_or_pending -and
            [bool]$project.joja_mail_received_or_pending -and
            -not [bool]$joja.project_order_pending
    } "next-day Joja settlement $($Case.Name)" 180
    Write-Json (Join-Path $caseDirectory "settled-after-snapshot.json") $settled

    $price = [int](Read-Parameter $queueItem "price")
    $moneyBefore = [int](Read-Parameter $queueItem "expected_money_before")
    $moneyAfter = [int](Read-Parameter $queueItem "expected_money_after")
    $datasetText = Get-Content -LiteralPath $datasetPath -Raw
    $passed = $null -ne $dailyPlan.plan -and
        [string]$dailyPlan.action_queue.status -eq "pending" -and
        [string]$queue.status -eq "pending" -and
        [string]$stepResult.status -eq "applied" -and
        [string]$stepResult.primitive_verification_status -eq "verified" -and
        $moneyBefore -eq 100000 -and $price -eq $Case.Price -and $moneyAfter -eq 100000 - $Case.Price -and
        [int]$settled.state.time.total_days.value -eq $dayBefore + 1 -and
        -not [bool]$settled.state.menus.active_menu.value.is_open -and
        $datasetText.Contains($expectedPrimitive)
        [ordered]@{
            case = $Case.Name
            passed = $passed
            candidate_id = [string]$candidate.candidate_id
            candidate_kind = [string]$candidate.kind
            primitive_option_id = $expectedPrimitive
            execution_status = [string]$stepResult.status
            verification_status = [string]$stepResult.primitive_verification_status
            verification_reasons = @($stepResult.primitive_verification_reasons)
            money_before = $moneyBefore
            price = $price
            money_immediate_after = [int]$immediate.state.player.money.value
            day_before_sleep = $dayBefore
            day_after_sleep = [int]$settled.state.time.total_days.value
            membership_received_after = [bool]$settled.state.world_progress.joja_development.value.actor_membership_received
            project_id = $Case.ProjectId
            isolated_save_slot = $isolatedSaveSlot
            dataset_path = $datasetPath
            daily_plan_path = $dailyPlanPath
            queue_path = $queuePath
            execution_path = $executionPath
        }
    }
    finally {
        $keepThisGame = $KeepGameRunning -and $Case.Name -eq "project-fish-tank"
        if (-not $keepThisGame -and $null -ne $caseGameProcess -and -not $caseGameProcess.HasExited) {
            Stop-Process -Id $caseGameProcess.Id -Force -ErrorAction SilentlyContinue
            Wait-Process -Id $caseGameProcess.Id -Timeout 15 -ErrorAction SilentlyContinue
        }
        if ($keepThisGame) {
            $script:gameProcess = $caseGameProcess
        }
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorBaseUrl = "http://127.0.0.1:8767"
$executorUrl = "$executorBaseUrl/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $sourceSavesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
$sourceSaveSlot = Join-Path $sourceSavesPath $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSaveSlot -PathType Container)) { throw "Source save slot not found: $sourceSaveSlot" }
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-joja-development-daily-plan\" + $RunId)
if (Test-Path -LiteralPath $artifactDirectory) { throw "Artifact directory already exists: $artifactDirectory" }
New-Item -ItemType Directory -Path $artifactDirectory | Out-Null

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
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $cases = @(
        [pscustomobject]@{ Name="membership-without-greeting"; Fixture="membership_without_greeting"; CandidateId="joja-membership"; CandidateKind="purchase_joja_membership"; IsMembership=$true; RouteBefore="undecided"; GreetingBefore=$false; MembershipBefore=$false; ProjectId=""; Price=5000 },
        [pscustomobject]@{ Name="membership-with-greeting"; Fixture="membership_with_greeting"; CandidateId="joja-membership"; CandidateKind="purchase_joja_membership"; IsMembership=$true; RouteBefore="undecided"; GreetingBefore=$true; MembershipBefore=$false; ProjectId=""; Price=5000 },
        [pscustomobject]@{ Name="project-vault"; Fixture="project_vault"; CandidateId="joja-project:vault"; CandidateKind="purchase_joja_project"; IsMembership=$false; RouteBefore="joja_locked"; GreetingBefore=$true; MembershipBefore=$true; ProjectId="vault"; Price=40000 },
        [pscustomobject]@{ Name="project-boiler-room"; Fixture="project_boiler_room"; CandidateId="joja-project:boiler_room"; CandidateKind="purchase_joja_project"; IsMembership=$false; RouteBefore="joja_locked"; GreetingBefore=$true; MembershipBefore=$true; ProjectId="boiler_room"; Price=15000 },
        [pscustomobject]@{ Name="project-crafts-room"; Fixture="project_crafts_room"; CandidateId="joja-project:crafts_room"; CandidateKind="purchase_joja_project"; IsMembership=$false; RouteBefore="joja_locked"; GreetingBefore=$true; MembershipBefore=$true; ProjectId="crafts_room"; Price=25000 },
        [pscustomobject]@{ Name="project-pantry"; Fixture="project_pantry"; CandidateId="joja-project:pantry"; CandidateKind="purchase_joja_project"; IsMembership=$false; RouteBefore="joja_locked"; GreetingBefore=$true; MembershipBefore=$true; ProjectId="pantry"; Price=35000 },
        [pscustomobject]@{ Name="project-fish-tank"; Fixture="project_fish_tank"; CandidateId="joja-project:fish_tank"; CandidateKind="purchase_joja_project"; IsMembership=$false; RouteBefore="joja_locked"; GreetingBefore=$true; MembershipBefore=$true; ProjectId="fish_tank"; Price=20000 }
    )
    $caseResults = @($cases | ForEach-Object { Invoke-JojaCase $_ })
    $passedCount = @($caseResults | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $cases.Count) { "passed" } else { "failed" }
        run_id = $RunId
        source_save_slot = $sourceSaveSlot
        isolated_save_layout = "<artifact>/<case>/isolated-saves/$SaveSlot"
        expected_case_count = $cases.Count
        passed_case_count = $passedCount
        cases = $caseResults
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if ($passedCount -ne $cases.Count) { throw "Runtime Joja development matrix failed: $artifactDirectory" }
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
