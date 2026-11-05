[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot ("..\artifacts\fishing-artifact-mapping\" + [guid]::NewGuid().ToString("N")))
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw "Fixture output must be new: $OutputRoot" }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1"), [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw "Fishing smoke parse failed." }
$function = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq "Resolve-CompactTrainingAttempts"
}, $true)
if ($null -eq $function) { throw "Original compact mapping function is missing." }
. ([scriptblock]::Create($function.Extent.Text))
foreach ($queue in @(
    @{ file = "compiled-queue-0001.json"; id = "initial" },
    @{ file = "continuation-compiled-queue-0001-item-0002.json"; id = "continuation" },
    @{ file = "compiled-queue-0005.json"; id = "sparse" }
)) {
    Write-JsonFile (Join-Path $OutputRoot $queue.file) @{ queue_id = $queue.id }
}
foreach ($ordinal in @("0001", "0005")) {
    Write-JsonFile (Join-Path $OutputRoot "execution-$ordinal.json") @{ status = "fixture_only" }
}
$dataset = Join-Path $OutputRoot "rows.jsonl"
$rows = @("continuation", "sparse") | ForEach-Object {
    @{ row_id = $_; queue_id = $_; action_features = @{ option_ids = @("executor.catch_fish"); changed_facts = @(); features = @{ boolean = @() } } } |
        ConvertTo-Json -Depth 6 -Compress
}
Write-Utf8Text -Path $dataset -Value ($rows -join "`n")
$attempts = @(Resolve-CompactTrainingAttempts -SnapshotDir $OutputRoot -DatasetPath $dataset)
if ($attempts.Count -ne 2 -or $attempts[0].iteration -ne 1 -or $attempts[1].iteration -ne 5 -or
    (Split-Path -Leaf $attempts[0].queue_path) -ne "continuation-compiled-queue-0001-item-0002.json") {
    throw "Compact rows did not bind original continuation/sparse queue identities."
}
$unknown = Join-Path $OutputRoot "unknown.jsonl"
Write-Utf8Text -Path $unknown -Value '{"row_id":"unknown","queue_id":"absent"}'
try {
    Resolve-CompactTrainingAttempts -SnapshotDir $OutputRoot -DatasetPath $unknown | Out-Null
    throw "Expected missing queue identity rejection."
}
catch { if ($_.Exception.Message -notlike 'Compact JSONL training row did not map*') { throw } }
Write-JsonFile (Join-Path $OutputRoot "continuation-compiled-queue-0001-item-0003.json") @{ queue_id = "continuation" }
try {
    Resolve-CompactTrainingAttempts -SnapshotDir $OutputRoot -DatasetPath $dataset | Out-Null
    throw "Expected duplicate queue identity rejection."
}
catch { if ($_.Exception.Message -notlike 'Duplicate compiled queue_id*') { throw } }
Write-Output "PASS: original continuation/sparse mapping; unknown and duplicate identities rejected. Fixtures retained at $OutputRoot"
