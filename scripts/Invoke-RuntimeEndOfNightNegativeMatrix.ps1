param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [int] $BackendPort = 8798,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $NoBuild,
    [switch] $VisibleGame,
    [switch] $OwnerVisualGate
)

$ErrorActionPreference = "Stop"

if ($OwnerVisualGate -and -not $VisibleGame) {
    throw "Owner visual negative matrix requires a visible game window."
}
if ($OwnerVisualGate) {
    $ownerWorktreeStatus = @(& git -C $ProjectRoot status `
        --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Owner visual negative matrix could not inspect the candidate worktree."
    }
    if ($ownerWorktreeStatus.Count -gt 0) {
        throw "Owner visual negative matrix requires one committed clean-worktree candidate."
    }
}

$matrixRunId = "runtime-end-of-night-negative-matrix-" +
    (Get-Date -Format "yyyyMMdd-HHmmss")
$matrixDirectory = Join-Path $ProjectRoot (
    "artifacts\runtime-end-of-night-negative-matrix\" + $matrixRunId)
New-Item -ItemType Directory -Force -Path $matrixDirectory | Out-Null

$profiles = @(
    "unknown_menu_transition_timeout",
    "overall_timeout"
)
$smokeScript = Join-Path $PSScriptRoot `
    "Invoke-RuntimeFullShipmentTerminalSmoke.ps1"
$results = [System.Collections.Generic.List[object]]::new()
$firstProfile = $true
$candidateHeadCommit = ""
$candidateWorktreeStatusSha256 = ""
$candidateSmokeScriptSha256 = ""
$candidateBinaryIdentity = ""
$candidateRuntimeBundleIdentity = ""

foreach ($profile in $profiles) {
    $runId = $matrixRunId + "-" + $profile
    $arguments = @{
        ProjectRoot = $ProjectRoot
        RuntimeRoot = $RuntimeRoot
        SaveSlot = $SaveSlot
        RunId = $runId
        EndOfNightNegativeProfile = $profile
        NoShipment = $true
        BackendPort = $BackendPort
        StartupTimeoutSeconds = $StartupTimeoutSeconds
    }
    if ($VisibleGame) {
        $arguments.VisibleGame = $true
    }
    if ($OwnerVisualGate) {
        $arguments.NegativeVisualHoldSeconds = 5
    }
    if ($NoBuild -or -not $firstProfile) {
        $arguments.NoBuild = $true
    }

    $stdoutPath = Join-Path $matrixDirectory ($profile + ".stdout.log")
    & $smokeScript @arguments 2>&1 |
        Set-Content -LiteralPath $stdoutPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "End-of-night negative profile failed: $profile"
    }

    $scenarioSummaryPath = Join-Path $ProjectRoot (
        "artifacts\runtime-full-shipment-terminal\" +
        $runId + "\summary.json")
    if (-not (Test-Path -LiteralPath $scenarioSummaryPath -PathType Leaf)) {
        throw "Negative profile did not produce a summary: $profile"
    }
    $summary = Get-Content -LiteralPath $scenarioSummaryPath -Raw |
        ConvertFrom-Json
    $expectedFailurePrefix = if (
        $profile -eq "unknown_menu_transition_timeout") {
        "end_of_night_transition_timeout:menu:QuestLog:"
    }
    else {
        "end_of_night_transaction_overall_timeout"
    }
    $expectedVisualHoldSeconds = if ($OwnerVisualGate) { 5 } else { 0 }
    if ([string]$summary.status -ne "passed" -or
        [string]$summary.negative_profile -ne $profile -or
        [string]$summary.transaction_status -ne "blocked" -or
        [string]::IsNullOrWhiteSpace(
            [string]$summary.transaction_evidence_sha256) -or
        -not ([string]$summary.transaction_failure_reason).StartsWith(
            $expectedFailurePrefix,
            [StringComparison]::Ordinal) -or
        -not [bool]$summary.debug_negative_fixture_injected -or
        -not [bool]$summary.saw_native_save_commit -or
        -not [bool]$summary.saw_expected_new_day -or
        -not [bool]$summary.saw_safe_new_day_ready_state -or
        [int]$summary.visual_hold_seconds -ne
            $expectedVisualHoldSeconds) {
        throw "Negative profile did not preserve one typed bounded failure: $profile"
    }

    $summaryBinaryIdentity = $summary.candidate_binary_sha256 |
        ConvertTo-Json -Depth 16 -Compress
    $summaryRuntimeBundleIdentity =
        $summary.candidate_runtime_bundle_sha256 |
            ConvertTo-Json -Depth 16 -Compress
    if ($firstProfile) {
        $candidateHeadCommit = [string]$summary.project_head_commit
        $candidateWorktreeStatusSha256 =
            [string]$summary.project_worktree_status_sha256
        $candidateSmokeScriptSha256 =
            [string]$summary.candidate_script_sha256
        $candidateBinaryIdentity = $summaryBinaryIdentity
        $candidateRuntimeBundleIdentity = $summaryRuntimeBundleIdentity
    }
    elseif ([string]$summary.project_head_commit -ne $candidateHeadCommit -or
        [string]$summary.project_worktree_status_sha256 -ne
            $candidateWorktreeStatusSha256 -or
        [string]$summary.candidate_script_sha256 -ne
            $candidateSmokeScriptSha256 -or
        $summaryBinaryIdentity -ne $candidateBinaryIdentity -or
        $summaryRuntimeBundleIdentity -ne $candidateRuntimeBundleIdentity) {
        throw "Negative-matrix candidate identity changed between profiles: $profile"
    }
    if ($OwnerVisualGate -and [bool]$summary.project_worktree_dirty) {
        throw "Owner visual negative matrix requires one committed clean-worktree candidate."
    }

    $results.Add([ordered]@{
        profile = $profile
        run_id = $runId
        summary_path = $scenarioSummaryPath
        summary_sha256 = ((Get-FileHash -LiteralPath $scenarioSummaryPath `
            -Algorithm SHA256).Hash).ToLowerInvariant()
        transaction_id = [string]$summary.transaction_id
        transaction_status = [string]$summary.transaction_status
        transaction_evidence_sha256 =
            [string]$summary.transaction_evidence_sha256
        failure_reason = [string]$summary.transaction_failure_reason
        transition_keys = @($summary.transition_keys)
        visual_hold_seconds = [int]$summary.visual_hold_seconds
    })
    $firstProfile = $false
}

$matrixSummary = [ordered]@{
    schema_version =
        "stardewai.runtime_end_of_night_negative_matrix.v1"
    status = "passed"
    run_id = $matrixRunId
    project_head_commit = $candidateHeadCommit
    project_worktree_status_sha256 = $candidateWorktreeStatusSha256
    matrix_script_sha256 = ((Get-FileHash -LiteralPath $PSCommandPath `
        -Algorithm SHA256).Hash).ToLowerInvariant()
    smoke_script_sha256 = $candidateSmokeScriptSha256
    candidate_binary_sha256 =
        $candidateBinaryIdentity | ConvertFrom-Json
    candidate_runtime_bundle_sha256 =
        $candidateRuntimeBundleIdentity | ConvertFrom-Json
    profile_count = $profiles.Count
    passed_profile_count = $results.Count
    owner_visual_gate_required = [bool]$OwnerVisualGate
    owner_visual_gate_passed = $false
    training_label_eligible = $false
    profiles = $results.ToArray()
}
$summaryPath = Join-Path $matrixDirectory "summary.json"
$matrixSummary | ConvertTo-Json -Depth 32 |
    Set-Content -LiteralPath $summaryPath -Encoding utf8
$matrixSummary | ConvertTo-Json -Depth 32
