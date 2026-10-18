[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RunRoot =
        "artifacts\runtime-money-threshold-native-outcome",
    [string] $BatchId = "money-threshold-production-native-v1",
    [string] $OutputDirectory =
        "artifacts\money-threshold-production-native-batch",
    [string] $ComparisonAdmissionIndex = "",
    [string] $CoverageRequestTemplate = "",
    [string] $CoverageRequestOutput = "",
    [string] $CoverageSourceId = "money-threshold-production-corpus",
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

$runRootPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $RunRoot
$outputRoot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputDirectory
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$batchRequestPath = Join-Path $outputRoot `
    "money-threshold-native-outcome-batch-request.json"
$batchPath = Join-Path $outputRoot `
    "money-threshold-native-outcome-batch.json"
$corpusPath = Join-Path $outputRoot `
    "money-threshold-native-outcome-corpus.json"
$workPlanRequestPath = Join-Path $outputRoot `
    "money-threshold-evidence-work-plan-request.json"
$workPlanPath = Join-Path $outputRoot `
    "money-threshold-evidence-work-plan.json"
$coverageAttachmentManifestPath = Join-Path $outputRoot `
    "money-threshold-coverage-request-manifest.json"

if (-not [string]::IsNullOrWhiteSpace($CoverageRequestOutput) -and
    [string]::IsNullOrWhiteSpace($CoverageRequestTemplate)) {
    throw "CoverageRequestOutput requires CoverageRequestTemplate."
}
if (-not [string]::IsNullOrWhiteSpace($CoverageRequestTemplate) -and
    [string]::IsNullOrWhiteSpace($CoverageSourceId)) {
    throw "CoverageSourceId is required when attaching the Money corpus."
}

if (-not (Test-Path -LiteralPath $runRootPath -PathType Container)) {
    throw "Money threshold native-outcome run root is missing: $runRootPath"
}
$summaryPaths = @(Get-ChildItem -LiteralPath $runRootPath `
    -Filter "summary.json" -File -Recurse | Where-Object {
        $summary = Get-Content -LiteralPath $_.FullName -Raw |
            ConvertFrom-Json
        [string]$summary.schema_version -eq
            "money_threshold_native_outcome_run_summary.v2" -and
        [string]$summary.status -eq "passed"
    } | Sort-Object FullName | Select-Object -ExpandProperty FullName)
if ($summaryPaths.Count -eq 0) {
    throw "No passed Money threshold native-outcome summaries were found."
}

$comparisonAdmissions = @()
if (-not [string]::IsNullOrWhiteSpace($ComparisonAdmissionIndex)) {
    $comparisonIndexPath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $ComparisonAdmissionIndex
    if (-not (Test-Path -LiteralPath $comparisonIndexPath -PathType Leaf)) {
        throw "Money threshold comparison-admission index is missing: $comparisonIndexPath"
    }
    $comparisonIndex = Get-Content -LiteralPath $comparisonIndexPath -Raw |
        ConvertFrom-Json
    $comparisonIndexVersion = [string]$comparisonIndex.schema_version
    if ($comparisonIndexVersion -notin @(
            "money_threshold_comparison_admission_index.v1",
            "money_threshold_comparison_admission_index.v2",
            "money_threshold_comparison_admission_index.v3") -or
        [bool]$comparisonIndex.formal_product_training_authorized) {
        throw "Money threshold comparison-admission index schema is invalid."
    }
    $comparisonAdmissions = @($comparisonIndex.admissions | ForEach-Object {
        $manifestPath = [string]$_.admission_manifest_path
        $receiptPath = [string]$_.admission_receipt_path
        if ([string]::IsNullOrWhiteSpace($manifestPath) -or
            [string]::IsNullOrWhiteSpace($receiptPath)) {
            throw "Money threshold comparison-admission paths are required."
        }
        $episodePath = [string]$_.execution_episode_path
        $trajectoryPath = [string]$_.native_trajectory_receipt_path
        if ($comparisonIndexVersion -eq
                "money_threshold_comparison_admission_index.v1" -and
            (-not [string]::IsNullOrWhiteSpace($episodePath) -or
             -not [string]::IsNullOrWhiteSpace($trajectoryPath))) {
            throw "Comparison-admission index v1 cannot override a native outcome."
        }
        if ($comparisonIndexVersion -eq
                "money_threshold_comparison_admission_index.v2" -and
            -not [string]::IsNullOrWhiteSpace($trajectoryPath)) {
            throw "Comparison-admission index v2 cannot override a native trajectory."
        }
        if (-not [string]::IsNullOrWhiteSpace($episodePath) -and
            -not [string]::IsNullOrWhiteSpace($trajectoryPath)) {
            throw "One comparison admission cannot override two native outcomes."
        }
        [ordered]@{
            run_id = [string]$_.run_id
            criterion_id = [string]$_.criterion_id
            admission_manifest_path = Resolve-RuntimeEvidenceInputPath `
                -ProjectRoot $ProjectRoot -Path $manifestPath
            admission_receipt_path = Resolve-RuntimeEvidenceInputPath `
                -ProjectRoot $ProjectRoot -Path $receiptPath
            execution_episode_path = if ([string]::IsNullOrWhiteSpace(
                $episodePath)) {
                ""
            } else {
                Resolve-RuntimeEvidenceInputPath `
                    -ProjectRoot $ProjectRoot `
                    -Path $episodePath
            }
            native_trajectory_receipt_path = if (
                [string]::IsNullOrWhiteSpace($trajectoryPath)) {
                ""
            } else {
                Resolve-RuntimeEvidenceInputPath `
                    -ProjectRoot $ProjectRoot `
                    -Path $trajectoryPath
            }
        }
    })
}

if (-not $SkipBuild) {
    & dotnet build $bootstrapProject -c Release --no-restore --nologo |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Goal-conditioned bootstrap Release build failed."
    }
}
if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
    throw "Goal-conditioned bootstrap Release DLL is missing: $bootstrapDll"
}

Write-JsonFile -Path $batchRequestPath -Value ([ordered]@{
    schema_version = "money_threshold_native_outcome_batch_request.v4"
    batch_id = $BatchId
    run_summary_paths = $summaryPaths
    comparison_admissions = $comparisonAdmissions
    formal_product_training_authorized = $false
})

Write-JsonFile -Path $workPlanRequestPath -Value ([ordered]@{
    schema_version = "money_threshold_evidence_work_plan_request.v1"
    plan_id = "$BatchId-work-plan"
    money_threshold_corpus_path = $corpusPath
    formal_product_training_authorized = $false
})

$coverageRequestPath = ""
if (-not [string]::IsNullOrWhiteSpace($CoverageRequestTemplate)) {
    $coverageTemplatePath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $CoverageRequestTemplate
    if (-not (Test-Path -LiteralPath $coverageTemplatePath -PathType Leaf)) {
        throw "Goal-method coverage request template is missing: $coverageTemplatePath"
    }
    $coverageRequestPath = if ([string]::IsNullOrWhiteSpace(
            $CoverageRequestOutput)) {
        Join-Path $outputRoot "goal-method-teacher-coverage-request.json"
    }
    else {
        Resolve-RuntimeEvidenceInputPath `
            -ProjectRoot $ProjectRoot -Path $CoverageRequestOutput
    }
    Write-JsonFile -Path $coverageAttachmentManifestPath -Value ([ordered]@{
        schema_version = "money_threshold_coverage_request_manifest.v1"
        base_coverage_request_path = $coverageTemplatePath
        money_threshold_corpus_path = $corpusPath
        source_id = $CoverageSourceId
        formal_product_training_authorized = $false
    })
}

