param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-forage-task-source-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-forage-task-source-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [string] $LocationId = "Forest",
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [ValidateSet("bush", "ginger", "fruit_tree", "wild_tree")]
    [string] $SourceKind = "bush",
    [ValidateSet("none", "ordinary_quest", "special_order")]
    [string] $TaskFamily = "ordinary_quest",
    [string] $TaskId = "stardewai.runtime.forage-source",
    [ValidateSet("berry_standard", "berry_botanist", "tea_leaf", "golden_walnut", "golden_walnut_collected", "berry_cooldown")]
    [string] $BushFixtureProfile = "berry_standard",
    [ValidateSet("dry_standard", "rain_efficient", "dry_insufficient_energy")]
    [string] $GingerFixtureProfile = "dry_standard",
    [ValidateSet("single_normal", "triple_gold", "lightning_coal", "empty", "active_shake")]
    [string] $FruitTreeFixtureProfile = "single_normal",
    [ValidateSet("ordinary_seed", "fall_hazelnut", "island_palm", "no_seed", "active_shake", "tapped")]
    [string] $WildTreeFixtureProfile = "ordinary_seed",
    [switch] $FillInventory,
    [ValidateSet("ready", "blocked_insufficient_energy", "golden_walnut_already_collected", "bush_shake_cooldown_active", "fruit_tree_has_no_fruit", "fruit_tree_shake_in_progress", "blocked_tree_has_no_seed", "blocked_tree_shake_in_progress", "blocked_tree_is_tapped")]
    [string] $ExpectedSourceStatus = "ready",
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
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $playerReadable = $snapshot.state.player.location_id.status -in @("available", "derived")
            $terrainReadable = $snapshot.state.current_location.terrain_features.status -in @("available", "derived")
            $largeReadable = $snapshot.state.current_location.large_terrain_features.status -in @("available", "derived")
            $debrisReadable = $snapshot.state.current_location.debris.status -in @("available", "derived")
            $questsReadable = $snapshot.state.quests.active_quests.status -in @("available", "derived")
            $lastStatus = "save=$saveReadable;player=$playerReadable;terrain=$terrainReadable;large=$largeReadable;debris=$debrisReadable;quests=$questsReadable"
            if ($saveReadable -and $playerReadable -and $terrainReadable -and
                $largeReadable -and $debrisReadable -and $questsReadable) {
                return $snapshot
            }
        }
        catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for forage-source snapshot. Last status: $lastStatus"
}

function Find-ForageSource {
    param($Snapshot)
    $features = if ($SourceKind -eq "bush") {
        @($Snapshot.state.current_location.large_terrain_features.value)
    } else {
        @($Snapshot.state.current_location.terrain_features.value)
    }
    return $features |
        Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY -and
            (($SourceKind -eq "bush" -and $_.is_bush -eq $true) -or
             ($SourceKind -eq "ginger" -and $_.is_ginger -eq $true) -or
             ($SourceKind -eq "fruit_tree" -and $_.is_fruit_tree -eq $true) -or
             ($SourceKind -eq "wild_tree" -and [string]$_.runtime_type -eq "StardewValley.TerrainFeatures.Tree"))
        } |
        Select-Object -First 1
}

