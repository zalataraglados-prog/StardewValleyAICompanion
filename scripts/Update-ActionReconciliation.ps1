[CmdletBinding()]
param(
    [string]$KnowledgeRoot = "I:\StardewAI-KnowledgeArtifacts\game-1.6.15",
    [string]$DecompileRoot = "I:\StardewValleyAICompanion-decompile-linux-server-1.6.15",
    [string]$GamePath = "E:\StardewValleyAICompanion-runtime\Stardew Valley",
    [string]$SnapshotPath = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot "artifacts\action-reconciliation-current"
$catalogRoot = Join-Path $projectRoot "catalogs\vanilla-1.6.15"
$compilerProject = Join-Path $projectRoot "tools\StardewAI.KnowledgeCompiler\StardewAI.KnowledgeCompiler.csproj"
$denominatorFreeze = Join-Path $catalogRoot "native-action-denominator-freeze.json"
$exportRoot = Join-Path $KnowledgeRoot "raw\game-1.6.15-20260723T093543Z"
$currentSnapshot = Join-Path $KnowledgeRoot "snapshots\current-live-full-snapshot.json"
$explicitSnapshot = -not [string]::IsNullOrWhiteSpace($SnapshotPath)
$snapshot = if ($explicitSnapshot) {
    [System.IO.Path]::GetFullPath($SnapshotPath)
} else {
    $currentSnapshot
}
if (-not (Test-Path -LiteralPath $snapshot -PathType Leaf)) {
    throw "Live full snapshot schema is missing: $snapshot"
}
if (-not $explicitSnapshot) {
    $lockPath = Join-Path $projectRoot "knowledge-artifacts.lock.json"
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    if ($null -eq $lock.current_snapshot -or [int]$lock.current_snapshot.blocking_count -ne 0) {
        throw "Current live snapshot has no zero-blocking checked-in lock entry: $lockPath"
    }
    $snapshotHash = (Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($snapshotHash -ne [string]$lock.current_snapshot.sha256) {
        throw "Current live snapshot hash differs from the checked-in lock: snapshot=$snapshot"
    }
}
$runtimeRoot = Join-Path $KnowledgeRoot "runtime-binaries\linux-server-1.6.15-20260719"

if (-not $NoBuild) {
    & dotnet build $compilerProject --no-restore "-p:GamePath=$GamePath" --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "KnowledgeCompiler build failed with exit code $LASTEXITCODE."
    }
}

$compilerArguments = @(
    "--export-root", $exportRoot,
    "--output", $outputRoot,
    "--snapshot-schema", $snapshot,
    "--game-assembly", (Join-Path $runtimeRoot "Stardew Valley.dll"),
    "--game-data-assembly", (Join-Path $runtimeRoot "StardewValley.GameData.dll"),
    "--decompile-root", $DecompileRoot
)
if (Test-Path -LiteralPath $denominatorFreeze -PathType Leaf) {
    $compilerArguments += @("--action-denominator-freeze", $denominatorFreeze)
}

& dotnet run --project $compilerProject --no-build -- @compilerArguments
$compilerExitCode = $LASTEXITCODE

$files = @(
    "native-action-denominator-fingerprint.json",
    "native-action-surface-inventory.json",
    "native-action-branch-inventory.json",
    "native-map-interaction-coverage.json",
    "semantic-action-catalog.json",
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

Write-Host "Action reconciliation used snapshot schema $snapshot."
Write-Host "Action reconciliation refreshed at $catalogRoot (compiler exit $compilerExitCode)."
exit $compilerExitCode
