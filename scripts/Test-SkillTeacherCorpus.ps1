[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $CorpusPath,
    [Parameter(Mandatory)] [string] $OutputRoot,
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
$CorpusPath = (Resolve-Path -LiteralPath $CorpusPath).Path
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw "Skills corpus probes need a new directory." }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$bootstrap = Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$text = Get-Content -LiteralPath $CorpusPath -Raw
$corpus = $text | ConvertFrom-Json
$request = Get-Content -LiteralPath $corpus.request_path -Raw | ConvertFrom-Json
$protected = @($CorpusPath, $corpus.request_path, $request.knowledge_path) + @($request.sources.teacher_row_path)
$hashes = @{}
foreach ($path in $protected) { $hashes[$path] = (Get-FileHash -LiteralPath $path).Hash }
$replay = Join-Path $OutputRoot "replay.json"
& dotnet $bootstrap verify-skill-teacher-corpus --corpus $CorpusPath --output $replay | Out-Null
if ($LASTEXITCODE -ne 0 -or $hashes[$CorpusPath] -cne (Get-FileHash -LiteralPath $replay).Hash) { throw "Skills corpus public Verify differs." }
foreach ($case in @("criterion", "partition", "formal", "native", "overwrite")) {
    $changed = $text | ConvertFrom-Json
    switch ($case) {
        "criterion" { $changed.rows[0].criterion_id = "player_level_25" }
        "partition" { $changed.rows[0].dataset_partition = "test" }
        "formal" { $changed.formal_product_training_authorized = $true }
        "native" { $changed.rows[0].native_outcome_verified = $false }
    }
    $fixture = Join-Path $OutputRoot ($case + ".json")
    $output = if ($case -eq "overwrite") { $CorpusPath } else { Join-Path $OutputRoot ($case + "-must-not-exist.json") }
    Write-JsonFile $fixture $changed
    $diagnostic = & dotnet $bootstrap verify-skill-teacher-corpus --corpus $fixture --output $output 2>&1 | Out-String
    $code = $LASTEXITCODE
    Write-Utf8Text -Path (Join-Path $OutputRoot ($case + ".log")) -Value $diagnostic
    if ($code -eq 0 -or ($case -ne "overwrite" -and (Test-Path -LiteralPath $output))) { throw "Skills corpus accepted a forged claim: $case" }
}

$singleRequest = Join-Path $OutputRoot "only15-request.json"
$singleCorpus = Join-Path $OutputRoot "only15-corpus.json"
$singleRow = @($corpus.rows | Where-Object criterion_id -eq "player_level_15") | Select-Object -First 1
if ($null -eq $singleRow) { throw "Probe requires a genuine Level 15 source." }
Write-JsonFile $singleRequest ([ordered]@{ schema_version = "skill_teacher_corpus_request.v1"; corpus_id = "only15-negative-scope"
    knowledge_path = $request.knowledge_path; sources = @(@{source_id = "only15"; teacher_row_path = $singleRow.teacher_row_path})
    formal_product_training_authorized = $false })
& dotnet $bootstrap build-skill-teacher-corpus --request $singleRequest --output $singleCorpus | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Original Level 15 source failed to build." }
$coverageRequest = Join-Path $OutputRoot "coverage-request.json"
$coveragePath = Join-Path $OutputRoot "coverage.json"
Write-JsonFile $coverageRequest ([ordered]@{schema_version = "goal_method_teacher_coverage_request.v1"; coverage_id = "only15-negative-scope"
    sources = @(@{source_id = "only15"; source_kind = "skill_level_threshold_corpus"; artifact_path = $singleCorpus})
    formal_product_training_authorized = $false})
$arguments = @("build-goal-method-teacher-coverage", "--request", $coverageRequest,
    "--expansion", (Join-Path $ProjectRoot "experiments\goal-method-expansion-overlay.v1.json"),
    "--dependencies", (Join-Path $ProjectRoot "experiments\goal-method-dependency-expansions.v1.json"),
    "--isolated-training-authorization", (Join-Path $ProjectRoot "experiments\isolated-training-authorization.v1.json"),
    "--requirement-inventory", (Join-Path $ProjectRoot "experiments\local-data\output\authoritative-requirement-inventory-v1.json"),
    "--acquisition-lowering", (Join-Path $ProjectRoot "experiments\local-data\output\acquisition-route-option-lowering-v1.json"),
    "--acquisition-lowering-catalog", (Join-Path $ProjectRoot "experiments\acquisition-route-option-lowering.v1.json"),
    "--knowledge", $request.knowledge_path,
    "--option-matrix", (Join-Path $ProjectRoot "experiments\local-data\current-knowledge\option-governance-matrix.json"),
    "--claim-ledger", (Join-Path $ProjectRoot "experiments\goal-claim-conflict-ledger.v1.json"),
    "--direction-catalog-source", (Join-Path $ProjectRoot "src\StardewAI.Core\Training\GrandpaDirectionCatalog.cs"),
    "--output", $coveragePath)
& dotnet $bootstrap @arguments | Out-Null
if ($LASTEXITCODE -notin @(0, 1, 2) -or -not (Test-Path -LiteralPath $coveragePath)) { throw "Original coverage CLI failed before its expected incomplete gate." }
$coverage = Get-Content -LiteralPath $coveragePath -Raw | ConvertFrom-Json
$fifteenRows = @($coverage.criteria | Where-Object criterion_id -eq "player_level_15")
$twentyFiveRows = @($coverage.criteria | Where-Object criterion_id -eq "player_level_25")
if ($fifteenRows.Count -ne 1 -or $twentyFiveRows.Count -ne 1 -or @($coverage.criteria).Count -ne 19) {
    throw "Original coverage lost or duplicated an authoritative criterion."
}
$fifteen = $fifteenRows[0]
$twentyFive = $twentyFiveRows[0]
if ($coverage.criterion_denominator_count -ne 19 -or $coverage.formal_product_training_authorized -or
    -not $fifteen.teacher_comparison_covered -or -not $fifteen.native_outcome_covered -or
    $twentyFive.teacher_comparison_covered -or $twentyFive.native_outcome_covered -or $twentyFive.coverage_gate_ready -or
    @($twentyFive.teacher_source_kinds).Count -ne 0 -or @($twentyFive.teacher_comparison_partitions).Count -ne 0 -or
    @($twentyFive.native_outcome_partitions).Count -ne 0) {
    throw "Level 15-only source credited Level 25 or altered the original denominator/gate."
}
foreach ($path in $hashes.Keys) { if ($hashes[$path] -cne (Get-FileHash -LiteralPath $path).Hash) { throw "Corpus probe changed input: $path" } }
Write-Output "PASS: public corpus replay identical, five forged/overwrite claims rejected, original 19-criterion coverage gives Level 25 no Level 15-only credit; inputs unchanged."
