[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $WorkPlanPath,
    [Parameter(Mandatory)]
    [ValidateSet("train", "validation", "test")]
    [string] $DatasetPartition,
    [Parameter(Mandatory)]
    [string] $SourceRowId,
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RunRoot =
        "artifacts\runtime-money-threshold-native-outcome",
    [string] $OutputRoot =
        "artifacts\money-revenue-comparison-work",
    [string] $AcquisitionRolloutIndex = "",
    [string] $SourceStrategyLedgerPath = "",
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Resolve-RequiredFile {
    param([Parameter(Mandatory)] [string] $Path, [string] $Label)
    $fullPath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Label is missing: $fullPath"
    }
    return $fullPath
}

$sourceWorkPlanPath = Resolve-RequiredFile `
    -Path $WorkPlanPath -Label "Money threshold evidence work plan"
$workPlan = Get-Content -LiteralPath $sourceWorkPlanPath -Raw |
    ConvertFrom-Json
if ([string]$workPlan.schema_version -ne
        "money_threshold_evidence_work_plan.v1" -or
    [bool]$workPlan.formal_product_training_authorized -or
    [int]$workPlan.required_cell_count -ne 18) {
    throw "Money threshold evidence work plan is invalid."
}
$waves = @($workPlan.comparison_collection_waves | Where-Object {
    [string]$_.dataset_partition -eq $DatasetPartition -and
    [string]$_.source_row_id -eq $SourceRowId
})
if ($waves.Count -ne 1) {
    throw "Expected one comparison wave for " +
        "$DatasetPartition/$SourceRowId; found $($waves.Count)."
}
$wave = $waves[0]
$criterionIds = @($wave.missing_criterion_ids |
    ForEach-Object { [string]$_ })
if ($criterionIds.Count -eq 0 -or
    @($criterionIds | Sort-Object -Unique).Count -ne $criterionIds.Count -or
    [string]$wave.status -ne "exact_comparison_collection_required") {
    throw "Money threshold comparison wave is invalid."
}

$corpusPath = Resolve-RequiredFile `
    -Path ([string]$workPlan.money_threshold_corpus_path) `
    -Label "Money threshold corpus"
$corpusSha256 = (Get-FileHash -LiteralPath $corpusPath `
    -Algorithm SHA256).Hash.ToLowerInvariant()
if ($corpusSha256 -ne
    [string]$workPlan.money_threshold_corpus_sha256) {
    throw "Money threshold corpus drifted from the evidence work plan."
}
$corpus = Get-Content -LiteralPath $corpusPath -Raw | ConvertFrom-Json
$rows = @($corpus.rows | Where-Object {
    [string]$_.row_id -eq $SourceRowId
})
if ($rows.Count -ne 1) {
    throw "Comparison wave source row is absent from the Money corpus."
}
$row = $rows[0]
$rowCriterionIds = @($row.crossed_thresholds |
    ForEach-Object { [string]$_.criterion_id })
if ([string]$row.source_id -ne [string]$wave.source_id -or
    [string]$row.dataset_partition -ne $DatasetPartition -or
    -not [bool]$row.native_terminal_outcome_verified -or
    @($criterionIds | Where-Object {
        $rowCriterionIds -notcontains $_
    }).Count -gt 0) {
    throw "Comparison wave does not bind its verified native corpus row."
}

$runRootPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $RunRoot
$sourceId = [string]$wave.source_id
if ([string]::IsNullOrWhiteSpace($sourceId) -or
    [IO.Path]::GetFileName($sourceId) -ne $sourceId -or
    $sourceId -in @(".", "..")) {
    throw "Money comparison wave source ID is not a safe run directory name."
}
$runDirectory = [IO.Path]::GetFullPath(
    (Join-Path $runRootPath $sourceId))