function Read-CollectionTaskProgress {
    param($Snapshot)
    if ($TaskFamily -eq "special_order") {
        $order = $Snapshot.state.quests.special_orders.value |
            Where-Object { [string]$_.quest_key -eq $TaskId } |
            Select-Object -First 1
        if ($null -eq $order) {
            if ($TaskId -in @($Snapshot.state.quests.completed_special_orders.value)) { return 1 }
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

function Add-CollectionTaskFields {
    param([System.Collections.IDictionary] $Request, [bool] $SourceStep)
    $Request.quest_candidate_id = "runtime_fixture:$TaskId"
    $Request.quest_family = $TaskFamily
    $Request.quest_id = $TaskId
    $Request.quest_key = if ($TaskFamily -eq "special_order") { $TaskId } else { "" }
    $Request.quest_objective_index = if ($TaskFamily -eq "special_order") { 0 } else { $null }
    $Request.quest_runtime_type = if ($TaskFamily -eq "special_order") { "SpecialOrder" } else { "ResourceCollectionQuest" }
    $Request.quest_expected_current_count = 0
    $Request.quest_expected_target_count = 1
    $Request.quest_acquisition_source_step = $SourceStep
    $Request.quest_acquisition_target_step = -not $SourceStep
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

function Wait-TaskReceiptOrStableDebris {
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
        $debris = Find-MatchingDebris -Snapshot $snapshot -QualifiedItemId $QualifiedItemId
        if ($null -ne $debris) {
            $chunk = @($debris.chunks)[0]
            $signature = "$($debris.debris_index):$($chunk.pixel_x):$($chunk.pixel_y)"
            $settled = [Math]::Abs([double]$chunk.x_velocity) -lt 0.01 -and
                [Math]::Abs([double]$chunk.y_velocity) -lt 0.01
            if ($settled -and $signature -eq $previousSignature) {
                return [pscustomobject]@{ snapshot = $snapshot; debris = $debris }
            }
            $previousSignature = $signature
            $lastStatus = "signature=$signature;xv=$($chunk.x_velocity);yv=$($chunk.y_velocity)"
        } else {
            $lastStatus = "task_progress=$progress;matching_debris=missing"
        }
        Start-Sleep -Milliseconds 120
    }
    throw "Forage output neither reached the task nor settled as transparent debris. Last status: $lastStatus"
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) { throw "Isolated saves path not found: $savesPath" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$smokeModsPath = Join-Path (
    Join-Path $RuntimeRoot "smoke-mods"
) $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath |
    Out-Null
foreach ($modName in @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)) {
    $sourceMod = Join-Path (
        Join-Path $runtimeGameDir "Mods"
    ) $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod |
        Out-Null
    Copy-Item `
        -Path (Join-Path $sourceMod "*") `
        -Destination $targetMod `
        -Recurse `
        -Force
}

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    SMAPI_MODS_PATH = $env:SMAPI_MODS_PATH
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
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $initialSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds

    $setupRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-forage-task-source"
        queue_item_id = "runtime-forage-task-source.setup"
        before_state_hash = $initialSnapshot.state_hash
        option_id = "debug.setup_forage_source_fixture"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        location_id = $LocationId
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        rule_key = $SourceKind
        fixture_bush_profile = if ($SourceKind -eq "bush") { $BushFixtureProfile } else { "" }
        fixture_ginger_profile = if ($SourceKind -eq "ginger") { $GingerFixtureProfile } else { "" }
        fixture_fruit_tree_profile = if ($SourceKind -eq "fruit_tree") { $FruitTreeFixtureProfile } else { "" }
        fixture_wild_tree_product_profile = if ($SourceKind -eq "wild_tree") { $WildTreeFixtureProfile } else { "" }
        debug_fill_inventory = $SourceKind -eq "ginger" -and $FillInventory.IsPresent
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Forage source fixture failed: $(@($setupResult.block_reasons) -join ',')"
    }
    Start-Sleep -Milliseconds 350
    $sourceSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $source = Find-ForageSource -Snapshot $sourceSnapshot
    $sourceStatus = switch ($SourceKind) {
        "bush" { [string]$source.bush_harvest_status }
        "ginger" { [string]$source.ginger_harvest_status }
        "fruit_tree" { [string]$source.fruit_tree_harvest_status }
        "wild_tree" { [string]$source.tree_product_harvest_status }
    }
    if ($null -eq $source -or $sourceStatus -ne $ExpectedSourceStatus) {
        Write-JsonFile (Join-Path $runDirectory "source-snapshot-rejected.json") $sourceSnapshot
        throw "Fixture exposed $sourceStatus instead of $ExpectedSourceStatus for $SourceKind at $LocationId $TargetTileX,$TargetTileY."
    }
    $qualifiedOutputId = switch ($SourceKind) {
        "bush" { [string]$source.bush_output_qualified_item_id }
        "ginger" { [string]$source.ginger_output_qualified_item_id }
        "fruit_tree" { [string](@($source.fruit_tree_expected_outputs)[0].qualified_item_id) }
        "wild_tree" { [string](@($source.tree_product_guaranteed_outputs)[0].qualified_item_id) }
    }

    $taskSetupResult = [pscustomobject]@{
        status = "not_requested"
        primitive_verification_status = "not_requested"
    }
    if ($TaskFamily -ne "none") {
        $taskSetupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-forage-task-source"
            queue_item_id = "runtime-forage-task-source.task-setup"
            before_state_hash = $sourceSnapshot.state_hash
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
        $taskSetupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $taskSetupRequest
        if ($taskSetupResult.status -ne "applied" -or $taskSetupResult.primitive_verification_status -ne "verified") {
            throw "Collection task fixture failed: $(@($taskSetupResult.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 300
    }
    $beforeHarvestSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $source = Find-ForageSource -Snapshot $beforeHarvestSnapshot
    if ($null -eq $source) { throw "Forage source disappeared before execution." }
    if ($ExpectedSourceStatus -ne "ready") {
        $summary = [ordered]@{
            status = "passed"
            run_id = $RunId
            save_slot = $SaveSlot
            source_kind = $SourceKind
            fixture_profile = switch ($SourceKind) { "bush" { $BushFixtureProfile } "ginger" { $GingerFixtureProfile } "fruit_tree" { $FruitTreeFixtureProfile } "wild_tree" { $WildTreeFixtureProfile } }
            location_id = $LocationId
            target_tile = "$TargetTileX,$TargetTileY"
            task_family = $TaskFamily
            source_status_before = $sourceStatus
            expected_source_status = $ExpectedSourceStatus
            harvest_status = "excluded_upstream"
            smoke_mods_path = $smokeModsPath
            loaded_mod_allowlist = @(
                "StardewAI.TransparentBridge",
                "StardewAI.RuntimeTestHarness"
            )
            executor_health = $executorHealth
            smapi_process_id = $process.Id
        }
        Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
        Write-JsonFile (Join-Path $runDirectory "before-snapshot.json") $beforeHarvestSnapshot
        Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
        $summary | ConvertTo-Json -Depth 24
        return
    }

    $harvestRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-forage-task-source"
        queue_item_id = "runtime-forage-task-source.harvest"
        before_state_hash = $beforeHarvestSnapshot.state_hash
        option_id = switch ($SourceKind) { "bush" { "executor.harvest_bush" } "ginger" { "executor.harvest_ginger" } "fruit_tree" { "executor.harvest_fruit_tree" } "wild_tree" { "executor.harvest_tree_product" } }
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_location = $LocationId
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
        qualified_item_id = $qualifiedOutputId
        quantity = switch ($SourceKind) { "bush" { [int]$source.bush_output_quantity_min } "ginger" { [int]$source.ginger_output_quantity_min } "fruit_tree" { [int]$source.fruit_tree_expected_output_quantity_total } "wild_tree" { 1 } }
        expected_output_quality = switch ($SourceKind) { "bush" { [int]$source.bush_output_quality } "ginger" { [int]$source.ginger_output_quality } "fruit_tree" { [int](@($source.fruit_tree_expected_outputs)[0].quality) } "wild_tree" { [int](@($source.tree_product_guaranteed_outputs)[0].quality) } }
        expected_foraging_experience_delta = switch ($SourceKind) { "bush" { [int]$source.bush_foraging_experience_on_success_min } "ginger" { [int]$source.ginger_foraging_experience_on_success_min } "fruit_tree" { 0 } "wild_tree" { 0 } }
        max_movement_tiles = 16
    }
    if ($SourceKind -in @("bush", "fruit_tree", "wild_tree")) {
        $harvestRequest.interaction_tile_x = $TargetTileX
        $harvestRequest.interaction_tile_y = $TargetTileY
        $harvestRequest.stand_tile_x = [int]$beforeHarvestSnapshot.state.player.tile_x.value
        $harvestRequest.stand_tile_y = [int]$beforeHarvestSnapshot.state.player.tile_y.value
        $harvestRequest.target_runtime_type = [string]$source.runtime_type
        if ($SourceKind -eq "fruit_tree") {
            $harvestRequest.fruit_tree_id = [string]$source.fruit_tree_id
            $harvestRequest.expected_fruit_count_before = [int]$source.fruit_count
            $harvestRequest.expected_fruit_count_after = [int]$source.fruit_tree_expected_fruit_count_after
            $harvestRequest.expected_output_items_json = ConvertTo-Json -InputObject @($source.fruit_tree_expected_outputs) -Depth 8 -Compress
            $harvestRequest.fruit_tree_projection_status = [string]$source.fruit_tree_projection_status
            $harvestRequest.fruit_tree_native_contract = [string]$source.fruit_tree_native_contract
        }
        if ($SourceKind -eq "wild_tree") {
            $harvestRequest.tree_product_tree_type = [string]$source.tree_type
            $harvestRequest.expected_tree_has_seed_before = [bool]$source.has_seed
            $harvestRequest.expected_tree_has_seed_after = [bool]$source.tree_product_expected_has_seed_after
            $harvestRequest.expected_tree_was_shaken_today_before = [bool]$source.was_shaken_today
            $harvestRequest.expected_tree_was_shaken_today_after = [bool]$source.tree_product_expected_was_shaken_today_after
            $harvestRequest.expected_output_items_json = ConvertTo-Json -InputObject @($source.tree_product_guaranteed_outputs) -Depth 8 -Compress
            $harvestRequest.tree_product_output_domain_json = ConvertTo-Json -InputObject @($source.tree_product_optional_output_domain) -Depth 8 -Compress
            $harvestRequest.tree_product_output_domain_contract = [string]$source.tree_product_output_distribution_status
            $harvestRequest.tree_product_projection_status = [string]$source.tree_product_projection_status
            $harvestRequest.tree_product_native_contract = [string]$source.tree_product_native_contract
            $harvestRequest.safe_slot_index = [int]$source.tree_product_safe_slot_index
            $harvestRequest.restore_slot_index = [int]$source.tree_product_restore_slot_index
        }
    } else {
        $harvestRequest.tool_slot_index = [int]$source.ginger_tool_slot_index
        $harvestRequest.required_tool_kind = [string]$source.ginger_required_tool_kind
    }
    if ($TaskFamily -ne "none") {
        Add-CollectionTaskFields -Request $harvestRequest -SourceStep $true
    }
    $harvestResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $harvestRequest
    if ($harvestResult.status -ne "applied" -or $harvestResult.primitive_verification_status -ne "verified") {
        throw "Native forage harvest failed: $(@($harvestResult.block_reasons) -join ',')"
    }
    Start-Sleep -Milliseconds 350
    $afterSourceSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    $sourceAfter = Find-ForageSource -Snapshot $afterSourceSnapshot
    $sourceConsumed = if ($SourceKind -eq "bush") {
        $null -ne $sourceAfter -and [string]$sourceAfter.bush_harvest_status -ne "ready"
    } elseif ($SourceKind -eq "fruit_tree") {
        $null -ne $sourceAfter -and [int]$sourceAfter.fruit_count -eq 0
    } elseif ($SourceKind -eq "wild_tree") {
        $null -ne $sourceAfter -and $sourceAfter.has_seed -eq $false
    } else {
        $null -eq $sourceAfter
    }
    $taskProgressAfterSource = if ($TaskFamily -eq "none") {
        $null
    } else {
        Read-CollectionTaskProgress -Snapshot $afterSourceSnapshot
    }
    $pickupResult = $null
    $afterReceiptSnapshot = $afterSourceSnapshot

    if ($TaskFamily -ne "none" -and $taskProgressAfterSource -eq 0) {
        $receipt = Wait-TaskReceiptOrStableDebris -Url $snapshotUrl -QualifiedItemId $qualifiedOutputId
        $afterSourceSnapshot = $receipt.snapshot
        $taskProgressAfterSource = Read-CollectionTaskProgress -Snapshot $afterSourceSnapshot
        if ($taskProgressAfterSource -eq 0) {
            $debris = $receipt.debris
            if ($null -eq $debris) { throw "No transparent debris available for task receipt." }
            $chunk = @($debris.chunks)[0]
            $pickupRequest = [ordered]@{
                schema_version = "training_execution_request.v1"
                run_id = $RunId
                queue_id = "runtime-forage-task-source"
                queue_item_id = "runtime-forage-task-source.pickup"
                before_state_hash = $afterSourceSnapshot.state_hash
                option_id = "executor.pickup_debris"
                execution_mode = "training_singleplayer"
                actor = "training_farmer.main"
                save_isolation_path = $savesPath
                request_nonce = [guid]::NewGuid().ToString("N")
                created_at = [DateTimeOffset]::UtcNow.ToString("O")
                target_location = $LocationId
                target_tile_x = [int]$chunk.tile_x
                target_tile_y = [int]$chunk.tile_y
                debris_index = [int]$debris.debris_index
                qualified_item_id = $qualifiedOutputId
            }
            Add-CollectionTaskFields -Request $pickupRequest -SourceStep $false
            $pickupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $pickupRequest
            if ($pickupResult.status -ne "applied" -or $pickupResult.primitive_verification_status -ne "verified") {
                throw "Forage debris pickup failed: $(@($pickupResult.block_reasons) -join ',')"
            }
            Start-Sleep -Milliseconds 300
            $afterReceiptSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        } else {
            $afterReceiptSnapshot = $afterSourceSnapshot
        }
    }
    $taskProgressAfterReceipt = if ($TaskFamily -eq "none") {
        $null
    } else {
        Read-CollectionTaskProgress -Snapshot $afterReceiptSnapshot
    }

    $summary = [ordered]@{
        status = if ($sourceConsumed -and
            ($TaskFamily -eq "none" -or $taskProgressAfterReceipt -ge 1)) {
            "passed"
        } else {
            "failed"
        }
        run_id = $RunId
        save_slot = $SaveSlot
        source_kind = $SourceKind
        fixture_profile = switch ($SourceKind) { "bush" { $BushFixtureProfile } "ginger" { $GingerFixtureProfile } "fruit_tree" { $FruitTreeFixtureProfile } "wild_tree" { $WildTreeFixtureProfile } }
        inventory_full = $SourceKind -eq "ginger" -and $FillInventory.IsPresent
        smoke_mods_path = $smokeModsPath
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness"
        )
        location_id = $LocationId
        target_tile = "$TargetTileX,$TargetTileY"
        qualified_output_item_id = $qualifiedOutputId
        task_family = $TaskFamily
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        task_setup_status = $taskSetupResult.status
        source_status_before = $sourceStatus
        harvest_status = $harvestResult.status
        harvest_verification = $harvestResult.primitive_verification_status
        harvest_reasons = @($harvestResult.primitive_verification_reasons)
        source_consumed = $sourceConsumed
        task_progress_after_source = $taskProgressAfterSource
        task_progress_after_receipt = $taskProgressAfterReceipt
        pickup_status = if ($null -eq $pickupResult) { "not_required" } else { $pickupResult.status }
        state_hash_changed = $beforeHarvestSnapshot.state_hash -ne $afterReceiptSnapshot.state_hash
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "task-setup-result.json") $taskSetupResult
    Write-JsonFile (Join-Path $runDirectory "before-harvest-snapshot.json") $beforeHarvestSnapshot
    Write-JsonFile (Join-Path $runDirectory "harvest-result.json") $harvestResult
    Write-JsonFile (Join-Path $runDirectory "after-source-snapshot.json") $afterSourceSnapshot
    if ($null -ne $pickupResult) {
        Write-JsonFile (Join-Path $runDirectory "pickup-result.json") $pickupResult
        Write-JsonFile (Join-Path $runDirectory "after-receipt-snapshot.json") $afterReceiptSnapshot
    }
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if ($summary.status -ne "passed") { throw "Runtime forage task source smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
