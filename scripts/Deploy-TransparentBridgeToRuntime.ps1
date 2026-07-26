param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$GamePath = (Join-Path $RuntimeRoot "Stardew Valley"),
    [string]$RuntimeModsDir = (Join-Path $RuntimeRoot "Stardew Valley\Mods"),
    [switch]$NoBuild,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$sourceDir = Join-Path $ProjectRoot "src\StardewAI.TransparentBridge\bin\Debug\net6.0"
$targetDir = Join-Path $RuntimeModsDir "StardewAI.TransparentBridge"
$requiredFiles = @(
    "manifest.json",
    "StardewAI.TransparentBridge.dll",
    "StardewAI.TransparentBridge.deps.json",
    "StardewAI.Contracts.dll"
)

if (-not $NoBuild -and -not $DryRun) {
    & dotnet build (Join-Path $ProjectRoot "src\StardewAI.TransparentBridge\StardewAI.TransparentBridge.csproj") -c Debug --nologo "-p:GamePath=$GamePath"
    if ($LASTEXITCODE -ne 0) {
        throw "TransparentBridge Debug build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $sourceDir)) {
    throw "TransparentBridge build output not found: $sourceDir"
}

foreach ($file in $requiredFiles) {
    $sourcePath = Join-Path $sourceDir $file
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required build output missing: $sourcePath"
    }
}

if ($DryRun) {
    [pscustomobject]@{
        status = "dry_run"
        source_dir = $sourceDir
        target_dir = $targetDir
        files = $requiredFiles
        preserves = "config.json"
    } | ConvertTo-Json -Depth 4
    exit 0
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
foreach ($file in $requiredFiles) {
    Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination (Join-Path $targetDir $file) -Force
}

[pscustomobject]@{
    status = "deployed"
    source_dir = $sourceDir
    target_dir = $targetDir
    files = $requiredFiles
    preserves = "config.json"
} | ConvertTo-Json -Depth 4
