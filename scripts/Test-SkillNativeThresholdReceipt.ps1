[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReceiptPath,
    [Parameter(Mandatory)] [string] $OutputRoot,
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
$ReceiptPath = (Resolve-Path -LiteralPath $ReceiptPath).Path
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw "Skills negative fixtures require a new output directory." }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$bootstrap = Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$receipt = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json
$requestPath = $receipt.request_path
$requestText = Get-Content -LiteralPath $requestPath -Raw
$request = $requestText | ConvertFrom-Json
$inputs = @($ReceiptPath, $requestPath, $request.preference_path, $request.action_queue_path,
    $request.execution_receipt_path, $request.after_snapshot_path)
$originalHashes = @{}
foreach ($path in $inputs) { $originalHashes[$path] = (Get-FileHash -LiteralPath $path).Hash }

$replay = Join-Path $OutputRoot "positive-replay.json"
& dotnet $bootstrap verify-skill-native-threshold-receipt --receipt $ReceiptPath --output $replay | Out-Null
if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $ReceiptPath).Hash -cne (Get-FileHash -LiteralPath $replay).Hash) {
    throw "Skills positive public Verify did not reproduce the original receipt."
}
foreach ($case in @("wrong_run", "wrong_threshold", "wrong_after", "forged_formal", "forged_hash", "overwrite_input")) {
    $fixture = Join-Path $OutputRoot ($case + ".json")
    $output = Join-Path $OutputRoot ($case + "-must-not-exist.json")
    if ($case -in @("forged_formal", "forged_hash")) {
        $changed = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json
        if ($case -eq "forged_formal") { $changed.formal_product_training_authorized = $true }
        else { $changed.source_sha256.before = ('0' * 64) }
        Write-JsonFile $fixture $changed
        $arguments = @("verify-skill-native-threshold-receipt", "--receipt", $fixture, "--output", $output)
    }
    else {
        $changed = $requestText | ConvertFrom-Json
        if ($case -eq "wrong_run") { $changed.run_id = "different-run" }
        if ($case -eq "wrong_threshold") { $changed.target_scoring_level = if ($request.target_scoring_level -eq 15) { 25 } else { 15 } }
        if ($case -eq "wrong_after") { $changed.after_snapshot_path = $request.preference_path }
        if ($case -eq "overwrite_input") { $output = $request.preference_path }
        Write-JsonFile $fixture $changed
        $arguments = @("build-skill-native-threshold-receipt", "--request", $fixture, "--output", $output)
    }
    $diagnostic = & dotnet $bootstrap @arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    Write-Utf8Text -Path (Join-Path $OutputRoot ($case + ".log")) -Value $diagnostic
    if ($exitCode -eq 0 -or ($case -ne "overwrite_input" -and (Test-Path -LiteralPath $output))) {
        throw "Skills negative public CLI accepted or wrote a success artifact: $case"
    }
}
foreach ($path in $inputs) {
    if ($originalHashes[$path] -cne (Get-FileHash -LiteralPath $path).Hash) { throw "Skills negative probe changed input: $path" }
}
Write-Output "PASS: public Verify identical, six negative cases rejected without success artifacts; original inputs unchanged."
