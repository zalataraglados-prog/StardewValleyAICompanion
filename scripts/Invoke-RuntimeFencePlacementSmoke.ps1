param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-fence-placement-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
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
                $snapshot.state.player.fence_placement.status -in @("available", "derived") -and
                $snapshot.state.current_location.objects.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for fence-placement snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-fence-placement"
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
        throw "Fence-placement smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-fence-placement\" + $RunId)
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
    foreach ($qualifiedItemId in @("(O)322", "(O)323", "(O)324", "(O)298", "(O)325")) {
        $fixtureRequest = New-Request $initial "debug.setup_fence_placement_target" "$RunId.fixture.$qualifiedItemId"
        $fixtureRequest["qualified_item_id"] = $qualifiedItemId
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Fence fixture failed for ${qualifiedItemId}: $(@($fixture.block_reasons) -join ',')"
        }
        $TargetTileX = [int]$fixture.target_tile_x
        $TargetTileY = [int]$fixture.target_tile_y

        Start-Sleep -Milliseconds 500
        $before = Wait-World $snapshotUrl 60
        $placement = $before.state.player.fence_placement.value
        $row = @($placement.rows | Where-Object {
            [string]$_.qualified_item_id -eq $qualifiedItemId -and [int]$_.stack -gt 0
        }) | Select-Object -First 1
        if ($null -eq $row) { throw "Transparent fence inventory row is missing for $qualifiedItemId" }
        $location = @($row.locations | Where-Object {
            [string]$_.location_id -eq [string]$before.state.player.location_id.value -and
            [string]$_.placement_probe_status -eq "native_legal_tiles_available"
        }) | Select-Object -First 1
        $range = @($location.static_legal_tile_ranges | Where-Object {
            [int]$_.y -eq $TargetTileY -and [int]$_.start_x -le $TargetTileX -and [int]$_.end_x -ge $TargetTileX
        }) | Select-Object -First 1
        if ($null -eq $range) { throw "Transparent fence target range is missing for $qualifiedItemId" }

        $applyRequest = New-Request $before "executor.place_fence" "$RunId.place.$qualifiedItemId"
        $applyRequest["location_id"] = [string]$before.state.player.location_id.value
        $applyRequest["inventory_slot_index"] = [int]$row.inventory_slot_index
        $applyRequest["expected_stack_before"] = [int]$row.stack
        $applyRequest["qualified_item_id"] = $qualifiedItemId
        $applyRequest["target_runtime_type"] = [string]$placement.placed_runtime_type
        $applyRequest["native_contract"] = [string]$placement.native_runtime_contract
        $applyRequest["fence_data_key"] = [string]$row.fence_data_key
        $applyRequest["expected_fence_is_gate"] = [bool]$row.is_gate
        $applyRequest["expected_fence_draw_sum"] = [int]$range.expected_draw_sum_after
        $applyRequest["expected_fence_gate_functional"] = [bool]$range.expected_gate_functional
        $applyRequest["expected_fence_health_min"] = [double]$row.expected_health_min
        $applyRequest["expected_fence_health_max"] = [double]$row.expected_health_max
        $applyRequest["expected_fence_max_health_min"] = [double]$row.expected_max_health_min
        $applyRequest["expected_fence_max_health_max"] = [double]$row.expected_max_health_max
        $applyRequest["max_movement_tiles"] = 512
        $result = Invoke-Post $executeUrl $applyRequest

        Start-Sleep -Milliseconds 500
        $after = Wait-World $snapshotUrl 60
        $placed = @($after.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $TargetTileX -and [int]$_.tile_y -eq $TargetTileY -and
            [string]$_.qualified_item_id -eq $qualifiedItemId -and [string]$_.type -eq "StardewValley.Fence"
        }) | Select-Object -First 1
        $transparentVerified = $null -ne $placed -and
            [string]$placed.fence_state.status -eq "available" -and
            [bool]$placed.fence_state.is_gate -eq [bool]$row.is_gate -and
            [int]$placed.fence_state.gate_position -eq 0 -and
            -not [bool]$placed.fence_state.is_passable -and
            [int]$placed.fence_state.draw_sum -eq [int]$range.expected_draw_sum_after
        $passed = $result.status -eq "applied" -and
            $result.primitive_verification_status -eq "verified" -and $transparentVerified
        $cases += [ordered]@{
            qualified_item_id = $qualifiedItemId
            target_tile = "$TargetTileX,$TargetTileY"
            is_gate = [bool]$row.is_gate
            expected_draw_sum = [int]$range.expected_draw_sum_after
            expected_gate_functional = [bool]$range.expected_gate_functional
            execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status
            transparent_state_verified = $transparentVerified
            status = if ($passed) { "passed" } else { "failed" }
        }
        if (-not $passed) { throw "Runtime fence placement failed for $qualifiedItemId" }
        $initial = $after
    }

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_fence_placement_smoke.v1"
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
