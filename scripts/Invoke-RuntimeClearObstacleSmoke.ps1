param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-clear-obstacle-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-clear-obstacle-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [int] $MaxToolSwings = 8,
    [ValidateSet("grass", "twig", "seed_spot", "artifact_spot", "tree_moss", "tree_chop")]
    [string] $FixtureKind = "grass",
    [ValidateSet("ordinary", "pine_professions", "mushroom", "mahogany", "fern", "mystic")]
    [string] $WildTreeChopProfile = "ordinary",
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $json = $Value | ConvertTo-Json -Depth 64
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body,
        [int] $TimeoutSeconds = 120
    )

    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") {
                return $response
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
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 5
            $saveReadable = $snapshot.save_id.status -in @("available", "derived")
            $timeReadable = $snapshot.in_game_time.status -in @("available", "derived")
            $locationReadable = $false
            $objectsReadable = $false
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "player" -and
                $snapshot.state.player.PSObject.Properties.Name -contains "location_id") {
                $locationReadable = $snapshot.state.player.location_id.status -in @("available", "derived")
            }
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "current_location" -and
                $snapshot.state.current_location.PSObject.Properties.Name -contains "objects") {
                $objectsReadable = $snapshot.state.current_location.objects.status -in @("available", "derived")
            }

            $lastStatus = "save_id=$($snapshot.save_id.status);in_game_time=$($snapshot.in_game_time.status);location_id_readable=$locationReadable;objects_readable=$objectsReadable;completeness=$($snapshot.completeness)"
            if ($saveReadable -and $timeReadable -and $locationReadable -and $objectsReadable) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Find-TargetObject {
    param($Snapshot)
    return $Snapshot.state.current_location.objects.value |
        Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY
        } |
        Select-Object -First 1
}

function Find-TargetTerrainFeature {
    param($Snapshot)
    return $Snapshot.state.current_location.terrain_features.value |
        Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and
            [int]$_.tile_y -eq $TargetTileY
        } |
        Select-Object -First 1
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}

if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }

    $SaveSlot = $slot.Name
}

