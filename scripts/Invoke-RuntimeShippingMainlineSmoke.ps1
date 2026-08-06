param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-shipping-mainline-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-shipping-mainline-smoke",
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

function Read-CompletedShippingExecution {
    param([string] $SnapshotDirectory)
    foreach ($path in @(Get-ChildItem -LiteralPath $SnapshotDirectory `
            -Filter "execution-*.json" | Sort-Object Name -Descending)) {
        $execution = Get-Content -LiteralPath $path.FullName -Raw | ConvertFrom-Json
        $shipping = @($execution.step_results | Where-Object {
            [string]$_.option_id -eq "executor.ship_inventory_item_to_bin" -and
            [string]$_.status -eq "applied" -and
            [string]$_.primitive_verification_status -eq "verified"
        } | Select-Object -First 1)[0]
        if ($execution.objective_continuation_completed -eq $true -and $null -ne $shipping) {
            return [pscustomobject]@{
                Execution = $execution
                Shipping = $shipping
                Path = $path.FullName
            }
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
$fixtureQualifiedItemId = "(O)388"

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
        throw "Runtime shipping smoke requires unused port $port."
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
    $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    if ([string](Read-FieldValue $snapshot "player" "location_id") -ne "Farm") {
        $connectors = @((Read-FieldValue $snapshot "locations" "route_connectors").connectors)
        $farmConnector = @($connectors | Where-Object {
            [string]$_.target_location -eq "Farm" -and $_.resolved -eq $true
        } | Select-Object -First 1)[0]
        if ($null -eq $farmConnector) { throw "No resolved current connector to Farm for fixture setup." }
        $routeRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture-route"
            queue_item_id = "$RunId.fixture-route.1"
            before_state_hash = $snapshot.state_hash
            option_id = "executor.traverse_connector"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$farmConnector.tile_x
            target_tile_y = [int]$farmConnector.tile_y
            connector_kind = [string]$farmConnector.kind
            expected_target_location = "Farm"
            expected_arrival_tile_x = [int]$farmConnector.target_x
            expected_arrival_tile_y = [int]$farmConnector.target_y
        }
        $route = Invoke-JsonPost -Url $executeUrl -Body $routeRequest -TimeoutSeconds 180
        if ($route.status -ne "applied" -or $route.primitive_verification_status -ne "verified") {
            throw "Fixture route to Farm failed: $(@($route.block_reasons) -join ',')"
        }
        $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    }

    $fixtureAwayTileX = [int](Read-FieldValue $snapshot "player" "tile_x")
    $fixtureAwayTileY = [int](Read-FieldValue $snapshot "player" "tile_y")
    $fixtureRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.fixture"
        queue_item_id = "$RunId.fixture.1"
        before_state_hash = $snapshot.state_hash
        option_id = "debug.setup_shipping_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        qualified_item_id = $fixtureQualifiedItemId
        quantity = 5
    }
    $fixture = Invoke-JsonPost -Url $executeUrl -Body $fixtureRequest
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Shipping fixture failed: $(@($fixture.block_reasons) -join ',')"
    }
    $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $standAfterFixtureX = [int](Read-FieldValue $snapshot "player" "tile_x")
    $standAfterFixtureY = [int](Read-FieldValue $snapshot "player" "tile_y")
    if ($fixtureAwayTileX -ne $standAfterFixtureX -or
        $fixtureAwayTileY -ne $standAfterFixtureY) {
        $awayRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture-away"
            queue_item_id = "$RunId.fixture-away.1"
            before_state_hash = $snapshot.state_hash
            option_id = "executor.move_to_tile"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $fixtureAwayTileX
            target_tile_y = $fixtureAwayTileY
        }
        $away = Invoke-JsonPost -Url $executeUrl -Body $awayRequest -TimeoutSeconds 180
        if ($away.status -ne "applied" -or $away.primitive_verification_status -ne "verified") {
            throw "Could not move away from the shipping stand for rolling approach coverage."
        }
        $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    }
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-fixture.json") $snapshot

    $item = @((Read-FieldValue $snapshot "player" "inventory") | Where-Object {
        [string]$_.qualified_item_id -eq $fixtureQualifiedItemId -and [int]$_.stack -gt 0
    } | Sort-Object slot_index | Select-Object -First 1)[0]
    $bin = @((Read-FieldValue $snapshot "farm" "shipping_bins") | Where-Object {
        [int]$_.days_of_construction_left -le 0 -and
        $null -ne $_.interaction_stand_tile_x -and
        $null -ne $_.interaction_stand_tile_y
    } | Select-Object -First 1)[0]
    if ($null -eq $item -or $null -eq $bin) {
        throw "Transparent shipping item or completed bin binding is missing."
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
        "--max-attempts", "8",
        "--skip-training",
        "--sleep-ms", "100",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "economy.ship_items",
        "--daily-plan-candidate-parameter", "continuation.option_id=economy.ship_items",
        "--daily-plan-candidate-parameter", "continuation.target_location=Farm",
        "--daily-plan-candidate-parameter", "continuation.item_id=$($item.item_id)",
        "--daily-plan-candidate-parameter", "continuation.qualified_item_id=$($item.qualified_item_id)",
        "--daily-plan-candidate-parameter", "continuation.slot_index=$($item.slot_index)",
        "--daily-plan-candidate-parameter", "continuation.quantity=1",
        "--daily-plan-candidate-parameter", "continuation.expected_unit_price=$($item.sell_to_store_price)",
        "--daily-plan-candidate-parameter", "continuation.bin_location=Farm",
        "--daily-plan-candidate-parameter", "continuation.bin_tile_x=$($bin.tile_x)",
        "--daily-plan-candidate-parameter", "continuation.bin_tile_y=$($bin.tile_y)",
        "--daily-plan-candidate-parameter", "continuation.stand_tile_x=$($bin.interaction_stand_tile_x)",
        "--daily-plan-candidate-parameter", "continuation.stand_tile_y=$($bin.interaction_stand_tile_y)",
        "--continue-after-blocked-queue-items",
        "--max-queue-item-attempts", "8",
        "--after-snapshot-wait-ms", "750",
        "--stop-after-objective-complete"
    )
    & dotnet @loopArgs
    if ($LASTEXITCODE -ne 0) { throw "Shipping LiveTrainingLoop failed with exit code $LASTEXITCODE." }

    $loopRunDirectory = Join-Path $loopRoot "runs\$RunId"
    $snapshotDirectory = Join-Path $loopRunDirectory "live-snapshots"
    $report = Get-Content -LiteralPath (Join-Path $loopRunDirectory "live-training-loop-report.json") -Raw | ConvertFrom-Json
    $queueOptionIds = Read-QueueOptionIds -SnapshotDirectory $snapshotDirectory
    $completed = Read-CompletedShippingExecution -SnapshotDirectory $snapshotDirectory
    $shipping = $completed.Shipping
    $passed =
        $report.objective_completed -eq $true -and
        $null -eq $report.active_objective_continuation -and
        $queueOptionIds -contains "executor.move_to_tile" -and
        $queueOptionIds -contains "executor.ship_inventory_item_to_bin" -and
        $null -ne $completed -and
        ($shipping.ship_inventory_count_before - $shipping.ship_inventory_count_after) -eq 1 -and
        ($shipping.ship_bin_count_after - $shipping.ship_bin_count_before) -eq 1
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-221"
        run_id = $RunId
        save_slot = $SaveSlot
        high_level_option_id = "economy.ship_items"
        objective_completed = [bool]$report.objective_completed
        queue_option_ids = @($queueOptionIds | Select-Object -Unique)
        qualified_item_id = [string]$item.qualified_item_id
        slot_index = [int]$item.slot_index
        expected_unit_price = [int]$item.sell_to_store_price
        bin_tile = "$($bin.tile_x),$($bin.tile_y)"
        stand_tile = "$($bin.interaction_stand_tile_x),$($bin.interaction_stand_tile_y)"
        inventory_count_before = $shipping.ship_inventory_count_before
        inventory_count_after = $shipping.ship_inventory_count_after
        bin_count_before = $shipping.ship_bin_count_before
        bin_count_after = $shipping.ship_bin_count_after
        pending_receipt_path = [string]$shipping.ship_pending_receipt_path
        completion_execution_path = $completed.Path
        rows_appended = [int]$report.rows_appended
        terminal_started_at_bin = $false
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Shipping mainline smoke did not close the exact objective." }
}
finally {
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force
    }
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }
}
