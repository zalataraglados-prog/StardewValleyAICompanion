param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-giant-crop-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-giant-crop-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $GiantCropId = "276",
    [ValidateSet("none", "ordinary_quest", "special_order")]
    [string] $TaskFamily = "none",
    [string] $TaskId = "stardewai.runtime.giant-crop-source",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    $json = $Body | ConvertTo-Json -Depth 48
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 60)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") { return $response }
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $farmReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "current_location" -and
                $snapshot.state.current_location.PSObject.Properties.Name -contains "resource_clumps") {
                $farmReadable = $snapshot.state.current_location.resource_clumps.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);farm_resource_clumps_readable=$farmReadable"
            if ($saveReadable -and $farmReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready farm resource-clump snapshot. Last status: $lastStatus"
}

function Find-GiantCrop {
    param($Snapshot, [int] $X, [int] $Y)
    if ($null -eq $Snapshot.state.current_location.resource_clumps.value) { return $null }
    foreach ($clump in @($Snapshot.state.current_location.resource_clumps.value)) {
        $cx = [int]$clump.tile_x
        $cy = [int]$clump.tile_y
        $width = [Math]::Max(1, [int]$clump.width)
        $height = [Math]::Max(1, [int]$clump.height)
        if ($clump.is_giant_crop -eq $true -and
            $X -ge $cx -and $X -lt ($cx + $width) -and
            $Y -ge $cy -and $Y -lt ($cy + $height)) {
            return $clump
        }
    }
    return $null
}

function Resolve-GiantCropApproach {
    param($Snapshot, $Clump)
    $standX = [int]$Snapshot.state.player.tile_x.value
    $standY = [int]$Snapshot.state.player.tile_y.value
    $anchorX = [int]$Clump.tile_x
    $anchorY = [int]$Clump.tile_y
    $width = [Math]::Max(1, [int]$Clump.width)
    $height = [Math]::Max(1, [int]$Clump.height)
    foreach ($offset in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
        $hitX = $standX + $offset[0]
        $hitY = $standY + $offset[1]
        if ($hitX -ge $anchorX -and $hitX -lt ($anchorX + $width) -and
            $hitY -ge $anchorY -and $hitY -lt ($anchorY + $height)) {
            return [pscustomobject]@{ stand_x=$standX; stand_y=$standY; hit_x=$hitX; hit_y=$hitY }
        }
    }
    throw "Fixture farmer is not adjacent to the transparent giant crop."
}

function Count-Debris {
    param($Snapshot)
    if ($null -eq $Snapshot.state.current_location.debris.value) { return 0 }
    return @($Snapshot.state.current_location.debris.value).Count
}

function Read-CollectionTaskProgress {
    param($Snapshot)
    if ($TaskFamily -eq "special_order") {
        $order = $Snapshot.state.quests.special_orders.value |
            Where-Object { [string]$_.quest_key -eq $TaskId } |
            Select-Object -First 1
        if ($null -eq $order) {
            if ($TaskId -in @($Snapshot.state.quests.completed_special_orders.value)) {
                return 1
            }
            return $null
        }
        if (@($order.objectives).Count -eq 0) { return $null }
        return [int]@($order.objectives)[0].current_count
    }
    $quest = $Snapshot.state.quests.active_quests.value |
        Where-Object {
            [string]$_.id -eq $TaskId -and
            [string]$_.runtime_type -eq "ResourceCollectionQuest"
        } |
        Select-Object -First 1
    if ($null -eq $quest) { return $null }
    return [int]$quest.per_type_fields.number_collected
}

function Find-MatchingDebris {
    param($Snapshot, [string] $QualifiedItemId)
    return $Snapshot.state.current_location.debris.value |
        Where-Object {
            [string]$_.qualified_item_id -eq $QualifiedItemId -and
            @($_.chunks).Count -gt 0
        } |
        Select-Object -First 1
}

function Wait-StableMatchingDebris {
    param([string] $Url, [string] $QualifiedItemId, [int] $TimeoutSeconds = 12)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $previousSignature = ""
    $lastStatus = "not_observed"
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot -Url $Url -TimeoutSeconds 10
        $progress = Read-CollectionTaskProgress -Snapshot $snapshot
        if ($progress -gt 0) {
            return [pscustomobject]@{ snapshot = $snapshot; debris = $null }
        }
        $debris = Find-MatchingDebris `
            -Snapshot $snapshot `
            -QualifiedItemId $QualifiedItemId
        if ($null -ne $debris) {
            $chunk = @($debris.chunks)[0]
            $signature = "$($debris.debris_index):$($chunk.pixel_x):$($chunk.pixel_y)"
            $velocitySettled =
                [Math]::Abs([double]$chunk.x_velocity) -lt 0.01 -and
                [Math]::Abs([double]$chunk.y_velocity) -lt 0.01
            if ($velocitySettled -and $signature -eq $previousSignature) {
                return [pscustomobject]@{ snapshot = $snapshot; debris = $debris }
            }
            $previousSignature = $signature
            $lastStatus = "signature=$signature;xv=$($chunk.x_velocity);yv=$($chunk.y_velocity)"
        } else {
            $ids = @($snapshot.state.current_location.debris.value |
                ForEach-Object { [string]$_.qualified_item_id }) -join ","
            $lastStatus = "matching_debris=missing;visible_ids=$ids"
        }
        Start-Sleep -Milliseconds 120
    }
    throw "Matching giant-crop debris did not settle. Last status: $lastStatus"
}

