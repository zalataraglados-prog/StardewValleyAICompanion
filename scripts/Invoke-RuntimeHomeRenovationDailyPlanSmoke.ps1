param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-home-renovation-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 5131,
    [string[]] $RenovationIds = @(),
    [switch] $FirstCaseOnly,
    [switch] $SkipNoRefundCase,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPostRaw([string] $Url, [string] $Json, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body $Json -TimeoutSec $TimeoutSeconds
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
    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Wait-HomeRenovationSnapshot([string] $RenovationId, [bool] $RefundEligible, [int] $TimeoutSeconds = 90) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $catalog = $snapshot.state.world_progress.marriage_house.value.home_renovations
            $row = @($catalog.options | Where-Object { [string]$_.renovation_id -eq $RenovationId }) | Select-Object -First 1
            $region = @($row.regions | Where-Object { [int]$_.selected_index -eq 0 }) | Select-Object -First 1
            $lastStatus = "projection=$($catalog.projection_status);service=$($catalog.service_status);option=$($row.availability_status);region=$($region.obstruction_status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                [string]$snapshot.state.player.location_id.value -eq "ScienceHouse" -and
                [string]$catalog.projection_status -eq "complete_live_native_home_renovation_catalog" -and
                [string]$catalog.data_contract_status -eq "exact_locked_base_1.6.15" -and
                [string]$catalog.service_status -eq "ready" -and
                $null -ne $row -and [bool]$row.native_menu_available -and
                [string]$row.availability_status -eq "available_in_native_renovation_shop" -and
                [bool]$row.refund_eligible -eq $RefundEligible -and
                $null -ne $region -and
                ([string]$region.obstruction_status -in @("clear", "native_obstruction_check_not_required"))) {
                return [pscustomobject]@{ Snapshot = $snapshot; Row = $row; Catalog = $catalog }
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for ready renovation '$RenovationId'. Last status: $lastStatus"
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Read-Parameter($QueueItem, [string] $Name) {
    [string](@($QueueItem.normalized_command.parameters) |
        Where-Object { [string]$_.name -eq $Name } |
        Select-Object -ExpandProperty value -First 1)
}

function Invoke-HomeRenovationCase([string] $RenovationId, [bool] $RefundEligible, [string] $Suffix) {
    $safeId = $RenovationId -replace '[^A-Za-z0-9._-]', '_'
    $caseName = $safeId + $Suffix
    $caseDirectory = Join-Path $artifactDirectory $caseName
    $loopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null

    $initial = Wait-WorldSnapshot 60
    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-home-renovation-fixture"
        queue_item_id = "runtime-home-renovation-fixture.$caseName"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_home_renovation"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        renovation_id = $RenovationId
        renovation_selected_index = 0
        renovation_refund_eligible = $RefundEligible
    }
    $setup = Invoke-JsonPost $executorUrl $setupRequest
    Write-Json (Join-Path $caseDirectory "setup-result.json") $setup
    if ($setup.status -ne "applied" -or $setup.primitive_verification_status -ne "verified") {
        throw "Home renovation fixture failed for ${caseName}: $(@($setup.block_reasons) -join ',')"
    }

    $ready = Wait-HomeRenovationSnapshot $RenovationId $RefundEligible
    $before = $ready.Snapshot
    $row = $ready.Row
    $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"
    Write-Json $snapshotPath $before
    Invoke-JsonPostRaw "$backendUrl/api/v1/snapshots" (Get-Content -LiteralPath $snapshotPath -Raw) | Out-Null

    $intent = @(
        [ordered]@{ name = "renovation_id"; value = $RenovationId },
        [ordered]@{ name = "selected_index"; value = "0" },
        [ordered]@{ name = "renovation_reason"; value = "isolated_native_runtime_verification" },
        [ordered]@{ name = "confirm_renovation"; value = "true" },
        [ordered]@{ name = "confirm_destructive"; value = if ([bool]$row.is_destructive) { "true" } else { "false" } }
    )
    $availabilityRequest = [ordered]@{
        state_hash = [string]$before.state_hash
        candidate_option_ids = @()
        candidates = @([ordered]@{
            option_id = "housing.renovate"
            parameters = $intent
            explicit_confirmation_granted = $true
            invocation_source = "PlayerCommand"
            actor_is_host = $true
            ownership_authorized = $true
            adapter_id = "vanilla_native"
        })
        include_executor_calibration_options = $false
    }
    $availability = Invoke-JsonPost "$backendUrl/api/v1/planner/options/availability" $availabilityRequest
    Write-Json (Join-Path $caseDirectory "availability.json") $availability
    $option = @($availability.options | Where-Object { $_.option_id -eq "housing.renovate" }) | Select-Object -First 1
    $candidate = @($option.event_candidates | Where-Object { [bool]$_.available }) | Select-Object -First 1
    if ($null -eq $option -or -not [bool]$option.available -or $null -eq $candidate -or [string]$candidate.kind -ne "renovate_home") {
        throw "No authorized renovation candidate for ${caseName}: $(@($option.blocking_reasons) -join ',')"
    }

    $loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @(
        $loopDll,
        "--root", $loopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $executorBaseUrl,
        "--snapshot-file", $snapshotPath,
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $savesPath,
        "--iterations", "1",
        "--skip-training",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "housing.renovate",
        "--daily-plan-invocation-source", "PlayerCommand",
        "--daily-plan-explicit-confirmation",
        "--daily-plan-candidate-kind", "renovate_home",
        "--daily-plan-candidate-id", [string]$candidate.candidate_id,
        "--after-snapshot-wait-ms", "750",
        "--max-queue-item-attempts", "1")) {
        $arguments.Add([string]$value)
    }
    foreach ($parameter in $intent) {
        $arguments.Add("--daily-plan-candidate-parameter")
        $arguments.Add([string]$parameter.name + "=" + [string]$parameter.value)
    }
    $loopOutput = & dotnet $arguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $caseDirectory "live-training-loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop failed for $caseName with exit code $LASTEXITCODE" }

    $snapshotDirectory = Join-Path $loopRoot ("runs\" + $RunId + "\live-snapshots")
    $dailyPlanPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $datasetPath = Join-Path $loopRoot "datasets\live-training-feature-rows.jsonl"
    foreach ($requiredPath in @($dailyPlanPath, $queuePath, $executionPath, $datasetPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required home renovation artifact missing: $requiredPath"
        }
    }

    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $queueItem = @($queue.items | Where-Object { $_.option_id -eq "executor.renovate_home" }) | Select-Object -First 1
    $stepResult = @($execution.step_results | Where-Object {
        $_.option_id -eq "executor.renovate_home" -and $_.queue_item_id -eq $queueItem.queue_item_id
    }) | Select-Object -First 1
    if ($null -eq $queueItem -or $null -eq $stepResult) {
        throw "Home renovation did not compile and execute its native primitive for $caseName."
    }

    $after = Wait-WorldSnapshot 60
    Write-Json (Join-Path $caseDirectory "after-snapshot.json") $after
    $moneyBefore = [int](Read-Parameter $queueItem "expected_money_before")
    $moneyAfter = [int](Read-Parameter $queueItem "expected_money_after")
    $price = [int](Read-Parameter $queueItem "price")
    $firstPurchaseBefore = [string](Read-Parameter $queueItem "first_purchase_mail_before")
    $firstPurchaseAfter = [string](Read-Parameter $queueItem "expected_first_purchase_mail_after")
    $datasetText = Get-Content -LiteralPath $datasetPath -Raw
    $passed = $null -ne $dailyPlan.plan -and
        [string]$dailyPlan.action_queue.status -eq "pending" -and
        [string]$queue.status -eq "pending" -and
        [string]$stepResult.status -eq "applied" -and
        [string]$stepResult.primitive_verification_status -eq "verified" -and
        @($stepResult.primitive_verification_reasons) -contains "native_Carpenter_Renovate_response_completed" -and
        @($stepResult.primitive_verification_reasons) -contains "exact_HouseRenovations_shop_order_and_row_completed" -and
        @($stepResult.primitive_verification_reasons) -contains "native_RenovateMenu_hover_and_world_region_click_completed" -and
        @($stepResult.primitive_verification_reasons) -contains "money_FirstPurchase_action_state_animation_and_return_verified" -and
        [int]$after.state.player.money.value -eq $moneyAfter -and
        $datasetText.Contains("executor.renovate_home")
    [ordered]@{
        case = $caseName
        renovation_id = $RenovationId
        refund_eligible = $RefundEligible
        destructive = [bool]$row.is_destructive
        passed = $passed
        candidate_id = [string]$candidate.candidate_id
        execution_status = [string]$stepResult.status
        verification_status = [string]$stepResult.primitive_verification_status
        verification_reasons = @($stepResult.primitive_verification_reasons)
        money_before = $moneyBefore
        money_after = [int]$after.state.player.money.value
        price = $price
        first_purchase_before = $firstPurchaseBefore
        first_purchase_after = $firstPurchaseAfter
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-home-renovation\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -c Release --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed." }

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
    $initial = Wait-WorldSnapshot 180

    $catalogSetupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-home-renovation-catalog-fixture"
        queue_item_id = "runtime-home-renovation-catalog-fixture"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_farmhouse_upgrade"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        expected_house_upgrade_level_before = 2
    }
    $catalogSetup = Invoke-JsonPost $executorUrl $catalogSetupRequest
    if ($catalogSetup.status -ne "applied") { throw "Unable to expose live renovation catalog." }
    $catalogSnapshot = Wait-WorldSnapshot 30
    $catalog = $catalogSnapshot.state.world_progress.marriage_house.value.home_renovations
    $liveIds = @($catalog.options | ForEach-Object { [string]$_.renovation_id })
    if ($liveIds.Count -ne 18 -or [string]$catalog.data_contract_status -ne "exact_locked_base_1.6.15") {
        throw "Live Data/HomeRenovations denominator drifted: count=$($liveIds.Count), status=$($catalog.data_contract_status)"
    }
    $selectedIds = if ($RenovationIds.Count -gt 0) { @($RenovationIds) } else { @($liveIds) }
    foreach ($id in $selectedIds) {
        if ($liveIds -notcontains $id) { throw "Requested renovation id is not in live catalog: $id" }
    }
    if ($FirstCaseOnly) { $selectedIds = @($selectedIds | Select-Object -First 1) }

    $caseResults = @()
    $caseNumber = 0
    foreach ($id in $selectedIds) {
        $caseNumber++
        Write-Host "[$caseNumber/$($selectedIds.Count)] home renovation: $id"
        $catalogRow = @($catalog.options | Where-Object { [string]$_.renovation_id -eq $id }) | Select-Object -First 1
        $caseResult = Invoke-HomeRenovationCase $id ([int]$catalogRow.price -lt 0) ""
        $caseResults += $caseResult
        Write-Host "[$caseNumber/$($selectedIds.Count)] result: $($caseResult.execution_status)/$($caseResult.verification_status)"
    }
    if (-not $SkipNoRefundCase -and -not $FirstCaseOnly -and $RenovationIds.Count -eq 0) {
        $negativeId = [string](@($catalog.options | Where-Object { [int]$_.price -lt 0 } | Select-Object -First 1).renovation_id)
        if ([string]::IsNullOrWhiteSpace($negativeId)) { throw "No negative-price renovation branch found." }
        $caseResults += Invoke-HomeRenovationCase $negativeId $false "-no-refund"
    }

    $passedCount = @($caseResults | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $caseResults.Count) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        live_catalog_count = $liveIds.Count
        expected_case_count = $caseResults.Count
        passed_case_count = $passedCount
        cases = $caseResults
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
    if ($passedCount -ne $caseResults.Count) { throw "Runtime home renovation matrix failed: $artifactDirectory" }
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
