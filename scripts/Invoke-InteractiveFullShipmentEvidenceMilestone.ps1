[CmdletBinding()]
param(
    [string] $TestLabRoot = "F:\StardewAI-TestLab",
    [string] $ProjectRoot = "",
    [string] $OutputRoot = "",
    [ValidateSet("high_risk", "standard", "all_missing", "all")]
    [string] $Batch = "high_risk",
    [int] $MaxScenarios = 1,
    [string[]] $ExistingSummaryPaths = @()
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path $TestLabRoot "repo-main-runtime"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $TestLabRoot `
        "artifacts\runtime-full-shipment-evidence-milestone-current"
}
if ($ExistingSummaryPaths.Count -eq 0) {
    $ExistingSummaryPaths = @(
        (Join-Path $TestLabRoot `
            "artifacts\runtime-full-shipment-sap-refresh\runtime-full-shipment-sap-refresh-20261005-183733\summary.json"),
        (Join-Path $TestLabRoot `
            "artifacts\runtime-full-shipment-radioactive-node\runtime-full-shipment-radioactive-node-20261005-134024\summary.json")
    )
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
$milestone = Join-Path $ProjectRoot `
    "scripts\Invoke-RuntimeFullShipmentEvidenceMilestone.ps1"
$requiredPaths = @(
    $dotnetExecutable,
    $authorityMirror,
    $milestone,
    (Join-Path $TestLabRoot `
        "runtime\Stardew Valley\StardewModdingAPI.exe")
) + $ExistingSummaryPaths
foreach ($path in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Interactive milestone input is missing: $path"
    }
}
if (Test-Path -LiteralPath "I:\") {
    throw "I: already exists; refusing to replace it with the authority mirror."
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

    & "$env:SystemRoot\System32\subst.exe" I: $authorityMirror | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath "I:\")) {
        throw "Failed to mount the portable authority mirror as I:."
    }
    $substCreated = $true

    & $milestone -ProjectRoot $ProjectRoot -OutputRoot $OutputRoot `
        -Batch $Batch -MaxScenarios $MaxScenarios `
        -ExistingSummaryPaths $ExistingSummaryPaths
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
