[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-mini-obelisk-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-mini-obelisk",
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

function Wait-MiniObeliskProjection([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot $Url 30
        $sources = @($snapshot.state.current_location.objects.value | Where-Object {
            $null -ne $_.mini_obelisk_use -and $_.mini_obelisk_use.status -eq "ready"
        })
        if ($sources.Count -eq 2) {
            return [pscustomobject]@{ Snapshot = $snapshot; Sources = $sources }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for exactly two ready Mini-Obelisk projections."
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-mini-obelisk"
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
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

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
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden -PassThru

    $current = Close-InitialMenus (Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds)
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $current
    $fixture = Invoke-JsonPost $executeUrl (New-Request $current "debug.setup_mini_obelisk" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture.json") $fixture
    if ($fixture.status -ne "applied") {
        throw "Mini-Obelisk fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $projected = Wait-MiniObeliskProjection $snapshotUrl 30
    $ready = $projected.Snapshot
    $source = @($projected.Sources | Sort-Object {
        [int]$_.mini_obelisk_use.native_pair_member_index
    })[0]
    $projection = $source.mini_obelisk_use
    $stand = @($projection.stand_tiles | Where-Object { $_.available }) | Select-Object -First 1
    $safe = $ready.state.player.safe_item_context.value
    if ($null -eq $stand -or $safe.safe_slot_kind -notin @("empty", "tool")) {
        throw "Mini-Obelisk source stand or safe toolbar slot is unavailable."
    }

    $request = New-Request $ready "movement.use_mini_obelisk" "$RunId.use"
    $request.location_id = [string]$ready.state.player.location_id.value
    $request.max_movement_tiles = 512
    $request.native_object_payload = [ordered]@{
        schema_version = "native_object_execution_payload.v2"
        kind = "mini_obelisk"
        target_tile_x = [int]$source.tile_x
        target_tile_y = [int]$source.tile_y
        stand_tile_x = [int]$stand.tile_x
        stand_tile_y = [int]$stand.tile_y
        safe_slot_index = [int]$safe.safe_slot_index
        safe_slot_kind = [string]$safe.safe_slot_kind
        restore_slot_index = [int]$safe.current_tool_index
        target_runtime_type = [string]$projection.target_runtime_type
        item_id = [string]$projection.canonical_item_id
        qualified_item_id = [string]$projection.canonical_qualified_item_id
        interaction_kind = [string]$projection.interaction_kind
        expected_action_type = [string]$projection.expected_action_type
        native_contract = [string]$projection.native_contract
        mini_obelisk = [ordered]@{
            pairMemberIndex = [int]$projection.native_pair_member_index
            pairFirstTileX = [int]$projection.native_pair_first_tile_x
            pairFirstTileY = [int]$projection.native_pair_first_tile_y
            pairSecondTileX = [int]$projection.native_pair_second_tile_x
            pairSecondTileY = [int]$projection.native_pair_second_tile_y
            destinationTileX = [int]$stand.native_destination_tile_x
            destinationTileY = [int]$stand.native_destination_tile_y
            landingTileX = [int]$stand.native_landing_tile_x
            landingTileY = [int]$stand.native_landing_tile_y
            expectedDelayMilliseconds = [int]$projection.expected_delay_milliseconds
            expectedLocationActionReturn = [bool]$projection.expected_native_location_action_return
        }
    }
    $result = Invoke-JsonPost $executeUrl $request
    Write-Json (Join-Path $runDirectory "execution.json") $result
    $after = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after

    $afterPair = @($after.state.current_location.objects.value | Where-Object {
        $_.qualified_item_id -eq "(BC)238"
    })
    $pairCoordinates = @(
        "$([int]$projection.native_pair_first_tile_x),$([int]$projection.native_pair_first_tile_y)",
        "$([int]$projection.native_pair_second_tile_x),$([int]$projection.native_pair_second_tile_y)")
    $afterCoordinates = @($afterPair | ForEach-Object { "$([int]$_.tile_x),$([int]$_.tile_y)" })
    $pairUnchanged = @($pairCoordinates | Where-Object { $_ -notin $afterCoordinates }).Count -eq 0
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        $result.training_impact_scope -eq "executor_calibration_only_not_strategy_desire" -and
        [int]$after.state.player.tile_x.value -eq [int]$stand.native_landing_tile_x -and
        [int]$after.state.player.tile_y.value -eq [int]$stand.native_landing_tile_y -and
        [int]$after.state.player.current_tool_index.value -eq [int]$safe.current_tool_index -and
        $pairUnchanged
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_mini_obelisk_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        execution_status = $result.status
        verification = $result.primitive_verification_status
        training_impact_scope = $result.training_impact_scope
        observed_effect = $result.observed_effect
        pair_coordinates_unchanged = $pairUnchanged
        expected_landing = "$([int]$stand.native_landing_tile_x),$([int]$stand.native_landing_tile_y)"
        observed_landing = "$([int]$after.state.player.tile_x.value),$([int]$after.state.player.tile_y.value)"
        loaded_mod_allowlist = $loadedModAllowlist
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 12
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name])
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
