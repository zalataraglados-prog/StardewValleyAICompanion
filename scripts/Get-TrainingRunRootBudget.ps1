param(
    [Parameter(Mandatory = $true)]
    [string] $RunRoot,
    [Parameter(Mandatory = $true)]
    [string] $RunRootPrefix,
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxRetainedRunRoots = 24,
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxHistoricalRunLogicalMb = 12288
)

$ErrorActionPreference = "Stop"

function Measure-RunRootLogicalSize {
    param([Parameter(Mandatory = $true)][string] $Path)

    $probeDestination = Join-Path `
        ([IO.Path]::GetTempPath()) `
        "stardewai-run-root-budget-null"
    $output = & robocopy.exe `
        $Path `
        $probeDestination `
        /L /S /BYTES /XJ /R:0 /W:0 /NFL /NDL /NJH /NP 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ge 8) {
        throw "Run-root measurement failed for $Path (robocopy=$exitCode)."
    }

    $text = @($output) -join [Environment]::NewLine
    $numericSummaryRows = [regex]::Matches(
        $text,
        '(?m)^[ \t]*[^:\r\n]+:[ \t]+([0-9]+)' +
        '(?:[ \t]+[0-9]+){5}[ \t]*\r?$')
    if ($numericSummaryRows.Count -lt 3) {
        throw "Run-root measurement summary was not parseable for $Path."
    }

    [pscustomobject]@{
        FileCount = [int64]$numericSummaryRows[1].Groups[1].Value
        LogicalBytes = [int64]$numericSummaryRows[2].Groups[1].Value
    }
}

if ([string]::IsNullOrWhiteSpace($RunRootPrefix)) {
    throw "RunRootPrefix cannot be empty."
}
if ([Management.Automation.WildcardPattern]::ContainsWildcardCharacters(
        $RunRootPrefix)) {
    throw "RunRootPrefix must be a literal prefix."
}

$resolvedRunRoot = [IO.Path]::GetFullPath($RunRoot).TrimEnd('\')
$runRootParent = Split-Path -Parent $resolvedRunRoot
if ([string]::IsNullOrWhiteSpace($runRootParent) -or
    -not (Test-Path -LiteralPath $runRootParent -PathType Container)) {
    throw "Run-root parent does not exist: $runRootParent"
}

$runRootLeaf = Split-Path -Leaf $resolvedRunRoot
$blockingReasons = [Collections.Generic.List[string]]::new()
if (-not $runRootLeaf.StartsWith(
        $RunRootPrefix,
        [StringComparison]::Ordinal)) {
    $blockingReasons.Add("current_run_root_outside_managed_prefix")
}

$entries = [Collections.Generic.List[object]]::new()
$totalLogicalBytes = [int64]0
$managedRoots = @(Get-ChildItem `
        -LiteralPath $runRootParent `
        -Force `
        -Directory `
        -ErrorAction Stop |
    Where-Object {
        $_.Name.StartsWith(
            $RunRootPrefix,
            [StringComparison]::Ordinal)
    } |
    Sort-Object Name)

foreach ($managedRoot in $managedRoots) {
    if (($managedRoot.Attributes -band
            [IO.FileAttributes]::ReparsePoint) -ne 0) {
        $blockingReasons.Add(
            "managed_run_root_is_reparse_point:$($managedRoot.Name)")
        continue
    }

    $measure = Measure-RunRootLogicalSize -Path $managedRoot.FullName
    $logicalBytes = [int64]$measure.LogicalBytes
    $totalLogicalBytes += $logicalBytes
    $entries.Add([ordered]@{
            run_root = $managedRoot.FullName
            logical_bytes = $logicalBytes
            logical_mib = [math]::Round($logicalBytes / 1MB, 3)
            file_count = [int64]$measure.FileCount
            last_write_utc = $managedRoot.LastWriteTimeUtc.ToString("o")
        })
}

$currentRootAlreadyCounted = Test-Path `
    -LiteralPath $resolvedRunRoot `
    -PathType Container
$projectedRunRootCount = $managedRoots.Count + $(
    if ($currentRootAlreadyCounted) { 0 } else { 1 })
$totalLogicalMb = [int64][math]::Ceiling($totalLogicalBytes / 1MB)

if ($projectedRunRootCount -gt $MaxRetainedRunRoots) {
    $blockingReasons.Add("projected_run_root_count_exceeds_limit")
}
if ($totalLogicalMb -gt $MaxHistoricalRunLogicalMb) {
    $blockingReasons.Add("historical_run_logical_budget_exceeded")
}

[ordered]@{
    schema_version = "stardewai.training_run_root_budget.v1"
    status = if ($blockingReasons.Count -eq 0) { "passed" } else { "blocked" }
    current_run_root = $resolvedRunRoot
    current_run_root_already_counted = $currentRootAlreadyCounted
    managed_parent_root = $runRootParent
    managed_run_root_prefix = $RunRootPrefix
    existing_run_root_count = $managedRoots.Count
    projected_run_root_count = $projectedRunRootCount
    max_retained_run_roots = $MaxRetainedRunRoots
    historical_run_logical_bytes = $totalLogicalBytes
    historical_run_logical_mib = $totalLogicalMb
    max_historical_run_logical_mb = $MaxHistoricalRunLogicalMb
    blocking_reasons = $blockingReasons.ToArray()
    entries = $entries.ToArray()
    cleanup_performed = $false
} | ConvertTo-Json -Depth 8
