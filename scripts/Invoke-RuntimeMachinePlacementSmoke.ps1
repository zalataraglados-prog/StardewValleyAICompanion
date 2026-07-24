param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-machine-placement-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-machine-placement-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 60,
    [int] $TargetTileY = 15,
    [string] $QualifiedItemId = "(BC)12",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
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
        catch { $lastError = $_.Exception.Message }
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 30
            $saveReadable = $snapshot.save_id.status -in @(
                "available",
                "derived"
            )
            $placementReadable = $false
            $placementProjectionStatus = "missing"
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "player" -and
                $snapshot.state.player.PSObject.Properties.Name -contains "machine_placement") {
                $placementProjectionStatus = [string](
                    $snapshot.state.player.machine_placement.value.projection_status
                )
                $placementReadable =
                    $snapshot.state.player.machine_placement.status -in @(
                        "available",
                        "derived"
                    ) -and
                    -not $placementProjectionStatus.StartsWith(
                        "unavailable",
                        [StringComparison]::Ordinal
                    )
            }
            $locationReadable =
                $null -ne $snapshot.state.player.location_id -and
                $snapshot.state.player.location_id.status -in @(
                    "available",
                    "derived"
                )
            $farmReadable = $false
            $farmStatus = "missing"
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "farm" -and
                $snapshot.state.farm.PSObject.Properties.Name -contains "machines") {
                $farmStatus = [string]$snapshot.state.farm.machines.status
                $farmReadable = $farmStatus -in @(
                    "available",
                    "derived",
                    "partial"
                )
            }
            $lastStatus = "save=$saveReadable;location=$locationReadable" +
                ";placement=$placementReadable" +
                ";placement_projection=$placementProjectionStatus" +
                ";machines=$farmReadable;machine_status=$farmStatus"
            if ($saveReadable -and $locationReadable -and $placementReadable) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for machine placement snapshot. Last status: $lastStatus"
}

function Read-SetupInt {
    param($SetupResult, [string] $Name)
    $texts = @($SetupResult.primitive_verification_reasons)
    $texts += [string]$SetupResult.observed_effect
    foreach ($text in $texts) {
        if ([string]$text -match ($Name + "=(-?\d+)")) {
            return [int]$Matches[1]
        }
    }
    return -1
}

function Find-PlacementRow {
    param($Snapshot, [int] $SlotIndex, [string] $ItemId)
    $context = $Snapshot.state.player.machine_placement.value
    foreach ($row in @($context.rows)) {
        if ([int]$row.inventory_slot_index -eq $SlotIndex -and
            [string]$row.qualified_item_id -eq $ItemId) {
            return $row
        }
    }
    return $null
}

function Test-LegalRangeContains {
    param($Row, [string] $LocationId, [int] $X, [int] $Y)
    foreach ($location in @($Row.locations)) {
        if ([string]$location.location_id -ne $LocationId) { continue }
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

function Find-MachineAtTile {
    param($Snapshot, [string] $LocationId, [int] $X, [int] $Y)
    foreach ($machine in @($Snapshot.state.farm.machines.value)) {
        if ([string]$machine.location_id -eq $LocationId -and
            [int]$machine.tile_x -eq $X -and
            [int]$machine.tile_y -eq $Y) {
            return $machine
        }
    }
    return $null
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=machine"
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
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null

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
    $executorHealth = Wait-JsonHealth `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $initialSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-placement-smoke"
        queue_item_id = "runtime-machine-placement-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_machine_placement_target"
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
        -Body $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Start-Sleep -Milliseconds 500
    $beforeSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds 30
    $slotIndex = Read-SetupInt `
        -SetupResult $setupResult `
        -Name "inventory_slot_index"
    $placementRow = Find-PlacementRow `
        -Snapshot $beforeSnapshot `
        -SlotIndex $slotIndex `
        -ItemId $QualifiedItemId
    $locationId = [string]$beforeSnapshot.state.player.location_id.value
    $targetInTransparentRange = $null -ne $placementRow -and (
        Test-LegalRangeContains `
            -Row $placementRow `
            -LocationId $locationId `
            -X $TargetTileX `
            -Y $TargetTileY
    )
    if ($setupResult.status -ne "applied" -or
        $slotIndex -lt 0 -or
        -not $targetInTransparentRange) {
        Write-JsonFile `
            (Join-Path $runDirectory "before-snapshot-rejected.json") `
            $beforeSnapshot
        throw "Machine placement fixture or transparent legal range was unavailable."
    }

    $stackBefore = [int]$placementRow.stack
    $placeRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-machine-placement-smoke"
        queue_item_id = "runtime-machine-placement-smoke.place"
        before_state_hash = $beforeSnapshot.state_hash
        option_id = "executor.place_machine"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        inventory_slot_index = $slotIndex
        qualified_item_id = $QualifiedItemId
        item_id = [string]$placementRow.item_id
        location_id = $locationId
    }
    $placeResult = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $placeRequest
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds 30
    $placedMachine = Find-MachineAtTile `
        -Snapshot $afterSnapshot `
        -LocationId $locationId `
        -X $TargetTileX `
        -Y $TargetTileY
    $afterRow = Find-PlacementRow `
        -Snapshot $afterSnapshot `
        -SlotIndex $slotIndex `
        -ItemId $QualifiedItemId
    $stackAfter = if ($null -ne $afterRow) {
        [int]$afterRow.stack
    }
    else {
        0
    }

    $summary = [ordered]@{
        status = if (
            $setupResult.status -eq "applied" -and
            $setupResult.primitive_verification_status -eq "verified" -and
            $targetInTransparentRange -and
            $placeResult.status -eq "applied" -and
            $placeResult.primitive_verification_status -eq "verified" -and
            $null -ne $placedMachine -and
            [string]$placedMachine.qualified_item_id -eq $QualifiedItemId -and
            $stackAfter -eq ($stackBefore - 1)
        ) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        location_id = $locationId
        target_tile = "$TargetTileX,$TargetTileY"
        qualified_item_id = $QualifiedItemId
        inventory_slot_index = $slotIndex
        transparent_row_present = $null -ne $placementRow
        transparent_target_legal = $targetInTransparentRange
        stack_before = $stackBefore
        stack_after = $stackAfter
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        place_status = $placeResult.status
        place_verification = $placeResult.primitive_verification_status
        place_reasons = @($placeResult.primitive_verification_reasons)
        place_block_reasons = @($placeResult.block_reasons)
        placed_machine_present = $null -ne $placedMachine
        placed_machine_qualified_item_id = if ($null -ne $placedMachine) {
            [string]$placedMachine.qualified_item_id
        } else { "" }
        state_hash_before = $beforeSnapshot.state_hash
        state_hash_after = $afterSnapshot.state_hash
        state_hash_changed =
            $beforeSnapshot.state_hash -ne $afterSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") `
        $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") `
        $beforeSnapshot
    Write-JsonFile (Join-Path $runDirectory "place-result.json") `
        $placeResult
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") `
        $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") {
        throw "Runtime machine placement smoke failed. See $runDirectory"
    }
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
