[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fish-pond-management-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path }

function Get-Json([string] $Url, [int] $Timeout = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $Timeout
}

function Post-Json([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Get-Json $snapshotUrl
            $lastStatus = "schema=$($snapshot.schema_version);save=$($snapshot.save_id.status);farm=$($snapshot.state.farm.buildings.status)"
            if ($snapshot.schema_version -eq "snapshot.v1" -and
                $snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.farm.buildings.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full farm snapshot. Last status: $lastStatus"
}

function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-fish-pond-management"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Close-InitialMenus($Snapshot) {
    $current = $Snapshot
    for ($pass = 0; $pass -lt 8 -and $current.state.menus.active_menu.value.is_open; $pass++) {
        $close = Post-Json $executeUrl (New-Request $current "executor.close_menu" "$RunId.initial-close.$pass")
        if ($close.status -ne "applied") { throw "Initial menu close failed: $(@($close.block_reasons) -join ',')" }
        Start-Sleep -Seconds 1
        $current = Wait-Snapshot 30
    }
    if ($current.state.menus.active_menu.value.is_open) { throw "Initial menu did not close." }
    return $current
}

function Wait-ManagementPond([int] $MinimumBuildingCount, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 15
        $ponds = @($snapshot.state.farm.buildings.value | Where-Object {
            $null -ne $_.fish_pond -and $_.fish_pond.status -eq "exact" -and $_.fish_pond.management_status -eq "ready"
        })
        if ($ponds.Count -ge $MinimumBuildingCount) {
            return [ordered]@{ snapshot = $snapshot; building = $ponds[-1] }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for exact ready fish-pond management projection."
}

function Add-ManagementFields($Request, $Snapshot, $Building, [string] $Operation) {
    $pond = $Building.fish_pond
    $Request.location_id = [string]$Snapshot.state.farm.farm_identity.value.location_id
    $Request.target_tile_x = [int]$pond.preferred_target_tile_x
    $Request.target_tile_y = [int]$pond.preferred_target_tile_y
    $Request.stand_tile_x = [int]$pond.preferred_stand_tile_x
    $Request.stand_tile_y = [int]$pond.preferred_stand_tile_y
    $Request.building_tile_x = [int]$Building.tile_x
    $Request.building_tile_y = [int]$Building.tile_y
    $Request.target_runtime_type = [string]$pond.runtime_type
    $Request.fish_type_item_id = [string]$pond.fish_type_item_id
    $Request.management_operation = $Operation
    $Request.fish_pond_management_reason = "isolated explicit runtime verification"
    $Request.confirm_empty_pond = $Operation -eq "empty_pond"
    $Request.expected_fish_count = [int]$pond.fish_count
    $Request.expected_fish_count_after = [int]$pond.management_empty_expected_fish_count_after
    $Request.expected_maximum_occupants_before = [int]$pond.maximum_occupants
    $Request.expected_maximum_occupants_after = [int]$pond.management_empty_expected_maximum_occupants_after
    $Request.expected_last_unlocked_population_gate_before = [int]$pond.last_unlocked_population_gate
    $Request.expected_last_unlocked_population_gate_after = [int]$pond.management_empty_expected_last_unlocked_population_gate_after
    $Request.expected_days_since_spawn_before = [int]$pond.days_since_spawn
    $Request.expected_days_since_spawn_after = [int]$pond.management_empty_expected_days_since_spawn_after
    $Request.expected_needed_item_qualified_item_id_before = [string]$pond.needed_item_qualified_item_id
    $Request.expected_needed_item_count_before = [int]$pond.needed_item_count
    $Request.expected_needed_item_count_after = [int]$pond.management_empty_expected_needed_item_count_after
    $Request.expected_has_completed_request_before = if ($pond.has_completed_request) { 1 } else { 0 }
    $Request.expected_has_completed_request_after = if ($pond.management_empty_expected_has_completed_request_after) { 1 } else { 0 }
    $Request.expected_golden_animal_cracker_before = if ($pond.golden_animal_cracker) { 1 } else { 0 }
    $Request.expected_golden_animal_cracker_after = if ($pond.management_empty_expected_golden_animal_cracker_after) { 1 } else { 0 }
    $Request.expected_has_spawned_fish_before = if ($pond.has_spawned_fish) { 1 } else { 0 }
    $Request.expected_has_spawned_fish_after = if ($pond.management_empty_expected_has_spawned_fish_after) { 1 } else { 0 }
    $Request.expected_netting_style_before = [int]$pond.netting_style
    $Request.expected_netting_style_after = if ($Operation -eq "cycle_netting") {
        [int]$pond.management_cycle_expected_netting_style_after
    } else { [int]$pond.management_empty_expected_netting_style_after }
    $Request.expected_fish_debris_qualified_item_id = [string]$pond.management_empty_expected_fish_debris_qualified_item_id
    $Request.expected_fish_debris_count = [int]$pond.management_empty_expected_fish_debris_count
    $Request.expected_sign_qualified_item_id_before = [string]$pond.sign_qualified_item_id
    $Request.expected_output_qualified_item_id_before = [string]$pond.output_qualified_item_id_before_management
    $Request.expected_override_water_color_packed_before = [long]$pond.override_water_color_packed
    $Request.safe_slot_index = [int]$pond.management_safe_slot_index
    $Request.restore_slot_index = [int]$pond.management_restore_slot_index
    $Request.native_contract = [string]$pond.management_native_contract
    $Request.max_movement_tiles = 512
}

function Invoke-ManagementCase([string] $Operation, [int] $CaseIndex, [int] $FixtureX, [int] $FixtureY) {
    $beforeSetup = Close-InitialMenus (Wait-Snapshot 30)
    $existingCount = @($beforeSetup.state.farm.buildings.value | Where-Object {
        $null -ne $_.fish_pond -and $_.fish_pond.status -eq "exact" -and $_.fish_pond.management_status -eq "ready"
    }).Count
    $setup = New-Request $beforeSetup "debug.setup_fish_pond_management" "$RunId.fixture.$CaseIndex"
    $setup.target_tile_x = $FixtureX
    $setup.target_tile_y = $FixtureY
    $setup.fish_type_item_id = "(O)698"
    $setupResult = Post-Json $executeUrl $setup
    Write-Json (Join-Path $runDirectory "$CaseIndex-$Operation-fixture.json") $setupResult
    if ($setupResult.status -ne "applied") { throw "Fish Pond management fixture failed: $(@($setupResult.block_reasons) -join ',')" }

    $ready = Wait-ManagementPond ($existingCount + 1) 30
    $building = $ready.building
    $request = New-Request $ready.snapshot "fishing.manage_fish_pond" "$RunId.$Operation"
    Add-ManagementFields $request $ready.snapshot $building $Operation
    Write-Json (Join-Path $runDirectory "$CaseIndex-$Operation-before.json") $ready.snapshot
    $result = Post-Json $executeUrl $request
    Write-Json (Join-Path $runDirectory "$CaseIndex-$Operation-result.json") $result
    Start-Sleep -Milliseconds 500
    $after = Wait-Snapshot 30
    $afterBuilding = @($after.state.farm.buildings.value | Where-Object {
        [int]$_.tile_x -eq [int]$building.tile_x -and [int]$_.tile_y -eq [int]$building.tile_y
    }) | Select-Object -First 1
    $expectedStyle = [int]$request.expected_netting_style_after
    $postStateMatches = if ($Operation -eq "cycle_netting") {
        $null -ne $afterBuilding -and [int]$afterBuilding.fish_pond.netting_style -eq $expectedStyle -and
            [int]$afterBuilding.fish_pond.fish_count -eq [int]$request.expected_fish_count
    } else {
        $null -ne $afterBuilding -and [string]::IsNullOrWhiteSpace([string]$afterBuilding.fish_pond.fish_type_item_id) -and
            [int]$afterBuilding.fish_pond.fish_count -eq 0 -and
            [int]$afterBuilding.fish_pond.maximum_occupants -eq [int]$request.expected_maximum_occupants_after
    }
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
        $result.training_impact_scope -eq "player_command_executor_evaluation_only" -and $postStateMatches
    return [ordered]@{
        operation = $Operation
        building_tile = "$($building.tile_x),$($building.tile_y)"
        fish_count_before = [int]$request.expected_fish_count
        fish_count_after = if ($null -eq $afterBuilding) { -1 } else { [int]$afterBuilding.fish_pond.fish_count }
        netting_style_before = [int]$request.expected_netting_style_before
        netting_style_after = if ($null -eq $afterBuilding) { -1 } else { [int]$afterBuilding.fish_pond.netting_style }
        expected_fish_debris_count = [int]$request.expected_fish_debris_count
        execution_status = $result.status
        verification = $result.primitive_verification_status
        training_impact_scope = $result.training_impact_scope
        block_reasons = @($result.block_reasons)
        observed_effect = $result.observed_effect
        passed = $passed
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path "artifacts\runtime-fish-pond-management" $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI executable not found: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$loadedMods = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeMods = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeMods | Out-Null
foreach ($mod in $loadedMods) {
    $target = Join-Path $smokeMods $mod
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path (Join-Path (Join-Path $gameDirectory "Mods") $mod) "*") -Destination $target -Recurse -Force
}

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$old = @{}
foreach ($name in $names) { $old[$name] = [Environment]::GetEnvironmentVariable($name) }
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
    $env:SMAPI_MODS_PATH = $smokeMods
    $process = Start-Process -FilePath $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru

    $null = Close-InitialMenus (Wait-Snapshot $StartupTimeoutSeconds)
    $cases = @(
        (Invoke-ManagementCase "cycle_netting" 1 64 18),
        (Invoke-ManagementCase "empty_pond" 2 72 18)
    )
    $passed = @($cases | Where-Object { -not $_.passed }).Count -eq 0
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_fish_pond_management_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        case_count = $cases.Count
        cases = $cases
        loaded_mod_allowlist = $loadedMods
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 24
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $old[$name]) }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
