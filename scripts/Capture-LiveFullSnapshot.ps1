[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $OutputPath = "artifacts\live-full-snapshot\full-schema-snapshot.json",
    [int] $StartupTimeoutSeconds = 150,
    [switch] $NoBuild,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $ProjectRoot $OutputPath))
}

if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slot exists under $savesPath"
    }
    $SaveSlot = $slot.Name
}
if (-not (Test-Path -LiteralPath (Join-Path $savesPath $SaveSlot) -PathType Container)) {
    throw "Isolated save slot not found: $SaveSlot"
}

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot `
    -RuntimeRoot $RuntimeRoot `
    -NoBuild:$NoBuild | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot `
    -RuntimeRoot $RuntimeRoot `
    -NoBuild:$NoBuild | Out-Null

$previousEnvironment = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $gameProcess = Start-Process `
        -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden `
        -PassThru

    $routeUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=route&fresh=1"
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $loaded = $false
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $route = Invoke-RestMethod -Method Get -Uri $routeUrl -TimeoutSec 15
            if ($route.schema_version -eq "snapshot.v1" -and
                $route.save_id.status -in @("available", "derived")) {
                $loaded = $true
                break
            }
            $lastError = "save_status=$($route.save_id.status)"
        } catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    if (-not $loaded) {
        throw "Timed out waiting for isolated save. Last status: $lastError"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutput) | Out-Null
    Invoke-WebRequest `
        -Method Get `
        -Uri "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1" `
        -OutFile $resolvedOutput `
        -TimeoutSec 180

    $snapshot = Get-Content -LiteralPath $resolvedOutput -Raw | ConvertFrom-Json
    if ($snapshot.schema_version -ne "snapshot.v1" -or
        $snapshot.save_id.status -notin @("available", "derived")) {
        throw "Captured file is not a loaded snapshot.v1 document."
    }

    [pscustomobject]@{
        status = "captured"
        path = $resolvedOutput
        bytes = (Get-Item -LiteralPath $resolvedOutput).Length
        save_status = $snapshot.save_id.status
        location = $snapshot.state.player.location_id.value
        state_hash = $snapshot.state_hash
    } | ConvertTo-Json -Depth 4
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        Set-Item -Path ("Env:" + $name) -Value $previousEnvironment[$name]
    }
    if (-not $KeepGameRunning -and
        $null -ne $gameProcess -and
        -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