$packageArguments = @(
    "build-money-threshold-production-package",
    "--batch-request", $batchRequestPath,
    "--batch-output", $batchPath,
    "--corpus-output", $corpusPath,
    "--work-plan-request", $workPlanRequestPath,
    "--work-plan-output", $workPlanPath)
if (-not [string]::IsNullOrWhiteSpace($coverageRequestPath)) {
    $packageArguments += @(
        "--coverage-manifest", $coverageAttachmentManifestPath,
        "--coverage-output", $coverageRequestPath)
}
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll `
    -Arguments $packageArguments

$batch = Get-Content -LiteralPath $batchPath -Raw | ConvertFrom-Json
$workPlan = Get-Content -LiteralPath $workPlanPath -Raw | ConvertFrom-Json
[ordered]@{
    status = [string]$batch.status
    batch_id = $BatchId
    verified_run_count = [int]$batch.verified_run_count
    native_complete_cell_count = [int]$batch.native_complete_cell_count
    verified_comparison_admission_count =
        [int]$batch.verified_comparison_admission_count
    teacher_comparison_complete_cell_count =
        [int]$batch.teacher_comparison_complete_cell_count
    required_cell_count = [int]$batch.required_cell_count
    fully_complete_cell_count = [int]$workPlan.fully_complete_cell_count
    native_collection_wave_count =
        [int]$workPlan.native_collection_wave_count
    comparison_collection_wave_count =
        [int]$workPlan.comparison_collection_wave_count
    comparison_blocked_on_native_cell_count =
        [int]$workPlan.comparison_blocked_on_native_cell_count
    batch_path = $batchPath
    corpus_path = $corpusPath
    work_plan_path = $workPlanPath
    coverage_request_path = $coverageRequestPath
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
