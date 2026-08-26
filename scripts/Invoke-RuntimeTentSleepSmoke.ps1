param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-tent-sleep-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [int] $StartupTimeoutSeconds = 180
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 300
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.temporary_sleep.status -in @("available", "derived") -and
                $snapshot.state.current_location.large_terrain_features.status -in @("available", "derived") -and
                $snapshot.state.menus.tent_sleep_prompt_context.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Tent sleep snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-tent-sleep"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI executable not found: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Tent sleep smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-tent-sleep\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru

    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    Write-Json (Join-Path $artifactDirectory "snapshot-initial.json") $initial
    $fixtureRequest = New-Request $initial "debug.setup_tent_placement_target" "$RunId.fixture"
    $fixtureRequest["direction"] = 1
    $fixture = Invoke-Post $executeUrl $fixtureRequest
    Write-Json (Join-Path $artifactDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Tent fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $beforePlacement = Wait-World $snapshotUrl 60
    $placement = $beforePlacement.state.player.tent_placement.value
    $kit = @($placement.rows | Where-Object { [string]$_.qualified_item_id -eq "(O)TentKit" -and [int]$_.stack -gt 0 }) | Select-Object -First 1
    if ($null -eq $kit) { throw "Transparent Tent Kit row unavailable after fixture." }
    $placeRequest = New-Request $beforePlacement "executor.place_tent" "$RunId.place"
    $placeRequest["target_tile_x"] = [int]$fixture.target_tile_x
    $placeRequest["target_tile_y"] = [int]$fixture.target_tile_y
    $placeRequest["stand_tile_x"] = [int]$fixture.tent_stand_tile_x
    $placeRequest["stand_tile_y"] = [int]$fixture.tent_stand_tile_y
    $placeRequest["direction"] = 1
    $placeRequest["tent_rectangle_x"] = [int]$fixture.tent_rectangle_x
    $placeRequest["tent_rectangle_y"] = [int]$fixture.tent_rectangle_y
    $placeRequest["tent_rectangle_width"] = [int]$fixture.tent_rectangle_width
    $placeRequest["tent_rectangle_height"] = [int]$fixture.tent_rectangle_height
    $placeRequest["tent_anchor_tile_x"] = [int]$fixture.tent_anchor_tile_x
    $placeRequest["tent_anchor_tile_y"] = [int]$fixture.tent_anchor_tile_y
    $placeRequest["location_id"] = [string]$beforePlacement.state.player.location_id.value
    $placeRequest["inventory_slot_index"] = [int]$kit.inventory_slot_index
    $placeRequest["qualified_item_id"] = "(O)TentKit"
    $placeRequest["native_contract"] = [string]$placement.native_runtime_contract
    $placeRequest["max_movement_tiles"] = 512
    $placed = Invoke-Post $executeUrl $placeRequest
    Write-Json (Join-Path $artifactDirectory "placement-result.json") $placed
    if ($placed.status -ne "applied" -or $placed.primitive_verification_status -ne "verified") {
        throw "Native Tent placement failed: $(@($placed.block_reasons) -join ',')"
    }

    $beforeSleep = Wait-World $snapshotUrl 60
    Write-Json (Join-Path $artifactDirectory "snapshot-before-sleep.json") $beforeSleep
    $tent = @($beforeSleep.state.current_location.large_terrain_features.value | Where-Object {
        $_.is_tent -eq $true -and [int]$_.tile_x -eq [int]$fixture.tent_anchor_tile_x -and [int]$_.tile_y -eq [int]$fixture.tent_anchor_tile_y
    }) | Select-Object -First 1
    if ($null -eq $tent) { throw "Placed Tent transparent handoff unavailable." }

    $sleepRequest = New-Request $beforeSleep "recovery.sleep_in_tent" "$RunId.sleep"
    $sleepRequest["target_tile_x"] = [int]$tent.sleep_interaction_tile_x
    $sleepRequest["target_tile_y"] = [int]$tent.sleep_interaction_tile_y
    $sleepRequest["stand_tile_x"] = [int]$tent.canonical_sleep_stand_tile_x
    $sleepRequest["stand_tile_y"] = [int]$tent.canonical_sleep_stand_tile_y
    $sleepRequest["direction"] = [int]$tent.canonical_sleep_facing_direction
    $sleepRequest["location_id"] = [string]$tent.sleep_location_id
    $sleepRequest["target_runtime_type"] = [string]$tent.runtime_type
    $sleepRequest["native_contract"] = "GameLocation.checkAction->Tent.performUseAction->SleepTent_Yes->startSleep->CanWakeUpHere(sleptInTemporaryBed)->Tent.dayUpdate/tickUpdate"
    $sleepRequest["max_movement_tiles"] = 512
    $sleep = Invoke-Post $executeUrl $sleepRequest
    Write-Json (Join-Path $artifactDirectory "sleep-result.json") $sleep

    $after = Wait-World $snapshotUrl 120
    Write-Json (Join-Path $artifactDirectory "snapshot-after-sleep.json") $after
    $afterTent = @($after.state.current_location.large_terrain_features.value | Where-Object {
        $_.is_tent -eq $true -and [int]$_.tile_x -eq [int]$tent.tile_x -and [int]$_.tile_y -eq [int]$tent.tile_y
    }) | Select-Object -First 1
    $temporary = $after.state.player.temporary_sleep.value
    $passed = $sleep.status -eq "applied" -and $sleep.primitive_verification_status -eq "verified" -and
        [int]$after.state.time.total_days.value -eq ([int]$beforeSleep.state.time.total_days.value + 1) -and
        [string]$after.state.player.location_id.value -eq [string]$tent.sleep_location_id -and
        [int]$after.state.player.tile_x.value -eq [int]$tent.canonical_sleep_stand_tile_x -and
        [int]$after.state.player.tile_y.value -eq [int]$tent.canonical_sleep_stand_tile_y -and
        [string]$temporary.last_sleep_location -eq [string]$tent.sleep_location_id -and
        [int]$temporary.last_sleep_point_x -eq [int]$tent.canonical_sleep_stand_tile_x -and
        [int]$temporary.last_sleep_point_y -eq [int]$tent.canonical_sleep_stand_tile_y -and
        $temporary.slept_in_temporary_bed -eq $false -and $null -eq $afterTent -and
        $after.state.menus.tent_sleep_prompt_context.value.prompt_open -eq $false
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_tent_sleep_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        location_id = [string]$tent.sleep_location_id
        tent_anchor = "$($tent.tile_x),$($tent.tile_y)"
        wake_tile = "$($after.state.player.tile_x.value),$($after.state.player.tile_y.value)"
        total_days_before = [int]$beforeSleep.state.time.total_days.value
        total_days_after = [int]$after.state.time.total_days.value
        execution_status = [string]$sleep.status
        verification_status = [string]$sleep.primitive_verification_status
        verification_reasons = @($sleep.primitive_verification_reasons)
        temporary_bed_flag_after = [bool]$temporary.slept_in_temporary_bed
        tent_present_after = $null -ne $afterTent
        installable_full_snapshot = (Join-Path $artifactDirectory "snapshot-after-sleep.json")
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime Tent sleep smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if ($null -ne $game -and -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
    }
}
