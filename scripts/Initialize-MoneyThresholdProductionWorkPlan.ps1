[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $PlanId = "money-threshold-production-work-plan-v1",
    [string] $MoneyThresholdCorpusPath = "",
    [string] $OutputDirectory =
        "artifacts\money-threshold-production-native-batch",
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

if ([string]::IsNullOrWhiteSpace($PlanId)) {
    throw "Money threshold production work-plan ID is required."
}
$outputRoot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputDirectory
$requestPath = Join-Path $outputRoot `
    "money-threshold-evidence-work-plan-request.json"
$workPlanPath = Join-Path $outputRoot `
    "money-threshold-evidence-work-plan.json"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

$corpusPath = ""
if (-not [string]::IsNullOrWhiteSpace($MoneyThresholdCorpusPath)) {
    $corpusPath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $MoneyThresholdCorpusPath
    if (-not (Test-Path -LiteralPath $corpusPath -PathType Leaf)) {
        throw "Money threshold corpus is missing: $corpusPath"
    }
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

Write-JsonFile -Path $requestPath -Value ([ordered]@{
    schema_version = "money_threshold_evidence_work_plan_request.v1"
    plan_id = $PlanId
    money_threshold_corpus_path = $corpusPath
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-threshold-evidence-work-plan",
    "--request", $requestPath,
    "--output", $workPlanPath)

$workPlan = Get-Content -LiteralPath $workPlanPath -Raw |
    ConvertFrom-Json
if ([string]$workPlan.schema_version -ne
        "money_threshold_evidence_work_plan.v1" -or
    [string]$workPlan.plan_id -ne $PlanId -or
    [int]$workPlan.criterion_count -ne 6 -or
    [int]$workPlan.required_partition_count -ne 3 -or
    [int]$workPlan.required_cell_count -ne 18 -or
    [bool]$workPlan.formal_product_training_authorized) {
    throw "Generated Money threshold production work plan is invalid."
}
if ([string]::IsNullOrWhiteSpace($corpusPath) -and
    ([int]$workPlan.native_outcome_complete_cell_count -ne 0 -or
     [int]$workPlan.teacher_comparison_complete_cell_count -ne 0 -or
     [int]$workPlan.fully_complete_cell_count -ne 0 -or
     [int]$workPlan.native_collection_wave_count -ne 3 -or
     [int]$workPlan.comparison_collection_wave_count -ne 0)) {
    throw "Empty Money production work plan did not expose 18 missing cells."
}

[ordered]@{
    status = [string]$workPlan.status
    plan_id = $PlanId
    work_plan_path = $workPlanPath
    corpus_path = $corpusPath
    native_complete_cell_count =
        [int]$workPlan.native_outcome_complete_cell_count
    teacher_comparison_complete_cell_count =
        [int]$workPlan.teacher_comparison_complete_cell_count
    fully_complete_cell_count = [int]$workPlan.fully_complete_cell_count
    required_cell_count = [int]$workPlan.required_cell_count
    native_collection_wave_count =
        [int]$workPlan.native_collection_wave_count
    comparison_collection_wave_count =
        [int]$workPlan.comparison_collection_wave_count
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
