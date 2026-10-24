param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $OutputRoot = "E:\StardewAITraining\golden-walnut-fresh-save-calibration",
    [string] $RunId = (
        "golden-walnut-fresh-save-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [ValidateRange(1, 64)]
    [int] $MaxAttempts = 8,
    [ValidateRange(1, 64)]
    [int] $RequiredVerifiedActions = 4,
    [ValidateRange(1, 128)]
    [int] $SaveBoundaryMaxAttempts = 16,
    [ValidateRange(1, 1000000)]
    [int] $MaxPersistedIterations = 64,
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

if ($RequiredVerifiedActions -gt $MaxAttempts) {
    throw "RequiredVerifiedActions must not exceed MaxAttempts."
}

function Write-Utf8Json([string] $Path, $Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    [IO.File]::WriteAllText(
        $Path,
        ($Value | ConvertTo-Json -Depth 32),
        [Text.UTF8Encoding]::new($false))
}

$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$freshSaveOutputRoot = Join-Path $resolvedOutputRoot "fresh-save"
$calibrationOutputRoot = Join-Path $resolvedOutputRoot "product-calibration"
$workflowDirectory = Join-Path (Join-Path $resolvedOutputRoot "workflows") $RunId

$freshOutput = & (Join-Path $PSScriptRoot "Invoke-RuntimeFreshSaveSmoke.ps1") `
    -ProjectRoot $ProjectRoot `
    -RuntimeRoot $RuntimeRoot `
    -RunId $RunId `
    -OutputRoot $freshSaveOutputRoot `
    -StartupTimeoutSeconds $StartupTimeoutSeconds
if ($LASTEXITCODE -ne 0) {
    throw "Native fresh-save stage failed with exit code $LASTEXITCODE."
}
$freshSummary = ($freshOutput | Out-String) | ConvertFrom-Json
if ($freshSummary.status -ne "passed") {
    throw "Native fresh-save stage did not pass."
}

$calibrationOutput = & (Join-Path $PSScriptRoot `
        "Invoke-ProductBootstrapCalibration.ps1") `
    -ProjectRoot $ProjectRoot `
    -RuntimeRoot $RuntimeRoot `
    -SavesPath ([string]$freshSummary.isolated_save_root) `
    -SaveSlot ([string]$freshSummary.native_save_slot) `
    -OutputRoot $calibrationOutputRoot `
    -RunId $RunId `
    -BackendPort $BackendPort `
    -ProductPort $ProductPort `
    -MaxAttempts $MaxAttempts `
    -RequiredVerifiedActions $RequiredVerifiedActions `
    -SaveBoundaryMaxAttempts $SaveBoundaryMaxAttempts `
    -ArtifactRetentionMode rolling `
    -MaxPersistedIterations $MaxPersistedIterations `
    -CaptureGoldenWalnutFreshSaveEvidence `
    -StartupTimeoutSeconds $StartupTimeoutSeconds
if ($LASTEXITCODE -ne 0) {
    throw "Product calibration stage failed with exit code $LASTEXITCODE."
}
$calibrationSummary = ($calibrationOutput | Out-String) | ConvertFrom-Json
if ($calibrationSummary.status -ne "passed" -or
    -not [bool]$calibrationSummary.golden_walnut_fresh_save_evidence_capture_enabled -or
    [int]$calibrationSummary.verified_actions -lt $RequiredVerifiedActions -or
    [int]$calibrationSummary.golden_walnut_fresh_save_episode_count -lt $RequiredVerifiedActions -or
    -not [bool]$calibrationSummary.native_save_boundary_verified) {
    throw "Product calibration did not produce Golden Walnut fresh-save evidence."
}

$freshSummaryPath = Join-Path (
    Join-Path $freshSaveOutputRoot $RunId) "summary.json"
$calibrationSummaryPath = Join-Path (
    Join-Path $calibrationOutputRoot $RunId) "summary.json"
$summaryPath = Join-Path $workflowDirectory "summary.json"
$summary = [ordered]@{
    schema_version = "stardewai.golden_walnut_fresh_save_calibration.v1"
    status = "passed"
    run_id = $RunId
    native_save_slot = [string]$freshSummary.native_save_slot
    isolated_save_root = [string]$freshSummary.isolated_save_root
    fresh_save_summary_path = $freshSummaryPath
    product_calibration_summary_path = $calibrationSummaryPath
    live_training_loop_report_path =
        [string]$calibrationSummary.live_training_loop_report_path
    episode_manifest_path =
        [string]$calibrationSummary.golden_walnut_fresh_save_episode_manifest_path
    episode_count =
        [int]$calibrationSummary.golden_walnut_fresh_save_episode_count
    max_attempts = $MaxAttempts
    verified_actions = [int]$calibrationSummary.verified_actions
    required_verified_actions = $RequiredVerifiedActions
    save_boundary_max_attempts = $SaveBoundaryMaxAttempts
    native_save_boundary_verified =
        [bool]$calibrationSummary.native_save_boundary_verified
    artifact_retention_mode = "rolling"
    snapshot_artifact_mode = "content_addressed_gzip"
    execution_snapshot_profile = "full"
    formal_training_started = $false
}
Write-Utf8Json $summaryPath $summary
$summary | ConvertTo-Json -Depth 32
