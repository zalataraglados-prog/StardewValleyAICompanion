[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "自动化_442159967",
    [string]$RunId = ("runtime-movement-timing-calibration-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$StartupTimeoutSeconds = 180,
    [double]$NativeMillisecondsPerGameMinute = 700,
    [double]$ConservativeGameMinutesPerTile = 1.0,
    [int]$ConservativeConnectorGameMinutes = 2
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-SaveFingerprint {
    param([string]$Path)

    $root = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    $lines = Get-ChildItem -LiteralPath $Path -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
            if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Save fingerprint escaped source root: $fullPath"
            }
            $relativePath = $fullPath.Substring($root.Length)
            $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
            $relativePath + "|" + $hash
        }
    $payload = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha256.ComputeHash($payload)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Wait-WorldSnapshot {
    param([string]$Profile, [int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $url = "http://127.0.0.1:8765/api/v1/snapshot?profile=$Profile&fresh=true"
            $response = Invoke-WebRequest -UseBasicParsing -Uri $url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 30
            $snapshot = $response.Content | ConvertFrom-Json
            $lastStatus = "save=$($snapshot.save_id.status);time=$($snapshot.state.time.time.status)"
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.time.time.status -in @("available", "derived")) {
                return [pscustomobject]@{ Raw = $response.Content; Parsed = $snapshot }
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for isolated world snapshot. Last status: $lastStatus"
}

function Wait-Executor {
    param([int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -UseBasicParsing `
                -Uri "http://127.0.0.1:8767/health" -TimeoutSec 10
            if ($health.status -eq "ok") {
                return
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for isolated training executor."
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Get-PlayerState {
    param($Snapshot)

    return [pscustomobject]@{
        StateHash = [string]$Snapshot.state_hash
        TotalDays = [int]$Snapshot.state.time.total_days.value
        Time = [int]$Snapshot.state.time.time.value
        Location = [string]$Snapshot.state.player.location_id.value
        X = [int]$Snapshot.state.player.tile_x.value
        Y = [int]$Snapshot.state.player.tile_y.value
    }
}

if ($NativeMillisecondsPerGameMinute -le 0) {
    throw "NativeMillisecondsPerGameMinute must be positive."
}
if ($ConservativeGameMinutesPerTile -le 0) {
    throw "ConservativeGameMinutesPerTile must be positive."
}
if ($ConservativeConnectorGameMinutes -le 0) {
    throw "ConservativeConnectorGameMinutes must be positive."
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesRoot = Join-Path $RuntimeRoot "saves"
$sourceSavePath = Join-Path $sourceSavesRoot $SaveSlot
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Isolated source save not found: $sourceSavePath"
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach to an existing process."
    }
}
if ($null -ne (Get-Process -Name @("StardewModdingAPI", "Stardew Valley") -ErrorAction SilentlyContinue)) {
    throw "A Stardew process is already running. Refusing to attach or stop it."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-movement-timing-calibration\" + $RunId)
$cloneRoot = Join-Path $runDirectory "isolated-saves"
$clonePath = Join-Path $cloneRoot $SaveSlot
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
$routeSnapshotPath = Join-Path $runDirectory "initial-social-future-snapshot.json"
$resultsPath = Join-Path $runDirectory "movement-results.json"
$partialConnectorResultsPath = Join-Path $runDirectory "connector-results.partial.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$sourceFingerprintBefore = Get-SaveFingerprint -Path $sourceSavePath

New-Item -ItemType Directory -Force -Path $cloneRoot | Out-Null
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
Copy-Item -LiteralPath $sourceSavePath -Destination $clonePath -Recurse

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $smokeModsPath $modName) -Recurse
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $cloneRoot
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $cloneRoot
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS = "false"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    Wait-WorldSnapshot -Profile "light" -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
    Wait-Executor -TimeoutSeconds $StartupTimeoutSeconds

    $routeSnapshot = Wait-WorldSnapshot -Profile "social_future" -TimeoutSeconds 120
    Write-Utf8NoBom -Path $routeSnapshotPath -Content $routeSnapshot.Raw
    $initial = Get-PlayerState -Snapshot $routeSnapshot.Parsed
    if ($initial.Location -ne "Farm" -or $initial.X -ne 66 -or $initial.Y -ne 19) {
        throw "Calibration fixture start mismatch: expected Farm 66,19; actual $($initial.Location) $($initial.X),$($initial.Y)."
    }

    $routeField = $routeSnapshot.Parsed.state.locations.social_route_date_evidence
    if ($routeField.status -notin @("available", "derived")) {
        throw "Current-date route evidence is unavailable."
    }
    $farmMaps = @($routeField.value.locations | Where-Object { $_.location_id -eq "Farm" })
    if ($farmMaps.Count -ne 1) {
        throw "Expected exactly one Farm route-evidence row."
    }
    $fixtureRange = @($farmMaps[0].static_walkable_tile_ranges | Where-Object {
        [int]$_.y -eq 19 -and [int]$_.start_x -le 42 -and [int]$_.end_x -ge 68
    })
    $fixtureActionTiles = @($farmMaps[0].unsupported_route_action_tiles | Where-Object {
        [int]$_.tile_y -eq 19 -and [int]$_.tile_x -ge 42 -and [int]$_.tile_x -le 68
    })
    if ($fixtureRange.Count -ne 1 -or $fixtureActionTiles.Count -ne 0) {
        throw "Calibration fixture row is no longer a continuous native static walkable range."
    }
    $movementContextField = $routeSnapshot.Parsed.state.player.movement_timing_context
    $movementContext = $movementContextField.value
    if ($movementContextField.status -notin @("available", "derived") -or
        $movementContext.projection_status -ne "exact_current_player_native_cardinal_movement_context" -or
        $movementContext.runtime_calibration_compatible -ne $true -or
        $movementContext.route_timing_ready_now -ne $true -or
        [double]$movementContext.theoretical_upper_bound_game_minutes_per_tile -gt $ConservativeGameMinutesPerTile) {
        throw "Current player movement context is outside the calibration scope."
    }

    $targets = @(
        [pscustomobject]@{ X = 43; Y = 19 },
        [pscustomobject]@{ X = 68; Y = 19 },
        [pscustomobject]@{ X = 50; Y = 19 }
    )
    $samples = New-Object System.Collections.Generic.List[object]
    for ($index = 0; $index -lt $targets.Count; $index++) {
        $beforeSnapshot = Wait-WorldSnapshot -Profile "light" -TimeoutSeconds 60
        $before = Get-PlayerState -Snapshot $beforeSnapshot.Parsed
        $target = $targets[$index]
        if ($before.Location -ne "Farm" -or $before.Y -ne 19) {
            throw "Movement sample $index did not start on the verified Farm fixture row."
        }

        $request = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.queue"
            queue_item_id = "$RunId.move.$($index + 1)"
            before_state_hash = $before.StateHash
            option_id = "executor.move_to_tile"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $cloneRoot
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            max_crops = 512
            max_movement_tiles = 512
            target_tile_x = $target.X
            target_tile_y = $target.Y
        }
        $body = $request | ConvertTo-Json -Depth 16
        $requestTimer = [System.Diagnostics.Stopwatch]::StartNew()
        $result = Invoke-RestMethod -UseBasicParsing -Method Post `
            -Uri "http://127.0.0.1:8767/api/v1/training/execute" `
            -ContentType "application/json; charset=utf-8" `
            -Body $body -TimeoutSec 180
        $requestTimer.Stop()
        $afterSnapshot = Wait-WorldSnapshot -Profile "light" -TimeoutSeconds 60
        $after = Get-PlayerState -Snapshot $afterSnapshot.Parsed

        $distance = [Math]::Abs($before.X - $target.X) + [Math]::Abs($before.Y - $target.Y)
        $startedAt = [DateTimeOffset]::Parse([string]$result.started_at)
        $completedAt = [DateTimeOffset]::Parse([string]$result.completed_at)
        $actionMilliseconds = ($completedAt - $startedAt).TotalMilliseconds
        $gameMinutesPerTile = ($actionMilliseconds / $NativeMillisecondsPerGameMinute) / $distance
        $samplePassed =
            $distance -gt 0 -and
            $result.status -eq "applied" -and
            $result.primitive_verification_status -eq "verified" -and
            [string]$result.before_state_hash -eq $before.StateHash -and
            [int]$result.actual_ticks -gt 0 -and
            $after.TotalDays -eq $before.TotalDays -and
            $after.Location -eq "Farm" -and
            $after.X -eq $target.X -and
            $after.Y -eq $target.Y -and
            $gameMinutesPerTile -le $ConservativeGameMinutesPerTile

        $samples.Add([pscustomobject][ordered]@{
            sample_index = $index + 1
            status = if ($samplePassed) { "passed" } else { "failed" }
            before_state_hash = $before.StateHash
            result_before_state_hash = [string]$result.before_state_hash
            after_state_hash = $after.StateHash
            total_days = $before.TotalDays
            time_before = $before.Time
            time_after = $after.Time
            location = $before.Location
            start_tile = [ordered]@{ x = $before.X; y = $before.Y }
            target_tile = [ordered]@{ x = $target.X; y = $target.Y }
            observed_tile = [ordered]@{ x = $after.X; y = $after.Y }
            manhattan_tile_distance = $distance
            actual_ticks = [int]$result.actual_ticks
            action_duration_ms = [Math]::Round($actionMilliseconds, 3)
            request_elapsed_ms = [Math]::Round($requestTimer.Elapsed.TotalMilliseconds, 3)
            derived_game_minutes_per_tile = [Math]::Round($gameMinutesPerTile, 6)
            primitive_verification_reasons = @($result.primitive_verification_reasons)
        })
        if (-not $samplePassed) {
            throw "Runtime movement timing sample $($index + 1) failed."
        }
    }

    # Put the farmer on the exact native stand tile so connector timings don't
    # silently include an approach path.
    $connectorSetupBeforeSnapshot = Wait-WorldSnapshot -Profile "light" -TimeoutSeconds 60
    $connectorSetupBefore = Get-PlayerState -Snapshot $connectorSetupBeforeSnapshot.Parsed
    $connectorSetupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.queue"
        queue_item_id = "$RunId.connector.setup"
        before_state_hash = $connectorSetupBefore.StateHash
        option_id = "executor.move_to_tile"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $cloneRoot
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        max_crops = 512
        max_movement_tiles = 512
        target_tile_x = 64
        target_tile_y = 15
    }
    $connectorSetupResult = Invoke-RestMethod -UseBasicParsing -Method Post `
        -Uri "http://127.0.0.1:8767/api/v1/training/execute" `
        -ContentType "application/json; charset=utf-8" `
        -Body ($connectorSetupRequest | ConvertTo-Json -Depth 16) -TimeoutSec 180
    $connectorSetupAfterSnapshot = Wait-WorldSnapshot -Profile "light" -TimeoutSeconds 60
    $connectorSetupAfter = Get-PlayerState -Snapshot $connectorSetupAfterSnapshot.Parsed
    if ($connectorSetupResult.status -ne "applied" -or
        $connectorSetupResult.primitive_verification_status -ne "verified" -or
        $connectorSetupAfter.Location -ne "Farm" -or
        $connectorSetupAfter.X -ne 64 -or
        $connectorSetupAfter.Y -ne 15) {
        throw "Failed to reach the exact FarmHouse connector stand tile."
    }

    $connectorCases = @(
        [pscustomobject]@{
            Kind = "building_door"
            SourceLocation = "Farm"
            SourceX = 64
            SourceY = 14
            ExpectedLocation = "FarmHouse"
            ExpectedX = 27
            ExpectedY = 30
        },
        [pscustomobject]@{
            Kind = "warp"
            SourceLocation = "FarmHouse"
            SourceX = 27
            SourceY = 31
            ExpectedLocation = "Farm"
            ExpectedX = 64
            ExpectedY = 15
        }
    )
    $connectorSamples = New-Object System.Collections.Generic.List[object]
    for ($index = 0; $index -lt $connectorCases.Count; $index++) {
        $case = $connectorCases[$index]
        $beforeSnapshot = Wait-WorldSnapshot -Profile "light" -TimeoutSeconds 60
        $before = Get-PlayerState -Snapshot $beforeSnapshot.Parsed
        if ($before.Location -ne $case.SourceLocation) {
            throw "Connector sample $($index + 1) source mismatch: expected $($case.SourceLocation); actual $($before.Location)."
        }

        $request = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.queue"
            queue_item_id = "$RunId.connector.$($index + 1)"
            before_state_hash = $before.StateHash
            option_id = "executor.traverse_connector"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $cloneRoot
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            max_crops = 512
            max_movement_tiles = 512
            target_tile_x = $case.SourceX
            target_tile_y = $case.SourceY
            connector_kind = $case.Kind
            expected_target_location = $case.ExpectedLocation
            expected_arrival_tile_x = $case.ExpectedX
            expected_arrival_tile_y = $case.ExpectedY
        }
        $requestTimer = [System.Diagnostics.Stopwatch]::StartNew()
        $result = Invoke-RestMethod -UseBasicParsing -Method Post `
            -Uri "http://127.0.0.1:8767/api/v1/training/execute" `
            -ContentType "application/json; charset=utf-8" `
            -Body ($request | ConvertTo-Json -Depth 16) -TimeoutSec 180
        $requestTimer.Stop()
        $afterSnapshot = Wait-WorldSnapshot -Profile "light" -TimeoutSeconds 60
        $after = Get-PlayerState -Snapshot $afterSnapshot.Parsed

        $startedAt = [DateTimeOffset]::Parse([string]$result.started_at)
        $completedAt = [DateTimeOffset]::Parse([string]$result.completed_at)
        $actionMilliseconds = ($completedAt - $startedAt).TotalMilliseconds
        $connectorGameMinutes = $actionMilliseconds / $NativeMillisecondsPerGameMinute
        $samplePassed =
            $result.status -eq "applied" -and
            $result.primitive_verification_status -eq "verified" -and
            [string]$result.before_state_hash -eq $before.StateHash -and
            $null -ne $result.actual_ticks -and
            [int]$result.actual_ticks -ge 0 -and
            $after.TotalDays -eq $before.TotalDays -and
            $after.Location -eq $case.ExpectedLocation -and
            $after.X -eq $case.ExpectedX -and
            $after.Y -eq $case.ExpectedY -and
            $connectorGameMinutes -le $ConservativeConnectorGameMinutes

        $connectorSamples.Add([pscustomobject][ordered]@{
            sample_index = $index + 1
            status = if ($samplePassed) { "passed" } else { "failed" }
            connector_kind = $case.Kind
            before_state_hash = $before.StateHash
            result_before_state_hash = [string]$result.before_state_hash
            after_state_hash = $after.StateHash
            total_days = $before.TotalDays
            time_before = $before.Time
            time_after = $after.Time
            source_location = $case.SourceLocation
            source_tile = [ordered]@{ x = $case.SourceX; y = $case.SourceY }
            expected_location = $case.ExpectedLocation
            expected_tile = [ordered]@{ x = $case.ExpectedX; y = $case.ExpectedY }
            observed_location = $after.Location
            observed_tile = [ordered]@{ x = $after.X; y = $after.Y }
            actual_ticks = [int]$result.actual_ticks
            action_duration_ms = [Math]::Round($actionMilliseconds, 3)
            request_elapsed_ms = [Math]::Round($requestTimer.Elapsed.TotalMilliseconds, 3)
            derived_connector_game_minutes = [Math]::Round($connectorGameMinutes, 6)
            primitive_verification_reasons = @($result.primitive_verification_reasons)
        })
        Write-Utf8NoBom -Path $partialConnectorResultsPath `
            -Content ($connectorSamples | ConvertTo-Json -Depth 32)
        if (-not $samplePassed) {
            $failure = ("Runtime connector timing sample {0} failed: status={1}; verification={2}; " +
                "actual_ticks={3}; observed={4}:{5},{6}; connector_game_minutes={7}.") -f `
                ($index + 1), $result.status, $result.primitive_verification_status,
                $result.actual_ticks, $after.Location, $after.X, $after.Y,
                ([Math]::Round($connectorGameMinutes, 6))
            throw $failure
        }
    }

    $sourceFingerprintAfter = Get-SaveFingerprint -Path $sourceSavePath
    $maximumObserved = [double](($samples | Measure-Object -Property derived_game_minutes_per_tile -Maximum).Maximum)
    $maximumObservedConnector = [double](($connectorSamples | Measure-Object -Property derived_connector_game_minutes -Maximum).Maximum)
    $passed =
        $samples.Count -eq $targets.Count -and
        @($samples | Where-Object { $_.status -ne "passed" }).Count -eq 0 -and
        $maximumObserved -le $ConservativeGameMinutesPerTile -and
        $connectorSamples.Count -eq $connectorCases.Count -and
        @($connectorSamples | Where-Object { $_.status -ne "passed" }).Count -eq 0 -and
        $maximumObservedConnector -le $ConservativeConnectorGameMinutes -and
        $sourceFingerprintBefore -eq $sourceFingerprintAfter

    Write-Utf8NoBom -Path $resultsPath `
        -Content ([ordered]@{
            movement_samples = $samples
            connector_setup_result = $connectorSetupResult
            connector_samples = $connectorSamples
        } | ConvertTo-Json -Depth 32)
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_movement_timing_calibration.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        game_version = [string]$routeSnapshot.Parsed.game_version
        smapi_version = [string]$routeSnapshot.Parsed.smapi_version
        save_slot = $SaveSlot
        fixture = "Farm:y=19:x=42..68:exact_current_date_static_native_walkability"
        capture_total_days = $initial.TotalDays
        sample_count = $samples.Count
        total_manhattan_tiles = [int](($samples | Measure-Object -Property manhattan_tile_distance -Sum).Sum)
        maximum_observed_actual_ticks_per_tile = [Math]::Round([double](($samples | ForEach-Object {
            $_.actual_ticks / [double]$_.manhattan_tile_distance
        } | Measure-Object -Maximum).Maximum), 6)
        maximum_observed_game_minutes_per_tile = [Math]::Round($maximumObserved, 6)
        native_milliseconds_per_game_minute = $NativeMillisecondsPerGameMinute
        captured_base_speed = [double]$movementContext.base_speed_now
        captured_added_speed = [double]$movementContext.added_speed_now
        captured_temporary_speed_buff = [double]$movementContext.temporary_speed_buff_now
        conservative_on_foot_speed_scalar = [double]$movementContext.conservative_on_foot_speed_scalar
        theoretical_upper_bound_game_minutes_per_tile = [double]$movementContext.theoretical_upper_bound_game_minutes_per_tile
        movement_context_projection_status = [string]$movementContext.projection_status
        movement_context_scope = [string]$movementContext.scope
        conservative_game_minute_numerator_per_tile = [int][Math]::Round($ConservativeGameMinutesPerTile * 1000)
        conservative_game_minute_denominator_per_tile = 1000
        connector_sample_count = $connectorSamples.Count
        connector_kinds = @($connectorSamples | ForEach-Object { $_.connector_kind })
        maximum_observed_connector_game_minutes = [Math]::Round($maximumObservedConnector, 6)
        conservative_connector_transition_game_minutes = $ConservativeConnectorGameMinutes
        calibration_scope = "ordinary_native_player_movement_on_verified_static_walkable_tiles"
        calibration_evidence_kind = "conservative_upper_bound"
        connector_transition_game_minutes_status = "runtime_proven_building_door_and_step_warp"
        source_save_fingerprint_before = $sourceFingerprintBefore
        source_save_fingerprint_after = $sourceFingerprintAfter
        source_save_untouched = $sourceFingerprintBefore -eq $sourceFingerprintAfter
        route_snapshot_path = $routeSnapshotPath
        movement_results_path = $resultsPath
        isolated_clone_path = $clonePath
    }
    Write-Utf8NoBom -Path $summaryPath `
        -Content ($summary | ConvertTo-Json -Depth 32)
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) {
        throw "Runtime movement timing calibration failed: $runDirectory"
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
