[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory)]
    [string] $WorkPlanPath,
    [Parameter(Mandatory)]
    [string] $SourceSaveSlotPath,
    [Parameter(Mandatory)]
    [ValidateSet("train", "validation", "test")]
    [string] $DatasetPartition,
    [string] $RunId =
        ("money-native-wave-" + $DatasetPartition + "-" +
            (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $RunRoot =
        "artifacts\runtime-money-threshold-native-outcome",
    [string] $BatchId = "money-threshold-production-native-v1",
    [string] $BatchOutputDirectory =
        "artifacts\money-threshold-production-native-batch",
    [string] $ComparisonAdmissionIndex = "",
    [string] $CoverageRequestTemplate = "",
    [string] $CoverageRequestOutput = "",
    [string] $CoverageSourceId = "money-threshold-production-corpus",
    [int] $BackendPort = 5132,
    [int] $StartupTimeoutSeconds = 180,
    [int] $NaturalWaitTimeoutSeconds = 360,
    [switch] $SkipBuild,
    [switch] $SkipDeploy
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Read-CollectionWorkPlan {
    param([Parameter(Mandatory)] [string] $Path)
    $fullPath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Money threshold evidence work plan is missing: $fullPath"
    }
    $plan = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    if ([string]$plan.schema_version -ne
            "money_threshold_evidence_work_plan.v1" -or
        [bool]$plan.formal_product_training_authorized -or
        [int]$plan.required_cell_count -ne 18) {
        throw "Money threshold evidence work plan is invalid."
    }
    return [pscustomobject]@{
        Path = $fullPath
        Value = $plan
    }
}

$workPlanCapture = Read-CollectionWorkPlan -Path $WorkPlanPath
$workPlan = $workPlanCapture.Value
$waves = @($workPlan.native_collection_waves | Where-Object {
    [string]$_.dataset_partition -eq $DatasetPartition
})
if ($waves.Count -ne 1) {
    throw "Expected exactly one native collection wave for partition " +
        "$DatasetPartition; found $($waves.Count)."
}
$wave = $waves[0]
$missingCriterionIds = @($wave.missing_criterion_ids |
    ForEach-Object { [string]$_ })
if ($missingCriterionIds.Count -eq 0 -or
    @($missingCriterionIds | Sort-Object -Unique).Count -ne
        $missingCriterionIds.Count -or
    $missingCriterionIds -notcontains
        [string]$wave.recommended_criterion_id -or
    [string]$wave.status -ne
        "conditional_native_outcome_collection_required") {
    throw "Money native collection wave is invalid."
}
foreach ($criterionId in $missingCriterionIds) {
    $cells = @($workPlan.cells | Where-Object {
        [string]$_.dataset_partition -eq $DatasetPartition -and
        [string]$_.criterion_id -eq $criterionId
    })
    if ($cells.Count -ne 1 -or [bool]$cells[0].native_outcome_verified) {
        throw "Money native collection wave disagrees with its cell: " +
            "$DatasetPartition/$criterionId."
    }
}

$collectorArguments = @{
    ProjectRoot = $ProjectRoot
    RuntimeRoot = $RuntimeRoot
    SourceSaveSlotPath = $SourceSaveSlotPath
    CollectionWaveWorkPlanPath = $workPlanCapture.Path
    DatasetPartition = $DatasetPartition
    RunId = $RunId
    OutputDirectory = $RunRoot
    BackendPort = $BackendPort
    StartupTimeoutSeconds = $StartupTimeoutSeconds
    NaturalWaitTimeoutSeconds = $NaturalWaitTimeoutSeconds
}
if ($SkipBuild) { $collectorArguments.SkipBuild = $true }
if ($SkipDeploy) { $collectorArguments.SkipDeploy = $true }
& (Join-Path $PSScriptRoot `
    "Invoke-RuntimeMoneyThresholdNativeOutcome.ps1") `
    @collectorArguments | Out-Null
$runRootPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $RunRoot
$runSummaryPath = Join-Path (Join-Path $runRootPath $RunId) "summary.json"
if (-not (Test-Path -LiteralPath $runSummaryPath -PathType Leaf)) {
    throw "Money native collection run summary is missing: $runSummaryPath"
}
$runSummary = Get-Content -LiteralPath $runSummaryPath -Raw |
    ConvertFrom-Json

$batchArguments = @{
    ProjectRoot = $ProjectRoot
    RunRoot = $RunRoot
    BatchId = $BatchId
    OutputDirectory = $BatchOutputDirectory
    SkipBuild = $true
}
if (-not [string]::IsNullOrWhiteSpace($ComparisonAdmissionIndex)) {
    $batchArguments.ComparisonAdmissionIndex = $ComparisonAdmissionIndex
}
if (-not [string]::IsNullOrWhiteSpace($CoverageRequestTemplate)) {
    $batchArguments.CoverageRequestTemplate = $CoverageRequestTemplate
    $batchArguments.CoverageRequestOutput = $CoverageRequestOutput
    $batchArguments.CoverageSourceId = $CoverageSourceId
}
$batchOutput = & (Join-Path $PSScriptRoot `
    "Update-MoneyThresholdNativeOutcomeBatch.ps1") @batchArguments |
    Out-String
$batch = $batchOutput | ConvertFrom-Json
$refreshedCapture = Read-CollectionWorkPlan -Path ([string]$batch.work_plan_path)
$refreshedPlan = $refreshedCapture.Value
$closed = @($missingCriterionIds | Where-Object {
    $criterionId = $_
    @($refreshedPlan.cells | Where-Object {
        [string]$_.dataset_partition -eq $DatasetPartition -and
        [string]$_.criterion_id -eq $criterionId -and
        [bool]$_.native_outcome_verified
    }).Count
})
if ($closed.Count -eq 0) {
    throw "Accepted native run did not close any scheduled wave cell."
}
$remaining = @($missingCriterionIds | Where-Object { $closed -notcontains $_ })

[ordered]@{
    schema_version = "money_threshold_native_collection_wave_run.v1"
    status = "passed"
    run_id = $RunId
    dataset_partition = $DatasetPartition
    criterion_id = [string]$runSummary.criterion_id
    closed_criterion_ids = $closed
    remaining_criterion_ids = $remaining
    source_work_plan_path = $workPlanCapture.Path
    refreshed_work_plan_path = $refreshedCapture.Path
    native_complete_cell_count =
        [int]$refreshedPlan.native_outcome_complete_cell_count
    fully_complete_cell_count = [int]$refreshedPlan.fully_complete_cell_count
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
