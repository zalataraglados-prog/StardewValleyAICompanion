param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-sale-mainline-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-sale-mainline-smoke",
    [int] $BackendPort = 5132,
    [int] $StartupTimeoutSeconds = 150
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") {
                return $response
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 6
            if (-not [string]::IsNullOrWhiteSpace(
                    [string](Read-FieldValue $snapshot "player" "location_id"))) {
                return $snapshot
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world-ready snapshot."
}

function Wait-SaleWindow {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastTime = -1
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 10
        $lastTime = [int](Read-FieldValue $snapshot "time" "time")
        if ($lastTime -ge 900 -and $lastTime -lt 1700) {
            return $snapshot
        }
        if ($lastTime -ge 1700) {
            throw "Isolated save started too late for SeedShop sale coverage: $lastTime"
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for SeedShop sale hours; last time was $lastTime."
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    return Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json" `
        -Body ($Body | ConvertTo-Json -Depth 64 -Compress) -TimeoutSec $TimeoutSeconds
}

function Read-QueueOptionIds {
    param([string] $SnapshotDirectory)
    return @(Get-ChildItem -LiteralPath $SnapshotDirectory -Filter "*compiled-queue-*.json" |
        Sort-Object Name | ForEach-Object {
            $queue = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
            @($queue.items) | ForEach-Object { [string]$_.option_id }
        } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Read-CompletedSaleExecution {
    param([string] $SnapshotDirectory)
    foreach ($path in @(Get-ChildItem -LiteralPath $SnapshotDirectory `
            -Filter "execution-*.json" | Sort-Object Name -Descending)) {
        $execution = Get-Content -LiteralPath $path.FullName -Raw | ConvertFrom-Json
        $sale = @($execution.step_results | Where-Object {
            [string]$_.option_id -eq "executor.sell_shop_item" -and
            [string]$_.status -eq "applied" -and
            [string]$_.primitive_verification_status -eq "verified"
        } | Select-Object -First 1)[0]
        if ($execution.objective_continuation_completed -eq $true -and $null -ne $sale) {
            return [pscustomobject]@{ Execution = $execution; Sale = $sale; Path = $path.FullName }
        }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
$loopDll = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$targetShopId = "Blacksmith"
$targetLocation = "Blacksmith"
$fixtureQualifiedItemId = "(O)378"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slot exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @($BackendPort, 8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Runtime sale smoke requires unused port $port."
    }
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
$gameStdout = Join-Path $runDirectory "game.stdout.log"
$gameStderr = Join-Path $runDirectory "game.stderr.log"

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --no-restore --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed." }

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_TRAINING_OUTPUT_DIR = $env:STARDEWAI_TRAINING_OUTPUT_DIR
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}
$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile") -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout -RedirectStandardError $backendStderr -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -RedirectStandardOutput $gameStdout `
        -RedirectStandardError $gameStderr -PassThru
    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 45 | Out-Null
    $before = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    if ([int](Read-FieldValue $before "time" "time") -lt 900) {
        $timeRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture"
            queue_item_id = "$RunId.fixture.time"
            before_state_hash = $before.state_hash
            option_id = "debug.advance_time_to"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_time = 900
        }
        $timeResult = Invoke-JsonPost -Url $executeUrl -Body $timeRequest
        if ($timeResult.status -ne "applied" -or
            $timeResult.primitive_verification_status -ne "verified") {
            throw "Sale time fixture failed: $(@($timeResult.block_reasons) -join ',')"
        }
        $before = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    }

    $fixtureRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.fixture"
        queue_item_id = "$RunId.fixture.sale"
        before_state_hash = $before.state_hash
        option_id = "debug.setup_sale_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        qualified_item_id = $fixtureQualifiedItemId
        quantity = 3
    }
    $fixture = Invoke-JsonPost -Url $executeUrl -Body $fixtureRequest
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Sale fixture failed: $(@($fixture.block_reasons) -join ',')"
    }
    $snapshot = Wait-SaleWindow -Url $snapshotUrl -TimeoutSeconds 300
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-fixture.json") $snapshot

    $item = @(Read-FieldValue $snapshot "player" "inventory" | Where-Object {
        [string]$_.qualified_item_id -eq $fixtureQualifiedItemId -and [int]$_.stack -gt 0
    } | Sort-Object slot_index | Select-Object -First 1)[0]
    if ($null -eq $item) { throw "Fixture parsnip is absent from transparent inventory." }
    $shopRows = @((Read-FieldValue $snapshot "locations" "shops").shops)
    $shop = @($shopRows | Where-Object {
        [string]$_.shop_id -eq $targetShopId -and
        $_.sale_preview.executor_sale_preview_enabled -eq $true -and
        @($_.sale_preview.salable_item_tags | Where-Object {
            @($item.context_tags) -contains [string]$_
        }).Count -gt 0
    } | Select-Object -First 1)[0]
    if ($null -eq $shop) {
        throw "Transparent Data/Shops sale preview does not prove $targetShopId accepts $fixtureQualifiedItemId."
    }
    $loopArgs = @(
        $loopDll,
        "--root", $loopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", "http://127.0.0.1:8767",
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $savesPath,
        "--goal", "daily.closed_loop",
        "--max-attempts", "24",
        "--skip-training",
        "--sleep-ms", "100",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "economy.sell_items",
        "--daily-plan-candidate-parameter", "continuation.option_id=economy.sell_items",
        "--daily-plan-candidate-parameter", "continuation.shop_id=$targetShopId",
        "--daily-plan-candidate-parameter", "continuation.target_location=$targetLocation",
        "--daily-plan-candidate-parameter", "continuation.item_id=$($item.item_id)",
        "--daily-plan-candidate-parameter", "continuation.qualified_item_id=$($item.qualified_item_id)",
        "--daily-plan-candidate-parameter", "continuation.slot_index=$($item.slot_index)",
        "--daily-plan-candidate-parameter", "continuation.quantity=$($item.stack)",
        "--daily-plan-candidate-parameter", "continuation.expected_unit_price=$($item.sell_to_store_price)",
        "--continue-after-blocked-queue-items",
        "--max-queue-item-attempts", "16",
        "--after-snapshot-wait-ms", "750",
        "--stop-after-objective-complete"
    )
    & dotnet @loopArgs
    if ($LASTEXITCODE -ne 0) { throw "Sale LiveTrainingLoop failed with exit code $LASTEXITCODE." }

    $loopRunDirectory = Join-Path $loopRoot "runs\$RunId"
    $snapshotDirectory = Join-Path $loopRunDirectory "live-snapshots"
    $reportPath = Join-Path $loopRunDirectory "live-training-loop-report.json"
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $queueOptionIds = Read-QueueOptionIds -SnapshotDirectory $snapshotDirectory
    $completed = Read-CompletedSaleExecution -SnapshotDirectory $snapshotDirectory
    $passed =
        $report.objective_completed -eq $true -and
        $null -eq $report.active_objective_continuation -and
        $queueOptionIds -contains "executor.traverse_connector" -and
        $queueOptionIds -contains "executor.interact" -and
        $queueOptionIds -contains "executor.sell_shop_item" -and
        $null -ne $completed
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-220"
        run_id = $RunId
        save_slot = $SaveSlot
        high_level_option_id = "economy.sell_items"
        objective_completed = [bool]$report.objective_completed
        queue_option_ids = @($queueOptionIds | Select-Object -Unique)
        transparent_shop_id = [string]$shop.shop_id
        transparent_salable_item_tags = @($shop.sale_preview.salable_item_tags)
        fixture_slot_index = [int]$item.slot_index
        fixture_qualified_item_id = [string]$item.qualified_item_id
        fixture_quantity = [int]$item.stack
        fixture_unit_price = [int]$item.sell_to_store_price
        exact_sale_applied_and_verified = $null -ne $completed
        completed_execution_path = if ($null -eq $completed) { "" } else { $completed.Path }
        report_path = $reportPath
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if (-not $passed) { throw "High-level sale mainline smoke failed." }
}
catch {
    Write-JsonFile (Join-Path $runDirectory "failure.json") ([ordered]@{
        status = "failed"
        run_id = $RunId
        error = $_.Exception.Message
        backend_stdout = $backendStdout
        backend_stderr = $backendStderr
        game_stdout = $gameStdout
        game_stderr = $gameStderr
    })
    throw
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else { Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value }
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
        $backendProcess.WaitForExit(10000) | Out-Null
    }
}
