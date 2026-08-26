param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-tent-placement-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.tent_placement.status -in @("available", "derived") -and
                $snapshot.state.current_location.large_terrain_features.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for Tent placement snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-tent-placement"
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
        throw "Tent placement smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-tent-placement\" + $RunId)
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

    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $cases = @()
    foreach ($direction in 0..3) {
        $caseId = "direction-$direction"
        $fixtureRequest = New-Request $initial "debug.setup_tent_placement_target" "$RunId.$caseId.fixture"
        $fixtureRequest["direction"] = $direction
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        Write-Json (Join-Path $artifactDirectory "$caseId-fixture-result.json") $fixture
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Tent placement fixture $caseId failed: $(@($fixture.block_reasons) -join ',')"
        }

        Start-Sleep -Milliseconds 500
        $before = Wait-World $snapshotUrl 60
        Write-Json (Join-Path $artifactDirectory "$caseId-snapshot-before.json") $before
        $placement = $before.state.player.tent_placement.value
        $kit = @($placement.rows | Where-Object {
            [string]$_.qualified_item_id -eq "(O)TentKit" -and [int]$_.stack -gt 0
        }) | Select-Object -First 1
        $location = @($kit.locations | Where-Object {
            [string]$_.location_id -eq [string]$before.state.player.location_id.value
        }) | Select-Object -First 1
        $directionRow = @($location.direction_rows | Where-Object { [int]$_.direction -eq $direction }) | Select-Object -First 1
        $standRange = @($directionRow.static_legal_stand_tile_ranges | Where-Object {
            [int]$_.y -eq [int]$fixture.tent_stand_tile_y -and
            [int]$_.start_x -le [int]$fixture.tent_stand_tile_x -and
            [int]$_.end_x -ge [int]$fixture.tent_stand_tile_x
        }) | Select-Object -First 1
        if ($null -eq $kit -or $null -eq $standRange) {
            throw "Transparent Tent projection does not contain fixture stand for $caseId."
        }

        $applyRequest = New-Request $before "executor.place_tent" "$RunId.$caseId.place"
        $applyRequest["target_tile_x"] = [int]$fixture.target_tile_x
        $applyRequest["target_tile_y"] = [int]$fixture.target_tile_y
        $applyRequest["stand_tile_x"] = [int]$fixture.tent_stand_tile_x
        $applyRequest["stand_tile_y"] = [int]$fixture.tent_stand_tile_y
        $applyRequest["direction"] = $direction
        $applyRequest["tent_rectangle_x"] = [int]$fixture.tent_rectangle_x
        $applyRequest["tent_rectangle_y"] = [int]$fixture.tent_rectangle_y
        $applyRequest["tent_rectangle_width"] = [int]$fixture.tent_rectangle_width
        $applyRequest["tent_rectangle_height"] = [int]$fixture.tent_rectangle_height
        $applyRequest["tent_anchor_tile_x"] = [int]$fixture.tent_anchor_tile_x
        $applyRequest["tent_anchor_tile_y"] = [int]$fixture.tent_anchor_tile_y
        $applyRequest["location_id"] = [string]$before.state.player.location_id.value
        $applyRequest["inventory_slot_index"] = [int]$kit.inventory_slot_index
        $applyRequest["qualified_item_id"] = "(O)TentKit"
        $applyRequest["native_contract"] = [string]$placement.native_runtime_contract
        $applyRequest["max_movement_tiles"] = 512
        $result = Invoke-Post $executeUrl $applyRequest
        Write-Json (Join-Path $artifactDirectory "$caseId-placement-result.json") $result

        Start-Sleep -Milliseconds 500
        $after = Wait-World $snapshotUrl 60
        Write-Json (Join-Path $artifactDirectory "$caseId-snapshot-after.json") $after
        $tent = @($after.state.current_location.large_terrain_features.value | Where-Object {
            $_.is_tent -eq $true -and
            [int]$_.tile_x -eq [int]$fixture.tent_anchor_tile_x -and
            [int]$_.tile_y -eq [int]$fixture.tent_anchor_tile_y
        }) | Select-Object -First 1
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            $null -ne $tent -and [int]$tent.health -eq 5 -and $tent.passable_for_player -eq $true -and
            $tent.passable_without_character -eq $false
        $cases += [ordered]@{
            case_id = $caseId
            direction = $direction
            status = if ($passed) { "passed" } else { "failed" }
            stand_tile = "$($fixture.tent_stand_tile_x),$($fixture.tent_stand_tile_y)"
            target_probe_tile = "$($fixture.target_tile_x),$($fixture.target_tile_y)"
            anchor_tile = "$($fixture.tent_anchor_tile_x),$($fixture.tent_anchor_tile_y)"
            execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status
            transparent_tent_handoff_verified = $null -ne $tent
        }
        $initial = $after
    }

    $passedCount = @($cases | Where-Object { $_.status -eq "passed" }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_tent_placement_smoke.v1"
        status = if ($passedCount -eq 4) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        passed_count = $passedCount
        total_count = 4
        cases = $cases
        installable_full_snapshot = (Join-Path $artifactDirectory "direction-3-snapshot-after.json")
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
    if ($passedCount -ne 4) { throw "Runtime Tent placement smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if ($null -ne $game -and -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
    }
}
