[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-house-plant-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-house-plant",
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "schema=$($snapshot.schema_version);save_id=$($snapshot.save_id.status)"
        } catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for full snapshot. Last status: $lastStatus"
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-house-plant"
        queue_item_id = $ItemId
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
        $close = Invoke-JsonPost $executeUrl (New-Request $current "executor.close_menu" "$RunId.initial-close.$pass")
        if ($close.status -ne "applied") { throw "Initial menu close failed: $(@($close.block_reasons) -join ',')" }
        Start-Sleep -Seconds 1
        $current = Wait-Snapshot $snapshotUrl 30
    }
    if ($current.state.menus.active_menu.value.is_open) { throw "Initial menu did not close." }
    return $current
}

function Find-HousePlant($Snapshot) {
    @($Snapshot.state.current_location.objects.value | Where-Object { $null -ne $_.house_plant_rotation }) | Select-Object -First 1
}

function Invoke-HousePlantRotation($Snapshot, $Plant, [int] $CaseIndex) {
    $projection = $Plant.house_plant_rotation
    if ($projection.status -ne "ready") { throw "House Plant projection is not ready: $($projection.status)" }
    $stand = @($projection.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    if ($null -eq $stand) { throw "House Plant has no available adjacent stand." }
    $safe = $Snapshot.state.player.safe_item_context.value
    if ($safe.safe_slot_kind -ne "empty") { throw "House Plant smoke requires an empty toolbar slot." }

    $request = New-Request $Snapshot "world.rotate_house_plant" "$RunId.rotate.$CaseIndex"
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.target_tile_x = [int]$Plant.tile_x
    $request.target_tile_y = [int]$Plant.tile_y
    $request.stand_tile_x = [int]$stand.tile_x
    $request.stand_tile_y = [int]$stand.tile_y
    $request.target_runtime_type = [string]$projection.target_runtime_type
    $request.item_id = [string]$projection.canonical_item_id
    $request.qualified_item_id = [string]$projection.canonical_qualified_item_id
    $request.house_plant_current_sprite_index = [int]$projection.current_sprite_index
    $request.house_plant_expected_sprite_index = [int]$projection.expected_sprite_index_after_native_location_action
    $request.house_plant_expected_object_action_calls = [int]$projection.expected_object_check_for_action_call_count
    $request.house_plant_expected_location_action_return = [bool]$projection.expected_native_location_action_return
    $request.safe_slot_index = [int]$safe.safe_slot_index
    $request.restore_slot_index = [int]$safe.current_tool_index
    $request.interaction_kind = [string]$projection.interaction_kind
    $request.expected_action_type = [string]$projection.expected_action_type
    $request.native_contract = [string]$projection.native_contract
    $request.max_movement_tiles = 512
    return Invoke-JsonPost $executeUrl $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$loadedModAllowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $current = Close-InitialMenus (Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds)
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $current

    $cases = @()
    for ($sprite = 0; $sprite -lt 8; $sprite++) {
        $fixtureRequest = New-Request $current "debug.setup_house_plant_rotation" "$RunId.fixture.$sprite"
        $fixtureRequest.house_plant_current_sprite_index = $sprite
        $fixture = Invoke-JsonPost $executeUrl $fixtureRequest
        Write-Json (Join-Path $runDirectory "fixture-$sprite.json") $fixture
        if ($fixture.status -ne "applied") { throw "House Plant fixture $sprite failed: $(@($fixture.block_reasons) -join ',')" }
        Start-Sleep -Milliseconds 500

        $ready = Wait-Snapshot $snapshotUrl 30
        $plant = Find-HousePlant $ready
        if ($null -eq $plant) { throw "House Plant projection missing for case $sprite." }
        $projection = $plant.house_plant_rotation
        $expected = if ($sprite -eq 7) { 1 } else { $sprite + 1 }
        $expectedCalls = if ($sprite -eq 7) { 2 } else { 1 }
        $restoreSlot = [int]$ready.state.player.safe_item_context.value.current_tool_index
        if ([int]$projection.current_sprite_index -ne $sprite -or
            [int]$projection.expected_sprite_index_after_native_location_action -ne $expected -or
            [int]$projection.expected_object_check_for_action_call_count -ne $expectedCalls) {
            throw "House Plant transparent projection mismatch for case $sprite."
        }

        $result = Invoke-HousePlantRotation $ready $plant $sprite
        Write-Json (Join-Path $runDirectory "rotation-$sprite.json") $result
        Start-Sleep -Milliseconds 500
        $after = Wait-Snapshot $snapshotUrl 30
        $afterPlant = @($after.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq [int]$plant.tile_x -and [int]$_.tile_y -eq [int]$plant.tile_y
        }) | Select-Object -First 1
        $casePassed = $result.status -eq "applied" -and
            $result.primitive_verification_status -eq "verified" -and
            $null -ne $afterPlant -and
            [int]$afterPlant.parent_sheet_index -eq $expected -and
            [string]$afterPlant.item_id -eq "0" -and
            [string]$afterPlant.qualified_item_id -eq "(BC)0" -and
            [int]$after.state.player.current_tool_index.value -eq $restoreSlot
        $cases += [ordered]@{
            start_sprite_index = $sprite
            expected_sprite_index = $expected
            expected_object_action_calls = $expectedCalls
            observed_sprite_index = if ($null -eq $afterPlant) { -1 } else { [int]$afterPlant.parent_sheet_index }
            item_id_after = if ($null -eq $afterPlant) { "missing" } else { [string]$afterPlant.item_id }
            qualified_item_id_after = if ($null -eq $afterPlant) { "missing" } else { [string]$afterPlant.qualified_item_id }
            execution_status = $result.status
            verification = $result.primitive_verification_status
            restore_slot_expected = $restoreSlot
            restore_slot_observed = [int]$after.state.player.current_tool_index.value
            passed = $casePassed
        }
        $current = $after
    }

    $passed = @($cases | Where-Object { -not $_.passed }).Count -eq 0
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_house_plant_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        case_count = $cases.Count
        expected_sequence = @(1, 2, 3, 4, 5, 6, 7, 1)
        cases = $cases
        loaded_mod_allowlist = $loadedModAllowlist
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
