param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $TrainingRoot = "E:\StardewAITraining",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory = $true)]
    [string] $SavesPath,
    [Parameter(Mandatory = $true)]
    [string] $SaveSlot,
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [int] $MaxAttempts = 2000,
    [int] $RequiredVerifiedActions = 0,
    [switch] $Launch
)

$ErrorActionPreference = "Stop"

function Wait-Json([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
            if ($null -ne $value) { return $value }
        } catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

$root = [IO.Path]::GetFullPath($TrainingRoot)
$saves = [IO.Path]::GetFullPath($SavesPath)
$slotFile = Join-Path (Join-Path $saves $SaveSlot) $SaveSlot
if (-not (Test-Path -LiteralPath $slotFile -PathType Leaf)) {
    throw "Formal save slot file was not found: $slotFile"
}

$gameDirectory = Join-Path ([IO.Path]::GetFullPath($RuntimeRoot)) "Stardew Valley"
$gameExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$backendDll = Join-Path $ProjectRoot "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$productDll = Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\bin\Release\net8.0\StardewAI.ProductExecutor.dll"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$trajectoryPath = Join-Path $root "datasets\policy-decision-trajectories.jsonl"
$datasetManifestPath = Join-Path $root "datasets\formal-policy\policy-dataset-manifest.json"
$checkpointPath = Join-Path $root "checkpoints\structured-policy-latest.json"
foreach ($path in @(
    $gameExecutable,
    $backendDll,
    $productDll,
    $loopDll,
    $trajectoryPath,
    $datasetManifestPath,
    $checkpointPath
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required formal training input was not found: $path"
    }
}

foreach ($port in @(8765, 8767, $BackendPort, $ProductPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Formal training requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $root "runs\formal-local-$stamp"
$manifestPath = Join-Path $runDirectory "training-run-manifest.json"
$backendUrl = "http://127.0.0.1:$BackendPort"
$productUrl = "http://127.0.0.1:$ProductPort"
New-Item -ItemType Directory -Path $runDirectory | Out-Null

$backend = $null
$launchResult = $null
$keepBackend = $false
try {
    $previousUrls = $env:ASPNETCORE_URLS
    try {
        $env:ASPNETCORE_URLS = $backendUrl
        $backend = Start-Process dotnet -ArgumentList @($backendDll) -WorkingDirectory $ProjectRoot `
            -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runDirectory "backend.stdout.log") `
            -RedirectStandardError (Join-Path $runDirectory "backend.stderr.log") -PassThru
    } finally {
        $env:ASPNETCORE_URLS = $previousUrls
    }
    Wait-Json "$backendUrl/health" 180 | Out-Null

    $request = [ordered]@{
        mode = "formal_product_training"
        root_path = $root
        dataset_path = Join-Path $root "datasets\live-training-feature-rows.jsonl"
        report_path = Join-Path $runDirectory "training-report.json"
        checkpoint_path = $checkpointPath
        policy_trajectory_path = $trajectoryPath
        policy_dataset_manifest_path = $datasetManifestPath
        product_receipt_root = Join-Path $runDirectory "product-receipts"
        product_executor_url = $productUrl
        native_executor_url = "http://127.0.0.1:8767"
        product_executor_executable_path = $productDll
        live_training_loop_executable_path = $loopDll
        max_attempts = $MaxAttempts
        required_verified_actions = $RequiredVerifiedActions
        manifest_path = $manifestPath
        game_executable_path = $gameExecutable
        game_working_directory = $gameDirectory
        save_isolation_path = $saves
        save_slot = $SaveSlot
        bridge_url = "http://127.0.0.1:8765"
        backend_url = $backendUrl
        allow_game_launch = $true
        sound_enabled = $false
        window_style = "hidden"
    }
    $body = $request | ConvertTo-Json -Depth 12
    $prepare = Invoke-RestMethod -Method Post -Uri "$backendUrl/api/v1/training/session/prepare" `
        -ContentType "application/json" -Body $body -TimeoutSec 60
    Write-Json (Join-Path $runDirectory "prepare-result.json") $prepare
    if ($prepare.blocked) {
        throw "Formal prepare blocked: $(@($prepare.block_reasons) -join ',')"
    }

    if (-not $Launch) {
        [ordered]@{
            schema_version = "stardewai.formal_training_start.v1"
            status = "prepared"
            run_directory = $runDirectory
            manifest_path = $prepare.manifest.manifest_path
            run_id = $prepare.manifest.run_id
            backend_process_id = $null
            backend_stopped_after_prepare = $true
            game_started = $false
        } | ConvertTo-Json -Depth 8
        return
    }

    $launchResult = Invoke-RestMethod -Method Post -Uri "$backendUrl/api/v1/training/session/launch" `
        -ContentType "application/json" -Body $body -TimeoutSec 360
    Write-Json (Join-Path $runDirectory "launch-result.json") $launchResult
    if ($launchResult.blocked -or -not $launchResult.started) {
        throw "Formal launch blocked: $(@($launchResult.block_reasons) -join ',')"
    }

    $keepBackend = $true
    $state = [ordered]@{
        schema_version = "stardewai.formal_training_start.v1"
        status = "running"
        run_directory = $runDirectory
        manifest_path = $launchResult.manifest.manifest_path
        run_id = $launchResult.manifest.run_id
        backend_url = $backendUrl
        backend_process_id = $backend.Id
        product_executor_process_id = $launchResult.manifest.product_executor_process_id
        game_process_id = $launchResult.manifest.process_id
        live_training_loop_process_id = $launchResult.manifest.live_training_loop_process_id
    }
    Write-Json (Join-Path $runDirectory "controller-state.json") $state
    $state | ConvertTo-Json -Depth 8
}
finally {
    if (-not $keepBackend -and $null -ne $launchResult -and $launchResult.started) {
        foreach ($processId in @(
            $launchResult.manifest.live_training_loop_process_id,
            $launchResult.manifest.process_id,
            $launchResult.manifest.product_executor_process_id
        )) {
            if ($processId -and (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            }
        }
    }
    if (-not $keepBackend -and $null -ne $backend -and -not $backend.HasExited) {
        Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue
    }
}
