param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-machine-lifecycle-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-machine-lifecycle-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $CompletionTimeoutSeconds = 180,
    [int] $TargetTileX = 60,
    [int] $TargetTileY = 15,
    [string] $RecipeName = "Furnace",
    [string] $MachineQualifiedItemId = "(BC)13",
    [string] $ProcessInputQualifiedItemId = "(O)378",
    [int] $ProcessInputQuantity = 5,
    [string] $ProcessAdditionalItemsJson =
        '[{"qualified_item_id":"(O)382","quantity":1}]',
    [switch] $UseWorkbench,
    [int] $WorkbenchTileX = 55,
    [int] $WorkbenchTileY = 15,
    [string] $WorkbenchProcessInputQualifiedItemId = "(O)380",
    [switch] $VerifyRelocation,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ($UseWorkbench) {
    $ProcessInputQualifiedItemId =
        $WorkbenchProcessInputQualifiedItemId
}

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [string] $Url,
        $Body,
        [int] $TimeoutSeconds = 120
    )
    $json = $Body | ConvertTo-Json -Depth 64
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param(
        [string] $Url,
        [int] $TimeoutSeconds = 30
    )
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Health {
    param(
        [string] $Url,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($health.status -eq "ok") {
                return $health
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
        [string] $Url,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url
            $saveReadable = $snapshot.save_id.status -in @(
                "available",
                "derived"
            )
            $player = $snapshot.state.player
            $farm = $snapshot.state.farm
            $craftingReadable =
                $null -ne $player.machine_crafting -and
                $player.machine_crafting.status -in @(
                    "available",
                    "derived"
                )
            $placementReadable =
                $null -ne $player.machine_placement -and
                $player.machine_placement.status -in @(
                    "available",
                    "derived"
                )
            $machinesReadable =
                $null -ne $farm.machines -and
                $farm.machines.status -in @(
                    "available",
                    "derived",
                    "partial"
                )
            $lastStatus =
                "save=$saveReadable" +
                ";crafting=$craftingReadable" +
                ";placement=$placementReadable" +
                ";machines=$machinesReadable"
            if ($saveReadable -and
                $craftingReadable -and
                $placementReadable -and
                $machinesReadable) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw (
        "Timed out waiting for machine lifecycle snapshot. " +
        "Last status: $lastStatus"
    )
}

function Find-RecipeRow {
    param(
        $Snapshot,
        [string] $Name
    )
    foreach ($row in @(
        $Snapshot.state.player.machine_crafting.value.rows
    )) {
        if ([string]$row.recipe_name -eq $Name) {
            return $row
        }
    }
    return $null
}

function Find-WorkbenchCraftingSource {
    param(
        $RecipeRow,
        [string] $LocationId,
        [int] $X,
        [int] $Y
    )
    foreach ($source in @(
        $RecipeRow.workbench_crafting_sources
    )) {
        if ([string]$source.location_id -eq $LocationId -and
            [int]$source.tile_x -eq $X -and
            [int]$source.tile_y -eq $Y) {
            return $source
        }
    }
    return $null
}

function Find-PlacementRow {
    param(
        $Snapshot,
        [string] $QualifiedItemId
    )
    foreach ($row in @(
        $Snapshot.state.player.machine_placement.value.rows
    )) {
        if ([string]$row.qualified_item_id -eq
            $QualifiedItemId) {
            return $row
        }
    }
    return $null
}

function Test-LegalRangeContains {
    param(
        $Row,
        [string] $LocationId,
        [int] $X,
        [int] $Y
    )
    foreach ($location in @($Row.locations)) {
        if ([string]$location.location_id -ne $LocationId) {
            continue
        }
        foreach ($range in @(
            $location.static_legal_tile_ranges
        )) {
            if ([int]$range.y -eq $Y -and
                $X -ge [int]$range.start_x -and
                $X -le [int]$range.end_x) {
                return $true
            }
        }
    }
    return $false
}

function Find-Machine {
    param(
        $Snapshot,
        [string] $LocationId,
        [int] $X,
        [int] $Y
    )
    foreach ($machine in @(
        $Snapshot.state.farm.machines.value
    )) {
        if ([string]$machine.location_id -eq $LocationId -and
            [int]$machine.tile_x -eq $X -and
            [int]$machine.tile_y -eq $Y) {
            return $machine
        }
    }
    return $null
}

function Find-Debris {
    param(
        $Snapshot,
        [string] $QualifiedItemId
    )
    foreach ($debris in @(
        $Snapshot.state.farm.debris.value
    )) {
        if ([string]$debris.qualified_item_id -eq
                $QualifiedItemId -and
            @($debris.chunks).Count -gt 0) {
            return $debris
        }
    }
    return $null
}

function Wait-MachineRecovery {
    param(
        [string] $Url,
        [string] $LocationId,
        [int] $X,
        [int] $Y,
        [string] $QualifiedItemId,
        [int] $InventoryCountBefore,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot `
            -Url $Url -TimeoutSeconds 20
        $machine = Find-Machine `
            -Snapshot $snapshot `
            -LocationId $LocationId -X $X -Y $Y
        $debris = Find-Debris `
            -Snapshot $snapshot `
            -QualifiedItemId $QualifiedItemId
        $inventoryCount = Inventory-Count `
            -Snapshot $snapshot `
            -QualifiedItemId $QualifiedItemId
        $lastStatus =
            "machine_present=$($null -ne $machine)" +
            ";debris_present=$($null -ne $debris)" +
            ";inventory=$inventoryCount"
        if ($null -eq $machine -and $null -ne $debris) {
            return [ordered]@{
                snapshot = $snapshot
                debris = $debris
                mode = "debris_visible"
            }
        }
        if ($null -eq $machine -and
            $inventoryCount -gt $InventoryCountBefore) {
            return [ordered]@{
                snapshot = $snapshot
                debris = $null
                mode = "native_auto_collected"
            }
        }
        Start-Sleep -Milliseconds 250
    }
    throw (
        "Native machine recovery did not settle before timeout. " +
        "Last status: $lastStatus"
    )
}

function Count-Machines {
    param(
        $Snapshot,
        [string] $QualifiedItemId
    )
    return @(
        $Snapshot.state.farm.machines.value |
            Where-Object {
                [string]$_.qualified_item_id -eq
                    $QualifiedItemId
            }
    ).Count
}

function Inventory-Count {
    param(
        $Snapshot,
        [string] $QualifiedItemId
    )
    $count = 0
    foreach ($item in @(
        $Snapshot.state.player.inventory.value
    )) {
        if ([string]$item.qualified_item_id -eq
            $QualifiedItemId) {
            $count += [int]$item.stack
        }
    }
    return $count
}

function Wait-RecipeReadyWithoutMachine {
    param(
        [string] $Url,
        [string] $Name,
        [string] $MachineId,
        [switch] $Workbench,
        [int] $WorkbenchX,
        [int] $WorkbenchY,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot `
            -Url $Url -TimeoutSeconds 20
        $row = Find-RecipeRow `
            -Snapshot $snapshot -Name $Name
        $machineCount = Count-Machines `
            -Snapshot $snapshot `
            -QualifiedItemId $MachineId
        $inventoryCount = Inventory-Count `
            -Snapshot $snapshot `
            -QualifiedItemId $MachineId
        $locationId =
            [string]$snapshot.state.player.location_id.value
        $workbenchSource = if ($Workbench -and
            $null -ne $row) {
            Find-WorkbenchCraftingSource `
                -RecipeRow $row `
                -LocationId $locationId `
                -X $WorkbenchX -Y $WorkbenchY
        }
        else {
            $null
        }
        $sourceReady = if ($Workbench) {
            $null -ne $workbenchSource -and
            [string]$workbenchSource.craft_candidate_status -eq
                "ready_for_native_workbench_crafting_menu"
        }
        else {
            $null -ne $row -and
            [string]$row.craft_candidate_status -eq
                "ready_for_native_personal_crafting_menu"
        }
        $lastStatus =
            "recipe_status=$([string]$row.craft_candidate_status)" +
            ";workbench_status=$([string]$workbenchSource.craft_candidate_status)" +
            ";machine_count=$machineCount" +
            ";inventory_count=$inventoryCount"
        if ($sourceReady -and
            $machineCount -eq 0 -and
            $inventoryCount -eq 0) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw (
        "Machine recipe did not become ready from an empty fleet. " +
        "Last status: $lastStatus"
    )
}

function Wait-PlacementInventory {
    param(
        [string] $Url,
        [string] $MachineId,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot `
            -Url $Url -TimeoutSeconds 20
        $row = Find-PlacementRow `
            -Snapshot $snapshot `
            -QualifiedItemId $MachineId
        if ($null -ne $row) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Crafted machine did not enter machine_placement."
}

function Wait-LoadableMachine {
    param(
        [string] $Url,
        [string] $LocationId,
        [int] $X,
        [int] $Y,
        [string] $InputId,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot `
            -Url $Url -TimeoutSeconds 20
        $machine = Find-Machine `
            -Snapshot $snapshot `
            -LocationId $LocationId -X $X -Y $Y
        $input = $null
        foreach ($row in @($machine.loadable_inputs)) {
            if ([string]$row.qualified_item_id -eq $InputId) {
                $input = $row
                break
            }
        }
        $lastStatus =
            "machine_present=$($null -ne $machine)" +
            ";probe_status=$([string]$machine.loadable_input_probe_status)" +
            ";input_present=$($null -ne $input)"
        if ($null -ne $machine -and
            $null -ne $input -and
            [string]$input.predicted_output.status -eq
                "available") {
            return [ordered]@{
                snapshot = $snapshot
                machine = $machine
                input = $input
            }
        }
        Start-Sleep -Milliseconds 500
    }
    throw (
        "Placed machine did not expose the expected native input. " +
        "Last status: $lastStatus"
    )
}

function Wait-MachineReady {
    param(
        [string] $Url,
        [string] $LocationId,
        [int] $X,
        [int] $Y,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot `
            -Url $Url -TimeoutSeconds 20
        $machine = Find-Machine `
            -Snapshot $snapshot `
            -LocationId $LocationId -X $X -Y $Y
        $heldId = if ($null -ne $machine.held_item) {
            [string]$machine.held_item.qualified_item_id
        }
        else {
            ""
        }
        $lastStatus =
            "present=$($null -ne $machine)" +
            ";minutes=$([int]$machine.minutes_until_ready)" +
            ";ready=$([bool]$machine.ready_for_harvest)" +
            ";held=$heldId"
        if ($null -ne $machine -and
            [bool]$machine.ready_for_harvest -and
            -not [string]::IsNullOrWhiteSpace($heldId)) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw (
        "Machine did not naturally become ready before timeout. " +
        "Last status: $lastStatus"
    )
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=training_machine"
$executeUrl =
    "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (
    Join-Path $OutputDirectory $RunId
)
New-Item -ItemType Directory -Force -Path $runDirectory |
    Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH =
        $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID =
        $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE =
        $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -PassThru
    $executorHealth = Wait-Health `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $initial = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-lifecycle-smoke"
        queue_item_id = "runtime-machine-lifecycle-smoke.setup"
        before_state_hash = $initial.state_hash
        option_id = "debug.setup_machine_lifecycle_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        recipe_name = $RecipeName
        output_qualified_item_id = $MachineQualifiedItemId
        process_input_qualified_item_id =
            $ProcessInputQualifiedItemId
        process_input_quantity = $ProcessInputQuantity
        process_additional_items_json =
            $ProcessAdditionalItemsJson
        crafting_source = if ($UseWorkbench) {
            "native_workbench_crafting_menu"
        }
        else {
            "native_personal_crafting_menu"
        }
        interaction_tile_x = if ($UseWorkbench) {
            $WorkbenchTileX
        }
        else {
            $null
        }
        interaction_tile_y = if ($UseWorkbench) {
            $WorkbenchTileY
        }
        else {
            $null
        }
    }
    $setupResult = Invoke-JsonPost `
        -Url $executeUrl -Body $setupRequest
    Write-JsonFile `
        (Join-Path $runDirectory "setup-result.json") `
        $setupResult
    if ($setupResult.status -ne "applied") {
        throw "Machine lifecycle fixture failed."
    }

    Start-Sleep -Milliseconds 750
    $beforeCraft = Wait-RecipeReadyWithoutMachine `
        -Url $snapshotUrl `
        -Name $RecipeName `
        -MachineId $MachineQualifiedItemId `
        -Workbench:$UseWorkbench `
        -WorkbenchX $WorkbenchTileX `
        -WorkbenchY $WorkbenchTileY `
        -TimeoutSeconds 45
    $recipe = Find-RecipeRow `
        -Snapshot $beforeCraft -Name $RecipeName
    $craftingLocationId =
        [string]$beforeCraft.state.player.location_id.value
    $workbenchSource = if ($UseWorkbench) {
        Find-WorkbenchCraftingSource `
            -RecipeRow $recipe `
            -LocationId $craftingLocationId `
            -X $WorkbenchTileX -Y $WorkbenchTileY
    }
    else {
        $null
    }
    if ($UseWorkbench -and $null -eq $workbenchSource) {
        throw "Transparent Workbench crafting source was unavailable."
    }
    $craftMaterialSource = if ($UseWorkbench) {
        $workbenchSource
    }
    else {
        $recipe
    }
    $craftRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-lifecycle-smoke"
        queue_item_id = "runtime-machine-lifecycle-smoke.craft"
        before_state_hash = $beforeCraft.state_hash
        option_id = "executor.craft_machine_item"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        recipe_name = [string]$recipe.recipe_name
        output_qualified_item_id =
            [string]$recipe.output_qualified_item_id
        output_item_id = [string]$recipe.output_item_id
        output_count = [int]$recipe.output_count_per_craft
        times_crafted_before = [int]$recipe.times_crafted
        ingredient_rows_json = ConvertTo-Json `
            -InputObject @($craftMaterialSource.ingredient_rows) `
            -Depth 32 -Compress
        crafting_source = if ($UseWorkbench) {
            "native_workbench_crafting_menu"
        }
        else {
            "native_personal_crafting_menu"
        }
    }
    if ($UseWorkbench) {
        $craftRequest["workbench_access_point_id"] =
            [string]$workbenchSource.workbench_access_point_id
        $craftRequest["workbench_container_node_ids_json"] =
            ConvertTo-Json `
                -InputObject @(
                    $workbenchSource.native_container_node_ids
                ) -Compress
        $craftRequest["location_id"] = $craftingLocationId
        $craftRequest["target_tile_x"] = $WorkbenchTileX
        $craftRequest["target_tile_y"] = $WorkbenchTileY
        $craftRequest["stand_tile_x"] = $WorkbenchTileX + 1
        $craftRequest["stand_tile_y"] = $WorkbenchTileY
    }
    $craftResult = Invoke-JsonPost `
        -Url $executeUrl -Body $craftRequest
    Write-JsonFile `
        (Join-Path $runDirectory "craft-result.json") `
        $craftResult
    if ($craftResult.status -ne "applied") {
        throw "Native machine crafting failed."
    }

    Start-Sleep -Milliseconds 750
    $beforePlace = Wait-PlacementInventory `
        -Url $snapshotUrl `
        -MachineId $MachineQualifiedItemId `
        -TimeoutSeconds 30
    $placement = Find-PlacementRow `
        -Snapshot $beforePlace `
        -QualifiedItemId $MachineQualifiedItemId
    $locationId =
        [string]$beforePlace.state.player.location_id.value
    $targetLegal = Test-LegalRangeContains `
        -Row $placement -LocationId $locationId `
        -X $TargetTileX -Y $TargetTileY
    if (-not $targetLegal) {
        throw "Target tile was not in the transparent legal range."
    }
    $moveToPlacementResult = $null
    if ($UseWorkbench) {
        $moveToPlacementRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-machine-lifecycle-smoke"
            queue_item_id =
                "runtime-machine-lifecycle-smoke.move-to-placement"
            before_state_hash = $beforePlace.state_hash
            option_id = "executor.move_to_tile"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            max_crops = 512
            target_tile_x = $TargetTileX + 1
            target_tile_y = $TargetTileY
        }
        $moveToPlacementResult = Invoke-JsonPost `
            -Url $executeUrl `
            -Body $moveToPlacementRequest `
            -TimeoutSeconds 180
        Write-JsonFile `
            (Join-Path $runDirectory `
                "move-to-placement-result.json") `
            $moveToPlacementResult
        if ($moveToPlacementResult.status -ne "applied") {
            throw "Native movement back to placement stand failed."
        }
        $beforePlace = Wait-PlacementInventory `
            -Url $snapshotUrl `
            -MachineId $MachineQualifiedItemId `
            -TimeoutSeconds 30
        $placement = Find-PlacementRow `
            -Snapshot $beforePlace `
            -QualifiedItemId $MachineQualifiedItemId
    }
    $placeRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-lifecycle-smoke"
        queue_item_id = "runtime-machine-lifecycle-smoke.place"
        before_state_hash = $beforePlace.state_hash
        option_id = "executor.place_machine"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        inventory_slot_index =
            [int]$placement.inventory_slot_index
        qualified_item_id =
            [string]$placement.qualified_item_id
        item_id = [string]$placement.item_id
        location_id = $locationId
    }
    $placeResult = Invoke-JsonPost `
        -Url $executeUrl -Body $placeRequest
    Write-JsonFile `
        (Join-Path $runDirectory "place-result.json") `
        $placeResult
    if ($placeResult.status -ne "applied") {
        throw "Native machine placement failed."
    }

    Start-Sleep -Milliseconds 750
    $loadable = Wait-LoadableMachine `
        -Url $snapshotUrl `
        -LocationId $locationId `
        -X $TargetTileX -Y $TargetTileY `
        -InputId $ProcessInputQualifiedItemId `
        -TimeoutSeconds 45
    $beforeLoad = $loadable.snapshot
    $input = $loadable.input
    $predictedOutputId =
        [string]$input.predicted_output.item.qualified_item_id
    $requiredInputCount =
        [int]$input.predicted_output.required_count
    $processInputCountBeforeLoad = Inventory-Count `
        -Snapshot $beforeLoad `
        -QualifiedItemId $ProcessInputQualifiedItemId
    $additionalItems = @(
        $ProcessAdditionalItemsJson | ConvertFrom-Json
    )
    $additionalCountsBeforeLoad = [ordered]@{}
    foreach ($additional in $additionalItems) {
        $additionalId =
            [string]$additional.qualified_item_id
        $additionalCountsBeforeLoad[$additionalId] =
            Inventory-Count `
                -Snapshot $beforeLoad `
                -QualifiedItemId $additionalId
    }
    $loadRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-lifecycle-smoke"
        queue_item_id = "runtime-machine-lifecycle-smoke.load"
        before_state_hash = $beforeLoad.state_hash
        option_id = "executor.load_machine_input"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        input_slot_index = [int]$input.slot_index
        location_id = $locationId
        qualified_item_id = $ProcessInputQualifiedItemId
    }
    $loadResult = Invoke-JsonPost `
        -Url $executeUrl -Body $loadRequest
    Write-JsonFile `
        (Join-Path $runDirectory "load-result.json") `
        $loadResult
    if ($loadResult.status -ne "applied") {
        throw "Native machine loading failed."
    }

    Start-Sleep -Milliseconds 750
    $afterLoad = Wait-WorldSnapshot `
        -Url $snapshotUrl -TimeoutSeconds 30
    $processInputCountAfterLoad = Inventory-Count `
        -Snapshot $afterLoad `
        -QualifiedItemId $ProcessInputQualifiedItemId
    $additionalCountsAfterLoad = [ordered]@{}
    foreach ($additional in $additionalItems) {
        $additionalId =
            [string]$additional.qualified_item_id
        $additionalCountsAfterLoad[$additionalId] =
            Inventory-Count `
                -Snapshot $afterLoad `
                -QualifiedItemId $additionalId
    }
    $inputConsumptionVerified =
        $requiredInputCount -gt 0 -and
        $processInputCountAfterLoad -eq (
            $processInputCountBeforeLoad -
            $requiredInputCount
        )
    $additionalConsumptionVerified = $true
    foreach ($additional in $additionalItems) {
        $additionalId =
            [string]$additional.qualified_item_id
        $additionalQuantity = [int]$additional.quantity
        if ($additionalCountsAfterLoad[$additionalId] -ne (
                $additionalCountsBeforeLoad[$additionalId] -
                $additionalQuantity
            )) {
            $additionalConsumptionVerified = $false
        }
    }
    if (-not $inputConsumptionVerified -or
        -not $additionalConsumptionVerified) {
        throw (
            "Native machine material consumption mismatch: " +
            "input_verified=$inputConsumptionVerified" +
            ";additional_verified=$additionalConsumptionVerified"
        )
    }

    $ready = Wait-MachineReady `
        -Url $snapshotUrl `
        -LocationId $locationId `
        -X $TargetTileX -Y $TargetTileY `
        -TimeoutSeconds $CompletionTimeoutSeconds
    $readyMachine = Find-Machine `
        -Snapshot $ready `
        -LocationId $locationId `
        -X $TargetTileX -Y $TargetTileY
    $outputId =
        [string]$readyMachine.held_item.qualified_item_id
    if ($outputId -ne $predictedOutputId) {
        throw (
            "Natural machine output drifted: predicted=" +
            $predictedOutputId + ";observed=" + $outputId
        )
    }
    $outputCountBefore = Inventory-Count `
        -Snapshot $ready -QualifiedItemId $outputId
    $collectRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-lifecycle-smoke"
        queue_item_id =
            "runtime-machine-lifecycle-smoke.collect"
        before_state_hash = $ready.state_hash
        option_id = "executor.collect_machine_output"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        location_id = $locationId
        qualified_item_id = $outputId
        expected_skill_experience_deltas_json =
            [string]$readyMachine.harvest_experience_deltas_json
        expected_mastery_experience_delta =
            [int]$readyMachine.harvest_mastery_experience_delta
    }
    $collectResult = Invoke-JsonPost `
        -Url $executeUrl -Body $collectRequest
    Write-JsonFile `
        (Join-Path $runDirectory "collect-result.json") `
        $collectResult
    Start-Sleep -Milliseconds 750
    $after = Wait-WorldSnapshot `
        -Url $snapshotUrl -TimeoutSeconds 30
    $afterMachine = Find-Machine `
        -Snapshot $after `
        -LocationId $locationId `
        -X $TargetTileX -Y $TargetTileY
    $outputCountAfter = Inventory-Count `
        -Snapshot $after -QualifiedItemId $outputId
    $afterHeldId = if ($null -ne $afterMachine.held_item) {
        [string]$afterMachine.held_item.qualified_item_id
    }
    else {
        ""
    }
    $removeResult = $null
    $afterRemove = $null
    $pickupResult = $null
    $afterPickup = $null
    $relocatePlaceResult = $null
    $afterRelocate = $null
    $relocationTargetX = $null
    $relocationTargetY = $null
    $relocationPassed = -not $VerifyRelocation
    if ($VerifyRelocation) {
        if ($null -eq $afterMachine -or
            -not [bool]$afterMachine.removal_safe_now -or
            [string]$afterMachine.removal_status -ne
                "safe_idle_native_pickaxe") {
            throw (
                "Collected machine was not transparently safe to remove: " +
                (ConvertTo-Json `
                    -InputObject $afterMachine.removal_block_reasons `
                    -Compress)
            )
        }
        $standX = [int]$after.state.player.tile_x.value
        $standY = [int]$after.state.player.tile_y.value
        $machineInventoryBeforeRemoval = Inventory-Count `
            -Snapshot $after `
            -QualifiedItemId $MachineQualifiedItemId
        $removeRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-machine-lifecycle-smoke"
            queue_item_id =
                "runtime-machine-lifecycle-smoke.remove"
            before_state_hash = $after.state_hash
            option_id = "executor.remove_machine"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at =
                [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $TargetTileX
            target_tile_y = $TargetTileY
            stand_tile_x = $standX
            stand_tile_y = $standY
            tool_slot_index =
                [int]$afterMachine.removal_tool_slot_index
            tool_qualified_item_id =
                [string]$afterMachine.removal_tool_qualified_item_id
            location_id = $locationId
            qualified_item_id = $MachineQualifiedItemId
            native_contract =
                [string]$afterMachine.removal_native_contract
            machine_removal_projection_fingerprint =
                [string]$afterMachine.removal_projection_fingerprint
            relocation_intent_id =
                "runtime-smoke:machine-relocation"
        }
        $removeResult = Invoke-JsonPost `
            -Url $executeUrl -Body $removeRequest
        Write-JsonFile `
            (Join-Path $runDirectory "remove-result.json") `
            $removeResult
        if ($removeResult.status -ne "applied") {
            throw "Native machine removal failed."
        }

        $recoveryState = Wait-MachineRecovery `
            -Url $snapshotUrl `
            -LocationId $locationId `
            -X $TargetTileX -Y $TargetTileY `
            -QualifiedItemId $MachineQualifiedItemId `
            -InventoryCountBefore $machineInventoryBeforeRemoval `
            -TimeoutSeconds 30
        $afterRemove = $recoveryState.snapshot
        $pickupVerified = $false
        if ($recoveryState.mode -eq "debris_visible") {
            $machineDebris = $recoveryState.debris
            $debrisChunk = @($machineDebris.chunks)[0]
            $pickupRequest = [ordered]@{
                schema_version = "training_execution_request.v1"
                run_id = $RunId
                queue_id = "runtime-machine-lifecycle-smoke"
                queue_item_id =
                    "runtime-machine-lifecycle-smoke.pickup"
                before_state_hash = $afterRemove.state_hash
                option_id = "executor.pickup_debris"
                execution_mode = "training_singleplayer"
                actor = "training_farmer.main"
                save_isolation_path = $savesPath
                request_nonce = [guid]::NewGuid().ToString("N")
                created_at =
                    [DateTimeOffset]::UtcNow.ToString("O")
                target_tile_x = [int]$debrisChunk.tile_x
                target_tile_y = [int]$debrisChunk.tile_y
                debris_index = [int]$machineDebris.debris_index
                qualified_item_id = $MachineQualifiedItemId
                location_id = $locationId
            }
            $pickupResult = Invoke-JsonPost `
                -Url $executeUrl -Body $pickupRequest `
                -TimeoutSeconds 150
            Write-JsonFile `
                (Join-Path $runDirectory "pickup-result.json") `
                $pickupResult
            $pickupVerified =
                $pickupResult.status -eq "applied" -and
                $pickupResult.primitive_verification_status -eq
                    "verified"
            if (-not $pickupVerified) {
                throw "Native machine debris pickup failed."
            }
            Start-Sleep -Milliseconds 750
            $afterPickup = Wait-PlacementInventory `
                -Url $snapshotUrl `
                -MachineId $MachineQualifiedItemId `
                -TimeoutSeconds 30
        }
        else {
            $pickupVerified =
                $recoveryState.mode -eq "native_auto_collected"
            $afterPickup = $afterRemove
        }

        $relocationPlacement = Find-PlacementRow `
            -Snapshot $afterPickup `
            -QualifiedItemId $MachineQualifiedItemId
        $playerX =
            [int]$afterPickup.state.player.tile_x.value
        $playerY =
            [int]$afterPickup.state.player.tile_y.value
        $candidateTiles = @(
            [ordered]@{ x = $playerX + 1; y = $playerY },
            [ordered]@{ x = $playerX - 1; y = $playerY },
            [ordered]@{ x = $playerX; y = $playerY + 1 },
            [ordered]@{ x = $playerX; y = $playerY - 1 }
        )
        foreach ($candidateTile in $candidateTiles) {
            if ($candidateTile.x -eq $TargetTileX -and
                $candidateTile.y -eq $TargetTileY) {
                continue
            }
            if (Test-LegalRangeContains `
                    -Row $relocationPlacement `
                    -LocationId $locationId `
                    -X $candidateTile.x `
                    -Y $candidateTile.y) {
                $relocationTargetX = [int]$candidateTile.x
                $relocationTargetY = [int]$candidateTile.y
                break
            }
        }
        if ($null -eq $relocationTargetX) {
            throw (
                "No transparent native-legal relocation tile was " +
                "adjacent after debris pickup."
            )
        }

        $relocatePlaceRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-machine-lifecycle-smoke"
            queue_item_id =
                "runtime-machine-lifecycle-smoke.relocate-place"
            before_state_hash = $afterPickup.state_hash
            option_id = "executor.place_machine"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at =
                [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = $relocationTargetX
            target_tile_y = $relocationTargetY
            inventory_slot_index =
                [int]$relocationPlacement.inventory_slot_index
            qualified_item_id =
                [string]$relocationPlacement.qualified_item_id
            item_id = [string]$relocationPlacement.item_id
            location_id = $locationId
        }
        $relocatePlaceResult = Invoke-JsonPost `
            -Url $executeUrl -Body $relocatePlaceRequest
        Write-JsonFile `
            (Join-Path $runDirectory `
                "relocate-place-result.json") `
            $relocatePlaceResult
        Start-Sleep -Milliseconds 750
        $afterRelocate = Wait-WorldSnapshot `
            -Url $snapshotUrl -TimeoutSeconds 30
        $relocatedMachine = Find-Machine `
            -Snapshot $afterRelocate `
            -LocationId $locationId `
            -X $relocationTargetX -Y $relocationTargetY
        $oldMachine = Find-Machine `
            -Snapshot $afterRelocate `
            -LocationId $locationId `
            -X $TargetTileX -Y $TargetTileY
        $relocationPassed =
            $removeResult.status -eq "applied" -and
            $removeResult.primitive_verification_status -eq
                "verified" -and
            $pickupVerified -and
            $relocatePlaceResult.status -eq "applied" -and
            $relocatePlaceResult.primitive_verification_status -eq
                "verified" -and
            $null -eq $oldMachine -and
            $null -ne $relocatedMachine -and
            [string]$relocatedMachine.qualified_item_id -eq
                $MachineQualifiedItemId
    }
    $passed =
        $setupResult.status -eq "applied" -and
        $setupResult.primitive_verification_status -eq "verified" -and
        $craftResult.status -eq "applied" -and
        $craftResult.primitive_verification_status -eq "verified" -and
        ($null -eq $moveToPlacementResult -or (
            $moveToPlacementResult.status -eq "applied" -and
            $moveToPlacementResult.primitive_verification_status -eq
                "verified"
        )) -and
        $placeResult.status -eq "applied" -and
        $placeResult.primitive_verification_status -eq "verified" -and
        $loadResult.status -eq "applied" -and
        $loadResult.primitive_verification_status -eq "verified" -and
        $inputConsumptionVerified -and
        $additionalConsumptionVerified -and
        $collectResult.status -eq "applied" -and
        $collectResult.primitive_verification_status -eq "verified" -and
        $null -ne $afterMachine -and
        -not [bool]$afterMachine.ready_for_harvest -and
        [string]::IsNullOrWhiteSpace($afterHeldId) -and
        $outputCountAfter -gt $outputCountBefore -and
        $relocationPassed

    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        recipe_name = $RecipeName
        machine_qualified_item_id = $MachineQualifiedItemId
        process_input_qualified_item_id =
            $ProcessInputQualifiedItemId
        crafting_source = if ($UseWorkbench) {
            "native_workbench_crafting_menu"
        }
        else {
            "native_personal_crafting_menu"
        }
        workbench_access_point_id = if ($UseWorkbench) {
            [string]$workbenchSource.workbench_access_point_id
        }
        else {
            ""
        }
        workbench_container_node_ids = if ($UseWorkbench) {
            [object[]]@(
                $workbenchSource.native_container_node_ids
            )
        }
        else {
            @()
        }
        workbench_source_status = if ($UseWorkbench) {
            [string]$workbenchSource.craft_candidate_status
        }
        else {
            "not_requested"
        }
        predicted_output_qualified_item_id =
            $predictedOutputId
        observed_output_qualified_item_id = $outputId
        location_id = $locationId
        target_tile = "$TargetTileX,$TargetTileY"
        transparent_target_legal = $targetLegal
        empty_fleet_machine_count =
            Count-Machines `
                -Snapshot $beforeCraft `
                -QualifiedItemId $MachineQualifiedItemId
        setup_status = $setupResult.status
        setup_verification =
            $setupResult.primitive_verification_status
        craft_status = $craftResult.status
        craft_verification =
            $craftResult.primitive_verification_status
        move_to_placement_status =
            if ($null -eq $moveToPlacementResult) {
                "not_required"
            }
            else {
                $moveToPlacementResult.status
            }
        move_to_placement_verification =
            if ($null -eq $moveToPlacementResult) {
                "not_required"
            }
            else {
                $moveToPlacementResult.primitive_verification_status
            }
        place_status = $placeResult.status
        place_verification =
            $placeResult.primitive_verification_status
        load_status = $loadResult.status
        load_verification =
            $loadResult.primitive_verification_status
        process_input_required_count = $requiredInputCount
        process_input_count_before_load =
            $processInputCountBeforeLoad
        process_input_count_after_load =
            $processInputCountAfterLoad
        process_input_consumption_verified =
            $inputConsumptionVerified
        additional_counts_before_load =
            $additionalCountsBeforeLoad
        additional_counts_after_load =
            $additionalCountsAfterLoad
        additional_consumption_verified =
            $additionalConsumptionVerified
        machine_minutes_after_load =
            [int]$loadable.input.predicted_output.effective_minutes_until_ready
        ready_observed = [bool]$readyMachine.ready_for_harvest
        collect_status = $collectResult.status
        collect_verification =
            $collectResult.primitive_verification_status
        output_count_before_collect = $outputCountBefore
        output_count_after_collect = $outputCountAfter
        machine_idle_after_collect =
            $null -ne $afterMachine -and
            -not [bool]$afterMachine.ready_for_harvest -and
            [string]::IsNullOrWhiteSpace($afterHeldId)
        relocation_requested = [bool]$VerifyRelocation
        relocation_status = if ($relocationPassed) {
            "verified"
        }
        else {
            "failed"
        }
        removal_status = $removeResult.status
        removal_verification =
            $removeResult.primitive_verification_status
        pickup_status = if ($null -ne $pickupResult) {
            $pickupResult.status
        }
        elseif ($VerifyRelocation) {
            "native_auto_collected"
        }
        else {
            ""
        }
        pickup_verification = if ($null -ne $pickupResult) {
            $pickupResult.primitive_verification_status
        }
        elseif ($VerifyRelocation) {
            "verified_inventory_delta_after_native_debris"
        }
        else {
            ""
        }
        relocation_place_status = $relocatePlaceResult.status
        relocation_place_verification =
            $relocatePlaceResult.primitive_verification_status
        relocation_target = if ($null -ne $relocationTargetX) {
            "$relocationTargetX,$relocationTargetY"
        }
        else {
            ""
        }
        state_hash_initial = $initial.state_hash
        state_hash_after = $after.state_hash
        state_hash_changed =
            $initial.state_hash -ne $after.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile `
        (Join-Path $runDirectory "initial-snapshot.json") `
        $initial
    Write-JsonFile `
        (Join-Path $runDirectory "before-craft-snapshot.json") `
        $beforeCraft
    Write-JsonFile `
        (Join-Path $runDirectory "before-place-snapshot.json") `
        $beforePlace
    Write-JsonFile `
        (Join-Path $runDirectory "before-load-snapshot.json") `
        $beforeLoad
    Write-JsonFile `
        (Join-Path $runDirectory "after-load-snapshot.json") `
        $afterLoad
    Write-JsonFile `
        (Join-Path $runDirectory "ready-snapshot.json") `
        $ready
    Write-JsonFile `
        (Join-Path $runDirectory "after-snapshot.json") `
        $after
    if ($VerifyRelocation) {
        Write-JsonFile `
            (Join-Path $runDirectory `
                "after-remove-snapshot.json") `
            $afterRemove
        Write-JsonFile `
            (Join-Path $runDirectory `
                "after-pickup-snapshot.json") `
            $afterPickup
        Write-JsonFile `
            (Join-Path $runDirectory `
                "after-relocate-snapshot.json") `
            $afterRelocate
    }
    Write-JsonFile `
        (Join-Path $runDirectory "summary.json") `
        $summary
    $summary | ConvertTo-Json -Depth 16
    if (-not $passed) {
        throw (
            "Runtime machine lifecycle smoke failed. See " +
            $runDirectory
        )
    }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and
        $null -ne $process -and
        -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force `
            -ErrorAction SilentlyContinue
    }
}
