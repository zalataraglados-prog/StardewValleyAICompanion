[CmdletBinding()]
param(
    [string]$KnowledgeRoot = "I:\StardewAI-KnowledgeArtifacts\game-1.6.15",
    [string]$DecompileRoot = "I:\StardewValleyAICompanion-decompile-linux-server-1.6.15",
    [string]$GamePath = "E:\StardewValleyAICompanion-runtime\Stardew Valley"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot "artifacts\action-reconciliation-current"
$catalogRoot = Join-Path $projectRoot "catalogs\vanilla-1.6.15"
$compilerProject = Join-Path $projectRoot "tools\StardewAI.KnowledgeCompiler\StardewAI.KnowledgeCompiler.csproj"
$exportRoot = Join-Path $KnowledgeRoot "raw\game-1.6.15-20260723T093543Z"
$snapshot = Join-Path $KnowledgeRoot "snapshots\live-full-snapshot-20260719.json"
$runtimeRoot = Join-Path $KnowledgeRoot "runtime-binaries\linux-server-1.6.15-20260719"

& dotnet build $compilerProject --no-restore "-p:GamePath=$GamePath" --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "KnowledgeCompiler build failed with exit code $LASTEXITCODE."
}

& dotnet run --project $compilerProject --no-build -- `
    "--export-root" $exportRoot `
    "--output" $outputRoot `
    "--snapshot-schema" $snapshot `
    "--game-assembly" (Join-Path $runtimeRoot "Stardew Valley.dll") `
    "--game-data-assembly" (Join-Path $runtimeRoot "StardewValley.GameData.dll") `
    "--decompile-root" $DecompileRoot
$compilerExitCode = $LASTEXITCODE

$files = @(
    "native-action-surface-inventory.json",
    "action-implementation-reconciliation.json",
    "action-progress-dashboard.json"
)
New-Item -ItemType Directory -Path $catalogRoot -Force | Out-Null
foreach ($file in $files) {
    $source = Join-Path $outputRoot $file
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "KnowledgeCompiler did not produce required action catalog file: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $catalogRoot $file) -Force
}

Write-Host "Action reconciliation refreshed at $catalogRoot (compiler exit $compilerExitCode)."
exit $compilerExitCode
