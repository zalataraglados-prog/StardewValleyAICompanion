param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-farmhouse-upgrade-daily-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 5131,
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

function Wait-FarmhouseSnapshot([int] $LevelBefore, [int] $TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $field = $snapshot.state.world_progress.marriage_house
            $value = $field.value
            $lastStatus = "status=$($field.status);location=$($snapshot.state.player.location_id.value);level=$($value.farmhouse_upgrade_level);action=$($value.house_upgrade.action_status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $field.status -in @("available", "derived") -and
                [string]$snapshot.state.player.location_id.value -eq "ScienceHouse" -and
                [int]$value.farmhouse_upgrade_level -eq $LevelBefore -and
                [int]$value.days_until_farmhouse_upgrade -eq -1 -and
                [string]$value.house_upgrade.action_status -eq "ready") {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for ready farmhouse level $LevelBefore snapshot. Last status: $lastStatus"
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

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Read-Parameter($QueueItem, [string] $Name) {
    [string](@($QueueItem.normalized_command.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -ExpandProperty value -First 1)
}

function Invoke-FarmhouseCase([int] $LevelBefore) {
    $levelAfter = $LevelBefore + 1
    $caseName = "level-$LevelBefore-to-$levelAfter"
    $caseRunId = $RunId
    $caseDirectory = Join-Path $artifactDirectory $caseName
    $caseLoopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null

    $initial = Wait-WorldSnapshot 180
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $caseRunId
        queue_id = "runtime-farmhouse-upgrade-fixture"
        queue_item_id = "runtime-farmhouse-upgrade-fixture.$caseName"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_farmhouse_upgrade"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        expected_house_upgrade_level_before = $LevelBefore
    }
    $setup = Invoke-JsonPost $executorUrl $setupRequest
    Write-Json (Join-Path $caseDirectory "setup-result.json") $setup
    if ($setup.status -ne "applied" -or $setup.primitive_verification_status -ne "verified") {
        throw "Farmhouse fixture setup failed for ${caseName}: $(@($setup.block_reasons) -join ',')"
    }

    Start-Sleep -Milliseconds 750
    $before = Wait-FarmhouseSnapshot $LevelBefore
    $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
    Write-Json $snapshotPath $before
    Invoke-JsonPostRaw "$backendUrl/api/v1/snapshots" (Get-Content -LiteralPath $snapshotPath -Raw) | Out-Null

    $availabilityRequest = [ordered]@{
        state_hash = [string]$before.state_hash
        candidate_option_ids = @("housing.advance_farmhouse")
        candidates = @()
        include_executor_calibration_options = $true
    }
    $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" $availabilityRequest
    Write-Json (Join-Path $caseDirectory "availability.json") $availability
    $candidate = @($availability.options | Where-Object { $_.option_id -eq "housing.advance_farmhouse" } |
        ForEach-Object { $_.event_candidates } | Where-Object { [bool]$_.available }) | Select-Object -First 1
    if ($null -eq $candidate) {
        throw "No available farmhouse candidate for $caseName."
    }
    $expectedKind = if ($LevelBefore -lt 2) { "purchase_farmhouse_upgrade" } else { "purchase_farmhouse_expansion" }
    if ([string]$candidate.kind -ne $expectedKind) {
        throw "Unexpected candidate kind for ${caseName}: $($candidate.kind)"
    }

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $caseLoopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorBaseUrl `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $caseRunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "housing.advance_farmhouse" `
        --daily-plan-candidate-kind $expectedKind `
        --daily-plan-candidate-id ([string]$candidate.candidate_id) `
        --after-snapshot-wait-ms 1000 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop failed for $caseName with exit code $LASTEXITCODE" }

    $runRoot = Join-Path $caseLoopRoot (Join-Path "runs" $caseRunId)
    $snapshotDirectory = Join-Path $runRoot "live-snapshots"
    $dailyPlanPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $datasetPath = Join-Path $caseLoopRoot "datasets\live-training-feature-rows.jsonl"
    foreach ($requiredPath in @($dailyPlanPath, $queuePath, $executionPath, $datasetPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required farmhouse artifact missing: $requiredPath"
        }
    }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $queueItem = @($queue.items) | Where-Object { $_.option_id -eq "executor.purchase_farmhouse_upgrade" } | Select-Object -First 1
    $stepResult = @($execution.step_results) | Where-Object {
        $_.option_id -eq "executor.purchase_farmhouse_upgrade" -and
        $_.queue_item_id -eq $queueItem.queue_item_id
    } | Select-Object -First 1
    if ($null -eq $queueItem -or $null -eq $stepResult) {
        throw "Farmhouse high-level plan did not compile and execute its sole primitive for $caseName."
    }

    $after = Wait-Json $snapshotUrl 30
    Write-Json (Join-Path $caseDirectory "after-snapshot.json") $after
    $beforeProgress = $before.state.world_progress.marriage_house.value
    $afterProgress = $after.state.world_progress.marriage_house.value
    $price = [int](Read-Parameter $queueItem "price")
    $requiredStack = [int](Read-Parameter $queueItem "required_stack")
    $materialId = [string](Read-Parameter $queueItem "qualified_item_id")
    $moneyBefore = [int](Read-Parameter $queueItem "expected_money_before")
    $moneyAfter = [int](Read-Parameter $queueItem "expected_money_after")
    $materialBefore = [int](Read-Parameter $queueItem "inventory_item_total_before")
    $materialAfter = [int](Read-Parameter $queueItem "inventory_item_total_after")
    $datasetText = Get-Content -LiteralPath $datasetPath -Raw
    $passed = $null -ne $dailyPlan.plan -and
        [string]$dailyPlan.action_queue.status -eq "pending" -and
        [string]$queue.status -eq "pending" -and
        [string]$stepResult.status -eq "applied" -and
        [string]$stepResult.primitive_verification_status -eq "verified" -and
        @($stepResult.primitive_verification_reasons) -contains "GameLocation.checkAction_Carpenter_completed" -and
        @($stepResult.primitive_verification_reasons) -contains "GameLocation.answerDialogue_carpenter_Upgrade_completed" -and
        @($stepResult.primitive_verification_reasons) -contains "GameLocation.answerDialogue_upgrade_Yes_completed" -and
        [int]$afterProgress.farmhouse_upgrade_level -eq $LevelBefore -and
        [int]$afterProgress.days_until_farmhouse_upgrade -eq 3 -and
        [int]$after.state.player.money.value -eq $moneyAfter -and
        $moneyAfter -eq $moneyBefore - $price -and
        $materialAfter -eq $materialBefore - $requiredStack -and
        $datasetText.Contains("executor.purchase_farmhouse_upgrade")
    [ordered]@{
        case = $caseName
        passed = $passed
        candidate_id = [string]$candidate.candidate_id
        candidate_kind = [string]$candidate.kind
        daily_plan_status = [string]$dailyPlan.action_queue.status
        queue_status = [string]$queue.status
        execution_status = [string]$stepResult.status
        verification_status = [string]$stepResult.primitive_verification_status
        verification_reasons = @($stepResult.primitive_verification_reasons)
        house_level_before = [int]$beforeProgress.farmhouse_upgrade_level
        house_level_immediate_after = [int]$afterProgress.farmhouse_upgrade_level
        days_until_upgrade_after = [int]$afterProgress.days_until_farmhouse_upgrade
        money_before = $moneyBefore
        money_after = [int]$after.state.player.money.value
        price = $price
        material_qualified_item_id = $materialId
        material_count_before = $materialBefore
        material_count_after = $materialAfter
        dataset_path = $datasetPath
        daily_plan_path = $dailyPlanPath
        queue_path = $queuePath
        execution_path = $executionPath
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-farmhouse-upgrade-daily-plan\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS")
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

    $caseResults = @(0, 1, 2 | ForEach-Object { Invoke-FarmhouseCase $_ })
    $passedCount = @($caseResults | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq 3) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        expected_case_count = 3
        passed_case_count = $passedCount
        cases = $caseResults
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if ($passedCount -ne 3) { throw "Runtime farmhouse upgrade matrix failed: $artifactDirectory" }
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
