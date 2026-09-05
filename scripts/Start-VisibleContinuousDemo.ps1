[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $DemoRoot = "E:\StardewAIVisualDemo",
    [string] $SaveSlot = "",
    [string] $PolicyCheckpointPath = "I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r35-round01-20260905-120836\canonical-state\checkpoints\structured-policy-latest.json",
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [int] $StartupTimeoutSeconds = 300,
    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 15
            if ($null -ne $value) { return $value }
        } catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-World([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 30
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.identity.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for the isolated demo save."
}

function Write-State([string] $Status, [hashtable] $Extra = @{}) {
    $state = [ordered]@{
        schema_version = "stardewai.visible_continuous_demo.v1"
        status = $Status
        run_id = $runId
        updated_at = [DateTimeOffset]::Now.ToString("O")
        visible = $true
        sound_enabled = $false
        save_isolation_path = $savesPath
        save_slot = $SaveSlot
        run_directory = $runDirectory
        host_process_id = $PID
    }
    foreach ($entry in $Extra.GetEnumerator()) { $state[$entry.Key] = $entry.Value }
    $state | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $statePath -Encoding utf8
}

$runtimeGameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $runtimeGameDirectory "StardewModdingAPI.exe"
$runtimeModsDirectory = Join-Path $runtimeGameDirectory "Mods"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$productDll = Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\bin\Release\net8.0\StardewAI.ProductExecutor.dll"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$backendUrl = "http://127.0.0.1:$BackendPort"
$productUrl = "http://127.0.0.1:$ProductPort"
$runId = "visible-continuous-" + (Get-Date -Format "yyyyMMdd-HHmmss")
$runDirectory = Join-Path $DemoRoot (Join-Path "runs" $runId)
$statePath = Join-Path $DemoRoot "current-demo.json"
$smapiModsPath = Join-Path $runDirectory "mods"

foreach ($path in @($smapiExecutable, $PolicyCheckpointPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required demo input was not found: $path"
    }
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated save root was not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1).Name
}
$saveFile = Join-Path (Join-Path $savesPath $SaveSlot) $SaveSlot
if (-not (Test-Path -LiteralPath $saveFile -PathType Leaf)) {
    throw "Isolated demo save slot was not found: $saveFile"
}

foreach ($port in @(8765, 8767, $BackendPort, $ProductPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Visible demo requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach to an unrelated game."
}

New-Item -ItemType Directory -Force -Path $runDirectory, $smapiModsPath | Out-Null
Write-State "preparing"

if (-not $NoBuild) {
    & dotnet build (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj") -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Backend Release build failed."
    }
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\StardewAI.ProductExecutor.csproj") -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "ProductExecutor Release build failed." }
    & dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed." }
}

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild:$NoBuild | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -NoBuild:$NoBuild | Out-Null

foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $source = Join-Path $runtimeModsDirectory $modName
    $target = Join-Path $smapiModsPath $modName
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Required runtime mod was not found: $source"
    }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
}

$backend = $null
$product = $null
$game = $null
$loop = $null
$restartCount = 0
$startedAt = Get-Date
try {
    $oldAspNetCoreUrls = $env:ASPNETCORE_URLS
    try {
        $env:ASPNETCORE_URLS = $backendUrl
        $backend = Start-Process dotnet -ArgumentList @($backendDll) -WorkingDirectory $ProjectRoot `
            -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runDirectory "backend.stdout.log") `
            -RedirectStandardError (Join-Path $runDirectory "backend.stderr.log") -PassThru
    } finally {
        $env:ASPNETCORE_URLS = $oldAspNetCoreUrls
    }
    Wait-Json "$backendUrl/health" 90 | Out-Null

    $env:STARDEWAI_PRODUCT_EXECUTOR_URL = $productUrl
    $env:STARDEWAI_NATIVE_EXECUTOR_URL = "http://127.0.0.1:8767"
    $env:STARDEWAI_BRIDGE_SNAPSHOT_URL = $snapshotUrl
    $env:STARDEWAI_PRODUCT_JOURNAL_ROOT = Join-Path $runDirectory "product-journal"
    $env:STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT = $savesPath
    $env:STARDEWAI_PRODUCT_RUN_ID = $runId
    $env:STARDEWAI_PRODUCT_NATIVE_TIMEOUT_SECONDS = "90"
    $product = Start-Process dotnet -ArgumentList @($productDll) -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runDirectory "product.stdout.log") `
        -RedirectStandardError (Join-Path $runDirectory "product.stderr.log") -PassThru
    Wait-Json "$productUrl/health" 90 | Out-Null

    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $runId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = Join-Path $runDirectory "runtime"
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "0"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smapiModsPath
    $game = Start-Process -FilePath $smapiExecutable -WorkingDirectory $runtimeGameDirectory `
        -RedirectStandardOutput (Join-Path $runDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $runDirectory "game.stderr.log") -PassThru
    Wait-Json "http://127.0.0.1:8767/health" $StartupTimeoutSeconds | Out-Null
    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $initial | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $runDirectory "initial-snapshot.json") -Encoding utf8

    $loopArguments = @(
        $loopDll,
        "--root", $DemoRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $productUrl,
        "--executor-timeout-seconds", "90",
        "--use-product-executor",
        "--no-manifest",
        "--run-id", $runId,
        "--artifact-run-id", $runId,
        "--save-isolation-path", $savesPath,
        "--save-slot", $SaveSlot,
        "--max-attempts", "2147483647",
        "--required-verified-actions", "0",
        "--skip-training",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "12",
        "--continue-after-blocked-queue-items",
        "--max-queue-item-attempts", "3",
        "--sleep-ms", "350",
        "--after-snapshot-wait-ms", "350",
        "--after-snapshot-poll-ms", "100",
        "--no-progress-backoff-ms", "250",
        "--no-progress-max-backoff-ms", "2000",
        "--artifact-retention-mode", "rolling",
        "--max-persisted-iterations", "48",
        "--min-free-space-mb", "4096",
        "--max-consecutive-errors", "8",
        "--policy-checkpoint-path", $PolicyCheckpointPath,
        "--require-structured-policy"
    )

    while (-not $game.HasExited) {
        if ($backend.HasExited) { throw "Demo backend exited unexpectedly." }
        if ($product.HasExited) { throw "Demo ProductExecutor exited unexpectedly." }
        if ($null -eq $loop -or $loop.HasExited) {
            if ($null -ne $loop) {
                $restartCount++
                Start-Sleep -Seconds ([Math]::Min(10, 1 + $restartCount))
            }
            $loop = Start-Process dotnet -ArgumentList $loopArguments -WorkingDirectory $ProjectRoot `
                -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runDirectory "loop.stdout.log") `
                -RedirectStandardError (Join-Path $runDirectory "loop.stderr.log") -PassThru
        }
        Write-State "running" @{
            backend_process_id = $backend.Id
            product_executor_process_id = $product.Id
            game_process_id = $game.Id
            live_training_loop_process_id = $loop.Id
            loop_restart_count = $restartCount
            started_at = $startedAt.ToString("O")
        }
        Start-Sleep -Seconds 5
        $game.Refresh(); $backend.Refresh(); $product.Refresh(); $loop.Refresh()
    }
    Write-State "game_closed" @{ loop_restart_count = $restartCount }
} catch {
    Write-State "failed" @{ error = $_.Exception.Message; loop_restart_count = $restartCount }
    throw
} finally {
    foreach ($process in @($loop, $product, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