$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) {
    throw "Isolated save slot not found: $slotPath"
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
    STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL = $env:STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL
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
    $env:STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru

    $executorHealth = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $beforeSnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds $StartupTimeoutSeconds

    $baseRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-clear-obstacle-smoke"
        before_state_hash = $beforeSnapshot.state_hash
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = ""
        created_at = ""
        max_crops = $MaxToolSwings
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }

    $setupRequest = [ordered]@{} + $baseRequest
    $setupRequest.queue_item_id = "runtime-clear-obstacle-smoke.setup"
    $setupRequest.option_id = "debug.setup_clear_obstacle"
    $setupRequest.request_nonce = [guid]::NewGuid().ToString("N")
    $setupRequest.created_at = [DateTimeOffset]::UtcNow.ToString("O")
    $setupRequest.rule_key = $FixtureKind
    if ($FixtureKind -eq "tree_chop") {
        $setupRequest.fixture_wild_tree_chop_profile = $WildTreeChopProfile
    }
    $setupResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $setupRequest -TimeoutSeconds 120

    $readySnapshot = Wait-WorldSnapshot -Url "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -TimeoutSeconds 30
    $targetObject = if ($FixtureKind -in @("tree_moss", "tree_chop")) {
        Find-TargetTerrainFeature -Snapshot $readySnapshot
    }
    else {
        Find-TargetObject -Snapshot $readySnapshot
    }
    if ($FixtureKind -ne "grass") {
        if ($null -eq $targetObject) {
            Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-ready-rejected.json") $readySnapshot
            throw "Fixture did not expose a transparent $FixtureKind object at $TargetTileX,$TargetTileY."
        }
        $projectionStatus = if ($FixtureKind -eq "tree_moss") {
            [string]$targetObject.moss_harvest_status
        }
        elseif ($FixtureKind -eq "tree_chop") {
            [string]$targetObject.tree_chop_acquisition_status
        }
        else {
            [string]$targetObject.clear_obstacle_executor_status
        }
        if ($projectionStatus -ne "ready") {
            Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-ready-rejected.json") $readySnapshot
            throw "Transparent $FixtureKind projection is not ready: $projectionStatus."
        }
    }

    $clearRequest = [ordered]@{} + $baseRequest
    $clearRequest.queue_item_id = "runtime-clear-obstacle-smoke.clear"
    $clearRequest.before_state_hash = $readySnapshot.state_hash
    $clearRequest.option_id = "executor.clear_obstacle"
    $clearRequest.request_nonce = [guid]::NewGuid().ToString("N")
    $clearRequest.created_at = [DateTimeOffset]::UtcNow.ToString("O")
    $clearRequest.target_location = [string]$readySnapshot.state.player.location_id.value
    if ($FixtureKind -ne "grass") {
        if ($FixtureKind -eq "tree_moss") {
            $clearRequest.max_crops = 1
            $clearRequest.clear_completion_mode = [string]$targetObject.moss_harvest_completion_mode
            $clearRequest.target_runtime_type = [string]$targetObject.runtime_type
            $clearRequest.tool_slot_index = [int]$targetObject.moss_harvest_tool_slot_index
            $clearRequest.required_tool_kind = [string]$targetObject.moss_harvest_required_tool_kind
            $clearRequest.clear_output_projection_status = "exact"
            $clearRequest.clear_output_items_json = ConvertTo-Json -InputObject @($targetObject.moss_harvest_output_items) -Depth 16 -Compress
            $clearRequest.expected_tree_has_moss_before = [bool]$targetObject.moss_harvest_has_moss_before
            $clearRequest.expected_tree_has_moss_after = [bool]$targetObject.moss_harvest_has_moss_after
            $clearRequest.expected_tree_has_seed_before = [bool]$targetObject.moss_harvest_has_seed_before
            $clearRequest.expected_tree_has_seed_after = [bool]$targetObject.moss_harvest_has_seed_after
            $clearRequest.expected_tree_was_shaken_today_before = [bool]$targetObject.moss_harvest_was_shaken_today_before
            $clearRequest.expected_tree_was_shaken_today_after = [bool]$targetObject.moss_harvest_was_shaken_today_after
            $clearRequest.expected_tree_growth_stage_before = [int]$targetObject.moss_harvest_growth_stage_before
            $clearRequest.expected_tree_growth_stage_after = [int]$targetObject.moss_harvest_growth_stage_after
            $clearRequest.expected_tree_health_before = [double]$targetObject.moss_harvest_health_before
            $clearRequest.expected_tree_health_after = [double]$targetObject.moss_harvest_health_after
            $clearRequest.expected_moss_harvested_before = [long]$targetObject.moss_harvest_moss_harvested_before
            $clearRequest.expected_moss_harvested_after = [long]$targetObject.moss_harvest_moss_harvested_after
            $clearRequest.expected_foraging_experience_before = [int]$targetObject.moss_harvest_foraging_experience_before
            $clearRequest.expected_foraging_experience_delta = [int]$targetObject.moss_harvest_quantity
            $clearRequest.expected_foraging_experience_after = [int]$targetObject.moss_harvest_foraging_experience_after
            $clearRequest.moss_harvest_projection_status = [string]$targetObject.moss_harvest_output_projection_status
            $clearRequest.moss_harvest_native_contract = [string]$targetObject.moss_harvest_native_contract
        }
        elseif ($FixtureKind -eq "tree_chop") {
            $clearRequest.max_crops = [int]$targetObject.tree_chop_expected_tool_swings
            $clearRequest.clear_completion_mode = [string]$targetObject.tree_chop_completion_mode
            $clearRequest.target_runtime_type = [string]$targetObject.runtime_type
            $clearRequest.tool_slot_index = [int]$targetObject.tree_chop_tool_slot_index
            $clearRequest.required_tool_kind = [string]$targetObject.tree_chop_required_tool_kind
            $clearRequest.tree_chop_tree_type = [string]$targetObject.tree_type
            $clearRequest.tree_chop_data_contract_status = [string]$targetObject.tree_chop_data_contract_status
            $clearRequest.tree_chop_protection_status = [string]$targetObject.tree_chop_protection_status
            $clearRequest.tree_chop_projection_status = [string]$targetObject.tree_chop_projection_status
            $clearRequest.tree_chop_output_domain_contract = [string]$targetObject.tree_chop_output_distribution_status
            $clearRequest.tree_chop_guaranteed_minimum_outputs_json = ConvertTo-Json -InputObject @($targetObject.tree_chop_guaranteed_minimum_outputs) -Depth 16 -Compress
            $clearRequest.tree_chop_output_domain_json = ConvertTo-Json -InputObject @($targetObject.tree_chop_optional_output_domain) -Depth 16 -Compress
            $clearRequest.tree_chop_native_contract = [string]$targetObject.tree_chop_native_contract
            $clearRequest.expected_tree_has_moss_before = [bool]$targetObject.has_moss
            $clearRequest.expected_tree_has_seed_before = [bool]$targetObject.has_seed
            $clearRequest.expected_tree_growth_stage_before = [int]$targetObject.growth_stage
            $clearRequest.expected_tree_health_before = [double]$targetObject.health
            $clearRequest.expected_tree_present_after = [bool]$targetObject.tree_chop_expected_tree_present_after
            $clearRequest.expected_foraging_experience_before = [int]$targetObject.tree_chop_foraging_experience_before
            $clearRequest.expected_foraging_experience_delta = [int]$targetObject.tree_chop_foraging_experience_delta
            $clearRequest.expected_foraging_experience_after = [int]$targetObject.tree_chop_foraging_experience_after
            $clearRequest.expected_trees_chopped_before = [long]$targetObject.tree_chop_trees_chopped_before
            $clearRequest.expected_trees_chopped_delta = [long]$targetObject.tree_chop_trees_chopped_delta
            $clearRequest.expected_trees_chopped_after = [long]$targetObject.tree_chop_trees_chopped_after
        }
        else {
            $clearRequest.max_crops = [int]$targetObject.expected_tool_hits_to_clear
            $clearRequest.tool_slot_index = [int]$targetObject.tool_slot_index
            $clearRequest.required_tool_kind = [string]$targetObject.required_tool_kind
            $clearRequest.clear_output_projection_status = [string]$targetObject.clear_output_projection_status
            $clearRequest.clear_output_items_json = [string]$targetObject.clear_output_items_json
            $clearRequest.expected_foraging_experience_delta = [int]$targetObject.harvest_experience_on_success_min
        }
        if ($FixtureKind -in @("seed_spot", "artifact_spot")) {
            $clearRequest.artifact_spots_dug_before = [int]$targetObject.artifact_spots_dug_before
            $clearRequest.artifact_spots_dug_delta = [int]$targetObject.artifact_spots_dug_delta
            $clearRequest.artifact_spots_dug_expected_after = [int]$targetObject.artifact_spots_dug_expected_after
            $clearRequest.clear_terrain_feature_expected_after = [string]$targetObject.clear_terrain_feature_expected_after
            $clearRequest.defense_book_mail_before = [int]$targetObject.defense_book_mail_before
            $clearRequest.defense_book_mail_expected_after = [int]$targetObject.defense_book_mail_expected_after
        }
    }
    $clearResult = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $clearRequest -TimeoutSeconds 120

    $afterSnapshot = Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8765/api/v1/snapshot?profile=full" -Headers @{ "Accept" = "application/json" } -TimeoutSec 10
    $targetObjectAfter = if ($FixtureKind -in @("tree_moss", "tree_chop")) {
        Find-TargetTerrainFeature -Snapshot $afterSnapshot
    }
    else {
        Find-TargetObject -Snapshot $afterSnapshot
    }

    $targetPostconditionPassed = if ($FixtureKind -eq "tree_moss") {
        $null -ne $targetObjectAfter -and -not [bool]$targetObjectAfter.has_moss
    }
    elseif ($FixtureKind -eq "tree_chop") {
        $null -eq $targetObjectAfter
    }
    else {
        $FixtureKind -eq "grass" -or $null -eq $targetObjectAfter
    }

    $summary = [ordered]@{
        status = if ($setupResult.status -eq "applied" -and $clearResult.status -eq "applied" -and $clearResult.primitive_verification_status -eq "verified" -and $targetPostconditionPassed) { "passed" } else { "unexpected_result" }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        smapi_process_id = $process.Id
        bridge_state_hash_before = $beforeSnapshot.state_hash
        bridge_state_hash_ready = $readySnapshot.state_hash
        bridge_state_hash_after = $afterSnapshot.state_hash
        target_tile = "$TargetTileX,$TargetTileY"
        fixture_kind = $FixtureKind
        target_qualified_item_id = if ($null -eq $targetObject) { "" } else { [string]$targetObject.qualified_item_id }
        target_projection_status = if ($null -eq $targetObject) { "not_applicable" } elseif ($FixtureKind -eq "tree_moss") { [string]$targetObject.moss_harvest_status } elseif ($FixtureKind -eq "tree_chop") { [string]$targetObject.tree_chop_acquisition_status } else { [string]$targetObject.clear_obstacle_executor_status }
        target_present_after = $null -ne $targetObjectAfter
        executor_health = $executorHealth
        setup_status = $setupResult.status
        setup_verification = $setupResult.primitive_verification_status
        setup_block_reasons = @($setupResult.block_reasons)
        clear_status = $clearResult.status
        clear_verification = $clearResult.primitive_verification_status
        clear_reasons = @($clearResult.primitive_verification_reasons)
        clear_block_reasons = @($clearResult.block_reasons)
        clear_observed_effect = $clearResult.observed_effect
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-before.json") $beforeSnapshot
    Write-JsonFile (Join-Path $runDirectory "setup-request.json") $setupRequest
    Write-JsonFile (Join-Path $runDirectory "setup-result.json") $setupResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-ready.json") $readySnapshot
    Write-JsonFile (Join-Path $runDirectory "clear-request.json") $clearRequest
    Write-JsonFile (Join-Path $runDirectory "clear-result.json") $clearResult
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after.json") $afterSnapshot
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary

    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }

    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
