param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $AnimalHouseLocationId = "",
    [int] $TargetTileX = 5,
    [int] $TargetTileY = 5,
    [string] $EggQualifiedItemId = "(O)176",
    [string] $AnimalName = "",
    [string] $RunId = ("runtime-incubator-hatch-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-incubator-hatch-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) `
        -TimeoutSec $TimeoutSeconds
}

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    if ($null -eq $Snapshot.state) { return $null }
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") { return $response }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 15
            $locationId = Read-FieldValue $snapshot "player" "location_id"
            $buildings = Read-FieldValue $snapshot "farm" "buildings"
            $lastStatus = "location=$locationId;buildings=$(@($buildings).Count)"
            if (-not [string]::IsNullOrWhiteSpace([string]$locationId)) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world snapshot. Last status: $lastStatus"
}

function Find-MachineAtTile {
    param($Snapshot, [string] $LocationId, [int] $X, [int] $Y)
    $machines = Read-FieldValue $Snapshot "farm" "machines"
    foreach ($machine in @($machines)) {
        if ([string]$machine.location_id -eq $LocationId -and
            [int]$machine.tile_x -eq $X -and
            [int]$machine.tile_y -eq $Y) {
            return $machine
        }
    }
    return $null
}

function Find-AnimalHouseInIsolatedSave {
    param([string] $SavesPath, [string] $Slot)
    $saveFile = Join-Path (Join-Path $SavesPath $Slot) $Slot
    if (-not (Test-Path -LiteralPath $saveFile -PathType Leaf)) {
        return $null
    }

    [xml]$saveXml = Get-Content -LiteralPath $saveFile -Raw
    foreach ($typeNode in $saveXml.SelectNodes(
            "//*[local-name()='buildingType']")) {
        if ([string]$typeNode.InnerText -notmatch "Coop|Barn") {
            continue
        }
        $uniqueNameNode = $typeNode.ParentNode.SelectSingleNode(
            "./indoors/uniqueName")
        if ($null -ne $uniqueNameNode -and
            -not [string]::IsNullOrWhiteSpace(
                [string]$uniqueNameNode.InnerText)) {
            return [string]$uniqueNameNode.InnerText
        }
    }
    return $null
}

function Wait-NamingSnapshot {
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
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 15
            $activeMenu = Read-FieldValue $snapshot "menus" "active_menu"
            $menuState = Read-FieldValue $snapshot "menus" "menu_specific_state"
            $machine = Find-MachineAtTile $snapshot $LocationId $X $Y
            $special = $machine.machine_special_state
            $lastStatus =
                "menu=$($activeMenu.type);kind=$($menuState.kind);" +
                "machine=$($null -ne $machine);special=$($special.status)"
            if ([string]$activeMenu.type -eq "NamingMenu" -and
                [string]$menuState.kind -eq "naming" -and
                [bool]$menuState.done_callback_present -and
                [bool]$menuState.done_button_present -and
                $null -ne $machine -and
                [string]$special.status -eq "ready_requires_native_naming_event" -and
                [bool]$special.native_ready_selected) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for native incubator NamingMenu. Last status: $lastStatus"
}

