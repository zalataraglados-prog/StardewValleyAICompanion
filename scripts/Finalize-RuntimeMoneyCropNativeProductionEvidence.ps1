[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [Parameter(Mandatory)]
    [string] $RunDirectory,
    [string] $ProductionInputManifestPath,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeMoneyCropProductionInput.ps1")
. (Join-Path $PSScriptRoot `
    "lib\RuntimeMoneyCropEvidenceFinalization.ps1")

$runDirectoryPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $RunDirectory
$runPlanPath = Join-Path $runDirectoryPath "crop-native-run-plan.json"
$evidenceDirectory = Join-Path $runDirectoryPath "evidence"
$summaryPath = Join-Path $runDirectoryPath `
    "evidence-finalization-summary.json"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

if (-not (Test-Path -LiteralPath $runDirectoryPath -PathType Container) -or
    -not (Test-Path -LiteralPath $runPlanPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $evidenceDirectory -PathType Container)) {
    throw "Money crop native-production run is incomplete: $runDirectoryPath"
}

if ([string]::IsNullOrWhiteSpace($ProductionInputManifestPath)) {
    foreach ($metadataName in @("summary.json", "failure.json")) {
        $metadataPath = Join-Path $runDirectoryPath $metadataName
        if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
            continue
        }
        $metadata = Get-Content -LiteralPath $metadataPath -Raw |
            ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace(
                [string]$metadata.production_input_manifest_path)) {
            $ProductionInputManifestPath =
                [string]$metadata.production_input_manifest_path
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($ProductionInputManifestPath)) {
    throw "Money crop production input manifest cannot be recovered from the run."
}

if (-not $SkipBuild) {
    & dotnet build (Join-Path $ProjectRoot `
        "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj") `
        -c Release --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed before Money crop evidence finalization."
    }
}
if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
    throw "Goal-conditioned bootstrap Release artifact is missing: $bootstrapDll"
}

$productionInput = Read-MoneyCropNativeProductionInputManifest `
    -ProjectRoot $ProjectRoot -Path $ProductionInputManifestPath
Assert-RuntimeFileIdentity -Path $runPlanPath `
    -ExpectedSha256 ([string]$productionInput.Manifest.crop_native_run_plan_sha256) `
    -Label "Executed Money crop native run plan" | Out-Null
$runPlan = Get-Content -LiteralPath $runPlanPath -Raw | ConvertFrom-Json
Assert-MoneyCropNativeRunPlan -Plan $runPlan
$productionPlanPath = $productionInput.ObligationPlanPath
$productionPlan = $productionInput.ObligationPlan

$plannedCommitmentIds = @($productionPlan.commitments | ForEach-Object {
    [string]$_.commitment_id
})
$coveredCommitmentIds = @($runPlan.covered_commitment_ids | ForEach-Object {
    [string]$_
})
if ([string]::Join("`n", $plannedCommitmentIds) -ne
    [string]::Join("`n", $coveredCommitmentIds)) {
    throw "Executed Money crop run does not cover the exact production commitments."
}

$layoutProofPaths = [Collections.Generic.List[string]]::new()
$seedAcquisitionProofPaths = [Collections.Generic.List[string]]::new()
$capacityProofPaths = [Collections.Generic.List[string]]::new()
$lifecycleProofPaths = [Collections.Generic.List[string]]::new()
$retentionProofPaths = [Collections.Generic.List[string]]::new()

foreach ($commitment in @($productionPlan.commitments)) {
    $commitmentId = [string]$commitment.commitment_id
    $safeId = Get-MoneyCropSafeArtifactName -Value $commitmentId
    $layoutProofPath = Join-Path $evidenceDirectory `
        "layout-proof-$safeId.json"
    $capacityProofPath = Join-Path $evidenceDirectory `
        "capacity-proof-$safeId.json"
    $lifecycleProofPath = Join-Path $evidenceDirectory `
        "lifecycle-proof-$safeId.json"
    Assert-MoneyCropEvidenceFiles -Paths @(
        $layoutProofPath,
        $capacityProofPath,
        $lifecycleProofPath)
    $layoutProofPaths.Add([IO.Path]::GetFullPath($layoutProofPath))
    $capacityProofPaths.Add([IO.Path]::GetFullPath($capacityProofPath))
    $lifecycleProofPaths.Add([IO.Path]::GetFullPath($lifecycleProofPath))

    $commitmentRetentionPaths = @(New-MoneyCropOutputRetentionProofs `
        -BootstrapDll $bootstrapDll `
        -ProductionPlanPath $productionPlanPath `
        -Commitment $commitment `
        -LifecycleProofPath $lifecycleProofPath `
        -EvidenceDirectory $evidenceDirectory)
    foreach ($path in $commitmentRetentionPaths) {
        $retentionProofPaths.Add([string]$path)
    }
}

foreach ($acquisition in @($runPlan.seed_acquisitions)) {
    $safeId = Get-MoneyCropSafeArtifactName `
        -Value ([string]$acquisition.commitment_id)
    $proofPath = Join-Path $evidenceDirectory `
        "seed-acquisition-proof-$safeId.json"
    Assert-MoneyCropEvidenceFiles -Paths @($proofPath)
    $seedAcquisitionProofPaths.Add([IO.Path]::GetFullPath($proofPath))
}

$checkpointResult = Complete-MoneyCropProductionProofCheckpoint `
    -BootstrapDll $bootstrapDll `
    -ProductionPlanPath $productionPlanPath `
    -EvidenceDirectory $evidenceDirectory `
    -PlantingLayoutProofPaths $layoutProofPaths.ToArray() `
    -SeedAcquisitionProofPaths $seedAcquisitionProofPaths.ToArray() `
    -DailyCapacityProofPaths $capacityProofPaths.ToArray() `
    -NativeLifecycleProofPaths $lifecycleProofPaths.ToArray() `
    -OutputRetentionProofPaths $retentionProofPaths.ToArray()

$summary = [ordered]@{
    schema_version = "money_crop_native_evidence_finalization_summary.v1"
    status = "passed"
    run_directory = $runDirectoryPath
    production_input_manifest_path = $productionInput.ManifestPath
    production_input_manifest_sha256 = $productionInput.ManifestSha256
    run_plan_path = $runPlanPath
    crop_production_plan_path = $productionPlanPath
    planting_layout_proof_count = $layoutProofPaths.Count
    seed_acquisition_proof_count = $seedAcquisitionProofPaths.Count
    daily_capacity_proof_count = $capacityProofPaths.Count
    native_lifecycle_proof_count = $lifecycleProofPaths.Count
    output_retention_proof_count = $retentionProofPaths.Count
    fragment_index_path = $checkpointResult.FragmentIndexPath
    checkpoint_request_path = $checkpointResult.CheckpointRequestPath
    checkpoint_path = $checkpointResult.CheckpointPath
    checkpoint_verified_fragment_count =
        [int]$checkpointResult.Checkpoint.verified_fragment_count
    checkpoint_pending_request_count =
        [int]$checkpointResult.Checkpoint.pending_request_count
    formal_product_training_authorized = $false
}
Write-JsonFile -Path $summaryPath -Value $summary
$summary | ConvertTo-Json -Depth 32
