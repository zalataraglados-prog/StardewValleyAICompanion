[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SourceSaveSlotPath,
    [Parameter(Mandatory)]
    [string] $DecisionRequestPath,
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $InputId =
        ("money-crop-input-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot =
        "artifacts\money-crop-native-production-input",
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

if ([string]::IsNullOrWhiteSpace($InputId) -or
    [IO.Path]::GetFileName($InputId) -ne $InputId -or
    $InputId.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "Money crop production input ID must be one safe path segment."
}

$decisionPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $DecisionRequestPath
if (-not (Test-Path -LiteralPath $decisionPath -PathType Leaf)) {
    throw "Money crop production decision request is missing: $decisionPath"
}
$sourceSave = Assert-RuntimeSourceSaveSlot -Path $SourceSaveSlotPath
$sourceSaveSha256Before = Get-DirectoryContentHash -Path $sourceSave
$outputBase = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputRoot
$outputDirectory = Join-Path $outputBase $InputId
if (Test-Path -LiteralPath $outputDirectory) {
    throw "Money crop production input directory already exists: $outputDirectory"
}

$receiptPath = Join-Path $outputDirectory `
    "crop-production-decision-receipt.json"
$ledgerPath = Join-Path $outputDirectory "strategy-ledger.json"
$obligationRequestPath = Join-Path $outputDirectory `
    "crop-production-obligation-plan-request.json"
$obligationPlanPath = Join-Path $outputDirectory `
    "crop-production-obligation-plan.json"
$runPlanRequestPath = Join-Path $outputDirectory `
    "crop-native-production-run-plan-request.json"
$runPlanPath = Join-Path $outputDirectory `
    "crop-native-production-run-plan.json"
$manifestPath = Join-Path $outputDirectory `
    "money-crop-native-production-input-manifest.json"
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

New-Item -ItemType Directory -Path $outputDirectory | Out-Null
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-crop-production-decision-receipt",
    "--request", $decisionPath,
    "--output", $receiptPath)

$receiptRaw = [IO.File]::ReadAllText(
    $receiptPath,
    [Text.Encoding]::UTF8)
$receipt = $receiptRaw | ConvertFrom-Json
if ([string]$receipt.schema_version -ne
        "money_revenue_crop_production_decision_receipt.v1" -or
    [string]$receipt.status -ne "crop_production_decision_committed" -or
    -not [bool]$receipt.all_commitments_accepted -or
    [bool]$receipt.formal_product_training_authorized -or
    [string]$receipt.goal_id -ne "earn_money" -or
    [int]$receipt.deadline_total_day_exclusive -ne 224 -or
    [long]$receipt.target_total_money_earned -ne 1000000 -or
    [string]::IsNullOrWhiteSpace([string]$receipt.source_state_hash) -or
    @($receipt.mutations).Count -eq 0 -or
    @($receipt.layout_intents).Count -ne @($receipt.mutations).Count -or
    [int]$receipt.max_queue_item_count -lt 1 -or
    [int]$receipt.max_queue_item_count -gt 8 -or
    $null -eq $receipt.final_ledger) {
    throw "Money crop production decision receipt is not committed and complete."
}
if (-not [string]::Equals(
        [IO.Path]::GetFullPath([string]$receipt.request_path),
        $decisionPath,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Money crop decision receipt points at a different request."
}
Assert-RuntimeFileIdentity -Path $decisionPath `
    -ExpectedSha256 ([string]$receipt.request_sha256) `
    -Label "Money crop production decision request" | Out-Null
$snapshotPath = [IO.Path]::GetFullPath([string]$receipt.snapshot_path)
Assert-RuntimeFileIdentity -Path $snapshotPath `
    -ExpectedSha256 ([string]$receipt.snapshot_sha256) `
    -Label "Money crop production decision snapshot" | Out-Null

Write-RawJsonProperty -Path $ledgerPath -RawJson $receiptRaw `
    -PropertyName "final_ledger"
Write-JsonFile -Path $obligationRequestPath -Value ([ordered]@{
    schema_version =
        "money_revenue_crop_production_obligation_plan_request.v1"
    snapshot_path = $snapshotPath
    strategy_ledger_path = $ledgerPath
    deadline_total_day_exclusive =
        [int]$receipt.deadline_total_day_exclusive
    target_total_money_earned = [long]$receipt.target_total_money_earned
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-crop-production-obligation-plan",
    "--request", $obligationRequestPath,
    "--output", $obligationPlanPath)

$obligationPlan = Get-Content -LiteralPath $obligationPlanPath -Raw |
    ConvertFrom-Json
if ([string]$obligationPlan.schema_version -ne
        "money_revenue_crop_production_obligation_plan.v2" -or
    [string]$obligationPlan.source_state_hash -ne
        [string]$receipt.source_state_hash -or
    [int]$obligationPlan.active_commitment_count -ne
        @($receipt.layout_intents).Count -or
    [bool]$obligationPlan.formal_product_training_authorized) {
    throw "Money crop production obligation plan drifted from its decision."
}

Write-JsonFile -Path $runPlanRequestPath -Value ([ordered]@{
    schema_version =
        "money_revenue_crop_native_production_run_plan_request.v1"
    crop_production_plan_path = $obligationPlanPath
    layout_intents = @($receipt.layout_intents)
    max_queue_item_count = [int]$receipt.max_queue_item_count
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-crop-native-production-run-plan",
    "--request", $runPlanRequestPath,
    "--output", $runPlanPath)

$runPlan = Get-Content -LiteralPath $runPlanPath -Raw | ConvertFrom-Json
$expectedCommitmentIds = @($receipt.layout_intents | ForEach-Object {
    [string]$_.commitment_id
} | Sort-Object)
$actualCommitmentIds = @($runPlan.covered_commitment_ids | ForEach-Object {
    [string]$_
} | Sort-Object)
if ([string]$runPlan.schema_version -ne
        "money_revenue_crop_native_production_run_plan.v2" -or
    [string]$runPlan.status -ne "native_crop_production_run_ready" -or
    -not [bool]$runPlan.native_production_run_ready -or
    [bool]$runPlan.formal_product_training_authorized -or
    [string]$runPlan.source_state_hash -ne
        [string]$receipt.source_state_hash -or
    @($runPlan.seed_acquisitions).Count -ne
        [int]$runPlan.seed_acquisition_count -or
    [string]::Join("`n", $expectedCommitmentIds) -ne
        [string]::Join("`n", $actualCommitmentIds)) {
    throw "Money crop native-production run plan drifted from its decision."
}

$sourceSaveSha256After = Get-DirectoryContentHash -Path $sourceSave
if ($sourceSaveSha256Before -ne $sourceSaveSha256After) {
    throw "Source save changed while Money crop production inputs were initialized."
}

$manifest = [ordered]@{
    schema_version = "money_crop_native_production_input_manifest.v1"
    status = "native_crop_production_input_ready"
    input_id = $InputId
    goal_id = [string]$receipt.goal_id
    deadline_total_day_exclusive =
        [int]$receipt.deadline_total_day_exclusive
    target_total_money_earned = [long]$receipt.target_total_money_earned
    source_state_hash = [string]$receipt.source_state_hash
    source_total_day = [int]$runPlan.source_total_day
    source_save_slot_path = $sourceSave
    source_save_tree_sha256 = $sourceSaveSha256Before
    decision_id = [string]$receipt.decision_id
    decision_request_path = $decisionPath
    decision_request_sha256 = Get-RuntimeFileSha256 -Path $decisionPath
    decision_receipt_path = $receiptPath
    decision_receipt_sha256 = Get-RuntimeFileSha256 -Path $receiptPath
    snapshot_path = $snapshotPath
    snapshot_sha256 = Get-RuntimeFileSha256 -Path $snapshotPath
    strategy_ledger_path = $ledgerPath
    strategy_ledger_sha256 = Get-RuntimeFileSha256 -Path $ledgerPath
    crop_production_obligation_request_path = $obligationRequestPath
    crop_production_obligation_request_sha256 =
        Get-RuntimeFileSha256 -Path $obligationRequestPath
    crop_production_obligation_plan_path = $obligationPlanPath
    crop_production_obligation_plan_sha256 =
        Get-RuntimeFileSha256 -Path $obligationPlanPath
    crop_native_run_plan_request_path = $runPlanRequestPath
    crop_native_run_plan_request_sha256 =
        Get-RuntimeFileSha256 -Path $runPlanRequestPath
    crop_native_run_plan_path = $runPlanPath
    crop_native_run_plan_sha256 = Get-RuntimeFileSha256 -Path $runPlanPath
    commitment_count = @($receipt.layout_intents).Count
    max_queue_item_count = [int]$receipt.max_queue_item_count
    formal_product_training_authorized = $false
}
Write-JsonFile -Path $manifestPath -Value $manifest

[ordered]@{
    status = [string]$manifest.status
    input_id = $InputId
    manifest_path = $manifestPath
    source_save_slot_path = $sourceSave
    source_save_tree_sha256 = $sourceSaveSha256Before
    decision_id = [string]$receipt.decision_id
    commitment_count = [int]$manifest.commitment_count
    first_total_day = [int]$runPlan.first_total_day
    last_total_day = [int]$runPlan.last_total_day
    day_count = [int]$runPlan.day_count
    action_count = [int]$runPlan.action_count
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