function Wait-HatchSnapshot {
    param(
        [string] $Url,
        [string] $LocationId,
        [int] $X,
        [int] $Y,
        [int] $ExpectedOccupants,
        [string] $ExpectedName,
        [string] $ExpectedType,
        [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 15
            $activeMenu = Read-FieldValue $snapshot "menus" "active_menu"
            $machine = Find-MachineAtTile $snapshot $LocationId $X $Y
            $special = $machine.machine_special_state
            $animals = @(Read-FieldValue $snapshot "farm" "animals")
            $created = @($animals | Where-Object {
                [string]$_.location_id -eq $LocationId -and
                [string]$_.name -eq $ExpectedName -and
                [string]$_.type -eq $ExpectedType
            })
            $lastStatus =
                "menu=$($activeMenu.type);occupants=$($special.animal_house_occupant_count);" +
                "egg=$($special.held_egg_qualified_item_id);created=$($created.Count)"
            if (($null -eq $activeMenu -or
                    $activeMenu.is_open -eq $false -or
                    [string]$activeMenu.type -eq "none") -and
                [int]$special.animal_house_occupant_count -eq $ExpectedOccupants -and
                [string]::IsNullOrWhiteSpace(
                    [string]$special.held_egg_qualified_item_id) -and
                $created.Count -eq 1) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for native hatch post-state. Last status: $lastStatus"
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotBaseUrl = "http://127.0.0.1:8765/api/v1/snapshot"
$snapshotUrl = $snapshotBaseUrl + "?profile=full"
$machineSnapshotUrl = $snapshotBaseUrl + "?profile=machine"
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
    Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
    -GamePath $runtimeGameDir | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
    -GamePath $runtimeGameDir | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
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
    $health = Wait-JsonHealth `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    $initial = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds
    Write-JsonFile (
        Join-Path $runDirectory "initial-snapshot.json") $initial
    $buildings = @(Read-FieldValue $initial "farm" "buildings")
    $animalHouseLocationSource = "explicit_parameter"
    if ([string]::IsNullOrWhiteSpace($AnimalHouseLocationId)) {
        $animalHouse = $buildings |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace(
                    [string]$_.indoor_location_id) -and
                ([string]$_.type -match "Coop|Barn" -or
                    [string]$_.runtime_type -match "Coop|Barn")
            } |
            Sort-Object @{
                Expression = {
                    if ([string]$_.type -match "Coop" -or
                        [string]$_.runtime_type -match "Coop") {
                        0
                    }
                    else {
                        1
                    }
                }
            } |
            Select-Object -First 1
        if ($null -ne $animalHouse) {
            $AnimalHouseLocationId =
                [string]$animalHouse.indoor_location_id
            $animalHouseLocationSource =
                "transparent_farm_buildings"
        }
        else {
            $AnimalHouseLocationId =
                Find-AnimalHouseInIsolatedSave `
                    -SavesPath $savesPath `
                    -Slot $SaveSlot
            $animalHouseLocationSource =
                "isolated_save_xml_fallback"
        }
        if ([string]::IsNullOrWhiteSpace($AnimalHouseLocationId)) {
            throw "No Coop/Barn AnimalHouse was exposed or present in the isolated save."
        }
    }

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-incubator-hatch-smoke"
        queue_item_id = "runtime-incubator-hatch-smoke.setup"
        before_state_hash = $initial.state_hash
        option_id = "debug.setup_incubator_hatch_naming"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        location_id = $AnimalHouseLocationId
        animal_house_location_source =
            $animalHouseLocationSource
        transparent_building_count = $buildings.Count
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        qualified_item_id = $EggQualifiedItemId
    }
    $setupResult = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    if ($setupResult.status -ne "applied" -or
        $setupResult.primitive_verification_status -ne "verified") {
        throw "Incubator hatch fixture setup failed."
    }

    $before = Wait-NamingSnapshot `
        -Url $machineSnapshotUrl `
        -LocationId $AnimalHouseLocationId `
        -X $TargetTileX -Y $TargetTileY `
        -TimeoutSeconds 30
    $beforeMachine = Find-MachineAtTile `
        $before $AnimalHouseLocationId $TargetTileX $TargetTileY
    $beforeSpecial = $beforeMachine.machine_special_state
    $beforeOccupants = [int]$beforeSpecial.animal_house_occupant_count
    $animalType = [string]$beforeSpecial.hatch_animal_type_id
    $suggestedName = [string]$beforeSpecial.suggested_hatch_name
    if ([string]::IsNullOrWhiteSpace($AnimalName)) {
        $AnimalName = $suggestedName
    }
    if ([string]::IsNullOrWhiteSpace($animalType)) {
        throw "Transparent incubator state did not expose hatch_animal_type_id."
    }

    $nameRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-incubator-hatch-smoke"
        queue_item_id = "runtime-incubator-hatch-smoke.name"
        before_state_hash = $before.state_hash
        option_id = "executor.name_hatched_animal"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        location_id = $AnimalHouseLocationId
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        target_name = $AnimalName
        target_runtime_type = $animalType
    }
    $nameResult = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $nameRequest
    Write-JsonFile (Join-Path $runDirectory "name-result.json") $nameResult
    if ($nameResult.status -ne "applied" -or
        $nameResult.primitive_verification_status -ne "verified") {
        throw "Native incubator naming executor failed."
    }

    $after = Wait-HatchSnapshot `
        -Url $snapshotUrl `
        -LocationId $AnimalHouseLocationId `
        -X $TargetTileX -Y $TargetTileY `
        -ExpectedOccupants ($beforeOccupants + 1) `
        -ExpectedName $AnimalName `
        -ExpectedType $animalType `
        -TimeoutSeconds 30
    $afterMachine = Find-MachineAtTile `
        $after $AnimalHouseLocationId $TargetTileX $TargetTileY
    $afterSpecial = $afterMachine.machine_special_state
    $beforeMenuState = Read-FieldValue `
        $before "menus" "menu_specific_state"
    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        location_id = $AnimalHouseLocationId
        target_tile = "$TargetTileX,$TargetTileY"
        egg_qualified_item_id = $EggQualifiedItemId
        animal_name = $AnimalName
        animal_type = $animalType
        suggested_name = $suggestedName
        occupants_before = $beforeOccupants
        occupants_after =
            [int]$afterSpecial.animal_house_occupant_count
        native_ready_selection_contract =
            [string]$beforeSpecial.native_ready_selection_contract
        menu_submit_contract =
            [string]$beforeMenuState.native_submit_contract
        setup_status = $setupResult.status
        setup_verification =
            $setupResult.primitive_verification_status
        naming_status = $nameResult.status
        naming_verification =
            $nameResult.primitive_verification_status
        naming_reasons =
            @($nameResult.primitive_verification_reasons)
        egg_cleared =
            [string]::IsNullOrWhiteSpace(
                [string]$afterSpecial.held_egg_qualified_item_id)
        executor_health = $health
        smapi_process_id = $process.Id
    }
    Write-JsonFile (Join-Path $runDirectory "before-naming-snapshot.json") $before
    Write-JsonFile (Join-Path $runDirectory "after-hatch-snapshot.json") $after
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and
        $null -ne $process -and
        -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
