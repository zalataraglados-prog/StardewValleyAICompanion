[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ArtifactDirectory,
    [string] $ProjectRoot = "",
    [int] $BackendPort = 8798,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
$planningDirectory = Join-Path $ArtifactDirectory "planning"
$runId = Split-Path -Leaf $ArtifactDirectory
$backendUrl = "http://127.0.0.1:$BackendPort"

function Require-File {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required resume artifact is missing: $Path"
    }
    return [IO.Path]::GetFullPath($Path)
}

function Write-Utf8Text {
    param([string] $Path, [string] $Value)
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText(
        $Path,
        $Value,
        [Text.UTF8Encoding]::new($false))
}

function Write-JsonFile {
    param([string] $Path, $Value)
    Write-Utf8Text -Path $Path -Value (
        $Value | ConvertTo-Json -Depth 96)
}

function Wait-Json {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                return $response.Content | ConvertFrom-Json
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Invoke-JsonPost {
    param([string] $Url, $Body)
    $json = if ($Body -is [string]) {
        $Body
    }
    else {
        $Body | ConvertTo-Json -Depth 96
    }
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
        -Method Post -ContentType "application/json" -Body $json `
        -TimeoutSec 120
    return [pscustomobject]@{
        Raw = $response.Content
        Value = $response.Content | ConvertFrom-Json
    }
}

$backendProject = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\StardewAI.Backend.csproj"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
if (-not $SkipBuild) {
    & dotnet build $backendProject -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Backend build failed." }
    & dotnet build $bootstrapProject -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Bootstrap build failed." }
}
$backendDll = Require-File (Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll")
$bootstrapDll = Require-File (Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll")

function Invoke-Bootstrap {
    param([string[]] $Arguments)
    & dotnet $bootstrapDll @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Bootstrap command failed: $($Arguments[0])"
    }
}

$initialSnapshotPath = Require-File (Join-Path $ArtifactDirectory `
    "recurrence-initial-snapshot.json")
$afterSnapshotPath = Require-File (Join-Path $ArtifactDirectory `
    "01-acquire-sample\runs\$runId\live-snapshots\after-snapshot-0001.json")
$executionReceiptPath = Require-File (Join-Path $ArtifactDirectory `
    "01-acquire-sample\runs\$runId\live-snapshots\execution-0001.json")
$requirementInventoryPath = Require-File (Join-Path $ProjectRoot `
    "experiments\local-data\output\authoritative-requirement-inventory-v1.json")
$acquisitionLoweringPath = Require-File (Join-Path $ProjectRoot `
    "experiments\local-data\output\acquisition-route-option-lowering-v1.json")
$masterAnglerWindowsPath = Require-File (Join-Path $ProjectRoot `
    "experiments\local-data\output\master-angler-stage-one-window-index-v1.json")
$routeTimingPath = Require-File `
    "I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json"

$paths = @{
    calendar = Require-File (Join-Path $planningDirectory `
        "calendar-resolution.json")
    target_calendar = Require-File (Join-Path $planningDirectory `
        "target-date-calendar.json")
    unlock = Require-File (Join-Path $planningDirectory `
        "target-date-unlock.json")
    festival = Require-File (Join-Path $planningDirectory `
        "target-date-festival.json")
    location = Require-File (Join-Path $planningDirectory `
        "target-date-location.json")
    facility = Require-File (Join-Path $planningDirectory `
        "target-date-facility.json")
    resource = Require-File (Join-Path $planningDirectory `
        "target-date-resource.json")
    currency = Require-File (Join-Path $planningDirectory `
        "target-date-currency.json")
    reservation = Require-File (Join-Path $planningDirectory `
        "target-date-reservation.json")
    processing = Require-File (Join-Path $planningDirectory `
        "target-date-processing.json")
    fishing_probability = Require-File (Join-Path $planningDirectory `
        "target-date-fishing-probability.json")
    stochastic_retry = Require-File (Join-Path $planningDirectory `
        "target-date-stochastic-retry.json")
    daily_time_energy = Require-File (Join-Path $planningDirectory `
        "target-date-daily-time-energy.json")
    opportunity_cost = Require-File (Join-Path $planningDirectory `
        "target-date-opportunity-cost.json")
    forecast = Require-File (Join-Path $planningDirectory `
        "fishing-forecast-manifest.json")
    strategy_ledger = Require-File (Join-Path $planningDirectory `
        "strategy-ledger-before-commit.json")
    proposal = Require-File (Join-Path $planningDirectory `
        "portfolio-selected-proposal.json")
    admission = Require-File (Join-Path $planningDirectory `
        "portfolio-selected-admission.json")
    preference_request = Require-File (Join-Path $planningDirectory `
        "portfolio-preference-request.json")
    teacher_preference = Require-File (Join-Path $planningDirectory `
        "portfolio-teacher-preference.json")
    commit_receipt = Require-File (Join-Path $planningDirectory `
        "portfolio-commit-receipt.json")
    committed_ledger = Require-File (Join-Path $planningDirectory `
        "committed-strategy-ledger.json")
    commit_result = Require-File (Join-Path $planningDirectory `
        "portfolio-commit-result.json")
    action_queue = Require-File (Join-Path $planningDirectory `
        "action-queue.json")
    execution_binding = Require-File (Join-Path $planningDirectory `
        "execution-binding.json")
    fresh_terminal = Require-File (Join-Path $planningDirectory `
        "fresh-terminal-receipt.json")
}

$binding = Get-Content -LiteralPath $paths.execution_binding -Raw |
    ConvertFrom-Json
$routeOccurrenceId = [string]$binding.route_occurrence_id
if ([string]::IsNullOrWhiteSpace($routeOccurrenceId)) {
    throw "Execution binding has no route occurrence id."
}
$requirementId = [string]$binding.requirement_id
$qualifiedItemId = [string]$binding.qualified_item_id
$routeKind = [string]$binding.route_kind
if ([string]::IsNullOrWhiteSpace($requirementId) -or
    [string]::IsNullOrWhiteSpace($qualifiedItemId) -or
    [string]::IsNullOrWhiteSpace($routeKind)) {
    throw "Execution binding has incomplete acquisition identity."
}
$scenario = switch ("$requirementId|$qualifiedItemId|$routeKind") {
    "full_shipment:item:24|(O)24|harvests_as" {
        "parsnip_harvest_sample"
    }
    "full_shipment:item:296|(O)296|native_bush_shake" {
        "berry_bush_harvest_sample"
    }
    "full_shipment:item:829|(O)829|native_ginger_harvest" {
        "ginger_harvest_sample"
    }
    "full_shipment:item:815|(O)815|native_tea_bush_harvest" {
        "tea_bush_harvest_sample"
    }
    "full_shipment:item:408|(O)408|native_wild_tree_seed_drop" {
        "wild_tree_seed_drop_sample"
    }
    "full_shipment:item:88|(O)88|native_wild_tree_seed" {
        "wild_tree_seed_sample"
    }
    "full_shipment:item:399|(O)399|native_spring_onion_harvest" {
        "spring_onion_harvest_sample"
    }
    "full_shipment:item:16|(O)16|native_location_forage_spawn" {
        "location_forage_spawn_sample"
    }
    "full_shipment:item:638|(O)638|native_fruit_tree_produce" {
        "fruit_tree_harvest_sample"
    }
    "full_shipment:item:184|(O)184|native_farm_animal_produce" {
        "farm_animal_product_sample"
    }
    "full_shipment:item:186|(O)186|native_farm_animal_deluxe_produce" {
        "farm_animal_deluxe_product_sample"
    }
    default {
        throw "No acquisition sample scenario maps execution binding " +
            "'$requirementId|$qualifiedItemId|$routeKind'."
    }
}

$executionCommon = @(
    "--requirement-inventory", $requirementInventoryPath,
    "--acquisition-lowering", $acquisitionLoweringPath,
    "--master-angler-windows", $masterAnglerWindowsPath,
    "--calendar-resolution", $paths.calendar,
    "--target-date-calendar", $paths.target_calendar,
    "--target-date-unlock", $paths.unlock,
    "--target-date-festival", $paths.festival,
    "--target-date-location", $paths.location,
    "--target-date-facility", $paths.facility,
    "--target-date-resource", $paths.resource,
    "--target-date-currency", $paths.currency,
    "--target-date-reservation", $paths.reservation,
    "--target-date-processing", $paths.processing,
    "--target-date-fishing-probability", $paths.fishing_probability,
    "--target-date-stochastic-retry", $paths.stochastic_retry,
    "--target-date-daily-time-energy", $paths.daily_time_energy,
    "--target-date-opportunity-cost", $paths.opportunity_cost,
    "--fishing-forecast-manifest", $paths.forecast,
    "--strategy-ledger", $paths.strategy_ledger,
    "--snapshot", $initialSnapshotPath,
    "--route-timing-calibration", $routeTimingPath,
    "--portfolio-proposal", $paths.proposal,
    "--portfolio-admission", $paths.admission,
    "--portfolio-preference-request", $paths.preference_request,
    "--portfolio-teacher-preference", $paths.teacher_preference,
    "--portfolio-commit-receipt", $paths.commit_receipt,
    "--committed-strategy-ledger", $paths.committed_ledger,
    "--portfolio-commit-result", $paths.commit_result,
    "--route-occurrence-id", $routeOccurrenceId
)

$settlementRequestPath = Join-Path $planningDirectory `
    "settlement-request.json"
$settlementResultPath = Join-Path $planningDirectory `
    "settlement-result.json"
$settledLedgerPath = Join-Path $planningDirectory `
    "settled-strategy-ledger.json"
$settlementReceiptPath = Join-Path $planningDirectory `
    "settlement-receipt.json"
$checkpointPath = Join-Path $planningDirectory "rollout-checkpoint.json"
$rolloutManifestPath = Join-Path $planningDirectory `
    "rollout-proof-manifest.json"
$rolloutReceiptPath = Join-Path $planningDirectory `
    "rollout-proof-receipt.json"

$afterSnapshot = Get-Content -LiteralPath $afterSnapshotPath -Raw |
    ConvertFrom-Json
$saveId = [string]$afterSnapshot.save_id.value
$playerId = [string]$afterSnapshot.player_id.value
if ([string]::IsNullOrWhiteSpace($saveId) -or
    [string]::IsNullOrWhiteSpace($playerId)) {
    throw "After snapshot has no available save/player identity."
}
$identityBytes = [Text.Encoding]::UTF8.GetBytes("$saveId`n$playerId")
$identityHasher = [Security.Cryptography.SHA256]::Create()
try {
    $ledgerFileName = [BitConverter]::ToString(
        $identityHasher.ComputeHash($identityBytes)
    ).Replace("-", "").ToLowerInvariant() + ".json"
}
finally {
    $identityHasher.Dispose()
}
$resumeLedgerRoot = Join-Path $planningDirectory "resume-strategy-ledger"
New-Item -ItemType Directory -Force -Path $resumeLedgerRoot | Out-Null
Copy-Item -LiteralPath $paths.committed_ledger `
    -Destination (Join-Path $resumeLedgerRoot $ledgerFileName) -Force

if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $BackendPort `
        -ErrorAction SilentlyContinue)) {
    throw "Resume proof requires unused backend port $BackendPort."
}

