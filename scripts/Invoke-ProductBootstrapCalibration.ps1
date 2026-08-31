param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory = $true)]
    [string] $SavesPath,
    [Parameter(Mandatory = $true)]
    [string] $SaveSlot,
    [string] $OutputRoot = "E:\StardewAITraining\bootstrap-runs",
    [string] $RunId = ("product-bootstrap-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [int] $MaxAttempts = 8,
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Json([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 15
            if ($null -ne $value) { return $value }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $last"
}

function Wait-World([string] $SnapshotUrl, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $SnapshotUrl -TimeoutSec 30
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.identity.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for the isolated bootstrap save."
}

$savesFullPath = [IO.Path]::GetFullPath($SavesPath)
$saveDirectory = Join-Path $savesFullPath $SaveSlot
$saveFile = Join-Path $saveDirectory $SaveSlot
if (-not (Test-Path -LiteralPath $saveFile -PathType Leaf)) {
    throw "Save slot file was not found: $saveFile"
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) {
    throw "SMAPI executable was not found: $smapi"
}

$backendUrl = "http://127.0.0.1:$BackendPort"
$productUrl = "http://127.0.0.1:$ProductPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
foreach ($port in @(8765, 8767, $BackendPort, $ProductPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Bootstrap calibration requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $RunId
$journalRoot = Join-Path $artifactDirectory "product-journal"
$trainingRoot = Join-Path $artifactDirectory "training"
New-Item -ItemType Directory -Path $artifactDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$productDll = Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\bin\Release\net8.0\StardewAI.ProductExecutor.dll"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
foreach ($path in @($backendDll, $productDll, $loopDll)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Release binary was not found: $path"
    }
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_PRODUCT_EXECUTOR_URL",
    "STARDEWAI_NATIVE_EXECUTOR_URL", "STARDEWAI_BRIDGE_SNAPSHOT_URL",
    "STARDEWAI_PRODUCT_JOURNAL_ROOT", "STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT",
    "STARDEWAI_PRODUCT_RUN_ID", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$game = $null
$backend = $null
$product = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesFullPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesFullPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process dotnet -ArgumentList @($backendDll) -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $env:STARDEWAI_PRODUCT_EXECUTOR_URL = $productUrl
    $env:STARDEWAI_NATIVE_EXECUTOR_URL = "http://127.0.0.1:8767"
    $env:STARDEWAI_BRIDGE_SNAPSHOT_URL = $snapshotUrl
    $env:STARDEWAI_PRODUCT_JOURNAL_ROOT = $journalRoot
    $env:STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT = $savesFullPath
    $env:STARDEWAI_PRODUCT_RUN_ID = $RunId
    $product = Start-Process dotnet -ArgumentList @($productDll) -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "product.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "product.stderr.log") -PassThru
    $productHealth = Wait-Json "$productUrl/health" 60
    if ($productHealth.status -ne "ready" -or [int]$productHealth.product_executor_count -le 0) {
        throw "Product executor health contract failed."
    }

    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    Wait-Json "http://127.0.0.1:8767/health" $StartupTimeoutSeconds | Out-Null
    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    Write-Json (Join-Path $artifactDirectory "initial-snapshot.json") $initial

    $arguments = @(
        $loopDll,
        "--root", $trainingRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $productUrl,
        "--use-product-executor",
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $savesFullPath,
        "--max-attempts", $MaxAttempts,
        "--required-verified-actions", 1,
        "--skip-training",
        "--use-daily-plan",
        "--daily-plan-max-candidates", 4,
        "--continue-after-blocked-queue-items",
        "--sleep-ms", 1000,
        "--after-snapshot-wait-ms", 500
    )
    $loopOutput = & dotnet $arguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $artifactDirectory "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Product bootstrap LiveTrainingLoop failed with exit code $LASTEXITCODE."
    }

    $trajectoryPath = Join-Path $trainingRoot "datasets\policy-decision-trajectories.jsonl"
    if (-not (Test-Path -LiteralPath $trajectoryPath -PathType Leaf)) {
        throw "Bootstrap calibration produced no policy trajectory file."
    }
    $trajectories = @(Get-Content -LiteralPath $trajectoryPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_ | ConvertFrom-Json })
    $accepted = @($trajectories | Where-Object {
        $_.schema_version -eq "policy_decision_trajectory.v2" -and
        $_.versions.executor -eq "product_executor.v1" -and
        $_.outcome.status -eq "applied" -and
        $_.outcome.success -eq $true -and
        $_.outcome.after_snapshot_fresh -eq $true -and
        @($_.candidates | Where-Object { $_.available -and $_.admitted_for_policy }).Count -ge 2
    })
    if ($accepted.Count -lt 1) {
        throw "Bootstrap calibration produced no Product trajectory with at least two admitted candidates."
    }

    $summary = [ordered]@{
        schema_version = "stardewai.product_bootstrap_calibration.v1"
        status = "passed"
        run_id = $RunId
        save_id = [string]$initial.state.identity.save_id.value
        year = [int]$initial.state.time.year.value
        season = [string]$initial.state.time.season.value
        day = [int]$initial.state.time.day.value
        trajectory_path = $trajectoryPath
        trajectory_count = $trajectories.Count
        accepted_multi_candidate_count = $accepted.Count
        selected_option_id = [string]$accepted[0].selection.option_id
        admitted_candidate_count = @($accepted[0].candidates | Where-Object { $_.available -and $_.admitted_for_policy }).Count
        product_executor_count = [int]$productHealth.product_executor_count
        formal_training_started = $false
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    foreach ($process in @($game, $product, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
