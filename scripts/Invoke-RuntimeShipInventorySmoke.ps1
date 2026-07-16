param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $TrainingOutputDir = "E:\StardewAITraining",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-ship-inventory-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-ship-inventory-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [string] $FixtureQualifiedItemId = "(O)388",
    [int] $FixtureStackQuantity = 5,
    [int] $OvernightPollTimeoutSeconds = 600,
    [switch] $SkipOvernight
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Value)
    $json = $Value | ConvertTo-Json -Depth 64
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Invoke-JsonPost {
    param([Parameter(Mandatory = $true)][string]$Url, [Parameter(Mandatory = $true)]$Body, [int]$TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([Parameter(Mandatory = $true)][string]$Url, [Parameter(Mandatory = $true)][int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{"Accept" = "application/json" } -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") { return $response }
        } catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([Parameter(Mandatory = $true)][string]$Url, [Parameter(Mandatory = $true)][int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{"Accept" = "application/json" } -TimeoutSec 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $timeReadable = $snapshot.in_game_time.status -in @("available", "derived")
            $farmReadable = $false
            if ($null -ne $snapshot.state -and $null -ne $snapshot.state.farm) { $farmReadable = $true }
            $lastStatus = "save_id=$($snapshot.save_id.status);in_game_time=$($snapshot.in_game_time.status);farm_readable=$farmReadable"
            if ($saveReadable -and $timeReadable -and $farmReadable) { return $snapshot }
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Find-ChangedFact {
    param([Parameter(Mandatory = $true)]$Result, [Parameter(Mandatory = $true)][string]$Path)
    foreach ($fact in @($Result.changed_facts)) {
        if ($fact.path -eq $Path) { return $fact }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }

# Pre-launch collision guard
$testPorts = @(8765, 8767)
foreach ($port in $testPorts) {
    $listener = (Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue) | Where-Object { $_.State -eq "Listen" }
    if ($null -ne $listener) {
        throw "Port $port is already listening. An existing SMAPI process may be running. Refusing to start."
    }
}
$existingSmapi = Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue
if ($null -ne $existingSmapi) {
    throw "StardewModdingAPI process already running (PID $($existingSmapi.Id)). Refusing to attach or start."
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) { throw "Isolated save slot not found: $slotPath" }

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_TRAINING_OUTPUT_DIR = $env:STARDEWAI_TRAINING_OUTPUT_DIR
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $TrainingOutputDir
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru

    Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30 | Out-Null
    $initialSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds $StartupTimeoutSeconds

    # Preflight: ensure player is on Farm before running shipping fixture
    $currentLocationIdentity = if ($null -ne $initialSnapshot.state -and
        $null -ne $initialSnapshot.state.current_location -and
        $null -ne $initialSnapshot.state.current_location.identity) {
        $initialSnapshot.state.current_location.identity.value
    } else { $null }

    $currentLocationName = if ($null -ne $currentLocationIdentity) { [string]$currentLocationIdentity.name_or_unique_name } else { "" }

    $connectorSnapshot = $initialSnapshot

    if ($currentLocationName -ne "Farm") {
        $warps = if ($null -ne $initialSnapshot.state -and
            $null -ne $initialSnapshot.state.current_location -and
            $null -ne $initialSnapshot.state.current_location.warps) {
            $initialSnapshot.state.current_location.warps.value
        } else { $null }

        if ($null -eq $warps) {
            $summary = [ordered]@{
                status = "preflight_failed"
                run_id = $RunId
                reason = "current_location.warps data unavailable; cannot route to Farm"
                current_location = $currentLocationName
            }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Preflight failed: no warp data available. Current location: $currentLocationName"
        }

        $farmWarps = @($warps | Where-Object { [string]$_.target_name -eq "Farm" })
        if ($farmWarps.Count -eq 0) {
            $summary = [ordered]@{
                status = "preflight_failed"
                run_id = $RunId
                reason = "no warp to Farm found in current_location.warps"
                current_location = $currentLocationName
                available_targets = @($warps | ForEach-Object { [string]$_.target_name } | Select-Object -Unique)
            }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Preflight failed: no warp to Farm found. Current location: $currentLocationName"
        }

        if ($farmWarps.Count -gt 1) {
            $summary = [ordered]@{
                status = "preflight_failed"
                run_id = $RunId
                reason = "ambiguous: multiple warps to Farm; cannot select automatically"
                current_location = $currentLocationName
                farm_warp_count = $farmWarps.Count
                warps = @($farmWarps | ForEach-Object { [ordered]@{ x = $_.x; y = $_.y; target_x = $_.target_x; target_y = $_.target_y } })
            }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Preflight failed: ambiguous Farm warps ($($farmWarps.Count)). Current location: $currentLocationName"
        }

        $farmWarp = $farmWarps[0]

        Write-Host "Preflight: routing from $currentLocationName to Farm via warp at ($([int]$farmWarp.x),$([int]$farmWarp.y)) -> ($([int]$farmWarp.target_x),$([int]$farmWarp.target_y))"

        $warpRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-ship-inventory-smoke"
            queue_item_id = "runtime-ship-inventory-smoke.preflight-warp"
            before_state_hash = $initialSnapshot.state_hash
            option_id = "executor.traverse_connector"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$farmWarp.x
            target_tile_y = [int]$farmWarp.y
            connector_kind = "warp"
            expected_target_location = "Farm"
            expected_arrival_tile_x = [int]$farmWarp.target_x
            expected_arrival_tile_y = [int]$farmWarp.target_y
        }

        $warpResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $warpRequest -TimeoutSeconds 180
        $connectorSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30

        Write-JsonFile (Join-Path $runDirectory "preflight-warp-request.json") $warpRequest
        Write-JsonFile (Join-Path $runDirectory "preflight-warp-result.json") $warpResult
        Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-preflight-warp.json") $connectorSnapshot

        if ($warpResult.status -ne "applied" -or $warpResult.primitive_verification_status -ne "verified") {
            $summary = [ordered]@{
                status = "preflight_failed"
                run_id = $RunId
                reason = "warp to Farm did not apply/verify"
                warp_status = $warpResult.status
                warp_verification = $warpResult.primitive_verification_status
                warp_block_reasons = @($warpResult.block_reasons)
            }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Preflight warp to Farm failed. Status: $($warpResult.status), Verification: $($warpResult.primitive_verification_status)"
        }

        $afterLocationIdentity = if ($null -ne $connectorSnapshot.state -and
            $null -ne $connectorSnapshot.state.current_location -and
            $null -ne $connectorSnapshot.state.current_location.identity) {
            $connectorSnapshot.state.current_location.identity.value
        } else { $null }

        $afterLocationName = if ($null -ne $afterLocationIdentity) { [string]$afterLocationIdentity.name_or_unique_name } else { "" }

        if ($afterLocationName -ne "Farm") {
            $summary = [ordered]@{
                status = "preflight_failed"
                run_id = $RunId
                reason = "post-warp location is not Farm"
                expected = "Farm"
                actual = $afterLocationName
            }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Preflight failed: expected Farm after warp, got '$afterLocationName'"
        }
    }

    # Stage 1: Fixture setup - ensure target item and resolve bin/stand tiles
    $fixtureRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-ship-inventory-smoke"
        queue_item_id = "runtime-ship-inventory-smoke.fixture"
        before_state_hash = $connectorSnapshot.state_hash
        option_id = "debug.setup_shipping_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        qualified_item_id = $FixtureQualifiedItemId
        quantity = $FixtureStackQuantity
    }

    $fixtureResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $fixtureRequest -TimeoutSeconds 120
    $fixtureSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-initial.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "fixture-request.json") $fixtureRequest
    Write-JsonFile (Join-Path $runDirectory "fixture-result.json") $fixtureResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-fixture.json") $fixtureSnapshot

    if ($fixtureResult.status -ne "applied" -or $fixtureResult.primitive_verification_status -ne "verified") {
        $summary = [ordered]@{
            status = "fixture_failed"
            run_id = $RunId
            fixture_status = $fixtureResult.status
            fixture_verification = $fixtureResult.primitive_verification_status
            fixture_reasons = @($fixtureResult.primitive_verification_reasons)
            fixture_block_reasons = @($fixtureResult.block_reasons)
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Fixture setup failed. See $runDirectory"
    }

    # Extract fixture facts
    $fixtureReasons = @($fixtureResult.primitive_verification_reasons)
    $slotIndexFact = $fixtureReasons | Where-Object { $_ -match "^slot_index=" } | Select-Object -First 1
    $slotIndex = if ($slotIndexFact -match "slot_index=(\d+)") { [int]$Matches[1] } else { throw "Fixture did not return slot_index" }
    $binTileFact = $fixtureReasons | Where-Object { $_ -match "^bin_tile=" } | Select-Object -First 1
    $standTileFact = $fixtureReasons | Where-Object { $_ -match "^stand_tile=" } | Select-Object -First 1

    $standTileFact -match "stand_tile=(\d+),(\d+)" | Out-Null
    $standTileX = [int]$Matches[1]; $standTileY = [int]$Matches[2]
    $binTileFact -match "bin_tile=(\d+),(\d+)" | Out-Null
    $binTileX = [int]$Matches[1]; $binTileY = [int]$Matches[2]

    # Stage 2: Move to stand tile (if not already there)
    $playerX = if ($null -ne $fixtureSnapshot.state.player.tile_x.value) { [int]$fixtureSnapshot.state.player.tile_x.value } else { -1 }
    $playerY = if ($null -ne $fixtureSnapshot.state.player.tile_y.value) { [int]$fixtureSnapshot.state.player.tile_y.value } else { -1 }

    if ($playerX -ne $standTileX -or $playerY -ne $standTileY) {
        $moveRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-ship-inventory-smoke"
            queue_item_id = "runtime-ship-inventory-smoke.move"
            before_state_hash = $fixtureSnapshot.state_hash
            option_id = "executor.move_to_tile"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $standTileX
            target_tile_y = $standTileY
        }
        $moveResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $moveRequest -TimeoutSeconds 120
        $moveSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30
        Write-JsonFile (Join-Path $runDirectory "move-request.json") $moveRequest
        Write-JsonFile (Join-Path $runDirectory "move-result.json") $moveResult
        if ($moveResult.status -ne "applied") {
            throw "Move to stand tile failed: status=$($moveResult.status) reasons=$($moveResult.block_reasons)"
        }
    } else {
        $moveSnapshot = $fixtureSnapshot
    }

    # Stage 3: Execute ship inventory item to bin
    $shipRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-ship-inventory-smoke"
        queue_item_id = "runtime-ship-inventory-smoke.ship"
        before_state_hash = $moveSnapshot.state_hash
        option_id = "executor.ship_inventory_item_to_bin"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        slot_index = $slotIndex
        qualified_item_id = $FixtureQualifiedItemId
        quantity = 1
        target_tile_x = $binTileX
        target_tile_y = $binTileY
        stand_tile_x = $standTileX
        stand_tile_y = $standTileY
    }

    $shipResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $shipRequest -TimeoutSeconds 120
    $shipSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30

    Write-JsonFile (Join-Path $runDirectory "ship-request.json") $shipRequest
    Write-JsonFile (Join-Path $runDirectory "ship-result.json") $shipResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-ship.json") $shipSnapshot

    $immediatePassed = $shipResult.status -eq "applied" -and $shipResult.primitive_verification_status -eq "verified"
    $inventoryDeltaOk = ($shipResult.ship_inventory_count_before - $shipResult.ship_inventory_count_after) -eq 1
    $binDeltaOk = ($shipResult.ship_bin_count_after - $shipResult.ship_bin_count_before) -eq 1

    if (-not $immediatePassed -or -not $inventoryDeltaOk -or -not $binDeltaOk) {
        $summary = [ordered]@{
            status = "immediate_postcondition_failed"
            run_id = $RunId
            ship_status = $shipResult.status
            ship_verification = $shipResult.primitive_verification_status
            ship_block_reasons = @($shipResult.block_reasons)
            inventory_count_before = $shipResult.ship_inventory_count_before
            inventory_count_after = $shipResult.ship_inventory_count_after
            bin_count_before = $shipResult.ship_bin_count_before
            bin_count_after = $shipResult.ship_bin_count_after
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Immediate postcondition failed. See $runDirectory"
    }

    # Stage 4: Route home through Farmhouse building-door connector
    Write-Host "Looking up Farmhouse building-door edge from locations.route_graph..."

    if ($null -eq $shipSnapshot.state.locations -or $null -eq $shipSnapshot.state.locations.route_graph) {
        $summary = [ordered]@{ status = "home_route_failed"; run_id = $RunId; reason = "locations.route_graph unavailable in post-ship snapshot" }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route preflight failed: locations.route_graph data unavailable."
    }

    $routeGraph = $shipSnapshot.state.locations.route_graph.value
    if ($null -eq $routeGraph.edges) {
        $summary = [ordered]@{ status = "home_route_failed"; run_id = $RunId; reason = "route_graph.edges unavailable" }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route preflight failed: route_graph.edges data unavailable."
    }

    $farmhouseEdges = @($routeGraph.edges | Where-Object {
        [string]$_.kind -eq "building_door" -and
        [string]$_.from_location -eq "Farm" -and
        [string]$_.building_type -eq "Farmhouse" -and
        $_.resolved -eq $true
    })
    if ($farmhouseEdges.Count -ne 1) {
        if ($farmhouseEdges.Count -eq 0) {
            $allDoorEdges = @($routeGraph.edges | Where-Object { [string]$_.kind -eq "building_door" -and [string]$_.from_location -eq "Farm" })
            $availableTypes = @($allDoorEdges | ForEach-Object { [string]$_.building_type } | Select-Object -Unique)
            $summary = [ordered]@{ status = "home_route_failed"; run_id = $RunId; reason = "no resolved Farmhouse building_door edge in route_graph"; available_door_building_types = $availableTypes }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Home route failed: no resolved Farmhouse building_door edge in locations.route_graph."
        }
        $summary = [ordered]@{ status = "home_route_failed"; run_id = $RunId; reason = "ambiguous: multiple resolved Farmhouse building_door edges in route_graph"; count = $farmhouseEdges.Count }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: multiple resolved Farmhouse building_door edges found in route_graph."
    }

    $fhEdge = $farmhouseEdges[0]
    $homeDoorTileX = $fhEdge.from_x
    $homeDoorTileY = $fhEdge.from_y
    $homeIndoorId = $fhEdge.target_location
    $homeArrivalTileX = $fhEdge.target_x
    $homeArrivalTileY = $fhEdge.target_y
    $homeExteriorEntryX = $homeDoorTileX
    $homeExteriorEntryY = $homeDoorTileY + 1

    if ($null -eq $homeDoorTileX -or $null -eq $homeIndoorId -or $null -eq $homeArrivalTileX) {
        $summary = [ordered]@{ status = "home_route_failed"; run_id = $RunId; reason = "Farmhouse route_graph edge door/indoor/arrival data incomplete"; edge = $fhEdge }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: incomplete Farmhouse building_door edge in route_graph."
    }

    # Cross-check against farm.buildings transparent row (fail closed)
    if ($null -eq $shipSnapshot.state.farm.buildings.value) {
        $summary = [ordered]@{
            status = "home_route_failed"
            run_id = $RunId
            reason = "farm.buildings data unavailable; cannot cross-check route_graph Farmhouse edge"
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: farm.buildings data unavailable."
    }

    $farmBuildings = $shipSnapshot.state.farm.buildings.value
    $farmhouseBuildings = @($farmBuildings | Where-Object { [string]$_.type -eq "Farmhouse" -and $_.has_door_access_resolved -eq $true })
    if ($farmhouseBuildings.Count -ne 1) {
        $summary = [ordered]@{
            status = "home_route_failed"
            run_id = $RunId
            reason = "expected exactly one resolved Farmhouse row in farm.buildings for cross-check"
            resolved_farmhouse_count = $farmhouseBuildings.Count
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: expected 1 resolved Farmhouse building row, found $($farmhouseBuildings.Count)."
    }

    $fhBuilding = $farmhouseBuildings[0]
    $buildingDoorX = $fhBuilding.door.human_door_absolute_tile_x
    $buildingDoorY = $fhBuilding.door.human_door_absolute_tile_y
    $buildingIndoorId = $fhBuilding.door.indoor_location_id
    $buildingArrivalX = $fhBuilding.door.indoor_arrival_tile_x
    $buildingArrivalY = $fhBuilding.door.indoor_arrival_tile_y
    $buildingEntryX = $fhBuilding.door.exterior_entry_tile_x
    $buildingEntryY = $fhBuilding.door.exterior_entry_tile_y

    if ($buildingDoorX -ne $homeDoorTileX -or $buildingDoorY -ne $homeDoorTileY -or
        [string]$buildingIndoorId -ne [string]$homeIndoorId -or
        $buildingArrivalX -ne $homeArrivalTileX -or $buildingArrivalY -ne $homeArrivalTileY -or
        $buildingEntryX -ne $homeExteriorEntryX -or $buildingEntryY -ne $homeExteriorEntryY) {
        $summary = [ordered]@{
            status = "home_route_failed"
            run_id = $RunId
            reason = "route_graph Farmhouse edge disagrees with farm.buildings transparent row"
            graph_door = "$homeDoorTileX,$homeDoorTileY"
            building_door = "$buildingDoorX,$buildingDoorY"
            graph_indoor = [string]$homeIndoorId
            building_indoor = [string]$buildingIndoorId
            graph_arrival = "$homeArrivalTileX,$homeArrivalTileY"
            building_arrival = "$buildingArrivalX,$buildingArrivalY"
            graph_entry = "$homeExteriorEntryX,$homeExteriorEntryY"
            building_entry = "$buildingEntryX,$buildingEntryY"
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: route_graph edge disagrees with farm.buildings transparent row."
    }

    Write-Host "Farmhouse building_door resolved via route_graph: action tile ($homeDoorTileX,$homeDoorTileY) -> $homeIndoorId arrival ($homeArrivalTileX,$homeArrivalTileY)"

    # Move to exterior entry stand tile first if not already there
    $playerAfterShipX = if ($null -ne $shipSnapshot.state.player.tile_x.value) { [int]$shipSnapshot.state.player.tile_x.value } else { -1 }
    $playerAfterShipY = if ($null -ne $shipSnapshot.state.player.tile_y.value) { [int]$shipSnapshot.state.player.tile_y.value } else { -1 }

    if ($playerAfterShipX -ne $homeExteriorEntryX -or $playerAfterShipY -ne $homeExteriorEntryY) {
        $homeStandMoveRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-ship-inventory-smoke"
            queue_item_id = "runtime-ship-inventory-smoke.home-stand-move"
            before_state_hash = $shipSnapshot.state_hash
            option_id = "executor.move_to_tile"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$homeExteriorEntryX
            target_tile_y = [int]$homeExteriorEntryY
        }
        $homeStandMoveResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $homeStandMoveRequest -TimeoutSeconds 120
        $homeStandSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30
        Write-JsonFile (Join-Path $runDirectory "home-stand-move-request.json") $homeStandMoveRequest
        Write-JsonFile (Join-Path $runDirectory "home-stand-move-result.json") $homeStandMoveResult
        if ($homeStandMoveResult.status -ne "applied") {
            $summary = [ordered]@{ status = "home_route_failed"; run_id = $RunId; reason = "could not move to Farmhouse exterior entry tile"; move_status = $homeStandMoveResult.status }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
            throw "Home route failed: could not reach Farmhouse exterior entry ($homeExteriorEntryX,$homeExteriorEntryY)."
        }
    } else {
        $homeStandSnapshot = $shipSnapshot
    }

    # Traverse Farmhouse building_door connector
    $homeConnectorRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-ship-inventory-smoke"
        queue_item_id = "runtime-ship-inventory-smoke.home-connector"
        before_state_hash = $homeStandSnapshot.state_hash
        option_id = "executor.traverse_connector"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = [int]$homeDoorTileX
        target_tile_y = [int]$homeDoorTileY
        connector_kind = "building_door"
        expected_target_location = [string]$homeIndoorId
        expected_arrival_tile_x = [int]$homeArrivalTileX
        expected_arrival_tile_y = [int]$homeArrivalTileY
    }

    $homeConnectorResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $homeConnectorRequest -TimeoutSeconds 180
    $homeConnectorSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30

    Write-JsonFile (Join-Path $runDirectory "home-connector-request.json") $homeConnectorRequest
    Write-JsonFile (Join-Path $runDirectory "home-connector-result.json") $homeConnectorResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-home-connector.json") $homeConnectorSnapshot

    if ($homeConnectorResult.status -ne "applied" -or $homeConnectorResult.primitive_verification_status -ne "verified") {
        $summary = [ordered]@{
            status = "home_route_failed"
            run_id = $RunId
            reason = "Farmhouse building_door connector did not apply/verify"
            connector_status = $homeConnectorResult.status
            connector_verification = $homeConnectorResult.primitive_verification_status
            connector_block_reasons = @($homeConnectorResult.block_reasons)
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: building_door to $homeIndoorId not verified."
    }

    # Verify post-route location is the home indoor location
    $homeLocationIdentity = if ($null -ne $homeConnectorSnapshot.state -and
        $null -ne $homeConnectorSnapshot.state.current_location -and
        $null -ne $homeConnectorSnapshot.state.current_location.identity) {
        $homeConnectorSnapshot.state.current_location.identity.value
    } else { $null }

    $homeLocationName = if ($null -ne $homeLocationIdentity) { [string]$homeLocationIdentity.name_or_unique_name } else { "" }

    if ($homeLocationName -ne $homeIndoorId) {
        $summary = [ordered]@{
            status = "home_route_failed"
            run_id = $RunId
            reason = "post-connector location is not Farmhouse interior"
            expected = $homeIndoorId
            actual = $homeLocationName
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: expected $homeIndoorId, got '$homeLocationName'."
    }

    $homePlayerTileX = if ($null -ne $homeConnectorSnapshot.state.player.tile_x.value) { [int]$homeConnectorSnapshot.state.player.tile_x.value } else { -1 }
    $homePlayerTileY = if ($null -ne $homeConnectorSnapshot.state.player.tile_y.value) { [int]$homeConnectorSnapshot.state.player.tile_y.value } else { -1 }
    if ($homePlayerTileX -ne [int]$homeArrivalTileX -or $homePlayerTileY -ne [int]$homeArrivalTileY) {
        $summary = [ordered]@{
            status = "home_route_failed"
            run_id = $RunId
            reason = "post-connector player tile does not match expected arrival tile"
            expected_x = [int]$homeArrivalTileX
            expected_y = [int]$homeArrivalTileY
            actual_x = $homePlayerTileX
            actual_y = $homePlayerTileY
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Home route failed: player at ($homePlayerTileX,$homePlayerTileY), expected arrival ($homeArrivalTileX,$homeArrivalTileY)."
    }

    Write-Host "Farmhouse connector traversed successfully. Player is now in: $homeLocationName at ($homePlayerTileX,$homePlayerTileY)"

    # Stage 5: Execute sleep to advance to next day and trigger overnight settlement
    $receiptPath = $shipResult.ship_pending_receipt_path
    Write-Host "Immediate postcondition passed. Pending receipt: $receiptPath"

    if ($SkipOvernight) {
        $summary = [ordered]@{ status = "immediate_only"; run_id = $RunId; receipt_path = $receiptPath; overnight_skipped = $true }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        Write-Host "Overnight stage skipped (-SkipOvernight). Receipt remains pending."
        return
    }

    $sleepRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-ship-inventory-smoke"
        queue_item_id = "runtime-ship-inventory-smoke.sleep"
        before_state_hash = $homeConnectorSnapshot.state_hash
        option_id = "executor.sleep"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }

    $sleepResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $sleepRequest -TimeoutSeconds 300
    Write-JsonFile (Join-Path $runDirectory "sleep-request.json") $sleepRequest
    Write-JsonFile (Join-Path $runDirectory "sleep-result.json") $sleepResult

    if ($sleepResult.status -ne "applied") {
        $summary = [ordered]@{ status = "sleep_failed"; run_id = $RunId; sleep_status = $sleepResult.status; receipt_path = $receiptPath }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Sleep execution failed. See $runDirectory"
    }

    # Stage 5: Poll exact durable receipt until settlement is completed
    $deadline = (Get-Date).AddSeconds($OvernightPollTimeoutSeconds)
    $settled = $false
    $settlementStatus = "timed_out"
    $settledReceipt = $null

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if (-not ([string]::IsNullOrWhiteSpace($receiptPath)) -and (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
            try {
                $receipt = Get-Content -LiteralPath $receiptPath -Encoding utf8 | ConvertFrom-Json
                if ($null -eq $receipt) { continue }

                if ($receipt.run_id -ne $RunId) {
                    Write-Host "Warning: receipt run_id mismatch. Expected $RunId, got $($receipt.run_id). Skipping."
                    continue
                }
                if ($receipt.queue_id -ne "runtime-ship-inventory-smoke") {
                    Write-Host "Warning: receipt queue_id mismatch. Skipping."
                    continue
                }
                if ($receipt.queue_item_id -ne "runtime-ship-inventory-smoke.ship") {
                    Write-Host "Warning: receipt queue_item_id mismatch. Expecting runtime-ship-inventory-smoke.ship, got $($receipt.queue_item_id). Skipping."
                    continue
                }
                if ($receipt.request_nonce -ne $shipRequest.request_nonce) {
                    Write-Host "Warning: receipt request_nonce mismatch. Skipping."
                    continue
                }
                if ($receipt.unqualified_item_id -ne $FixtureQualifiedItemId.Substring($FixtureQualifiedItemId.IndexOf(')') + 1)) {
                    Write-Host "Warning: receipt unqualified_item_id mismatch against fixture. Skipping."
                    continue
                }
                if ($receipt.qualified_item_id -ne $FixtureQualifiedItemId) {
                    Write-Host "Warning: receipt qualified_item_id mismatch against fixture. Skipping."
                    continue
                }
                if ($receipt.quantity -ne 1) {
                    Write-Host "Warning: receipt quantity mismatch. Expected 1, got $($receipt.quantity). Skipping."
                    continue
                }
                if ($receipt.source_date -ne $shipResult.ship_source_date) {
                    Write-Host "Warning: receipt source_date mismatch. Expected $($shipResult.ship_source_date), got $($receipt.source_date). Skipping."
                    continue
                }
                $receiptFileName = Split-Path -Leaf $receiptPath
                if ($receiptFileName -notmatch [regex]::Escape($shipRequest.request_nonce)) {
                    Write-Host "Warning: receipt filename does not contain request nonce $($shipRequest.request_nonce). Skipping."
                    continue
                }

                if ($receipt.status -in @("completed", "failed", "ambiguous", "timed_out")) {
                    $settled = $true
                    $settlementStatus = $receipt.status
                    $settledReceipt = $receipt
                    Write-JsonFile (Join-Path $runDirectory "settled-receipt.json") $receipt
                    break
                }
            } catch {
                Start-Sleep -Seconds 1
            }
        }
        if ($settled) { break }
    }

    if (-not $settled) {
        $summary = [ordered]@{ status = "overnight_timed_out"; run_id = $RunId; receipt_path = $receiptPath }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Overnight settlement timed out after ${OvernightPollTimeoutSeconds}s. Receipt path: $receiptPath"
    }

    if ($settlementStatus -ne "completed") {
        $summary = [ordered]@{
            status = "failed"
            run_id = $RunId
            receipt_path = $receiptPath
            settlement_status = $settlementStatus
            settlement_reason = $settledReceipt.settlement_reason
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        throw "Receipt settled with non-completed status: $settlementStatus. Reason: $($settledReceipt.settlement_reason)"
    }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        fixture_slot_index = $slotIndex
        fixture_qualified_item_id = $FixtureQualifiedItemId
        bin_tile = "$binTileX,$binTileY"
        stand_tile = "$standTileX,$standTileY"
        ship_status = $shipResult.status
        ship_verification = $shipResult.primitive_verification_status
        ship_inventory_count_before = $shipResult.ship_inventory_count_before
        ship_inventory_count_after = $shipResult.ship_inventory_count_after
        ship_bin_count_before = $shipResult.ship_bin_count_before
        ship_bin_count_after = $shipResult.ship_bin_count_after
        ship_bin_signature_before = $shipResult.ship_bin_signature_before
        ship_bin_signature_after = $shipResult.ship_bin_signature_after
        ship_source_date = $shipResult.ship_source_date
        ship_pending_receipt_path = $shipResult.ship_pending_receipt_path
        home_connector_target = "$homeIndoorId"
        home_connector_door_tile = "$homeDoorTileX,$homeDoorTileY"
        home_connector_arrival_tile = "$homeArrivalTileX,$homeArrivalTileY"
        home_connector_status = $homeConnectorResult.status
        home_connector_verification = $homeConnectorResult.primitive_verification_status
        overnight_settlement = $settlementStatus
        kept_game_running = $false
    }

    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    Write-Host "=== PASSED: $settlementStatus ==="
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        } else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
