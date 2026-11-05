[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $RowPaths,
    [Parameter(Mandatory)] [string] $OutputRoot,
    [string] $LegacyTrajectoryPath = "",
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw "Skills row probes require a new output directory." }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$bootstrap = Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$datasetCli = Join-Path $ProjectRoot "tools\StardewAI.PolicyDataset\bin\Release\net8.0\StardewAI.PolicyDataset.dll"
$RowPaths = @($RowPaths | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
$inputHashes = @{}
$lines = @()
for ($index = 0; $index -lt $RowPaths.Count; $index++) {
    $path = $RowPaths[$index]
    $inputHashes[$path] = (Get-FileHash -LiteralPath $path).Hash
    $replay = Join-Path $OutputRoot "replay-$index.json"
    & dotnet $bootstrap verify-skill-teacher-row --row $path --output $replay | Out-Null
    if ($LASTEXITCODE -ne 0 -or $inputHashes[$path] -cne (Get-FileHash -LiteralPath $replay).Hash) {
        throw "Skills public row Verify differs: $path"
    }
    $row = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $preference = Get-Content -LiteralPath $row.audit.teacher_supervision.sources.preference_path -Raw | ConvertFrom-Json
    foreach ($skill in $preference.deficit.skills) {
        foreach ($field in @(@{ suffix = 'experience'; value = $skill.experience },
            @{ suffix = 'unmodified_level'; value = $skill.unmodifiedLevel })) {
            $name = 'player.skills.' + $skill.skillId + '.' + $field.suffix
            $feature = @($row.state_features.numeric | Where-Object name -eq $name)
            if ($feature.Count -ne 1 -or $feature[0].value -ne $field.value) { throw "Skills row omitted or changed native policy input: $name" }
        }
    }
    $lines += $row | ConvertTo-Json -Depth 100 -Compress
}
if (-not [string]::IsNullOrWhiteSpace($LegacyTrajectoryPath)) {
    $LegacyTrajectoryPath = (Resolve-Path -LiteralPath $LegacyTrajectoryPath).Path
    $inputHashes[$LegacyTrajectoryPath] = (Get-FileHash -LiteralPath $LegacyTrajectoryPath).Hash
    $lines += @(Get-Content -LiteralPath $LegacyTrajectoryPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}
$inputPath = Join-Path $OutputRoot "mixed-trajectories.jsonl"
Write-Utf8Text -Path $inputPath -Value (($lines -join "`n") + "`n")
$dataset = Join-Path $OutputRoot "dataset"
& dotnet $datasetCli --input $inputPath --output-root $dataset --no-horizon-observations | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Original dataset CLI rejected the independently replayed mixed Teacher rows." }
$manifest = Get-Content -LiteralPath (Join-Path $dataset "policy-dataset-manifest.json") -Raw | ConvertFrom-Json
if ($manifest.counts.accepted_rows -ne $lines.Count -or $manifest.counts.rejected_rows -ne 0) {
    throw "Mixed Teacher dataset lost or rejected input rows."
}
$rowText = Get-Content -LiteralPath $RowPaths[0] -Raw
foreach ($case in @("outcome", "native_hash", "missing_native", "provider", "partition", "legacy_schema", "return", "unknown_claim", "overwrite_input")) {
    $changed = $rowText | ConvertFrom-Json
    $teacher = $changed.audit.teacher_supervision
    switch ($case) {
        "outcome" { $changed.outcome.actual_ticks++ }
        "native_hash" { $teacher.skill_native_threshold_receipt_sha256 = ('f' * 64) }
        "missing_native" { $teacher.sources.skill_native_threshold_receipt_path = $null }
        "provider" { $teacher.teacher_id = "unknown" }
        "partition" { $changed.context.dataset_partition = if ($changed.context.dataset_partition -eq "train") { "validation" } else { "train" } }
        "legacy_schema" { $teacher.schema_version = "policy_teacher_supervision.v2" }
        "return" { $changed.returns.immediate++ }
        "unknown_claim" { $changed | Add-Member -NotePropertyName formal_product_training_authorized -NotePropertyValue $true }
    }
    $fixture = Join-Path $OutputRoot ($case + ".json")
    $output = if ($case -eq "overwrite_input") { $RowPaths[0] } else { Join-Path $OutputRoot ($case + "-must-not-exist.json") }
    Write-JsonFile $fixture $changed
    $diagnostic = & dotnet $bootstrap verify-skill-teacher-row --row $fixture --output $output 2>&1 | Out-String
    $code = $LASTEXITCODE
    Write-Utf8Text -Path (Join-Path $OutputRoot ($case + ".log")) -Value $diagnostic
    if ($code -eq 0 -or ($case -ne "overwrite_input" -and (Test-Path -LiteralPath $output))) { throw "Accepted invalid Skills row: $case" }
}
foreach ($path in $inputHashes.Keys) {
    if ($inputHashes[$path] -cne (Get-FileHash -LiteralPath $path).Hash) { throw "Row probe changed source: $path" }
}
[pscustomobject]@{ status = "passed"; accepted_rows = $manifest.counts.accepted_rows
    partitions = $manifest.partitions | Select-Object partition, rows
    negative_cases = 9; source_inputs_unchanged = $true; model_training_performed = $false } | ConvertTo-Json -Depth 5
