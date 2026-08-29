param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-grass-placement-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.grass_placement.status -in @("available", "derived") -and
                $snapshot.state.current_location.terrain_features.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for grass-placement snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-grass-placement"
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
        throw "Grass-placement smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-grass-placement\" + $RunId)
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
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru

    $snapshot = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $cases = @()
    foreach ($variant in @(
        [ordered]@{ qualified_item_id = "(O)297"; expected_grass_type = 1 },
        [ordered]@{ qualified_item_id = "(O)BlueGrassStarter"; expected_grass_type = 7 })) {
        $fixtureRequest = New-Request $snapshot "debug.setup_grass_placement_target" "$RunId.fixture.$($variant.expected_grass_type)"
        $fixtureRequest["qualified_item_id"] = $variant.qualified_item_id
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Grass fixture failed for $($variant.qualified_item_id): $(@($fixture.block_reasons) -join ',')"
        }

        Start-Sleep -Milliseconds 500
        $before = Wait-World $snapshotUrl 60
        $placement = $before.state.player.grass_placement.value
        $row = @($placement.rows | Where-Object {
            [string]$_.qualified_item_id -eq [string]$variant.qualified_item_id -and [int]$_.stack -gt 0
        }) | Select-Object -First 1
        if ($null -eq $row) { throw "Transparent grass inventory row is missing for $($variant.qualified_item_id)" }
        $targetX = [int]$fixture.target_tile_x
        $targetY = [int]$fixture.target_tile_y
        $location = @($row.locations | Where-Object {
            [string]$_.location_id -eq [string]$before.state.player.location_id.value -and
            [string]$_.placement_probe_status -eq "native_legal_tiles_available"
        }) | Select-Object -First 1
        $range = @($location.static_legal_tile_ranges | Where-Object {
            [int]$_.y -eq $targetY -and [int]$_.start_x -le $targetX -and [int]$_.end_x -ge $targetX
        }) | Select-Object -First 1
        if ($null -eq $range) { throw "Transparent grass target range is missing for $($variant.qualified_item_id)" }

        $applyRequest = New-Request $before "executor.plant_grass" "$RunId.place.$($variant.expected_grass_type)"
        $applyRequest["location_id"] = [string]$before.state.player.location_id.value
        $applyRequest["target_tile_x"] = $targetX
        $applyRequest["target_tile_y"] = $targetY
        $applyRequest["inventory_slot_index"] = [int]$row.inventory_slot_index
        $applyRequest["expected_stack_before"] = [int]$row.stack
        $applyRequest["qualified_item_id"] = [string]$row.qualified_item_id
        $applyRequest["target_runtime_type"] = [string]$placement.placed_runtime_type
        $applyRequest["native_contract"] = [string]$placement.native_runtime_contract
        $applyRequest["expected_grass_type"] = [int]$row.expected_grass_type
        $applyRequest["expected_initial_number_of_weeds"] = [int]$row.expected_initial_number_of_weeds
        $applyRequest["grass_placement_sound"] = [string]$row.placement_sound
        $applyRequest["max_movement_tiles"] = 512
        $result = Invoke-Post $executeUrl $applyRequest

        Start-Sleep -Milliseconds 500
        $after = Wait-World $snapshotUrl 60
        $placed = @($after.state.current_location.terrain_features.value | Where-Object {
            [int]$_.tile_x -eq $targetX -and [int]$_.tile_y -eq $targetY -and
            [string]$_.type -eq "StardewValley.TerrainFeatures.Grass"
        }) | Select-Object -First 1
        $transparentVerified = $null -ne $placed -and
            [int]$placed.grass_type -eq [int]$variant.expected_grass_type -and [int]$placed.number_of_weeds -eq 4
        $passed = $result.status -eq "applied" -and
            $result.primitive_verification_status -eq "verified" -and $transparentVerified
        $cases += [ordered]@{
            qualified_item_id = [string]$variant.qualified_item_id
            expected_grass_type = [int]$variant.expected_grass_type
            target_tile = "$targetX,$targetY"
            execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status
            transparent_state_verified = $transparentVerified
            status = if ($passed) { "passed" } else { "failed" }
        }
        if (-not $passed) { throw "Runtime grass placement failed for $($variant.qualified_item_id)" }
        $snapshot = $after
    }

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_grass_placement_smoke.v1"
        status = if (@($cases | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        passed_case_count = @($cases | Where-Object { $_.status -eq "passed" }).Count
        total_case_count = $cases.Count
        cases = $cases
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
    }
}