$runRootPrefix = [IO.Path]::GetFullPath($runRootPath).TrimEnd('\') + '\'
if (-not $runDirectory.StartsWith(
        $runRootPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Money comparison wave source ID escapes the native run root."
}
$runSummaryPath = Join-Path $runDirectory "summary.json"
$runSummaryPath = Resolve-RequiredFile `
    -Path $runSummaryPath -Label "Money native run summary"
$runSummary = Get-Content -LiteralPath $runSummaryPath -Raw |
    ConvertFrom-Json
if ([string]$runSummary.schema_version -ne
        "money_threshold_native_outcome_run_summary.v2" -or
    [string]$runSummary.status -ne "passed" -or
    [string]$runSummary.run_id -ne [string]$wave.source_id -or
    [string]$runSummary.dataset_partition -ne $DatasetPartition) {
    throw "Comparison wave native run summary identity is invalid."
}

$initializeArguments = @{
    RunSummaryPath = $runSummaryPath
    ProjectRoot = $ProjectRoot
    PlanId = $sourceId + "-comparison-wave"
    OutputRoot = $OutputRoot
}
if (-not [string]::IsNullOrWhiteSpace($AcquisitionRolloutIndex)) {
    $initializeArguments.AcquisitionRolloutIndex = $AcquisitionRolloutIndex
}
if (-not [string]::IsNullOrWhiteSpace($SourceStrategyLedgerPath)) {
    $initializeArguments.SourceStrategyLedgerPath =
        $SourceStrategyLedgerPath
}
if ($SkipBuild) { $initializeArguments.SkipBuild = $true }
$initializationOutput = & (Join-Path $PSScriptRoot `
    "Initialize-MoneyRevenueComparisonWork.ps1") @initializeArguments |
    Out-String
$initialization = $initializationOutput | ConvertFrom-Json

$comparisonRoot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputRoot
$workDirectory = Join-Path $comparisonRoot $sourceId
$comparisonPlanPath = Resolve-RequiredFile `
    -Path (Join-Path $workDirectory `
        "money-revenue-comparison-collection-plan.json") `
    -Label "Money Revenue comparison collection plan"
$comparisonPlan = Get-Content -LiteralPath $comparisonPlanPath -Raw |
    ConvertFrom-Json
$comparisonCriterionIds = @($comparisonPlan.crossed_criterion_ids |
    ForEach-Object { [string]$_ })
if ([string]$comparisonPlan.run_id -ne [string]$wave.source_id -or
    [string]$comparisonPlan.dataset_partition -ne $DatasetPartition -or
    @($criterionIds | Where-Object {
        $comparisonCriterionIds -notcontains $_
    }).Count -gt 0) {
    throw "Comparison collection plan does not cover its scheduled wave."
}

$workPlanSnapshotPath = Join-Path $workDirectory `
    "source-money-threshold-evidence-work-plan.json"
$corpusSnapshotPath = Join-Path $workDirectory `
    "source-money-threshold-corpus.json"
Copy-Item -LiteralPath $sourceWorkPlanPath `
    -Destination $workPlanSnapshotPath -Force
Copy-Item -LiteralPath $corpusPath `
    -Destination $corpusSnapshotPath -Force
$workPlanSnapshotSha256 =
    (Get-FileHash -LiteralPath $workPlanSnapshotPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
$corpusSnapshotSha256 =
    (Get-FileHash -LiteralPath $corpusSnapshotPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceWorkPlanSha256 =
    (Get-FileHash -LiteralPath $sourceWorkPlanPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
if ($workPlanSnapshotSha256 -ne $sourceWorkPlanSha256 -or
    $corpusSnapshotSha256 -ne $corpusSha256) {
    throw "Comparison-wave scheduling snapshot copy drifted from its source."
}

$handoffPath = Join-Path $workDirectory `
    "money-revenue-comparison-wave-handoff.json"
Write-JsonFile -Path $handoffPath -Value ([ordered]@{
    schema_version = "money_revenue_comparison_wave_handoff.v1"
    status = "comparison_wave_initialized"
    source_work_plan_path = $workPlanSnapshotPath
    source_work_plan_sha256 = $workPlanSnapshotSha256
    source_corpus_path = $corpusSnapshotPath
    source_corpus_sha256 = $corpusSnapshotSha256
    origin_work_plan_path = $sourceWorkPlanPath
    origin_work_plan_sha256 = $sourceWorkPlanSha256
    origin_corpus_path = $corpusPath
    origin_corpus_sha256 = $corpusSha256
    source_row_id = $SourceRowId
    source_id = $sourceId
    dataset_partition = $DatasetPartition
    scheduled_criterion_ids = $criterionIds
    native_run_summary_path = $runSummaryPath
    native_run_summary_sha256 =
        (Get-FileHash -LiteralPath $runSummaryPath `
            -Algorithm SHA256).Hash.ToLowerInvariant()
    work_directory = $workDirectory
    comparison_collection_plan_path = $comparisonPlanPath
    comparison_collection_plan_sha256 =
        (Get-FileHash -LiteralPath $comparisonPlanPath `
            -Algorithm SHA256).Hash.ToLowerInvariant()
    concrete_denominator_status =
        [string]$initialization.concrete_denominator_status
    counterfactual_request_status =
        [string]$initialization.counterfactual_request_status
    formal_product_training_authorized = $false
})

[ordered]@{
    status = "comparison_wave_initialized"
    source_id = $sourceId
    source_row_id = $SourceRowId
    dataset_partition = $DatasetPartition
    scheduled_criterion_ids = $criterionIds
    work_directory = $workDirectory
    comparison_wave_handoff_path = $handoffPath
    concrete_denominator_status =
        [string]$initialization.concrete_denominator_status
    counterfactual_request_status =
        [string]$initialization.counterfactual_request_status
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
