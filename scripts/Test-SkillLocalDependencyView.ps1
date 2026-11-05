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
if (Test-Path -LiteralPath $OutputRoot) { throw "Skills local-view probes need a new directory." }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$bootstrap = Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$dependencies = Join-Path $ProjectRoot "experiments\goal-method-dependency-expansions.v1.json"
$corpus = Get-Content -LiteralPath $CorpusPath -Raw | ConvertFrom-Json
$protected = @($CorpusPath, $corpus.request_path, $dependencies) + @($corpus.rows.teacher_row_path) + @($corpus.rows.native_receipt_path)
$hashes = @{}
foreach ($path in $protected) { $hashes[$path] = (Get-FileHash -LiteralPath $path).Hash }
$viewPath = Join-Path $OutputRoot "view.json"
$replayPath = Join-Path $OutputRoot "replay.json"
& dotnet $bootstrap build-skill-local-dependency-view --corpus $CorpusPath --dependencies $dependencies --output $viewPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Skills local view rejected original sources." }
& dotnet $bootstrap verify-skill-local-dependency-view --view $viewPath --output $replayPath | Out-Null
if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $viewPath).Hash -cne (Get-FileHash -LiteralPath $replayPath).Hash) {
    throw "Skills local view differs from its independent replay."
}
$text = Get-Content -LiteralPath $viewPath -Raw
foreach ($case in @("formal", "global", "other_root", "source", "overwrite")) {
    $changed = $text | ConvertFrom-Json
    switch ($case) {
        "formal" { $changed.skill_local_method_view.formal_product_training_authorized = $true }
        "global" { $changed.skill_local_method_view.global_dependency_blockers = @() }
        "other_root" { ($changed.directions | Where-Object direction_id -eq "earn_money").status = "complete" }
        "source" { $changed.skill_local_method_view.corpus_sha256 = "forged" }
    }
    $fixture = Join-Path $OutputRoot ($case + ".json")
    $output = if ($case -eq "overwrite") { $viewPath } else { Join-Path $OutputRoot ($case + "-must-not-exist.json") }
    Write-JsonFile $fixture $changed
    $diagnostic = & dotnet $bootstrap verify-skill-local-dependency-view --view $fixture --output $output 2>&1 | Out-String
    $code = $LASTEXITCODE
    Write-Utf8Text -Path (Join-Path $OutputRoot ($case + ".log")) -Value $diagnostic
    if ($code -eq 0 -or ($case -ne "overwrite" -and (Test-Path -LiteralPath $output))) { throw "Skills local view accepted forged scope: $case" }
}
foreach ($path in $hashes.Keys) { if ($hashes[$path] -cne (Get-FileHash -LiteralPath $path).Hash) { throw "Local-view probe changed input: $path" } }
Write-Output "PASS: local dependency view and independent replay identical; five forged-scope/overwrite cases rejected; original sources unchanged."
