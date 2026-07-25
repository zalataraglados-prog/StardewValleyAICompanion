param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-storage-placement-smoke-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    ),
    [string] $OutputDirectory =
        "artifacts\runtime-storage-placement-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 60,
    [int] $TargetTileY = 15,
    [string] $QualifiedItemId = "(BC)130"
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
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Health {
    param([string] $Url, [int] $TimeoutSeconds)
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

function Wait-StorageSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url
            $storage = $snapshot.state.player.storage_placement
            $chests = $snapshot.state.current_location.chests
            $ready =
                $snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.player.location_id.status -in @(
                    "available",
                    "derived"
                ) -and
                $storage.status -in @("available", "derived") -and
                $storage.value.projection_status -eq
                    "complete_inventory_player_chests_across_persistent_player_locations" -and
                $chests.status -in @("available", "derived")
            $lastStatus =
                "save=$($snapshot.save_id.status)" +
                ";location=$($snapshot.state.player.location_id.status)" +
                ";storage=$($storage.status)" +
                ";projection=$($storage.value.projection_status)" +
                ";chests=$($chests.status)"
            if ($ready) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for storage snapshot. Last status: $lastStatus"
}

function Read-SetupInt {
    param($Result, [string] $Name)
    $texts = @($Result.primitive_verification_reasons)
    $texts += [string]$Result.observed_effect
    foreach ($text in $texts) {
        if ([string]$text -match ($Name + "=(-?\d+)")) {
            return [int]$Matches[1]
        }
    }
    return -1
}

function Find-PlacementRow {
    param($Snapshot, [int] $SlotIndex, [string] $QualifiedId)
    foreach ($row in @(
        $Snapshot.state.player.storage_placement.value.rows
    )) {
        if ([int]$row.inventory_slot_index -eq $SlotIndex -and
            [string]$row.qualified_item_id -eq $QualifiedId) {
            return $row
        }
    }
    return $null
}

function Test-LegalTile {
    param($Row, [string] $LocationId, [int] $X, [int] $Y)
    foreach ($location in @($Row.locations)) {
        if ([string]$location.location_id -ne $LocationId) {
            continue
        }
        foreach ($range in @($location.static_legal_tile_ranges)) {
            if ([int]$range.y -eq $Y -and
                $X -ge [int]$range.start_x -and
                $X -le [int]$range.end_x) {
                return $true
            }
        }
    }
    return $false
}

function Read-StorageRole {
    param($Row)
    if ($Row.shipping_storage) { return "shipping" }
    if ($Row.fridge_storage) { return "fridge" }
    if ($Row.shared_global_storage) { return "shared_global" }
    if ($Row.ordinary_material_storage) {
        return "ordinary_material"
    }
    return "special_storage"
}

function Find-PlacedStorage {
    param($Snapshot, [string] $LocationId, [int] $X, [int] $Y)
    foreach ($row in @(
        $Snapshot.state.current_location.chests.value.access_points
    )) {
        if ([string]$row.location_id -eq $LocationId -and
            [int]$row.tile_x -eq $X -and
            [int]$row.tile_y -eq $Y) {
            return $row
        }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=training_machine"
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
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH =
        $env:STARDEWAI_SAVE_ISOLATION_PATH
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
    $health = Wait-Health `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $initial = Wait-StorageSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $setup = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-storage-placement-smoke"
        queue_item_id = "runtime-storage-placement-smoke.setup"
        before_state_hash = $initial.state_hash
        option_id = "debug.setup_storage_placement_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        qualified_item_id = $QualifiedItemId
    }
    $setupResult = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $setup
    Start-Sleep -Milliseconds 500
    $before = Wait-StorageSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $slotIndex = Read-SetupInt `
        -Result $setupResult `
        -Name "inventory_slot_index"
    $row = Find-PlacementRow `
        -Snapshot $before `
        -SlotIndex $slotIndex `
        -QualifiedId $QualifiedItemId
    $locationId = [string]$before.state.player.location_id.value
    $targetLegal = $null -ne $row -and (
        Test-LegalTile `
            -Row $row `
            -LocationId $locationId `
            -X $TargetTileX `
            -Y $TargetTileY
    )
    if ($setupResult.status -ne "applied" -or
        $slotIndex -lt 0 -or
        -not $targetLegal) {
        Write-JsonFile `
            (Join-Path $runDirectory "setup-result.json") `
            $setupResult
        Write-JsonFile `
            (Join-Path $runDirectory "before-snapshot-rejected.json") `
            $before
        throw "Storage placement fixture or legal range unavailable."
    }

    $stackBefore = [int]$row.stack
    $storageRole = Read-StorageRole -Row $row
    $place = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-storage-placement-smoke"
        queue_item_id = "runtime-storage-placement-smoke.place"
        before_state_hash = $before.state_hash
        option_id = "executor.place_storage"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        inventory_slot_index = $slotIndex
        qualified_item_id = $QualifiedItemId
        item_id = [string]$row.item_id
        location_id = $locationId
        native_storage_branch = [string]$row.native_storage_branch
        special_chest_type = [string]$row.special_chest_type
        expected_storage_capacity = [int]$row.actual_capacity
        storage_role = $storageRole
    }
    $placeResult = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $place
    Start-Sleep -Milliseconds 500
    $after = Wait-StorageSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $placed = Find-PlacedStorage `
        -Snapshot $after `
        -LocationId $locationId `
        -X $TargetTileX `
        -Y $TargetTileY
    $afterRow = Find-PlacementRow `
        -Snapshot $after `
        -SlotIndex $slotIndex `
        -QualifiedId $QualifiedItemId
    $stackAfter = if ($null -eq $afterRow) {
        0
    }
    else {
        [int]$afterRow.stack
    }
    $passed =
        $setupResult.status -eq "applied" -and
        $setupResult.primitive_verification_status -eq "verified" -and
        $targetLegal -and
        $placeResult.status -eq "applied" -and
        $placeResult.primitive_verification_status -eq "verified" -and
        $null -ne $placed -and
        [string]$placed.qualified_item_id -eq $QualifiedItemId -and
        $stackAfter -eq ($stackBefore - 1)
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        location_id = $locationId
        target_tile = "$TargetTileX,$TargetTileY"
        qualified_item_id = $QualifiedItemId
        storage_role = $storageRole
        inventory_slot_index = $slotIndex
        transparent_target_legal = $targetLegal
        stack_before = $stackBefore
        stack_after = $stackAfter
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        place_status = $placeResult.status
        place_verification = $placeResult.primitive_verification_status
        place_reasons = @($placeResult.primitive_verification_reasons)
        place_block_reasons = @($placeResult.block_reasons)
        placed_storage_present = $null -ne $placed
        placed_storage_qualified_item_id = if ($null -ne $placed) {
            [string]$placed.qualified_item_id
        }
        else {
            ""
        }
        state_hash_before = $before.state_hash
        state_hash_after = $after.state_hash
        state_hash_changed =
            $before.state_hash -ne $after.state_hash
        executor_health = $health
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initial
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $before
    Write-JsonFile (Join-Path $runDirectory "place-result.json") $placeResult
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $after
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if (-not $passed) {
        throw "Runtime storage placement smoke failed. See $runDirectory"
    }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