$backend = $null
try {
    $env:ASPNETCORE_URLS = $backendUrl
    $env:STARDEWAI_STRATEGY_LEDGER_DIR = $resumeLedgerRoot
    $backend = Start-Process dotnet -ArgumentList @($backendDll) `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $ArtifactDirectory `
            "resume-backend.stdout.log") `
        -RedirectStandardError (Join-Path $ArtifactDirectory `
            "resume-backend.stderr.log") -PassThru
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $afterSnapshotRaw = Get-Content -LiteralPath $afterSnapshotPath -Raw
    Invoke-JsonPost -Url "$backendUrl/api/v1/snapshots?profile=full" `
        -Body $afterSnapshotRaw | Out-Null

    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-settlement-request-from-verified-artifacts"
    ) + $executionCommon + @(
        "--action-queue", $paths.action_queue,
        "--execution-binding", $paths.execution_binding,
        "--execution-receipt", $executionReceiptPath,
        "--after-snapshot", $afterSnapshotPath,
        "--fresh-terminal-receipt", $paths.fresh_terminal,
        "--run-id", $runId,
        "--executor-version", "product_executor.v1",
        "--output", $settlementRequestPath
    ))

    $settlementRequest = Get-Content -LiteralPath $settlementRequestPath `
        -Raw | ConvertFrom-Json
    $settlementResponse = Invoke-JsonPost -Url (
        "$backendUrl/api/v1/strategy/commitments/" +
        "reservation-portfolios/settle-completed-route") `
        -Body $settlementRequest
    Write-Utf8Text -Path $settlementResultPath `
        -Value $settlementResponse.Raw
    Write-JsonFile -Path $settledLedgerPath `
        -Value $settlementResponse.Value.ledger

    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-settlement-receipt-from-verified-artifacts"
    ) + $executionCommon + @(
        "--action-queue", $paths.action_queue,
        "--execution-binding", $paths.execution_binding,
        "--execution-receipt", $executionReceiptPath,
        "--after-snapshot", $afterSnapshotPath,
        "--fresh-terminal-receipt", $paths.fresh_terminal,
        "--run-id", $runId,
        "--executor-version", "product_executor.v1",
        "--settlement-request", $settlementRequestPath,
        "--settlement-result", $settlementResultPath,
        "--settled-ledger", $settledLedgerPath,
        "--output", $settlementReceiptPath
    ))

    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-rollout-checkpoint-from-verified-artifacts"
    ) + $executionCommon + @(
        "--action-queue", $paths.action_queue,
        "--execution-binding", $paths.execution_binding,
        "--execution-receipt", $executionReceiptPath,
        "--after-snapshot", $afterSnapshotPath,
        "--fresh-terminal-receipt", $paths.fresh_terminal,
        "--run-id", $runId,
        "--executor-version", "product_executor.v1",
        "--settlement-request", $settlementRequestPath,
        "--settlement-result", $settlementResultPath,
        "--settled-ledger", $settledLedgerPath,
        "--settlement-receipt", $settlementReceiptPath,
        "--output", $checkpointPath
    ))

    $executionInputs = [ordered]@{
        requirement_inventory_path = $requirementInventoryPath
        acquisition_lowering_path = $acquisitionLoweringPath
        master_angler_windows_path = $masterAnglerWindowsPath
        calendar_resolution_path = $paths.calendar
        target_date_calendar_path = $paths.target_calendar
        target_date_unlock_path = $paths.unlock
        target_date_festival_path = $paths.festival
        target_date_location_path = $paths.location
        target_date_facility_path = $paths.facility
        target_date_resource_path = $paths.resource
        target_date_currency_path = $paths.currency
        target_date_reservation_path = $paths.reservation
        target_date_processing_path = $paths.processing
        target_date_fishing_probability_path = $paths.fishing_probability
        target_date_stochastic_retry_path = $paths.stochastic_retry
        target_date_daily_time_energy_path = $paths.daily_time_energy
        target_date_opportunity_cost_path = $paths.opportunity_cost
        fishing_forecast_manifest_path = $paths.forecast
        strategy_ledger_path = $paths.strategy_ledger
        before_snapshot_path = $initialSnapshotPath
        route_timing_calibration_path = $routeTimingPath
        portfolio_proposal_path = $paths.proposal
        portfolio_admission_path = $paths.admission
        portfolio_preference_request_path = $paths.preference_request
        portfolio_teacher_preference_path = $paths.teacher_preference
        portfolio_commit_receipt_path = $paths.commit_receipt
        committed_strategy_ledger_path = $paths.committed_ledger
        portfolio_commit_result_path = $paths.commit_result
        action_queue_path = $paths.action_queue
        route_occurrence_id = $routeOccurrenceId
    }
    Write-JsonFile -Path $rolloutManifestPath -Value ([ordered]@{
        schema_version =
            "acquisition_route_portfolio_rollout_proof_manifest.v1"
        initial_checkpoint_proof = [ordered]@{
            execution_inputs = $executionInputs
            execution_binding_path = $paths.execution_binding
            execution_receipt_path = $executionReceiptPath
            after_snapshot_path = $afterSnapshotPath
            fresh_terminal_receipt_path = $paths.fresh_terminal
            run_id = $runId
            executor_version = "product_executor.v1"
            settlement_request_path = $settlementRequestPath
            settlement_result_path = $settlementResultPath
            settled_ledger_path = $settledLedgerPath
            settlement_receipt_path = $settlementReceiptPath
        }
        initial_checkpoint_path = $checkpointPath
        continuation_transitions = @()
        formal_training_authorized = $false
    })

    Invoke-Bootstrap @(
        "build-acquisition-route-portfolio-rollout-proof-receipt",
        "--rollout-proof-manifest", $rolloutManifestPath,
        "--output", $rolloutReceiptPath
    )

    $receipt = Get-Content -LiteralPath $rolloutReceiptPath -Raw |
        ConvertFrom-Json
    if (-not [bool]$receipt.proof_chain_verified -or
        -not [bool]$receipt.portfolio_completion_verified -or
        [bool]$receipt.formal_training_authorized) {
        throw "Resumed acquisition rollout proof is incomplete."
    }
    Write-JsonFile -Path (Join-Path $ArtifactDirectory "summary.json") `
        -Value ([ordered]@{
            schema_version =
                "stardewai.runtime_full_shipment_acquisition_sample.v1"
            status = "passed"
            scenario = $scenario
            run_id = $runId
            fixture_excluded_from_proof_root = $true
            initial_state_hash = [string](
                (Get-Content -LiteralPath $initialSnapshotPath -Raw |
                    ConvertFrom-Json).state_hash)
            requirement_id = $requirementId
            qualified_item_id = $qualifiedItemId
            acquisition_route_occurrence_id = $routeOccurrenceId
            rollout_id = [string]$receipt.rollout_id
            rollout_proof_manifest_path = $rolloutManifestPath
            rollout_proof_receipt_path = $rolloutReceiptPath
            resumed_from_verified_execution_artifacts = $true
        })
    Get-Content -LiteralPath (Join-Path $ArtifactDirectory "summary.json") -Raw
}
finally {
    if ($null -ne $backend -and -not $backend.HasExited) {
        Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue
        $backend.WaitForExit(10000) | Out-Null
    }
    Remove-Item Env:STARDEWAI_STRATEGY_LEDGER_DIR -ErrorAction SilentlyContinue
}
