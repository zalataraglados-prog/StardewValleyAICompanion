[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "",
    [string]$RunId = ("runtime-mine-elevator-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$StartupTimeoutSeconds = 150
)

$ErrorActionPreference = "Stop"
$game = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $game "StardewModdingAPI.exe"
$saves = Join-Path $RuntimeRoot "saves"
$output = Join-Path $ProjectRoot (Join-Path "artifacts\runtime-mine-elevator" $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"

function Invoke-Post($body) {
    Invoke-RestMethod -Method Post -Uri $executeUrl -ContentType "application/json; charset=utf-8" `
        -Body ($body | ConvertTo-Json -Depth 32) -TimeoutSec 120
}

function Wait-Snapshot([int]$timeout) {
    $deadline = (Get-Date).AddSeconds($timeout)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 15
            if ($snapshot.save_id.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for loaded full snapshot."
}

function Wait-Condition([scriptblock]$predicate, [string]$description, [int]$timeout = 30) {
    $deadline = (Get-Date).AddSeconds($timeout)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 10
        if (& $predicate $snapshot) { return $snapshot }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $description."
}

function Request-Base($snapshot, [string]$id, [string]$option) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-mine-elevator"
        queue_item_id = $id
        before_state_hash = $snapshot.state_hash
        option_id = $option
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $saves
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-OpenElevator($snapshot, [string]$id) {
    $tile = @($snapshot.state.current_location.mine_elevator_action_tiles.value)[0]
    if ($null -eq $tile) { throw "No transparent MineElevator action tile in $($snapshot.state.player.location_id.value)." }
    $distance = [Math]::Abs([int]$snapshot.state.player.tile_x.value - [int]$tile.tile_x) +
        [Math]::Abs([int]$snapshot.state.player.tile_y.value - [int]$tile.tile_y)
    if ($distance -ne 1) {
        $adjacent = @(
            [pscustomobject]@{ x = [int]$tile.tile_x + 1; y = [int]$tile.tile_y },
            [pscustomobject]@{ x = [int]$tile.tile_x - 1; y = [int]$tile.tile_y },
            [pscustomobject]@{ x = [int]$tile.tile_x; y = [int]$tile.tile_y + 1 },
            [pscustomobject]@{ x = [int]$tile.tile_x; y = [int]$tile.tile_y - 1 }
        ) | Where-Object {
            $candidate = $_
            @($snapshot.state.locations.collision_grid.value.notable_tiles | Where-Object {
                [int]$_.tile_x -eq $candidate.x -and [int]$_.tile_y -eq $candidate.y -and $_.collision_blocked
            }).Count -eq 0
        } | Sort-Object @{ Expression = {
            [Math]::Abs([int]$snapshot.state.player.tile_x.value - $_.x) +
                [Math]::Abs([int]$snapshot.state.player.tile_y.value - $_.y)
        } }
        $stand = @($adjacent)[0]
        if ($null -eq $stand) { throw "No collision-clear adjacent elevator stand tile." }
        $move = Request-Base $snapshot "$id.approach" "executor.move_to_tile"
        $move.target_tile_x = [int]$stand.x
        $move.target_tile_y = [int]$stand.y
        $move.max_crops = 512
        $moveResult = Invoke-Post $move
        if ($moveResult.status -ne "applied" -or $moveResult.primitive_verification_status -ne "verified") {
            throw "Elevator approach failed: $(@($moveResult.block_reasons) -join ',')"
        }
        $targetX = [int]$stand.x
        $targetY = [int]$stand.y
        $snapshot = Wait-Condition { param($s) [int]$s.state.player.tile_x.value -eq $targetX -and [int]$s.state.player.tile_y.value -eq $targetY } "elevator adjacent stand tile"
    }
    $request = Request-Base $snapshot $id "executor.interact"
    $request.target_tile_x = [int]$tile.tile_x
    $request.target_tile_y = [int]$tile.tile_y
    $request.interaction_kind = "map_action"
    $request.expected_action_type = "MineElevator"
    $result = Invoke-Post $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Elevator open failed: $(@($result.block_reasons) -join ',')"
    }
    return $result
}

function Invoke-SelectFloor($snapshot, [string]$id, [int]$floor) {
    $menu = $snapshot.state.menus.menu_specific_state.value
    if ($snapshot.state.menus.active_menu.value.type -ne "MineElevatorMenu" -or $menu.kind -ne "mine_elevator") {
        throw "Transparent MineElevatorMenu state is not active."
    }
    $entry = @($menu.entries | Where-Object { [int]$_.floor -eq $floor -and $_.selectable })
    if ($entry.Count -ne 1) { throw "Floor $floor is not exactly one selectable live entry." }
    $request = Request-Base $snapshot $id "executor.close_menu"
    $request.target_runtime_type = "MineElevatorMenu"
    $request.target_runtime_identity = [string]$menu.menu_identity_sha256
    $request.expected_mine_level_after = $floor
    $result = Invoke-Post $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Elevator floor $floor selection failed: $(@($result.block_reasons) -join ',')"
    }
    return $result
}

if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI missing: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $saves -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$previous = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_TEST_AUTO_LOAD = $env:STARDEWAI_TEST_AUTO_LOAD
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_MINE_ELEVATOR_FIXTURE = $env:STARDEWAI_MINE_ELEVATOR_FIXTURE
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
}
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $saves
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $saves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_MINE_ELEVATOR_FIXTURE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapi -WorkingDirectory $game -WindowStyle Hidden -PassThru
    $snapshot = Wait-Snapshot $StartupTimeoutSeconds

    $setup = Request-Base $snapshot "$RunId.setup" "debug.setup_mining_floor"
    $setup.mine_level = 25
    $setupResult = Invoke-Post $setup
    if ($setupResult.status -ne "applied") { throw "Elevator fixture failed: $(@($setupResult.block_reasons) -join ','); observed=$($setupResult.observed_effect)" }
    $floor25 = Wait-Condition { param($s) [int]$s.state.player.current_mine_level.value -eq 25 -and $s.state.menus.active_menu.value.type -eq "none" } "ordinary mine floor 25"
    $openFloor = Invoke-OpenElevator $floor25 "$RunId.open-floor-25"
    $floorMenu = Wait-Condition { param($s) $s.state.menus.active_menu.value.type -eq "MineElevatorMenu" } "MineElevatorMenu on floor 25"
    $toEntrance = Invoke-SelectFloor $floorMenu "$RunId.select-0" 0
    $entrance = Wait-Condition { param($s) $s.state.player.location_id.value -eq "Mine" -and [int]$s.state.player.tile_x.value -eq 17 -and [int]$s.state.player.tile_y.value -eq 4 } "Mine entrance after floor zero"
    if ($entrance.state.player.location_id.value -ne "Mine" -or [int]$entrance.state.player.tile_x.value -ne 17 -or [int]$entrance.state.player.tile_y.value -ne 4) {
        throw "Floor zero did not return to Mine 17,4."
    }
    $openEntrance = Invoke-OpenElevator $entrance "$RunId.open-entrance"
    $entranceMenu = Wait-Condition { param($s) $s.state.menus.active_menu.value.type -eq "MineElevatorMenu" } "MineElevatorMenu at entrance"
    $to25 = Invoke-SelectFloor $entranceMenu "$RunId.select-25" 25
    $final = Wait-Condition { param($s) [int]$s.state.player.current_mine_level.value -eq 25 -and $s.state.menus.active_menu.value.type -eq "none" } "ordinary mine floor 25 after entrance selection"

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_mine_elevator_smoke.v1"
        status = "pass"
        run_id = $RunId
        cases = @(
            [ordered]@{ name = "floor_25_to_mine_entrance"; open = $openFloor.status; select = $toEntrance.status; final_location = "Mine"; final_tile = "17,4" },
            [ordered]@{ name = "mine_entrance_to_floor_25"; open = $openEntrance.status; select = $to25.status; final_level = [int]$final.state.player.current_mine_level.value }
        )
    }
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 12
} finally {
    foreach ($name in $previous.Keys) { Set-Item -Path ("Env:" + $name) -Value $previous[$name] }
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
