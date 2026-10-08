[CmdletBinding()]
param(
    [string] $TestLabRoot = "F:\StardewAI-TestLab",
    [string] $ProjectRoot = "",
    [Parameter(Mandatory = $true)]
    [string] $Scenario,
    [string] $RunId = ("runtime-full-shipment-sample-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path $TestLabRoot "repo-main-runtime"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $TestLabRoot `
        "artifacts\runtime-full-shipment-sample-current"
}

$currentSessionId = (Get-Process -Id $PID).SessionId
$interactiveExplorer = @(Get-Process explorer -ErrorAction SilentlyContinue |
    Where-Object SessionId -eq $currentSessionId)
if ($interactiveExplorer.Count -eq 0) {
    throw "Full Shipment native evidence requires an interactive desktop session."
}

$dotnetRoot = Join-Path $TestLabRoot "dotnet"
$dotnetExecutable = Join-Path $dotnetRoot "dotnet.exe"
$authorityMirror = Join-Path $TestLabRoot "authority-mirror"
$runner = Join-Path $ProjectRoot `
    "scripts\Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1"
$runtimeRoot = Join-Path $TestLabRoot "runtime"
$archivedFreshSaveRoot = Join-Path $TestLabRoot `
    "inputs\fresh-save\ProofFarm_450250338"
$requirementInventory = Join-Path $TestLabRoot `
    "inputs\authoritative-requirement-inventory-v1.json"
$acquisitionLowering = Join-Path $TestLabRoot `
    "inputs\acquisition-route-option-lowering-v1.json"
$masterAnglerWindows = Join-Path $TestLabRoot `
    "inputs\master-angler-stage-one-window-index-v1.json"
$routeTimingCalibration = Join-Path $TestLabRoot `
    "route-timing-calibration.json"
$requiredPaths = @(
    $dotnetExecutable,
    $authorityMirror,
    $runner,
    (Join-Path $runtimeRoot "Stardew Valley\StardewModdingAPI.exe"),
    $archivedFreshSaveRoot,
    $requirementInventory,
    $acquisitionLowering,
    $masterAnglerWindows,
    $routeTimingCalibration
)
foreach ($path in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Interactive Full Shipment sample input is missing: $path"
    }
}

$expectedAuthorityMapping = "I:\: => $authorityMirror"
$authorityMappingLines = @(
    & "$env:SystemRoot\System32\subst.exe" |
        ForEach-Object { ([string]$_).Trim() }
)
$authorityMappingPresent = @($authorityMappingLines |
    Where-Object { $_ -ieq $expectedAuthorityMapping }).Count -eq 1
if ((Test-Path -LiteralPath "I:\") -and -not $authorityMappingPresent) {
    throw "I: exists but is not the expected authority mirror mapping."
}

$environmentNames = @(
    "DOTNET_ROOT",
    "DOTNET_CLI_HOME",
    "NUGET_PACKAGES",
    "PATH"
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$substCreated = $false
try {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_CLI_HOME = Join-Path $TestLabRoot "dotnet-home"
    $env:NUGET_PACKAGES = Join-Path $TestLabRoot "nuget"
    $env:PATH = $dotnetRoot + ";" + $env:PATH

    if (-not $authorityMappingPresent) {
        & "$env:SystemRoot\System32\subst.exe" I: $authorityMirror | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath "I:\")) {
            throw "Failed to mount the portable authority mirror as I:."
        }
        $substCreated = $true
    }

    & $runner -ProjectRoot $ProjectRoot -RuntimeRoot $runtimeRoot `
        -ArchivedFreshSaveRoot $archivedFreshSaveRoot `
        -SaveSlot "ProofFarm_450250338" -RunId $RunId `
        -OutputRoot $OutputRoot `
        -RequirementInventory $requirementInventory `
        -AcquisitionLowering $acquisitionLowering `
        -MasterAnglerWindows $masterAnglerWindows `
        -RouteTimingCalibration $routeTimingCalibration `
        -Scenario $Scenario
    if ($LASTEXITCODE -ne 0) {
        throw "Interactive Full Shipment sample failed with exit $LASTEXITCODE."
    }
}
finally {
    if ($substCreated) {
        & "$env:SystemRoot\System32\subst.exe" I: /D | Out-Null
    }
    foreach ($name in $environmentNames) {
        $value = $previousEnvironment[$name]
        if ($null -eq $value) {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, [string]$value)
        }
    }
}
