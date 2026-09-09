[CmdletBinding()]
param(
    [string]$DashboardPath = "",
    [string]$ReconciliationPath = "",
    [string]$DocumentPath = "",
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($DashboardPath)) {
    $DashboardPath = Join-Path $projectRoot "catalogs\vanilla-1.6.15\action-progress-dashboard.json"
}
if ([string]::IsNullOrWhiteSpace($ReconciliationPath)) {
    $ReconciliationPath = Join-Path $projectRoot "catalogs\vanilla-1.6.15\action-implementation-reconciliation.json"
}
if ([string]::IsNullOrWhiteSpace($DocumentPath)) {
    $DocumentPath = Join-Path $projectRoot "docs\CURRENT_WORK_CN.md"
}

foreach ($path in @($DashboardPath, $ReconciliationPath, $DocumentPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required checkpoint input is missing: $path"
    }
}

$dashboard = Get-Content -LiteralPath $DashboardPath -Raw -Encoding utf8 | ConvertFrom-Json
$reconciliation = Get-Content -LiteralPath $ReconciliationPath -Raw -Encoding utf8 | ConvertFrom-Json
if ($dashboard.schema_version -ne "stardewai.action_progress_dashboard.v2") {
    throw "Unsupported dashboard schema: $($dashboard.schema_version)"
}
if ($reconciliation.schema_version -ne "stardewai.action_implementation_reconciliation.v1") {
    throw "Unsupported reconciliation schema: $($reconciliation.schema_version)"
}

$sourceCommit = [string]$dashboard.catalog_source_commit_sha
if ($sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw "Dashboard catalog source commit is not bound: $sourceCommit"
}
& git -C $projectRoot merge-base --is-ancestor $sourceCommit HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Dashboard catalog source commit is not an ancestor of HEAD: $sourceCommit"
}
if ([int]$dashboard.registered_option_count -ne [int]$reconciliation.registered_option_count -or
    [int]$dashboard.compiler_bound_count -ne @($reconciliation.options | Where-Object compilerBinding -ne 'unbound').Count -or
    [int]$dashboard.product_executor_count -ne @($reconciliation.options | Where-Object productExecutorSupported -eq $true).Count) {
    throw "Dashboard and implementation reconciliation counts differ."
}

$beginMarker = "<!-- BEGIN GENERATED CURRENT CHECKPOINT -->"
$endMarker = "<!-- END GENERATED CURRENT CHECKPOINT -->"
$block = @(
    $beginMarker
    "## Machine-generated current checkpoint"
    ""
    ('- Source commit: `{0}` (generation input; must be an ancestor of current HEAD)' -f $sourceCommit)
    ('- Latest evidence: `{0}`; generated at: `{1}`' -f $dashboard.latest_evidence_id, $dashboard.generated_at_utc)
    ('- Catalog: `{0} registered / {1} semantic / {2} compiler-bound / {3} five-gate / {4} training-allowlist`' -f $dashboard.registered_option_count, $dashboard.semantic_action_catalog_count, $dashboard.compiler_bound_count, $dashboard.five_gate_evidence_closed_count, $dashboard.training_allowlist_count)
    ('- Execution: `{0} product-executor / {1} catalogued-blocked`' -f $dashboard.product_executor_count, $dashboard.catalogued_blocked_action_count)
    ('- Native evidence: `{0} surfaces / {1} branches / {2} map tokens`; fingerprint: `{3}`' -f $dashboard.native_surface_count, $dashboard.native_branch_count, $dashboard.native_map_interaction_token_count, $dashboard.native_action_surface_fingerprint_sha256)
    ('- Planning catalog: `{0}`; `{1}`; fingerprint: `{2}`' -f $dashboard.planning_semantic_catalog_status, $dashboard.planning_semantic_catalog_fingerprint_schema, $dashboard.planning_semantic_catalog_fingerprint_sha256)
    $endMarker
) -join "`n"

$document = Get-Content -LiteralPath $DocumentPath -Raw -Encoding utf8
$escapedBegin = [regex]::Escape($beginMarker)
$escapedEnd = [regex]::Escape($endMarker)
$pattern = "(?s)$escapedBegin.*?$escapedEnd"
if ([regex]::IsMatch($document, $pattern)) {
    $checkpointRegex = [regex]::new($pattern)
    $expected = $checkpointRegex.Replace(
        $document,
        [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $block },
        1)
}
else {
    $headingEnd = $document.IndexOf("`n")
    if ($headingEnd -lt 0) {
        throw "Current work document has no heading boundary."
    }
    $expected = $document.Insert($headingEnd + 1, "`n$block`n")
}

if ($Check) {
    if (-not [string]::Equals($document, $expected, [System.StringComparison]::Ordinal)) {
        throw "CURRENT_WORK_CN.md generated checkpoint is stale. Run scripts/Update-CurrentWorkCheckpoint.ps1."
    }
    Write-Output "PASS: current work checkpoint matches $($dashboard.latest_evidence_id) at $sourceCommit"
    exit 0
}

[System.IO.File]::WriteAllText(
    [System.IO.Path]::GetFullPath($DocumentPath),
    $expected,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "Updated current work checkpoint to $($dashboard.latest_evidence_id) at $sourceCommit"
