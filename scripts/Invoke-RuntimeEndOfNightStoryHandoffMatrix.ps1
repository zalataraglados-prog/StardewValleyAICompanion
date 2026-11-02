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
    throw "Owner visual story-handoff matrix requires a visible game window."
}
if ($OwnerVisualGate) {
    $ownerWorktreeStatus = @(& git -C $ProjectRoot status `
        --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Owner visual story-handoff matrix could not inspect the candidate worktree."
    }
    if ($ownerWorktreeStatus.Count -gt 0) {
        throw "Owner visual story-handoff matrix requires one committed clean-worktree candidate."
    }
}

$matrixRunId = "runtime-end-of-night-story-handoff-matrix-" +
    (Get-Date -Format "yyyyMMdd-HHmmss")
$matrixDirectory = Join-Path $ProjectRoot (
    "artifacts\runtime-end-of-night-story-handoff-matrix\" + $matrixRunId)
New-Item -ItemType Directory -Force -Path $matrixDirectory | Out-Null

$profiles = @(
    "native_kent_post_save",
    "native_cat_adoption_unsafe_question"
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
        PostSaveStoryHandoff = $profile
        NoShipment = $true
        BackendPort = $BackendPort
        StartupTimeoutSeconds = $StartupTimeoutSeconds
    }
    if ($VisibleGame) {
        $arguments.VisibleGame = $true
    }
    if ($OwnerVisualGate -and
        $profile -eq "native_cat_adoption_unsafe_question") {
        $arguments.PostSaveStoryVisualHoldSeconds = 5
    }
    if ($NoBuild -or -not $firstProfile) {
        $arguments.NoBuild = $true
    }

    $stdoutPath = Join-Path $matrixDirectory ($profile + ".stdout.log")
    & $smokeScript @arguments 2>&1 |
        Set-Content -LiteralPath $stdoutPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "End-of-night story-handoff profile failed: $profile"
    }

    $scenarioSummaryPath = Join-Path $ProjectRoot (
        "artifacts\runtime-full-shipment-terminal\" +
        $runId + "\summary.json")
    if (-not (Test-Path -LiteralPath $scenarioSummaryPath -PathType Leaf)) {
        throw "Story-handoff profile did not produce a summary: $profile"
    }
    $summary = Get-Content -LiteralPath $scenarioSummaryPath -Raw |
        ConvertFrom-Json
    $handoff = $summary.post_save_story_handoff
    $expectedVisualHoldSeconds = if ($OwnerVisualGate -and
        $profile -eq "native_cat_adoption_unsafe_question") { 5 } else { 0 }
    if ([string]$summary.status -ne "passed" -or
        [string]$summary.transaction_status -ne "complete" -or
        [string]::IsNullOrWhiteSpace(
            [string]$summary.transaction_evidence_sha256) -or
        [string]$summary.post_save_story_handoff_requested -ne $profile -or
        [string]$handoff.profile -ne $profile -or
        [int]$summary.post_save_story_visual_hold_seconds -ne
            $expectedVisualHoldSeconds) {
        throw "Story-handoff profile did not preserve one complete transaction: $profile"
    }
    if ($profile -eq "native_kent_post_save" -and
        ([string]$handoff.event_id -ne "63" -or
         [string]$handoff.terminal_state -ne "native_event_completed")) {
        throw "Kent native handoff did not complete event 63."
    }
    if ($profile -eq "native_cat_adoption_unsafe_question" -and
        ([string]$handoff.event_id -ne "1590166" -or
         [string]$handoff.terminal_state -ne
            "native_question_held_for_model_decision" -or
         [string]$handoff.unsafe_question_block_reason -ne
            "story_event_dialogue_response_mismatch")) {
        throw "Cat adoption handoff did not hold the unsafe native question."
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
        throw "Story-handoff candidate identity changed between profiles: $profile"
    }
    if ($OwnerVisualGate -and [bool]$summary.project_worktree_dirty) {
        throw "Owner visual story-handoff matrix requires one committed clean-worktree candidate."
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
        event_id = [string]$handoff.event_id
        terminal_state = [string]$handoff.terminal_state
        unsafe_question_block_reason =
            [string]$handoff.unsafe_question_block_reason
    })
    $firstProfile = $false
}

$matrixSummary = [ordered]@{
    schema_version =
        "stardewai.runtime_end_of_night_story_handoff_matrix.v1"
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
