param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("product-executor-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96 -Compress) -TimeoutSec 180
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

function Wait-TailoringRow([int] $Seconds = 90) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $snapshotUrl -TimeoutSec 30
            $row = @($snapshot.state.player.tailoring.value.rows | Where-Object {
                $_.source_kind -eq "placed_sewing_machine" -and
                $_.left_source_id -eq "inventory:0" -and
                $_.right_source_id -eq "inventory:1" -and
                $_.recipe_id -eq "BasicPullover_FromWood" -and
                $_.tailoring_candidate_status -eq "ready_for_native_tailoring_menu"
            }) | Select-Object -First 1
            if ($null -ne $row) { return [pscustomobject]@{ snapshot = $snapshot; row = $row } }
        } catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for deterministic tailoring fixture row."
}

function Wait-World([int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $snapshotUrl -TimeoutSec 30
            if ($snapshot.state.player.location_id.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for isolated save world."
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$savesPath = Join-Path $RuntimeRoot "saves"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$backendUrl = "http://127.0.0.1:$BackendPort"
$productUrl = "http://127.0.0.1:$ProductPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$nativeExecuteUrl = "http://127.0.0.1:8767/api/v1/training/execute"
$productExecuteUrl = "$productUrl/api/v1/product/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767, $BackendPort, $ProductPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Product executor smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\product-executor-smoke\" + $RunId)
$journalRoot = Join-Path $artifactDirectory "journal"
$loopRoot = Join-Path $artifactDirectory "loop"
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
dotnet build (Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\StardewAI.ProductExecutor.csproj") `
    -c Release --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ProductExecutor build failed." }
dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --nologo "-p:GamePath=$gameDirectory" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop build failed." }

$names = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_PRODUCT_EXECUTOR_URL",
    "STARDEWAI_NATIVE_EXECUTOR_URL", "STARDEWAI_BRIDGE_SNAPSHOT_URL",
    "STARDEWAI_PRODUCT_JOURNAL_ROOT", "STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT",
    "STARDEWAI_PRODUCT_RUN_ID", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS"
)
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
$backend = $null
$product = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process dotnet -ArgumentList @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile") -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $env:STARDEWAI_PRODUCT_EXECUTOR_URL = $productUrl
    $env:STARDEWAI_NATIVE_EXECUTOR_URL = "http://127.0.0.1:8767"
    $env:STARDEWAI_BRIDGE_SNAPSHOT_URL = $snapshotUrl
    $env:STARDEWAI_PRODUCT_JOURNAL_ROOT = $journalRoot
    $env:STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT = $savesPath
    $env:STARDEWAI_PRODUCT_RUN_ID = $RunId
    $productDll = Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\bin\Release\net8.0\StardewAI.ProductExecutor.dll"
    $product = Start-Process dotnet -ArgumentList @($productDll) -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "product.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "product.stderr.log") -PassThru
    $health = Wait-Json "$productUrl/health" 60
    if ($health.status -ne "ready" -or [int]$health.product_executor_count -le 0) {
        throw "Product executor health contract failed."
    }

    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 60 | Out-Null
    $initial = Wait-World $StartupTimeoutSeconds
    $fixture = Invoke-Post $nativeExecuteUrl ([ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "product-fixture"
        queue_item_id = "$RunId.fixture"
        before_state_hash = [string]$initial.state_hash
        option_id = "debug.setup_tailoring_fixture"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        tailoring_recipe_id = "deterministic_recipe"
    })
    if ($fixture.status -ne "applied") { throw "Native fixture setup failed." }
    $ready = Wait-TailoringRow
    $snapshotPath = Join-Path $artifactDirectory "before-snapshot.json"
    Write-Json $snapshotPath $ready.snapshot

    $loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
    $arguments = @(
        $loopDll, "--root", $loopRoot, "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl, "--executor-url", $productUrl,
        "--use-product-executor", "--snapshot-file", $snapshotPath, "--no-manifest",
        "--run-id", $RunId, "--save-isolation-path", $savesPath,
        "--iterations", "1", "--skip-training", "--sleep-ms", "0",
        "--use-daily-plan", "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "tailoring.sew_item",
        "--daily-plan-candidate-kind", "tailor_item", "--daily-plan-explicit-confirmation",
        "--daily-plan-candidate-parameter", "tailoring_candidate_id=$($ready.row.tailoring_candidate_id)",
        "--daily-plan-candidate-parameter", "tailoring_purpose=$($ready.row.tailoring_purpose)",
        "--after-snapshot-wait-ms", "500"
    )
    $loopOutput = & dotnet $arguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $artifactDirectory "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Product LiveTrainingLoop failed with exit code $LASTEXITCODE." }

    $snapshotDirectory = Join-Path $loopRoot ("runs\" + $RunId + "\live-snapshots")
    $execution = Get-Content -LiteralPath (Join-Path $snapshotDirectory "execution-0001.json") -Raw | ConvertFrom-Json
    $result = @($execution.step_results | Where-Object option_id -eq "executor.tailor_item") | Select-Object -Last 1
    $firstPassed = $null -ne $result -and $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        $result.source -eq "product_executor" -and
        $result.product_authorization_status -eq "authorized" -and
        $result.product_dispatch_guard -eq "native_action_preconditions" -and
        -not [string]::IsNullOrWhiteSpace([string]$result.product_before_state_hash) -and
        -not [string]::IsNullOrWhiteSpace([string]$result.product_after_state_hash) -and
        -not [string]::IsNullOrWhiteSpace([string]$result.product_receipt_id)

    if (-not $firstPassed) {
        $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $artifactDirectory "failed-product-result.json") -Encoding utf8
        throw "Native product execution did not produce a verified receipt."
    }

    $resolved = Get-ChildItem -LiteralPath $journalRoot -Filter "*.pending.json.resolved" | Select-Object -First 1
    if ($null -eq $resolved) { throw "Resolved product pending journal was not found." }
    $pending = Get-Content -LiteralPath $resolved.FullName -Raw | ConvertFrom-Json
    $hashBeforeReplay = [string](Wait-Json $snapshotUrl 30).state_hash
    $replay = Invoke-Post $productExecuteUrl $pending.request
    $hashAfterReplay = [string](Wait-Json $snapshotUrl 30).state_hash
    $replayPassed = $replay.status -eq "applied" -and $replay.product_idempotent_replay -eq $true -and
        $replay.product_receipt_id -eq $result.product_receipt_id -and
        $replay.product_after_state_hash -eq $result.product_after_state_hash

    $unauthorized = Invoke-Post $productExecuteUrl ([ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "product-negative"
        queue_item_id = "$RunId.negative"
        before_state_hash = $hashAfterReplay
        option_id = "debug.setup_tailoring_fixture"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    })
    $negativePassed = $unauthorized.status -eq "blocked" -and
        @($unauthorized.block_reasons) -contains "option_not_product_authorized"

    $summary = [ordered]@{
        schema_version = "stardewai.product_executor_smoke.v1"
        status = if ($firstPassed -and $replayPassed -and $negativePassed) { "passed" } else { "failed" }
        run_id = $RunId
        product_executor_count = [int]$health.product_executor_count
        native_execution = if ($firstPassed) { "passed" } else { "failed" }
        idempotent_replay = if ($replayPassed) { "passed" } else { "failed" }
        unauthorized_debug_rejection = if ($negativePassed) { "passed" } else { "failed" }
        receipt_id = [string]$result.product_receipt_id
        replay_cached_receipt_unchanged = $replay.product_after_state_hash -eq $result.product_after_state_hash
        live_world_changed_while_replaying = $hashBeforeReplay -ne $hashAfterReplay
        source = [string]$result.source
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") { throw "Product executor smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $saved.Keys) {
        [Environment]::SetEnvironmentVariable($name, $saved[$name], "Process")
    }
    foreach ($process in @($game, $product, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
