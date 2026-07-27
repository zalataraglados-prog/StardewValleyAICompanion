param(
    [string] $ProjectRoot = "I:\StardewValleyAICompanion",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $EggQualifiedItemId = "(O)176",
    [string] $MachineQualifiedItemId = "(BC)101",
    [string] $AnimalHouseTypePattern = "Coop",
    [int] $TargetTileX = 5,
    [int] $TargetTileY = 5,
    [string] $AnimalName = "NaturalPip",
    [string] $RunId = (
        "runtime-incubator-natural-cycle-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-incubator-natural-cycle",
    [int] $StartupTimeoutSeconds = 120,
    [int] $MaximumSleepCount = 14,
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
        -Headers @{ Accept = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 180)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) `
        -TimeoutSec $TimeoutSeconds
}

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $node = $Snapshot.state.$Domain.$Field
    if ($null -eq $node) { return $null }
    return $node.value
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-JsonGet $Url 3
            if ($value.status -eq "ok") { return $value }
        }
        catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $last"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-JsonGet $Url 15
            $location = Read-FieldValue $value "player" "location_id"
            if (-not [string]::IsNullOrWhiteSpace(
                    [string]$location)) {
                return $value
            }
            $last = "location_missing"
        }
        catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world snapshot. Last status: $last"
}

function Wait-PlayerLocation {
    param(
        [string] $Url,
        [string] $ExpectedLocationId,
        [int] $TimeoutSeconds = 30
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 15
            $last = [string](
                Read-FieldValue $snapshot "player" "location_id")
            if ($last -eq $ExpectedLocationId) {
                return $snapshot
            }
        }
        catch { $last = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for player location " +
        "$ExpectedLocationId. Last location: $last"
}

function Find-Machine {
    param($Snapshot, [string] $LocationId, [int] $X, [int] $Y)
    foreach ($machine in @(
            Read-FieldValue $Snapshot "farm" "machines")) {
        if ([string]$machine.location_id -eq $LocationId -and
            [int]$machine.tile_x -eq $X -and
            [int]$machine.tile_y -eq $Y) {
            return $machine
        }
    }
    return $null
}

function Wait-Machine {
    param(
        [string] $Url,
        [string] $LocationId,
        [int] $X,
        [int] $Y,
        [scriptblock] $Predicate,
        [int] $TimeoutSeconds = 30
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 15
            $machine = Find-Machine $snapshot $LocationId $X $Y
            $last = if ($null -eq $machine) {
                "machine_missing"
            }
            else {
                "minutes=$($machine.minutes_until_ready);" +
                "ready=$($machine.ready_for_harvest);" +
                "egg=$($machine.machine_special_state.held_egg_qualified_item_id)"
            }
            if ($null -ne $machine -and
                (& $Predicate $machine)) {
                return $snapshot
            }
        }
        catch { $last = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for incubator. Last status: $last"
}

function Wait-NamingMenu {
    param(
        [string] $Url,
        [string] $LocationId,
        [int] $X,
        [int] $Y,
        [int] $TimeoutSeconds = 30
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 15
            $menu = Read-FieldValue $snapshot "menus" "active_menu"
            $specific =
                Read-FieldValue $snapshot "menus" "menu_specific_state"
            $machine = Find-Machine $snapshot $LocationId $X $Y
            $last =
                "menu=$($menu.type);kind=$($specific.kind);" +
                "machine=$($null -ne $machine)"
            if ([string]$menu.type -eq "NamingMenu" -and
                [string]$specific.kind -eq "naming" -and
                [bool]$specific.done_callback_present -and
                [bool]$specific.done_button_present -and
                [bool]$machine.machine_special_state.native_ready_selected) {
                return $snapshot
            }
        }
        catch { $last = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for native NamingMenu. Last status: $last"
}

function Wait-MenuType {
    param(
        [string] $Url,
        [string] $ExpectedType,
        [int] $TimeoutSeconds = 30
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 15
            $menu = Read-FieldValue $snapshot "menus" "active_menu"
            $last = [string]$menu.type
            if ($last -eq $ExpectedType) {
                return $snapshot
            }
        }
        catch { $last = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for menu $ExpectedType. Last menu: $last"
}

function New-ExecutionRequest {
    param(
        [string] $OptionId,
        [string] $QueueItemId,
        [string] $StateHash,
        [string] $SavesPath,
        [hashtable] $Additional = @{}
    )
    $body = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-incubator-natural-cycle"
        queue_item_id = $QueueItemId
        before_state_hash = $StateHash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
    foreach ($entry in $Additional.GetEnumerator()) {
        $body[$entry.Key] = $entry.Value
    }
    return $body
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$sourceSavesPath = Join-Path $RuntimeRoot "saves"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $sourceSavesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated source save found under $sourceSavesPath"
    }
    $SaveSlot = $slot.Name
}

$sourceSlotPath = Join-Path $sourceSavesPath $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSlotPath -PathType Container)) {
    throw "Source save slot not found: $sourceSlotPath"
}
$runDirectory = Join-Path $ProjectRoot (
    Join-Path $OutputDirectory $RunId)
$runSavesPath = Join-Path $runDirectory "isolated-saves"
$trainingOutputPath = Join-Path $runDirectory "training-output"
New-Item -ItemType Directory -Force -Path $runSavesPath | Out-Null
New-Item -ItemType Directory -Force `
    -Path $trainingOutputPath | Out-Null
Copy-Item -LiteralPath $sourceSlotPath `
    -Destination $runSavesPath -Recurse

& (Join-Path $ProjectRoot `
    "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
    -GamePath $runtimeGameDir | Out-Null
& (Join-Path $ProjectRoot `
    "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
    -GamePath $runtimeGameDir | Out-Null

$snapshotBase = "http://127.0.0.1:8765/api/v1/snapshot"
$fullSnapshotUrl = $snapshotBase + "?profile=full"
$machineSnapshotUrl = $snapshotBase + "?profile=machine"
$executeUrl =
    "http://127.0.0.1:8767/api/v1/training/execute"
$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH =
        $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_TRAINING_OUTPUT_DIR =
        $env:STARDEWAI_TRAINING_OUTPUT_DIR
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $runSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $runSavesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR =
        $trainingOutputPath
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden -PassThru
    $health = Wait-JsonHealth `
        "http://127.0.0.1:8767/health" 30
    $initial = Wait-WorldSnapshot `
        $fullSnapshotUrl $StartupTimeoutSeconds
    $homeLocationId = [string](
        Read-FieldValue $initial "player" "location_id")
    Write-JsonFile (
        Join-Path $runDirectory "initial-snapshot.json") $initial

    $building = @(
        Read-FieldValue $initial "farm" "buildings") |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace(
                [string]$_.indoor_location_id) -and
            ([string]$_.type -match $AnimalHouseTypePattern -or
                [string]$_.runtime_type -match
                    $AnimalHouseTypePattern)
        } |
        Select-Object -First 1
    if ($null -eq $building) {
        throw "No matching AnimalHouse exposed by transparent bridge."
    }
    $houseId = [string]$building.indoor_location_id
    $targetFields = @{
        location_id = $houseId
        animal_house_type_pattern = $AnimalHouseTypePattern
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }

    $setupFields = $targetFields.Clone()
    $setupFields.qualified_item_id = $EggQualifiedItemId
    $setupFields.expected_shop_id = $MachineQualifiedItemId
    $setupFields.quantity = 1
    $setup = Invoke-JsonPost $executeUrl (
        New-ExecutionRequest `
            "debug.setup_machine_input_target" `
            "$RunId.setup" $initial.state_hash `
            $runSavesPath $setupFields)
    Write-JsonFile (
        Join-Path $runDirectory "setup-result.json") $setup
    if ($setup.status -ne "applied" -or
        $setup.primitive_verification_status -ne "verified") {
        throw "Incubator input fixture setup failed."
    }

    $idleSnapshot = Wait-Machine `
        $machineSnapshotUrl $houseId `
        $TargetTileX $TargetTileY {
            param($machine)
            [int]$machine.machine_special_state.compatible_egg_count -gt 0
        }
    $idleMachine = Find-Machine `
        $idleSnapshot $houseId $TargetTileX $TargetTileY
    Write-JsonFile (
        Join-Path $runDirectory "idle-catalog-snapshot.json") `
        $idleSnapshot
    if ([string]$idleMachine.machine_special_state.
            compatible_egg_catalog_status -ne
        "available_complete_native_data_and_rule_probe") {
        throw "Incubator compatibility catalog is incomplete."
    }
    $inputRow = @($idleMachine.loadable_inputs |
        Where-Object {
            [string]$_.qualified_item_id -eq
                $EggQualifiedItemId
        }) | Select-Object -First 1
    if ($null -eq $inputRow) {
        throw "Transparent bridge did not expose the requested egg input slot."
    }

    $loadFields = $targetFields.Clone()
    $loadFields.qualified_item_id = $EggQualifiedItemId
    $loadFields.input_slot_index = [int]$inputRow.slot_index
    $load = Invoke-JsonPost $executeUrl (
        New-ExecutionRequest `
            "executor.load_machine_input" `
            "$RunId.load" $idleSnapshot.state_hash `
            $runSavesPath $loadFields)
    Write-JsonFile (
        Join-Path $runDirectory "load-result.json") $load
    if ($load.status -ne "applied" -or
        $load.primitive_verification_status -ne "verified") {
        throw "Native incubator load failed."
    }

    $loaded = Wait-Machine `
        $machineSnapshotUrl $houseId `
        $TargetTileX $TargetTileY {
            param($machine)
            -not [string]::IsNullOrWhiteSpace(
                [string]$machine.machine_special_state.
                    held_egg_qualified_item_id) -and
            [int]$machine.minutes_until_ready -gt 0
        }
    $loadedMachine = Find-Machine `
        $loaded $houseId $TargetTileX $TargetTileY
    $initialMinutes = [int]$loadedMachine.minutes_until_ready
    $expectedAnimalType =
        [string]$loadedMachine.machine_special_state.
            hatch_animal_type_id
    Write-JsonFile (
        Join-Path $runDirectory "loaded-snapshot.json") $loaded

    $sleepEvidence = @()
    $current = $loaded
    for ($day = 1; $day -le $MaximumSleepCount; $day++) {
        $beforeMachine = Find-Machine `
            $current $houseId $TargetTileX $TargetTileY
        $minutesBefore = [int]$beforeMachine.minutes_until_ready
        if ($minutesBefore -le 0) { break }

        $prepare = Invoke-JsonPost $executeUrl (
            New-ExecutionRequest `
                "debug.prepare_incubator_sleep" `
                "$RunId.prepare.$day" $current.state_hash `
                $runSavesPath $targetFields)
        Write-JsonFile (
            Join-Path $runDirectory `
                "prepare-sleep-$day-result.json") $prepare
        if ($prepare.status -ne "applied") {
            throw "Prepare sleep failed on incubation day $day."
        }

        $homeSnapshot = Wait-PlayerLocation `
            $machineSnapshotUrl $homeLocationId 30
        $sleep = Invoke-JsonPost $executeUrl (
            New-ExecutionRequest `
                "executor.sleep" `
                "$RunId.sleep.$day" $homeSnapshot.state_hash `
                $runSavesPath)
        Write-JsonFile (
            Join-Path $runDirectory `
                "sleep-$day-result.json") $sleep
        if ($sleep.status -ne "applied" -or
            $sleep.primitive_verification_status -ne "verified") {
            throw "Native sleep failed on incubation day $day."
        }

        $current = Wait-Machine `
            $machineSnapshotUrl $houseId `
            $TargetTileX $TargetTileY {
                param($machine)
                [int]$machine.minutes_until_ready -lt $minutesBefore
            } 60
        $afterMachine = Find-Machine `
            $current $houseId $TargetTileX $TargetTileY
        $minutesAfter = [int]$afterMachine.minutes_until_ready
        $sleepEvidence += [ordered]@{
            ordinal = $day
            minutes_before = $minutesBefore
            minutes_after = $minutesAfter
            native_minutes_elapsed =
                $minutesBefore - $minutesAfter
            sleep_status = $sleep.status
            sleep_verification =
                $sleep.primitive_verification_status
        }
        Write-JsonFile (
            Join-Path $runDirectory `
                "post-sleep-$day-snapshot.json") $current
    }

    $readyMachine = Find-Machine `
        $current $houseId $TargetTileX $TargetTileY
    if ([int]$readyMachine.minutes_until_ready -gt 0) {
        throw "Incubator did not become ready after $MaximumSleepCount sleeps."
    }
    if ($sleepEvidence.Count -lt 2) {
        throw "Natural lifecycle did not traverse multiple native days."
    }

    $enter = Invoke-JsonPost $executeUrl (
        New-ExecutionRequest `
            "debug.enter_ready_incubator_house" `
            "$RunId.enter" $current.state_hash `
            $runSavesPath $targetFields)
    Write-JsonFile (
        Join-Path $runDirectory "enter-house-result.json") $enter
    if ($enter.status -ne "applied") {
        throw "Native animal-house entry failed."
    }

    $birthMessage = Wait-MenuType `
        $machineSnapshotUrl "DialogueBox" 30
    $closeFields = @{
        interaction_kind = "incubator_birth_message"
    }
    $closeBirthMessage = Invoke-JsonPost $executeUrl (
        New-ExecutionRequest `
            "executor.close_menu" `
            "$RunId.birth-message" `
            $birthMessage.state_hash `
            $runSavesPath $closeFields)
    Write-JsonFile (
        Join-Path $runDirectory `
            "birth-message-result.json") $closeBirthMessage
    if ($closeBirthMessage.status -ne "applied" -or
        $closeBirthMessage.primitive_verification_status -ne
            "verified") {
        throw "Native incubator birth message advance failed."
    }

    $naming = Wait-NamingMenu `
        $machineSnapshotUrl $houseId `
        $TargetTileX $TargetTileY 30
    $namingMachine = Find-Machine `
        $naming $houseId $TargetTileX $TargetTileY
    $occupantsBefore =
        [int]$namingMachine.machine_special_state.
            animal_house_occupant_count
    $nameFields = $targetFields.Clone()
    $nameFields.target_name = $AnimalName
    $nameFields.target_runtime_type = $expectedAnimalType
    $name = Invoke-JsonPost $executeUrl (
        New-ExecutionRequest `
            "executor.name_hatched_animal" `
            "$RunId.name" $naming.state_hash `
            $runSavesPath $nameFields)
    Write-JsonFile (
        Join-Path $runDirectory "name-result.json") $name
    if ($name.status -ne "applied" -or
        $name.primitive_verification_status -ne "verified") {
        throw "Native hatch naming failed."
    }

    $after = Wait-Machine `
        $fullSnapshotUrl $houseId `
        $TargetTileX $TargetTileY {
            param($machine)
            [string]::IsNullOrWhiteSpace(
                [string]$machine.machine_special_state.
                    held_egg_qualified_item_id)
        } 30
    $afterMachine = Find-Machine `
        $after $houseId $TargetTileX $TargetTileY
    $animals = @(Read-FieldValue $after "farm" "animals")
    $created = @($animals | Where-Object {
        [string]$_.location_id -eq $houseId -and
        [string]$_.name -eq $AnimalName -and
        [string]$_.type -eq $expectedAnimalType
    })
    if ($created.Count -ne 1) {
        throw "Exact naturally hatched animal was not exposed."
    }

    $summary = [ordered]@{
        status = "passed"
        run_id = $RunId
        source_save_path = $sourceSlotPath
        isolated_save_path =
            (Join-Path $runSavesPath $SaveSlot)
        shared_source_save_was_not_runtime_target = $true
        location_id = $houseId
        machine_qualified_item_id = $MachineQualifiedItemId
        egg_qualified_item_id = $EggQualifiedItemId
        animal_type = $expectedAnimalType
        animal_name = $AnimalName
        initial_minutes_until_ready = $initialMinutes
        native_sleep_count = $sleepEvidence.Count
        sleep_evidence = $sleepEvidence
        occupants_before_naming = $occupantsBefore
        occupants_after_naming =
            [int]$afterMachine.machine_special_state.
                animal_house_occupant_count
        egg_cleared = $true
        exact_created_animal_count = $created.Count
        native_load_verification =
            $load.primitive_verification_status
        native_naming_verification =
            $name.primitive_verification_status
        native_birth_message_verification =
            $closeBirthMessage.primitive_verification_status
        compatibility_catalog_status =
            [string]$idleMachine.machine_special_state.
                compatible_egg_catalog_status
        native_egg_candidate_count =
            [int]$idleMachine.machine_special_state.
                native_egg_candidate_count
        compatible_egg_count =
            [int]$idleMachine.machine_special_state.
                compatible_egg_count
        executor_health = $health
        smapi_process_id = $process.Id
    }
    Write-JsonFile (
        Join-Path $runDirectory "after-hatch-snapshot.json") $after
    Write-JsonFile (
        Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 24
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
