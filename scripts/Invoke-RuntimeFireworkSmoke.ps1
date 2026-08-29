param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-firework-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
                $snapshot.state.player.firework_placement.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for firework snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-firework"
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
        throw "Firework smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-firework\" + $RunId)
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
        [ordered]@{ qualified_item_id = "(O)893"; firework_type = 0; source_rect_x = 256 },
        [ordered]@{ qualified_item_id = "(O)894"; firework_type = 1; source_rect_x = 272 },
        [ordered]@{ qualified_item_id = "(O)895"; firework_type = 2; source_rect_x = 288 })) {
        $fixtureRequest = New-Request $snapshot "debug.setup_firework_target" "$RunId.fixture.$($variant.firework_type)"
        $fixtureRequest["qualified_item_id"] = $variant.qualified_item_id
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
            throw "Firework fixture failed for $($variant.qualified_item_id): $(@($fixture.block_reasons) -join ',')"
        }

        $before = Wait-World $snapshotUrl 60
        $context = $before.state.player.firework_placement.value
        $row = @($context.rows | Where-Object {
            [string]$_.qualified_item_id -eq [string]$variant.qualified_item_id -and [int]$_.stack_before -gt 0
        }) | Select-Object -First 1
        if ($null -eq $row) { throw "Transparent firework row missing for $($variant.qualified_item_id)" }

        $request = New-Request $before "executor.use_firework" "$RunId.launch.$($variant.firework_type)"
        $request["location_id"] = [string]$before.state.player.location_id.value
        $request["target_tile_x"] = [int]$fixture.target_tile_x
        $request["target_tile_y"] = [int]$fixture.target_tile_y
        $request["inventory_slot_index"] = [int]$row.inventory_slot_index
        $request["expected_stack_before"] = [int]$row.stack_before
        $request["qualified_item_id"] = [string]$row.qualified_item_id
        $request["expected_firework_type"] = [int]$row.firework_type
        $request["expected_firework_source_rect_x"] = [int]$row.source_rect_x
        $request["expected_firework_source_rect_y"] = [int]$row.source_rect_y
        $request["expected_firework_fuse_duration_ms"] = [int]$row.fuse_duration_ms
        $request["expected_firework_rocket_delay_ms"] = [int]$row.rocket_delay_ms
        $request["expected_firework_rocket_id_min"] = [int]$row.rocket_id_min
        $request["expected_firework_rocket_id_max"] = [int]$row.rocket_id_max
        $request["firework_acceleration_y_min"] = [string]$row.acceleration_y_min
        $request["firework_acceleration_y_max"] = [string]$row.acceleration_y_max
        $request["firework_acceleration_y_step"] = [string]$row.acceleration_y_step
        $request["firework_random_contract"] = [string]$row.random_outcome_contract
        $request["native_contract"] = [string]$row.native_contract
        $request["max_movement_tiles"] = 512
        $result = Invoke-Post $executeUrl $request
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified"
        $cases += [ordered]@{
            qualified_item_id = [string]$variant.qualified_item_id
            firework_type = [int]$variant.firework_type
            target_tile = "$($fixture.target_tile_x),$($fixture.target_tile_y)"
            execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status
            observed_effect = [string]$result.observed_effect
            status = if ($passed) { "passed" } else { "failed" }
        }
        if (-not $passed) { throw "Runtime firework failed for $($variant.qualified_item_id)" }
        Start-Sleep -Milliseconds 3500
        $snapshot = Wait-World $snapshotUrl 60
    }

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_firework_smoke.v1"
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
