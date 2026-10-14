[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RunSummaryPath,
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $PlanId = "",
    [string] $OutputRoot =
        "artifacts\money-revenue-comparison-work",
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

$summaryPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $RunSummaryPath
if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
    throw "Money threshold native-outcome run summary is missing: $summaryPath"
}
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
if ([string]$summary.schema_version -ne
        "money_threshold_native_outcome_run_summary.v1" -or
    [string]$summary.status -ne "passed" -or
    [string]::IsNullOrWhiteSpace([string]$summary.run_id)) {
    throw "Money threshold native-outcome run summary is not passed and versioned."
}

$candidatePath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path ([string]$summary.candidate_denominator_path)
if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
    throw "Money Revenue candidate denominator is missing: $candidatePath"
}
$effectivePlanId = if ([string]::IsNullOrWhiteSpace($PlanId)) {
    ([string]$summary.run_id) + "-comparison"
}
else {
    $PlanId
}
$outputBase = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputRoot
$outputDirectory = Join-Path $outputBase ([string]$summary.run_id)
$alternativePath = Join-Path $outputDirectory `
    "money-revenue-alternative-denominator.json"
$requestPath = Join-Path $outputDirectory `
    "money-revenue-comparison-collection-plan-request.json"
$planPath = Join-Path $outputDirectory `
    "money-revenue-comparison-collection-plan.json"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

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

Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-alternative-denominator",
    "--candidate-catalog", $candidatePath,
    "--output", $alternativePath)
Write-JsonFile -Path $requestPath -Value ([ordered]@{
    schema_version =
        "money_revenue_comparison_collection_plan_request.v1"
    plan_id = $effectivePlanId
    native_run_summary_path = $summaryPath
    alternative_denominator_catalog_path = $alternativePath
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-comparison-collection-plan",
    "--request", $requestPath,
    "--output", $planPath)

$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
[ordered]@{
    status = [string]$plan.status
    plan_id = [string]$plan.plan_id
    run_id = [string]$plan.run_id
    dataset_partition = [string]$plan.dataset_partition
    crossed_criterion_ids = @($plan.crossed_criterion_ids)
    alternative_count = [int]$plan.alternative_count
    direct_alternative_count = [int]$plan.direct_alternative_count
    fresh_rebind_alternative_count =
        [int]$plan.fresh_rebind_alternative_count
    existing_native_episode_reuse_count =
        [int]$plan.existing_native_episode_reuse_count
    alternative_denominator_path = $alternativePath
    comparison_collection_plan_path = $planPath
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
