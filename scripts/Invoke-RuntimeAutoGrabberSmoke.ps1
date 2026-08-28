[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-auto-grabber-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-auto-grabber",
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
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
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
        queue_id = "runtime-auto-grabber"
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

    $fixture = Invoke-JsonPost $executeUrl (New-Request $current "debug.setup_auto_grabber" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture.json") $fixture
    if ($fixture.status -ne "applied") { throw "Auto-Grabber fixture failed: $(@($fixture.block_reasons) -join ',')" }
    Start-Sleep -Milliseconds 500

    $ready = Wait-Snapshot $snapshotUrl 30
    $object = @($ready.state.current_location.objects.value | Where-Object { $null -ne $_.auto_grabber_collection }) | Select-Object -First 1
    if ($null -eq $object) { throw "Auto-Grabber transparent projection is missing." }
    $projection = $object.auto_grabber_collection
    $stand = @($projection.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    $safe = $ready.state.player.safe_item_context.value
    if ($projection.status -ne "ready" -or $null -eq $stand) { throw "Auto-Grabber projection is not executable: $($projection.status)" }
    if ($safe.safe_slot_kind -notin @("empty", "tool")) { throw "No safe toolbar slot is available." }

    $request = New-Request $ready "animals.collect_auto_grabber_contents" "$RunId.collect"
    $request.location_id = [string]$ready.state.player.location_id.value
    $request.target_tile_x = [int]$object.tile_x
    $request.target_tile_y = [int]$object.tile_y
    $request.stand_tile_x = [int]$stand.tile_x
    $request.stand_tile_y = [int]$stand.tile_y
    $request.target_runtime_type = [string]$projection.target_runtime_type
    $request.item_id = [string]$projection.canonical_item_id
    $request.qualified_item_id = [string]$projection.canonical_qualified_item_id
    $request.safe_slot_index = [int]$safe.safe_slot_index
    $request.auto_grabber_safe_slot_kind = [string]$safe.safe_slot_kind
    $request.restore_slot_index = [int]$safe.current_tool_index
    $request.auto_grabber_held_container_runtime_type = [string]$projection.held_container_runtime_type
    $request.auto_grabber_contents_before_json = [string]$projection.contents_before_json
    $request.auto_grabber_transferable_contents_json = [string]$projection.transferable_contents_json
    $request.auto_grabber_remaining_contents_json = [string]$projection.remaining_contents_json
    $request.auto_grabber_content_stack_count_before = [int]$projection.content_stack_count_before
    $request.auto_grabber_transferable_stack_count = [int]$projection.transferable_stack_count
    $request.auto_grabber_expected_stack_count_after = [int]$projection.expected_stack_count_after
    $request.auto_grabber_content_quantity_before = [int]$projection.content_quantity_before
    $request.auto_grabber_expected_transfer_quantity = [int]$projection.expected_transfer_quantity
    $request.auto_grabber_expected_quantity_after = [int]$projection.expected_quantity_after
    $request.auto_grabber_expected_location_action_return = [bool]$projection.expected_native_location_action_return
    $request.interaction_kind = [string]$projection.interaction_kind
    $request.expected_action_type = [string]$projection.expected_action_type
    $request.native_contract = [string]$projection.native_contract
    $request.max_movement_tiles = 512
    $result = Invoke-JsonPost $executeUrl $request
    Write-Json (Join-Path $runDirectory "execution.json") $result

    $after = Wait-Snapshot $snapshotUrl 30
    $afterObject = @($after.state.current_location.objects.value | Where-Object {
        [int]$_.tile_x -eq [int]$object.tile_x -and [int]$_.tile_y -eq [int]$object.tile_y
    }) | Select-Object -First 1
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        $result.training_impact_scope -eq "policy_training" -and
        $result.observed_effect -match "transferred_stacks=$([int]$projection.transferable_stack_count)" -and
        $result.observed_effect -match "transferred_quantity=$([int]$projection.expected_transfer_quantity)" -and
        $null -ne $afterObject -and
        [string]$afterObject.qualified_item_id -eq "(BC)165" -and
        [int]$afterObject.auto_grabber_collection.content_stack_count_before -eq [int]$projection.expected_stack_count_after -and
        [int]$afterObject.auto_grabber_collection.content_quantity_before -eq [int]$projection.expected_quantity_after -and
        [int]$after.state.player.current_tool_index.value -eq [int]$safe.current_tool_index
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_auto_grabber_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        execution_status = $result.status
        verification = $result.primitive_verification_status
        expected_transfer_stack_count = [int]$projection.transferable_stack_count
        expected_transfer_quantity = [int]$projection.expected_transfer_quantity
        expected_remaining_stack_count = [int]$projection.expected_stack_count_after
        expected_remaining_quantity = [int]$projection.expected_quantity_after
        observed_effect = $result.observed_effect
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
