[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SnapshotPath,
    [Parameter(Mandatory)]
    [string[]] $FarmLayoutSnapshotPaths,
    [string] $BaseStrategyLedgerPath = "",
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $DecisionId =
        ("money-crop-teacher-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $DecisionRecordedAt =
        ([DateTimeOffset]::UtcNow.ToString("o")),
    [ValidateRange(1, 64)]
    [int] $MaximumTileCount = 8,
    [ValidateRange(1, 8)]
    [int] $MaxQueueItemCount = 8,
    [string] $OutputId =
        ("money-crop-teacher-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot =
        "artifacts\money-crop-production-teacher-decisions",
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

if ([string]::IsNullOrWhiteSpace($DecisionId)) {
    throw "Money crop Teacher decision ID is required."
}
if ([string]::IsNullOrWhiteSpace($OutputId) -or
    [IO.Path]::GetFileName($OutputId) -ne $OutputId -or
    $OutputId.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "Money crop Teacher output ID must be one safe path segment."
}
if ($FarmLayoutSnapshotPaths.Count -eq 0) {
    throw "At least one bounded Farm layout snapshot is required."
}

$recordedAt = [DateTimeOffset]::Parse(
    $DecisionRecordedAt,
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::RoundtripKind)
if ($recordedAt.Offset -ne [TimeSpan]::Zero) {
    throw "Money crop Teacher decision timestamp must be explicit UTC."
}

$snapshot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $SnapshotPath
if (-not (Test-Path -LiteralPath $snapshot -PathType Leaf)) {
    throw "Money crop Teacher source snapshot is missing: $snapshot"
}
$layoutSnapshots = @($FarmLayoutSnapshotPaths | ForEach-Object {
    $resolved = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $_
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Money crop Teacher Farm layout snapshot is missing: $resolved"
    }
    $resolved
})
$distinctLayouts = @($layoutSnapshots | Sort-Object -Unique)
if ($distinctLayouts.Count -ne $layoutSnapshots.Count) {
    throw "Money crop Teacher Farm layout snapshots must be unique."
}

$baseLedger = ""
if (-not [string]::IsNullOrWhiteSpace($BaseStrategyLedgerPath)) {
    $baseLedger = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $BaseStrategyLedgerPath
    if (-not (Test-Path -LiteralPath $baseLedger -PathType Leaf)) {
        throw "Money crop Teacher base strategy ledger is missing: $baseLedger"
    }
}

$outputBase = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputRoot
$outputDirectory = Join-Path $outputBase $OutputId
if (Test-Path -LiteralPath $outputDirectory) {
    throw "Money crop Teacher output directory already exists: $outputDirectory"
}
$teacherRequestPath = Join-Path $outputDirectory `
    "crop-production-teacher-request.json"
$decisionPath = Join-Path $outputDirectory `
    "crop-production-decision-request.json"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

if (-not $SkipBuild) {
    & dotnet build $bootstrapProject -c Release --no-restore --nologo `
        -p:GameFreeGovernance=true | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Goal-conditioned bootstrap Release build failed."
    }
}
if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
    throw "Goal-conditioned bootstrap Release DLL is missing: $bootstrapDll"
}

New-Item -ItemType Directory -Path $outputDirectory | Out-Null
Write-JsonFile -Path $teacherRequestPath -Value ([ordered]@{
    schema_version = "money_revenue_crop_production_teacher_request.v1"
    snapshot_path = $snapshot
    farm_layout_snapshot_paths = @($layoutSnapshots)
    base_strategy_ledger_path = $baseLedger
    decision_id = $DecisionId
    decision_recorded_at = $recordedAt.ToUniversalTime().ToString("o")
    maximum_tile_count = $MaximumTileCount
    max_queue_item_count = $MaxQueueItemCount
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-crop-production-teacher-decision",
    "--request", $teacherRequestPath,
    "--output", $decisionPath)

$decision = Get-Content -LiteralPath $decisionPath -Raw | ConvertFrom-Json
if ([string]$decision.schema_version -ne
        "money_revenue_crop_production_decision_request.v2" -or
    [string]$decision.goal_id -ne "earn_money" -or
    [bool]$decision.formal_product_training_authorized -or
    [string]$decision.teacher_selection_policy -ne
        "authoritative_transparent_crop_net_revenue_then_earliest_terminal_v1" -or
    @($decision.commitments).Count -ne 1 -or
    @($decision.teacher_layout_snapshots).Count -ne $layoutSnapshots.Count -or
    @($decision.teacher_candidate_evaluations |
        Where-Object { [bool]$_.selected }).Count -ne 1) {
    throw "Money crop Teacher did not produce one complete v2 decision."
}
if (-not [string]::Equals(
        [IO.Path]::GetFullPath([string]$decision.teacher_request_path),
        $teacherRequestPath,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Money crop Teacher decision points at a different request."
}
Assert-RuntimeFileIdentity -Path $teacherRequestPath `
    -ExpectedSha256 ([string]$decision.teacher_request_sha256) `
    -Label "Money crop production Teacher request" | Out-Null

[ordered]@{
    status = "money_crop_production_teacher_decision_ready"
    output_id = $OutputId
    teacher_request_path = $teacherRequestPath
    teacher_request_sha256 = Get-RuntimeFileSha256 -Path $teacherRequestPath
    decision_request_path = $decisionPath
    decision_request_sha256 = Get-RuntimeFileSha256 -Path $decisionPath
    decision_id = [string]$decision.decision_id
    selected_seed_id =
        [string]@($decision.teacher_candidate_evaluations |
            Where-Object { [bool]$_.selected })[0].seed_id
    selected_tile_count = [int]$decision.commitments[0].tile_count
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 16
