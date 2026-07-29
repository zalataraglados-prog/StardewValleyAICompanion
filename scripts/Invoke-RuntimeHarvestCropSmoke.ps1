param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-harvest-crop-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-harvest-crop-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [string] $SeedId = "472",
    [string] $HarvestMethod = "Grab",
    [ValidateSet("none", "ordinary_quest", "special_order")]
    [string] $TaskFamily = "none",
    [string] $TaskId = "stardewai.runtime.crop-source",
    [switch] $FillInventoryBeforeHarvest,
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
                $snapshot.state.PSObject.Properties.Name -contains "farm" -and
                $snapshot.state.farm.PSObject.Properties.Name -contains "crops") {
                $farmReadable = $snapshot.state.farm.crops.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);farm_crops_readable=$farmReadable"
            if ($saveReadable -and $farmReadable) { return $snapshot }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready farm crop snapshot. Last status: $lastStatus"
}

function Find-Crop {
    param($Snapshot, [int] $X, [int] $Y)
    if ($null -eq $Snapshot.state.farm.crops.value) { return $null }
    foreach ($crop in @($Snapshot.state.farm.crops.value)) {
        if ([int]$crop.tile_x -eq $X -and [int]$crop.tile_y -eq $Y) { return $crop }
    }
    return $null
}

