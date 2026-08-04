param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-route-connector-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-route-connector-smoke",
    [int] $BackendPort = 5129,
    [int] $StartupTimeoutSeconds = 120,
    [switch] $HighLevelVisit,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $Value | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body,
        [int] $TimeoutSeconds = 120
    )

    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") {
                return $response
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 5
            $locationReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "player" -and
                $snapshot.state.player.PSObject.Properties.Name -contains "location_id") {
                $locationReadable = $snapshot.state.player.location_id.status -in @("available", "derived")
            }

            $lastStatus = "location_id_readable=$locationReadable;completeness=$($snapshot.completeness)"
            if ($locationReadable) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Read-FieldValue {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field
    )

    if ($null -eq $Snapshot.state) {
        return $null
    }

    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) {
        return $null
    }

    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) {
        return $null
    }

    return $fieldNode.value
}

function Find-FarmHouseExitConnector {
    param([Parameter(Mandatory = $true)] $Snapshot)

    $routeIndex = Read-FieldValue $Snapshot "locations" "route_connectors"
    if ($null -eq $routeIndex -or $null -eq $routeIndex.connectors) {
        return $null
    }

    return @($routeIndex.connectors | Where-Object {
        (([string]$_.target_location) -eq "Farm") -or (([string]$_.target_name) -eq "Farm")
    } | Sort-Object tile_y, tile_x | Select-Object -First 1)[0]
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"
$loopDll = Join-Path $ProjectRoot (
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\" +
    "StardewAI.LiveTrainingLoop.dll")

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}

if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }

    $SaveSlot = $slot.Name
}

$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) {
    throw "Isolated save slot not found: $slotPath"
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$loopRoot = Join-Path $runDirectory "loop"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

if ($HighLevelVisit) {
    foreach ($port in @($BackendPort, 8765, 8767)) {
        $listener = Get-NetTCPConnection -State Listen -LocalPort $port `
            -ErrorAction SilentlyContinue
        if ($null -ne $listener) {
            throw "High-level route smoke requires unused port $port."
        }
    }

    & dotnet build (Join-Path $ProjectRoot (
        "tools\StardewAI.LiveTrainingLoop\" +
        "StardewAI.LiveTrainingLoop.csproj")) `
        -c Release --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $loopDll -PathType Leaf)) {
        throw "LiveTrainingLoop Release build failed."
    }
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}

