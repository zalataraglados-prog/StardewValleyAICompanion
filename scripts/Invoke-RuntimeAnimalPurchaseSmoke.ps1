[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-animal-purchase-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 5133,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
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
    throw "Timed out waiting for a world-ready snapshot. Last status: $lastStatus"
}

function Get-CandidateParameter($Candidate, [string] $Name) {
    $row = @($Candidate.parameters | Where-Object { [string]$_.name -eq $Name }) | Select-Object -First 1
    if ($null -eq $row) { return "" }
    return [string]$row.value
}

function Invoke-AnimalPurchaseStage(
    [int] $Ordinal,
    [string] $CandidateKind,
    [string] $TargetLocationId,
    [string] $ExpectedQueueOptionId) {
    $stageName = ("{0:D2}-{1}" -f $Ordinal, $CandidateKind)
    $stageDirectory = Join-Path $artifactDirectory ("stages\" + $stageName)
    $stageLoopRoot = Join-Path $stageDirectory "loop"
    $stageRunId = $RunId
    New-Item -ItemType Directory -Force -Path $stageDirectory | Out-Null

    $snapshot = Wait-WorldSnapshot 60
    $snapshotPath = Join-Path $stageDirectory "before-snapshot.json"
    Write-Json $snapshotPath $snapshot
    Invoke-RestMethod -Method Post -Uri "$backendUrl/api/v1/snapshots" `
        -ContentType "application/json; charset=utf-8" -Body (Get-Content -LiteralPath $snapshotPath -Raw) `
        -TimeoutSec 120 | Out-Null
    $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" ([ordered]@{
        state_hash = [string]$snapshot.state_hash
        candidate_option_ids = @("animals.purchase")
        candidates = @()
        include_executor_calibration_options = $true
    })
    Write-Json (Join-Path $stageDirectory "availability.json") $availability
    $candidates = @($availability.options | Where-Object { $_.option_id -eq "animals.purchase" } |
        ForEach-Object { $_.event_candidates })
    $candidate = @($candidates | Where-Object {
        [bool]$_.available -and [string]$_.kind -eq $CandidateKind -and
        ((Get-CandidateParameter $_ "continuation.target_location_id") -eq $TargetLocationId -or
         (Get-CandidateParameter $_ "target_location_id") -eq $TargetLocationId)
    }) | Select-Object -First 1
    if ($null -eq $candidate) {
        $observedKinds = @($candidates | Where-Object { [bool]$_.available } | ForEach-Object { [string]$_.kind } | Sort-Object -Unique)
        throw "No bound animals.purchase candidate for stage=$stageName target=$TargetLocationId; observed=$($observedKinds -join ',')"
    }

    $loopOutput = & dotnet (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll") `
        --root $stageLoopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorBaseUrl `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $stageRunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --skip-training `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options animals.purchase `
        --daily-plan-candidate-kind $CandidateKind `
        --daily-plan-candidate-id ([string]$candidate.candidate_id) `
        --after-snapshot-wait-ms 500
    $loopOutput | Set-Content -LiteralPath (Join-Path $stageDirectory "live-training-loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Animal purchase stage $stageName failed with exit code $LASTEXITCODE." }

    $snapshotDirectory = Join-Path $stageLoopRoot "runs\$stageRunId\live-snapshots"
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $queueItem = @($queue.items | Where-Object { $_.option_id -eq $ExpectedQueueOptionId }) | Select-Object -First 1
    $stepResult = @($execution.step_results | Where-Object { $_.option_id -eq $ExpectedQueueOptionId }) | Select-Object -Last 1
    $allApplied = @($execution.step_results).Count -gt 0 -and
        @($execution.step_results | Where-Object { $_.status -ne "applied" }).Count -eq 0
    $passed = $null -ne $queueItem -and $null -ne $stepResult -and $allApplied -and
        [string]$stepResult.primitive_verification_status -eq "verified"
    $stageSummary = [ordered]@{
        ordinal = $Ordinal
        candidate_kind = $CandidateKind
        candidate_id = [string]$candidate.candidate_id
        target_location_id = $TargetLocationId
        expected_queue_option_id = $ExpectedQueueOptionId
        status = if ($passed) { "passed" } else { "failed" }
        queue_option_ids = @($queue.items | ForEach-Object { [string]$_.option_id })
        execution_statuses = @($execution.step_results | ForEach-Object { [string]$_.option_id + ":" + [string]$_.status })
        verification_status = [string]$stepResult.primitive_verification_status
        verification_reasons = @($stepResult.primitive_verification_reasons)
        observed_effect = [string]$stepResult.observed_effect
        queue_path = $queuePath
        execution_path = $executionPath
    }
    Write-Json (Join-Path $stageDirectory "summary.json") $stageSummary
    if (-not $passed) { throw "Runtime animal purchase stage failed: $stageDirectory" }
    return [pscustomobject]$stageSummary
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorBaseUrl = "http://127.0.0.1:8767"
$executorUrl = "$executorBaseUrl/api/v1/training/execute"

if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI missing: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Runtime animal purchase smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-animal-purchase\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --no-restore --nologo -p:GamePath="$gameDirectory" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed." }

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
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile") -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    Wait-Json "$executorBaseUrl/health" 45 | Out-Null
    $initial = Wait-WorldSnapshot $StartupTimeoutSeconds

    $fixtureRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-animal-purchase-fixture"
        queue_item_id = "$RunId.fixture"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_animal_purchase"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        animal_type_id = "White Chicken"
        target_runtime_identity = "full_chain_paged"
    }
    $fixture = Invoke-JsonPost $executorUrl $fixtureRequest
    Write-Json (Join-Path $artifactDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Animal purchase fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $targetLocationId = "StardewAIAnimalPurchaseFixture7"
    $stages = @(
        Invoke-AnimalPurchaseStage 1 "interact_endpoint" $targetLocationId "executor.interact"
        Invoke-AnimalPurchaseStage 2 "animal_purchase_select_service" $targetLocationId "executor.choose_animal_purchase_response"
        Invoke-AnimalPurchaseStage 3 "animal_purchase_navigate_location_page" $targetLocationId "executor.choose_animal_purchase_response"
        Invoke-AnimalPurchaseStage 4 "animal_purchase_select_location" $targetLocationId "executor.choose_animal_purchase_response"
        Invoke-AnimalPurchaseStage 5 "purchase_animal" $targetLocationId "executor.purchase_animal"
    )
    $after = Wait-WorldSnapshot 30
    Write-Json (Join-Path $artifactDirectory "after-snapshot.json") $after
    $terminal = $stages[-1]
    $passed = @($stages).Count -eq 5 -and
        @($stages | Where-Object { $_.status -ne "passed" }).Count -eq 0 -and
        @($terminal.verification_reasons) -contains "native_PurchaseAnimalsMenu_stock_home_and_name_controls_used" -and
        @($terminal.verification_reasons) -contains "exact_new_animal_type_owner_home_name_occupancy_and_money_receipt_verified"
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_animal_purchase_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-247"
        run_id = $RunId
        save_slot = $SaveSlot
        scope = "full_rolling_counter_service_native_paging_location_selection_and_terminal_purchase"
        target_location_id = $targetLocationId
        stages = $stages
        final_location = [string]$after.state.player.location_id.value
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if (-not $passed) { throw "Runtime animal purchase smoke failed: $artifactDirectory" }
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