function Count-DebrisForItem {
    param($Snapshot, [string] $QualifiedItemId)
    if ([string]::IsNullOrWhiteSpace($QualifiedItemId) -or $null -eq $Snapshot.state.farm.debris.value) { return 0 }
    $count = 0
    foreach ($debris in @($Snapshot.state.farm.debris.value)) {
        if ($debris.item_id -eq $QualifiedItemId -or
            ($null -ne $debris.item -and $debris.item.qualified_item_id -eq $QualifiedItemId)) {
            $count += 1
        }
    }
    return $count
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
            return [pscustomobject]@{
                snapshot = $snapshot
                debris = $null
            }
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
            if ($velocitySettled -and
                $signature -eq $previousSignature) {
                return [pscustomobject]@{
                    snapshot = $snapshot
                    debris = $debris
                }
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
    throw "Matching debris did not settle before pickup. Last status: $lastStatus"
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
        queue_id = "runtime-harvest-crop-smoke"
        queue_item_id = "runtime-harvest-crop-smoke.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_harvest_crop_target"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        seed_id = $SeedId
        debug_fill_inventory = [bool]$FillInventoryBeforeHarvest
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120
    $beforeHarvestSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $beforeCrop = Find-Crop -Snapshot $beforeHarvestSnapshot -X $TargetTileX -Y $TargetTileY
    if ($null -eq $beforeCrop -or $beforeCrop.ready_for_harvest -ne $true) {
        Write-JsonFile (Join-Path $runDirectory "snapshot-before-harvest-rejected.json") $beforeHarvestSnapshot
        throw "Fixture did not produce ready_for_harvest crop at $TargetTileX,$TargetTileY."
    }
    $harvestItemId = if ($null -eq $beforeCrop.harvest_item_id) {
        ""
    } else {
        [string]$beforeCrop.harvest_item_id
    }
    $qualifiedHarvestItemId = if ([string]::IsNullOrWhiteSpace($harvestItemId)) {
        ""
    } elseif ($harvestItemId.StartsWith("(O)")) {
        $harvestItemId
    } else {
        "(O)$harvestItemId"
    }
    $taskSetupResult = $null
    if ($TaskFamily -ne "none") {
        $taskSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-harvest-crop-smoke"
            queue_item_id = "runtime-harvest-crop-smoke.task-setup"
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
            qualified_item_id = $qualifiedHarvestItemId
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
        $beforeCrop = Find-Crop -Snapshot $beforeHarvestSnapshot -X $TargetTileX -Y $TargetTileY
    }

    $harvestRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-harvest-crop-smoke"
        queue_item_id = "runtime-harvest-crop-smoke.harvest"
        before_state_hash = $beforeHarvestSnapshot.state_hash
        option_id = "executor.harvest_crop"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        harvest_method = $HarvestMethod
    }
    Add-CollectionTaskFields -Request $harvestRequest -SourceStep $true
    $harvestResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $harvestRequest -TimeoutSeconds 120
    Start-Sleep -Milliseconds 500
    $afterSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $afterCrop = Find-Crop -Snapshot $afterSnapshot -X $TargetTileX -Y $TargetTileY
    $beforeHarvestDebrisCount = Count-DebrisForItem -Snapshot $beforeHarvestSnapshot -QualifiedItemId $qualifiedHarvestItemId
    $afterHarvestDebrisCount = Count-DebrisForItem -Snapshot $afterSnapshot -QualifiedItemId $qualifiedHarvestItemId
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
            -QualifiedItemId $qualifiedHarvestItemId
        $afterSnapshot = $settled.snapshot
        $debris = $settled.debris
        $taskProgressAfterSource = Read-CollectionTaskProgress -Snapshot $afterSnapshot
        if ($taskProgressAfterSource -gt 0) {
            $afterReceiptSnapshot = $afterSnapshot
        }
    }
    if ($TaskFamily -ne "none" -and $taskProgressAfterSource -eq 0) {
        if ($null -eq $debris) {
            throw "Task source created no matching transparent debris."
        }
        $chunk = @($debris.chunks)[0]
        $pickupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-harvest-crop-smoke"
            queue_item_id = "runtime-harvest-crop-smoke.pickup"
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
            qualified_item_id = $qualifiedHarvestItemId
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

    $expectedInventoryBlock = [bool]$FillInventoryBeforeHarvest -and $HarvestMethod -eq "Grab"
    $harvestPassed = if ($expectedInventoryBlock) {
        $harvestResult.status -eq "blocked" -and @($harvestResult.block_reasons) -contains "harvest_crop_inventory_cannot_accept_grab_yield"
    } else {
        $harvestResult.status -eq "applied" -and $harvestResult.primitive_verification_status -eq "verified"
    }

    $summary = [ordered]@{
        status = if (
            $setupResult.status -eq "applied" -and
            $setupResult.primitive_verification_status -eq "verified" -and
            $harvestPassed -and
            ($TaskFamily -eq "none" -or $taskProgressAfterReceipt -ge 1)
        ) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        target_tile = "$TargetTileX,$TargetTileY"
        seed_id = $SeedId
        harvest_method = $HarvestMethod
        harvest_item_id = $qualifiedHarvestItemId
        task_family = $TaskFamily
        task_setup_status = if ($null -eq $taskSetupResult) { "not_requested" } else { $taskSetupResult.status }
        task_progress_after_source = $taskProgressAfterSource
        task_progress_after_receipt = $taskProgressAfterReceipt
        pickup_status = if ($null -eq $pickupResult) { "not_required" } else { $pickupResult.status }
        fill_inventory_before_harvest = [bool]$FillInventoryBeforeHarvest
        expected_inventory_block = $expectedInventoryBlock
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        ready_for_harvest_before = [bool]$beforeCrop.ready_for_harvest
        harvest_status = $harvestResult.status
        harvest_verification = $harvestResult.primitive_verification_status
        harvest_reasons = @($harvestResult.primitive_verification_reasons)
        harvest_block_reasons = @($harvestResult.block_reasons)
        crop_present_after = $null -ne $afterCrop
        ready_for_harvest_after = if ($null -eq $afterCrop) { $false } else { [bool]$afterCrop.ready_for_harvest }
        harvest_debris_count_before = $beforeHarvestDebrisCount
        harvest_debris_count_after = $afterHarvestDebrisCount
        harvest_debris_count_increased = $afterHarvestDebrisCount -gt $beforeHarvestDebrisCount
        bridge_state_hash_before = $beforeHarvestSnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        state_hash_changed = $beforeHarvestSnapshot.state_hash -ne $afterSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "initial-snapshot.json") $initialSnapshot
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "snapshot-before-harvest.json") $beforeHarvestSnapshot
    Write-JsonFile (Join-Path $runDirectory "harvest-request.json") $harvestRequest
    Write-JsonFile (Join-Path $runDirectory "harvest-result.json") $harvestResult
    Write-JsonFile (Join-Path $runDirectory "snapshot-after-harvest.json") $afterSnapshot
    if ($null -ne $taskSetupResult) {
        Write-JsonFile (Join-Path $runDirectory "task-setup-result.json") $taskSetupResult
    }
    if ($null -ne $pickupResult) {
        Write-JsonFile (Join-Path $runDirectory "pickup-result.json") $pickupResult
        Write-JsonFile (Join-Path $runDirectory "snapshot-after-receipt.json") $afterReceiptSnapshot
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 64
}
finally {
    if ($null -ne $process -and -not $process.HasExited -and -not $KeepGameRunning) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
}