function Add-CollectionTaskFields {
    param([System.Collections.IDictionary] $Request, [bool] $SourceStep)
    if ($TaskFamily -eq "none") { return }
    $Request.quest_candidate_id = "runtime_fixture:$TaskId"
    $Request.quest_family = $TaskFamily
    $Request.quest_id = $TaskId
    $Request.quest_key = if ($TaskFamily -eq "special_order") { $TaskId } else { "" }
    $Request.quest_objective_index = if ($TaskFamily -eq "special_order") { 0 } else { $null }
    $Request.quest_runtime_type = if ($TaskFamily -eq "special_order") {
        "SpecialOrder"
    } else {
        "ResourceCollectionQuest"
    }
    $Request.quest_expected_current_count = 0
    $Request.quest_expected_target_count = 1
    $Request.quest_acquisition_source_step = $SourceStep
    $Request.quest_acquisition_target_step = -not $SourceStep
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
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
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-giant-crop-smoke"
        queue_item_id = "runtime-giant-crop-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_giant_crop_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        giant_crop_id = $GiantCropId
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Start-Sleep -Milliseconds 500
    $beforeHarvestSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $beforeClump = Find-GiantCrop -Snapshot $beforeHarvestSnapshot -X $TargetTileX -Y $TargetTileY
    if ($null -eq $beforeClump) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-giant-harvest-rejected.json") $beforeHarvestSnapshot
        throw "Fixture did not produce giant crop resource clump at $TargetTileX,$TargetTileY."
    }
    $approach = Resolve-GiantCropApproach -Snapshot $beforeHarvestSnapshot -Clump $beforeClump
    $guaranteedOutput = @($beforeClump.giant_crop_guaranteed_outputs) |
        Select-Object -First 1
    $qualifiedOutputId = if ($null -eq $guaranteedOutput) {
        ""
    } else {
        [string]$guaranteedOutput.qualified_item_id
    }
    $taskSetupResult = $null
    if ($TaskFamily -ne "none") {
        if ([string]::IsNullOrWhiteSpace($qualifiedOutputId)) {
            throw "Giant crop exposes no guaranteed task output."
        }
        $taskSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-giant-crop-smoke"
            queue_item_id = "runtime-giant-crop-smoke.task-setup"
            before_state_hash = $beforeHarvestSnapshot.state_hash
            option_id = "debug.setup_collection_task_fixture"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            quest_id = $TaskId
            quest_family = $TaskFamily
            quest_expected_target_count = 1
            qualified_item_id = $qualifiedOutputId
        }
        $taskSetupResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $taskSetupRequest `
            -TimeoutSeconds 120
        if ($taskSetupResult.status -ne "applied" -or
            $taskSetupResult.primitive_verification_status -ne "verified") {
            throw "Collection task fixture failed: $(@($taskSetupResult.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 300
        $beforeHarvestSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $beforeClump = Find-GiantCrop `
            -Snapshot $beforeHarvestSnapshot `
            -X $TargetTileX `
            -Y $TargetTileY
    }

    $harvestRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-giant-crop-smoke"
        queue_item_id = "runtime-giant-crop-smoke.harvest"
        before_state_hash = $beforeHarvestSnapshot.state_hash
        option_id = "executor.harvest_giant_crop"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = [int]$approach.hit_x
        target_tile_y = [int]$approach.hit_y
        stand_tile_x = [int]$approach.stand_x
        stand_tile_y = [int]$approach.stand_y
        resource_clump_tile_x = [int]$beforeClump.tile_x
        resource_clump_tile_y = [int]$beforeClump.tile_y
        resource_clump_width = [int]$beforeClump.width
        resource_clump_height = [int]$beforeClump.height
        resource_clump_parent_sheet_index = [int]$beforeClump.parent_sheet_index
        target_runtime_type = [string]$beforeClump.runtime_type
        tool_slot_index = [int]$beforeClump.tool_slot_index
        required_tool_kind = "axe"
        max_crops = [Math]::Max(1, [int]$beforeClump.expected_tool_hits_to_clear)
        max_movement_tiles = 512
        location_id = [string]$beforeHarvestSnapshot.state.player.location_id.value
        giant_crop_id = $GiantCropId
    }
    Add-CollectionTaskFields -Request $harvestRequest -SourceStep $true
    if ($TaskFamily -ne "none") {
        $harvestRequest.qualified_item_id = $qualifiedOutputId
    }
    $harvestResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $harvestRequest -TimeoutSeconds 120
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $afterClump = Find-GiantCrop -Snapshot $afterSnapshot -X $TargetTileX -Y $TargetTileY
    $beforeDebrisCount = Count-Debris -Snapshot $beforeHarvestSnapshot
    $afterDebrisCount = Count-Debris -Snapshot $afterSnapshot
    $taskProgressAfterSource = if ($TaskFamily -eq "none") {
        $null
    } else {
        Read-CollectionTaskProgress -Snapshot $afterSnapshot
    }
    $pickupResult = $null
    $afterReceiptSnapshot = $afterSnapshot
    if ($TaskFamily -ne "none" -and $taskProgressAfterSource -eq 0) {
        $settled = Wait-StableMatchingDebris `
            -Url $snapshotUrl `
            -QualifiedItemId $qualifiedOutputId
        $afterSnapshot = $settled.snapshot
        $debris = $settled.debris
        $taskProgressAfterSource = Read-CollectionTaskProgress -Snapshot $afterSnapshot
        if ($taskProgressAfterSource -gt 0) {
            $afterReceiptSnapshot = $afterSnapshot
        }
    }
    if ($TaskFamily -ne "none" -and $taskProgressAfterSource -eq 0) {
        if ($null -eq $debris) {
            throw "Giant crop created no matching transparent debris."
        }
        $chunk = @($debris.chunks)[0]
        $pickupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-giant-crop-smoke"
            queue_item_id = "runtime-giant-crop-smoke.pickup"
            before_state_hash = $afterSnapshot.state_hash
            option_id = "executor.pickup_debris"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$chunk.tile_x
            target_tile_y = [int]$chunk.tile_y
            debris_index = [int]$debris.debris_index
            qualified_item_id = $qualifiedOutputId
        }
        Add-CollectionTaskFields -Request $pickupRequest -SourceStep $false
        $pickupResult = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $pickupRequest `
            -TimeoutSeconds 120
        if ($pickupResult.status -ne "applied" -or
            $pickupResult.primitive_verification_status -ne "verified") {
            throw "Task debris pickup failed: $(@($pickupResult.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 300
        $afterReceiptSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    }
    $taskProgressAfterReceipt = if ($TaskFamily -eq "none") {
        $null
    } else {
        Read-CollectionTaskProgress -Snapshot $afterReceiptSnapshot
    }

    $summary = [ordered]@{
        status = if (
            $setupResult.status -eq "applied" -and
            $setupResult.primitive_verification_status -eq "verified" -and
            $harvestResult.status -eq "applied" -and
            $harvestResult.primitive_verification_status -eq "verified" -and
            $null -eq $afterClump -and
            ($TaskFamily -eq "none" -or $taskProgressAfterReceipt -ge 1)
        ) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        giant_crop_id = $GiantCropId
        guaranteed_output_qualified_item_id = $qualifiedOutputId
        task_family = $TaskFamily
        task_setup_status = if ($null -eq $taskSetupResult) { "not_requested" } else { $taskSetupResult.status }
        task_progress_after_source = $taskProgressAfterSource
        task_progress_after_receipt = $taskProgressAfterReceipt
        pickup_status = if ($null -eq $pickupResult) { "not_required" } else { $pickupResult.status }
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        giant_crop_present_before = $null -ne $beforeClump
        harvest_status = $harvestResult.status
        harvest_verification = $harvestResult.primitive_verification_status
        harvest_reasons = @($harvestResult.primitive_verification_reasons)
        harvest_block_reasons = @($harvestResult.block_reasons)
        giant_crop_present_after = $null -ne $afterClump
        debris_count_before = $beforeDebrisCount
        debris_count_after = $afterDebrisCount
        debris_count_increased = $afterDebrisCount -gt $beforeDebrisCount
        bridge_state_hash_before = $beforeHarvestSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        state_hash_changed = $beforeHarvestSnapshot.state_hash -ne $afterSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }

    Write-JsonFile (Join-Path $runDirectory "harvest-result.json") $harvestResult
    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "before-harvest-snapshot.json") $beforeHarvestSnapshot
    Write-JsonFile (Join-Path $runDirectory "after-snapshot.json") $afterSnapshot
    if ($null -ne $taskSetupResult) {
        Write-JsonFile (Join-Path $runDirectory "task-setup-result.json") $taskSetupResult
    }
    if ($null -ne $pickupResult) {
        Write-JsonFile (Join-Path $runDirectory "pickup-result.json") $pickupResult
        Write-JsonFile (Join-Path $runDirectory "after-receipt-snapshot.json") $afterReceiptSnapshot
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Runtime giant crop smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
