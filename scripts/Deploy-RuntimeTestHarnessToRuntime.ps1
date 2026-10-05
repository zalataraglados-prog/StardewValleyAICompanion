param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$GamePath = (Join-Path $RuntimeRoot "Stardew Valley"),
    [string]$RuntimeModsDir = (Join-Path $RuntimeRoot "Stardew Valley\Mods"),
    [switch]$NoBuild,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Deploy.Common.ps1")

$sourceDir = Join-Path $ProjectRoot "tools\StardewAI.RuntimeTestHarness\bin\Debug\net6.0"
$targetDir = Join-Path $RuntimeModsDir "StardewAI.RuntimeTestHarness"
$contractSource = Join-Path $ProjectRoot "src\StardewAI.Contracts\bin\Debug\netstandard2.1\StardewAI.Contracts.dll"
$runtimePrimitivesSource = Join-Path $ProjectRoot "src\StardewAI.RuntimePrimitives\bin\Debug\netstandard2.1\StardewAI.RuntimePrimitives.dll"
$harnessAssembly = Join-Path $sourceDir "StardewAI.RuntimeTestHarness.dll"
$buildConfigurationInputs = @(
    (Join-Path $ProjectRoot "Directory.Build.props"),
    (Join-Path $ProjectRoot "Directory.Build.targets"),
    (Join-Path $ProjectRoot "Directory.Packages.props")
)
$requiredFiles = @(
    "manifest.json",
    "StardewAI.RuntimeTestHarness.dll",
    "StardewAI.RuntimeTestHarness.deps.json"
)

if (-not $NoBuild -and -not $DryRun) {
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.RuntimeTestHarness\StardewAI.RuntimeTestHarness.csproj") -c Debug --nologo "-p:GamePath=$GamePath"
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

if ($NoBuild -and -not $DryRun) {
    Assert-StardewAIBuildOutputFresh `
        -OutputPath $harnessAssembly `
        -InputRoots @(
            (Join-Path $ProjectRoot "tools\StardewAI.RuntimeTestHarness"),
            (Join-Path $ProjectRoot "src\StardewAI.Contracts"),
            (Join-Path $ProjectRoot "src\StardewAI.RuntimePrimitives")
        ) `
        -InputFiles $buildConfigurationInputs
    Assert-StardewAIBuildOutputFresh `
        -OutputPath $contractSource `
        -InputRoots @((Join-Path $ProjectRoot "src\StardewAI.Contracts")) `
        -InputFiles $buildConfigurationInputs
    Assert-StardewAIBuildOutputFresh `
        -OutputPath $runtimePrimitivesSource `
        -InputRoots @((Join-Path $ProjectRoot "src\StardewAI.RuntimePrimitives")) `
        -InputFiles $buildConfigurationInputs
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
$deployedHashes = [ordered]@{}
foreach ($file in $requiredFiles) {
    $deployedHashes[$file] = Copy-StardewAIVerifiedFile `
        -SourcePath (Join-Path $sourceDir $file) `
        -DestinationPath (Join-Path $targetDir $file)
}
$deployedHashes["StardewAI.Contracts.dll"] = Copy-StardewAIVerifiedFile `
    -SourcePath $contractSource `
    -DestinationPath (Join-Path $targetDir "StardewAI.Contracts.dll")
$deployedHashes["StardewAI.RuntimePrimitives.dll"] = Copy-StardewAIVerifiedFile `
    -SourcePath $runtimePrimitivesSource `
    -DestinationPath (Join-Path $targetDir "StardewAI.RuntimePrimitives.dll")

[pscustomobject]@{
    status = "deployed"
    source_dir = $sourceDir
    target_dir = $targetDir
    files = $requiredFiles + @("StardewAI.Contracts.dll", "StardewAI.RuntimePrimitives.dll")
    deployed_sha256 = $deployedHashes
    preserves = "config.json"
} | ConvertTo-Json -Depth 4