$process = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    if ($HighLevelVisit) {
        $env:ASPNETCORE_URLS = $backendUrl
        $backendProcess = Start-Process -FilePath "dotnet" `
            -ArgumentList @(
                "run", "--no-restore", "--project",
                (Join-Path $ProjectRoot (
                    "src\StardewAI.Backend\StardewAI.Backend.csproj")),
                "--no-launch-profile") `
            -WorkingDirectory $ProjectRoot `
            -WindowStyle Hidden `
            -RedirectStandardOutput $backendStdout `
            -RedirectStandardError $backendStderr `
            -PassThru
        Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 |
            Out-Null
    }

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru

    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $before = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $location = Read-FieldValue $before "player" "location_id"
    $connector = Find-FarmHouseExitConnector $before

    if ($location -ne "FarmHouse" -or $null -eq $connector) {
        $summary = [ordered]@{
            status = "skipped_no_farmhouse_farm_connector"
            run_id = $RunId
            save_slot = $SaveSlot
            location = $location
            reason = "expected current FarmHouse snapshot with a Farm warp connector"
            executor_health = $executorHealth
            kept_game_running = [bool]$KeepGameRunning
        }
        Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $before
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        $summary | ConvertTo-Json -Depth 32
        return
    }

    if ($HighLevelVisit) {
        & dotnet $loopDll `
            --root $loopRoot `
            --backend-url $backendUrl `
            --bridge-snapshot-url $snapshotUrl `
            --executor-url $executorUrl `
            --no-manifest `
            --run-id $RunId `
            --save-isolation-path $savesPath `
            --goal daily.closed_loop `
            --iterations 1 `
            --train-every 1 `
            --skip-training `
            --sleep-ms 0 `
            --use-daily-plan `
            --daily-plan-max-candidates 1 `
            --daily-plan-candidate-options exploration.visit_location `
            --daily-plan-candidate-kind route_connector_tile `
            --after-snapshot-wait-ms 1000
        if ($LASTEXITCODE -ne 0) {
            throw "High-level LiveTrainingLoop failed with exit code $LASTEXITCODE."
        }

        $snapshotDirectory = Join-Path $loopRoot (
            "runs\$RunId\live-snapshots")
        $rankingPath = Join-Path $snapshotDirectory "ranking-response-0001.json"
        $planPath = Join-Path $snapshotDirectory "daily-plan-response-0001.json"
        $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
        $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
        foreach ($path in @($rankingPath, $planPath, $queuePath, $executionPath)) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Required high-level route artifact missing: $path"
            }
        }

        $ranking = Get-Content -LiteralPath $rankingPath -Raw | ConvertFrom-Json
        $dailyPlan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
        $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
        $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
        $datasetPath = Join-Path $loopRoot (
            "datasets\live-training-feature-rows.jsonl")
        $datasetRows = if (Test-Path -LiteralPath $datasetPath -PathType Leaf) {
            @(Get-Content -LiteralPath $datasetPath)
        } else {
            @()
        }
        $ranked = @($ranking.ranked_event_candidates)
        $steps = @($dailyPlan.plan.steps)
        $queueItems = @($queue.items)
        $result = @($execution.step_results) |
            Where-Object { [string]$_.option_id -eq "executor.traverse_connector" } |
            Select-Object -First 1
        if ($null -eq $result -and
            [string]$execution.option_id -eq "executor.traverse_connector") {
            $result = $execution
        }
        $afterPath = [string]$result.after_snapshot_path
        if ([string]::IsNullOrWhiteSpace($afterPath) -or
            -not (Test-Path -LiteralPath $afterPath -PathType Leaf)) {
            throw "High-level route result did not preserve a fresh after snapshot."
        }
        $after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json

        $passed =
            $ranked.Count -ge 1 -and
            [string]$ranked[0].option_id -eq "exploration.visit_location" -and
            [string]$ranked[0].kind -eq "route_connector_tile" -and
            $steps.Count -eq 1 -and
            [string]$steps[0].kind -eq "traverse_connector" -and
            $queueItems.Count -eq 1 -and
            [string]$queueItems[0].option_id -eq "executor.traverse_connector" -and
            $null -ne $result -and
            [string]$result.status -eq "applied" -and
            [string]$result.primitive_verification_status -eq "verified" -and
            [bool]$result.after_snapshot_fresh -and
            [string]$result.after_state_hash -ne [string]$before.state_hash -and
            [string](Read-FieldValue $after "player" "location_id") -eq "Farm" -and
            $datasetRows.Count -eq 1

        $summary = [ordered]@{
            status = if ($passed) { "passed" } else { "failed" }
            evidence_id = "EVD-218"
            run_id = $RunId
            save_slot = $SaveSlot
            high_level_option_id = "exploration.visit_location"
            candidate_id = [string]$ranked[0].candidate_id
            candidate_kind = [string]$ranked[0].kind
            plan_step_kind = [string]$steps[0].kind
            queue_option_id = [string]$queueItems[0].option_id
            result_status = [string]$result.status
            primitive_verification_status = [string]$result.primitive_verification_status
            before_location = $location
            after_location = Read-FieldValue $after "player" "location_id"
            before_state_hash = [string]$before.state_hash
            after_state_hash = [string]$result.after_state_hash
            after_snapshot_fresh = [bool]$result.after_snapshot_fresh
            ranked_candidate_count = $ranked.Count
            training_feature_row_count = $datasetRows.Count
            dataset_path = $datasetPath
            ranking_path = $rankingPath
            daily_plan_path = $planPath
            compiled_queue_path = $queuePath
            execution_path = $executionPath
            executor_health = $executorHealth
            kept_game_running = [bool]$KeepGameRunning
        }
        Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $before
        Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after.json") $after
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        $summary | ConvertTo-Json -Depth 32
        if (-not $passed) {
            throw "High-level route connector smoke failed."
        }
        return
    }

    $request = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-route-connector-smoke"
        queue_item_id = "runtime-route-connector-smoke.farmhouse-to-farm"
        before_state_hash = $before.state_hash
        option_id = "executor.traverse_connector"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        target_tile_x = [int]$connector.tile_x
        target_tile_y = [int]$connector.tile_y
        connector_kind = [string]$connector.kind
        expected_target_location = if ([string]::IsNullOrWhiteSpace([string]$connector.target_location)) { [string]$connector.target_name } else { [string]$connector.target_location }
        expected_arrival_tile_x = [int]$connector.target_x
        expected_arrival_tile_y = [int]$connector.target_y
    }

    $result = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body $request -TimeoutSeconds 180
    Start-Sleep -Milliseconds 500
    $after = Invoke-RestMethod -Method Get -Uri $snapshotUrl -Headers @{ "Accept" = "application/json" } -TimeoutSec 10

    $summary = [ordered]@{
        status = if ($result.status -eq "applied" -and $result.primitive_verification_status -eq "verified") { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        connector = $connector
        result_status = $result.status
        primitive_verification_status = $result.primitive_verification_status
        primitive_verification_reasons = @($result.primitive_verification_reasons)
        block_reasons = @($result.block_reasons)
        before_location = $location
        after_location = (Read-FieldValue $after "player" "location_id")
        before_state_hash = $before.state_hash
        after_state_hash = $after.state_hash
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $before
    Write-JsonFile (Join-Path $runDirectory "traverse-connector-request.json") $request
    Write-JsonFile (Join-Path $runDirectory "traverse-connector-result.json") $result
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after.json") $after
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32

    if ($summary.status -ne "passed") {
        throw "Route connector smoke failed."
    }
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }

    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force
    }
}
