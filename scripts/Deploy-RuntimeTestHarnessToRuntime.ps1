param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$RuntimeModsDir = (Join-Path $RuntimeRoot "Stardew Valley\Mods"),
    [switch]$NoBuild,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$sourceDir = Join-Path $ProjectRoot "tools\StardewAI.RuntimeTestHarness\bin\Debug\net6.0"
$targetDir = Join-Path $RuntimeModsDir "StardewAI.RuntimeTestHarness"
$contractSource = Join-Path $ProjectRoot "src\StardewAI.Contracts\bin\Debug\netstandard2.1\StardewAI.Contracts.dll"
$runtimePrimitivesSource = Join-Path $ProjectRoot "src\StardewAI.RuntimePrimitives\bin\Debug\netstandard2.1\StardewAI.RuntimePrimitives.dll"
$requiredFiles = @(
    "manifest.json",
    "StardewAI.RuntimeTestHarness.dll",
    "StardewAI.RuntimeTestHarness.deps.json"
)

if (-not $NoBuild -and -not $DryRun) {
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.RuntimeTestHarness\StardewAI.RuntimeTestHarness.csproj") -c Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "RuntimeTestHarness Debug build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $sourceDir)) {
    throw "RuntimeTestHarness build output not found: $sourceDir"
}

foreach ($file in $requiredFiles) {
    $sourcePath = Join-Path $sourceDir $file
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required build output missing: $sourcePath"
    }
}

if (-not (Test-Path -LiteralPath $contractSource)) {
    throw "Required contract output missing: $contractSource"
}
if (-not (Test-Path -LiteralPath $runtimePrimitivesSource)) {
    throw "Required runtime primitives output missing: $runtimePrimitivesSource"
}

if ($DryRun) {
    [pscustomobject]@{
        status = "dry_run"
        source_dir = $sourceDir
        target_dir = $targetDir
        files = $requiredFiles + @("StardewAI.Contracts.dll", "StardewAI.RuntimePrimitives.dll")
        preserves = "config.json"
    } | ConvertTo-Json -Depth 4
    exit 0
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
foreach ($file in $requiredFiles) {
    Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination (Join-Path $targetDir $file) -Force
}
Copy-Item -LiteralPath $contractSource -Destination (Join-Path $targetDir "StardewAI.Contracts.dll") -Force
Copy-Item -LiteralPath $runtimePrimitivesSource -Destination (Join-Path $targetDir "StardewAI.RuntimePrimitives.dll") -Force

[pscustomobject]@{
    status = "deployed"
    source_dir = $sourceDir
    target_dir = $targetDir
    files = $requiredFiles + @("StardewAI.Contracts.dll", "StardewAI.RuntimePrimitives.dll")
    preserves = "config.json"
} | ConvertTo-Json -Depth 4
