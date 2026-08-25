param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-furniture-placement-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180
)
$ErrorActionPreference = "Stop"

function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 60
            if ($snapshot.state.player.furniture_placement.status -in @("available", "derived") -and
                $snapshot.state.current_location.furniture.status -in @("available", "derived")) { return $snapshot }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for furniture snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-furniture-placement"
        queue_item_id = $QueueItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already in use." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-furniture-placement\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $target = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path (Join-Path $gameDirectory "Mods\$modName") "*") -Destination $target -Recurse -Force
}

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$saved = @{}; foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath; $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"; $env:SMAPI_MODS_PATH = $smokeModsPath
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $catalog = @($initial.state.player.furniture_placement.value.furniture_catalog | Where-Object {
        [string]$_.canonical_factory_status -eq "canonical_factory_available" -and -not [string]::IsNullOrWhiteSpace([string]$_.qualified_item_id)
    })
    if ($catalog.Count -eq 0) { throw "Live Data/Furniture catalog is empty." }

    $matrix = @()
    $matrix += $catalog | Group-Object canonical_runtime_type | ForEach-Object { $_.Group | Select-Object -First 1 }
    $matrix += $catalog | Group-Object furniture_type | ForEach-Object { $_.Group | Select-Object -First 1 }
    $matrix = @($matrix | Sort-Object qualified_item_id -Unique | ForEach-Object {
        [ordered]@{ row = $_; rotation_steps = 0; endpoint = "location_furniture" }
    })
    $fourWay = $catalog | Where-Object { [int]$_.rotations -eq 4 } | Select-Object -First 1
    if ($null -ne $fourWay) {
        foreach ($steps in @(1, 2, 3)) { $matrix += [ordered]@{ row = $fourWay; rotation_steps = $steps; endpoint = "location_furniture" } }
    }
    $tableItem = $catalog | Where-Object {
        [int]$_.default_tiles_wide -eq 1 -and [int]$_.default_tiles_high -eq 1 -and [bool]$_.is_ground_furniture -and [int]$_.furniture_type -notin @(5, 11)
    } | Select-Object -First 1
    if ($null -eq $tableItem) { throw "No canonical 1x1 table-placeable furniture candidate found." }
    $matrix += [ordered]@{ row = $tableItem; rotation_steps = 0; endpoint = "table_held_object" }

    $cases = @()
    for ($caseIndex = 0; $caseIndex -lt $matrix.Count; $caseIndex++) {
        $case = $matrix[$caseIndex]; $qid = [string]$case.row.qualified_item_id; $steps = [int]$case.rotation_steps
        $fixtureRequest = New-Request $initial "debug.setup_furniture_placement_target" "$RunId.fixture.$caseIndex"
        $fixtureRequest.qualified_item_id = $qid; $fixtureRequest.furniture_rotation_steps = $steps
        $fixtureRequest.furniture_placement_endpoint = [string]$case.endpoint
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied") { throw "Furniture fixture failed for $qid steps=$steps endpoint=$($case.endpoint)." }
        Start-Sleep -Milliseconds 350
        $before = Wait-World $snapshotUrl 90
        $placement = $before.state.player.furniture_placement.value
        $row = @($placement.rows | Where-Object { [string]$_.qualified_item_id -eq $qid -and [int]$_.stack -gt 0 }) | Select-Object -First 1
        $rotation = @($row.rotations | Where-Object { [int]$_.rotation_steps_from_inventory -eq $steps }) | Select-Object -First 1
        $targetX = [int]$fixture.target_tile_x; $targetY = [int]$fixture.target_tile_y
        $range = @($rotation.static_legal_tile_ranges | Where-Object {
            [int]$_.y -eq $targetY -and [int]$_.start_x -le $targetX -and [int]$_.end_x -ge $targetX
        }) | Select-Object -First 1
        if ($null -eq $row -or $null -eq $rotation -or $null -eq $range) { throw "Transparent furniture binding missing for $qid." }

        $apply = New-Request $before "executor.place_furniture" "$RunId.place.$caseIndex"
        $apply.location_id = [string]$rotation.location_id; $apply.target_tile_x = $targetX; $apply.target_tile_y = $targetY
        $apply.inventory_slot_index = [int]$row.inventory_slot_index; $apply.expected_stack_before = [int]$row.stack
        $apply.qualified_item_id = $qid; $apply.target_runtime_type = [string]$row.expected_placed_runtime_type
        $apply.native_contract = [string]$placement.native_runtime_contract
        $apply.furniture_inventory_rotation_before = [int]$row.inventory_current_rotation
        $apply.furniture_desired_rotation = [int]$rotation.desired_current_rotation; $apply.furniture_rotation_steps = $steps
        $apply.furniture_type = [int]$rotation.furniture_type; $apply.furniture_can_free_place = [bool]$rotation.can_free_place_furniture
        $apply.furniture_expected_passable = [bool]$range.expected_passable; $apply.furniture_placement_endpoint = [string]$range.placement_endpoint
        $apply.furniture_expected_anchor_x = $targetX + [int]$range.anchor_offset_x
        $apply.furniture_expected_anchor_y = $targetY + [int]$range.anchor_offset_y
        $apply.furniture_footprint_width = [int]$range.footprint_width; $apply.furniture_footprint_height = [int]$range.footprint_height
        $apply.furniture_table_index = [int]$range.table_index; $apply.furniture_table_tile_x = [int]$range.table_tile_x
        $apply.furniture_table_tile_y = [int]$range.table_tile_y; $apply.max_movement_tiles = 512
        $result = Invoke-Post $executeUrl $apply
        Start-Sleep -Milliseconds 350
        $after = Wait-World $snapshotUrl 90
        $transparent = $false
        if ([string]$range.placement_endpoint -eq "table_held_object") {
            $placed = @($after.state.current_location.furniture.value | Where-Object { [int]$_.index -eq [int]$range.table_index }) | Select-Object -First 1
            $transparent = [string]$placed.held_object_qualified_item_id -eq $qid
        } else {
            $placed = @($after.state.current_location.furniture.value | Where-Object {
                [string]$_.qualified_item_id -eq $qid -and [int]$_.tile_x -eq [int]$apply.furniture_expected_anchor_x -and
                [int]$_.tile_y -eq [int]$apply.furniture_expected_anchor_y -and [int]$_.current_rotation -eq [int]$rotation.desired_current_rotation
            }) | Select-Object -First 1
            $transparent = $null -ne $placed -and [string]$placed.runtime_type -eq [string]$row.expected_placed_runtime_type
        }
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $transparent
        $cases += [ordered]@{ qualified_item_id = $qid; runtime_type = [string]$row.expected_placed_runtime_type
            furniture_type = [int]$rotation.furniture_type; rotation_steps = $steps; endpoint = [string]$range.placement_endpoint
            target_tile = "$targetX,$targetY"; execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status; observed_effect = [string]$result.observed_effect
            block_reasons = @($result.block_reasons); transparent_verified = $transparent
            status = if ($passed) { "passed" } else { "failed" } }
        $cases | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "progress.json") -Encoding utf8
        if (-not $passed) { throw "Native furniture placement failed for $qid steps=$steps endpoint=$($range.placement_endpoint)." }
        $initial = $after
    }
    $summary = [ordered]@{ schema_version = "stardewai.runtime_furniture_placement_smoke.v1"; status = "passed"
        run_id = $RunId; save_slot = $SaveSlot; live_catalog_count = $catalog.Count; passed_case_count = $cases.Count
        total_case_count = $cases.Count; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], "Process") }
    if ($null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue }
}
