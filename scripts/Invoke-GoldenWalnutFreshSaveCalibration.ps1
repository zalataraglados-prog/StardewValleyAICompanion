param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $OutputRoot = "E:\StardewAITraining\golden-walnut-fresh-save-calibration",
    [string] $FreshSaveOutputRoot = "",
    [string] $PrebuiltBinaryRoot = "",
    [string] $RunId = (
        "golden-walnut-fresh-save-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [ValidateRange(1, 64)]
    [int] $MaxAttempts = 8,
    [switch] $RequireSourceScheduleReady,
    [ValidateRange(65, 1000000)]
    [int] $LongRunMaxAttempts = 100000,
    [ValidateRange(1, 64)]
    [int] $RequiredVerifiedActions = 4,
    [ValidateRange(1, 128)]
    [int] $SaveBoundaryMaxAttempts = 16,
    [ValidateRange(1, 1000000)]
    [int] $MaxPersistedIterations = 64,
    [ValidateRange(1, 64)]
    [int] $MaxUnverifiedExecutionStreak = 8,
    [ValidateRange(1, 64)]
    [int] $MaxQueueNoProgressStreak = 8,
    [ValidateRange(1024, [int]::MaxValue)]
    [int] $MinFreeSpaceMb = 8192,
    [string] $StageOneCollectionRequirementInventory = (Join-Path `
        $ProjectRoot `
        "experiments\local-data\output\authoritative-requirement-inventory-v1.json"),
    [string] $StageOneCollectionAcquisitionLowering = (Join-Path `
        $ProjectRoot `
        "experiments\local-data\output\acquisition-route-option-lowering-v1.json"),
    [string] $MasterAnglerStageOneWindowIndex = (Join-Path `
        $ProjectRoot `
        "experiments\local-data\output\master-angler-stage-one-window-index-v1.json"),
    [string] $RouteTimingCalibration = `
        "I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json",
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

$effectiveMaxAttempts = if ($RequireSourceScheduleReady) {
    $LongRunMaxAttempts
} else {
    $MaxAttempts
}

if ($RequiredVerifiedActions -gt $effectiveMaxAttempts) {
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
$freshSaveOutputRoot = if ([string]::IsNullOrWhiteSpace(
        $FreshSaveOutputRoot)) {
    Join-Path $resolvedOutputRoot "fresh-save"
} else {
    [IO.Path]::GetFullPath($FreshSaveOutputRoot)
}
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
    -PrebuiltBinaryRoot $PrebuiltBinaryRoot `
    -RunId $RunId `
    -BackendPort $BackendPort `
    -ProductPort $ProductPort `
    -MaxAttempts $effectiveMaxAttempts `
    -RequiredVerifiedActions $RequiredVerifiedActions `
    -SaveBoundaryMaxAttempts $SaveBoundaryMaxAttempts `
    -ArtifactRetentionMode rolling `
    -MaxPersistedIterations $MaxPersistedIterations `
    -MaxUnverifiedExecutionStreak $MaxUnverifiedExecutionStreak `
    -MaxQueueNoProgressStreak $MaxQueueNoProgressStreak `
    -MinFreeSpaceMb $MinFreeSpaceMb `
    -CaptureGoldenWalnutFreshSaveEvidence `
    -RequireGoldenWalnutSourceScheduleReady:$RequireSourceScheduleReady `
    -StageOneCollectionRequirementInventory `
        $StageOneCollectionRequirementInventory `
    -StageOneCollectionAcquisitionLowering `
        $StageOneCollectionAcquisitionLowering `
    -MasterAnglerStageOneWindowIndex $MasterAnglerStageOneWindowIndex `
    -RouteTimingCalibration $RouteTimingCalibration `
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
if ($RequireSourceScheduleReady -and
    (-not [bool]$calibrationSummary.golden_walnut_source_milestone_ready -or
     -not [bool]$calibrationSummary.golden_walnut_source_schedule_verified -or
     [string]::IsNullOrWhiteSpace(
         [string]$calibrationSummary.golden_walnut_source_schedule_path))) {
    throw "Product calibration did not produce a verified Golden Walnut source schedule."
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
    fresh_save_output_root = $freshSaveOutputRoot
    evidence_output_root = $resolvedOutputRoot
    fresh_save_summary_path = $freshSummaryPath
    product_calibration_summary_path = $calibrationSummaryPath
    live_training_loop_report_path =
        [string]$calibrationSummary.live_training_loop_report_path
    binary_root = [string]$calibrationSummary.binary_root
    prebuilt_binaries_reused =
        [bool]$calibrationSummary.prebuilt_binaries_reused
    episode_manifest_path =
        [string]$calibrationSummary.golden_walnut_fresh_save_episode_manifest_path
    episode_count =
        [int]$calibrationSummary.golden_walnut_fresh_save_episode_count
    max_attempts = $effectiveMaxAttempts
    verified_actions = [int]$calibrationSummary.verified_actions
    required_verified_actions = $RequiredVerifiedActions
    save_boundary_max_attempts = $SaveBoundaryMaxAttempts
    native_save_boundary_verified =
        [bool]$calibrationSummary.native_save_boundary_verified
    source_schedule_required = [bool]$RequireSourceScheduleReady
    source_milestone_ready =
        [bool]$calibrationSummary.golden_walnut_source_milestone_ready
    source_current_total_day =
        [int]$calibrationSummary.golden_walnut_source_current_total_day
    deadline_total_day_exclusive =
        [int]$calibrationSummary.golden_walnut_deadline_total_day_exclusive
    source_schedule_verified =
        [bool]$calibrationSummary.golden_walnut_source_schedule_verified
    source_schedule_path =
        [string]$calibrationSummary.golden_walnut_source_schedule_path
    artifact_retention_mode = "rolling"
    min_free_space_mb = $MinFreeSpaceMb
    snapshot_artifact_mode = "content_addressed_gzip"
    execution_snapshot_profile = "full"
    formal_training_started = $false
}
Write-Utf8Json $summaryPath $summary
$summary | ConvertTo-Json -Depth 32
