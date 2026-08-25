param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-sign-display-item-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 180
)
$ErrorActionPreference = "Stop"

function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds); $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 60
            if ($snapshot.state.current_location.objects.status -in @("available", "derived")) { return $snapshot }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for sign display snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-sign-display-item"
        queue_item_id = $QueueItemId; before_state_hash = [string]$Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"; $smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"; $snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already in use." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-sign-display-item\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $target = Join-Path $smokeModsPath $modName; New-Item -ItemType Directory -Force -Path $target | Out-Null
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
    $families = @("ordinary_object", "big_object", "hat", "ring", "furniture", "tool_default")
    $cases = @(); $targetX = $null; $targetY = $null
    for ($caseIndex = 0; $caseIndex -lt $families.Count; $caseIndex++) {
        $family = $families[$caseIndex]
        $fixtureRequest = New-Request $initial "debug.setup_sign_display_item_target" "$RunId.fixture.$caseIndex"
        $fixtureRequest.sign_display_fixture_family = $family; $fixtureRequest.quantity = 1
        if ($null -ne $targetX) { $fixtureRequest.target_tile_x = $targetX; $fixtureRequest.target_tile_y = $targetY }
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied") { throw "Sign display fixture failed for $family." }
        $targetX = [int]$fixture.target_tile_x; $targetY = [int]$fixture.target_tile_y
        Start-Sleep -Milliseconds 350
        $before = Wait-World $snapshotUrl 90
        $target = @($before.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $targetX -and [int]$_.tile_y -eq $targetY
        }) | Select-Object -First 1
        $assignment = $target.sign_state.display_assignment
        if ([string]$assignment.status -ne "ready") { throw "Sign display projection unavailable for $family." }
        $sourceRuntime = ([string]$fixture.observed_effect -split ";" | Where-Object { $_ -like "source_runtime_type=*" }) -replace "^source_runtime_type=", ""
        $sourceQid = ([string]$fixture.observed_effect -split ";" | Where-Object { $_ -like "qualified_item_id=*" }) -replace "^qualified_item_id=", ""
        $row = @($assignment.inventory_rows | Where-Object {
            [string]$_.source_runtime_type -eq $sourceRuntime -and [string]$_.qualified_item_id -eq $sourceQid
        }) | Select-Object -First 1
        if ($null -eq $row) { throw "Source row missing for $family ($sourceRuntime)." }

        $apply = New-Request $before "executor.set_sign_display_item" "$RunId.apply.$caseIndex"
        $apply.location_id = [string]$assignment.target_location; $apply.target_tile_x = $targetX; $apply.target_tile_y = $targetY
        $apply.target_runtime_type = [string]$assignment.target_runtime_type; $apply.inventory_slot_index = [int]$row.inventory_slot_index
        $apply.sign_display_target_qualified_item_id = [string]$assignment.target_qualified_item_id
        $apply.sign_display_target_state_sha256 = [string]$assignment.target_state_sha256
        $apply.expected_stack_before = [int]$row.stack; $apply.item_id = [string]$row.item_id; $apply.qualified_item_id = [string]$row.qualified_item_id
        $apply.native_contract = [string]$assignment.native_contract; $apply.max_movement_tiles = 512
        $apply.sign_display_source_runtime_type = [string]$row.source_runtime_type
        $apply.sign_display_source_quality = [int]$row.quality; $apply.sign_display_source_state_sha256 = [string]$row.source_state_sha256
        $apply.sign_expected_display_type = [int]$row.expected_display_type
        $apply.sign_display_target_projection_fingerprint = [string]$assignment.target_projection_fingerprint
        $apply.sign_previous_display_item_qualified_item_id = [string]$assignment.previous_display_item_qualified_item_id
        $apply.sign_previous_display_item_runtime_type = [string]$assignment.previous_display_item_runtime_type
        $apply.sign_previous_display_item_state_sha256 = [string]$assignment.previous_display_item_state_sha256
        $apply.sign_previous_display_type = [int]$assignment.previous_display_type
        $apply.sign_replace_existing_display = [bool]$assignment.replace_existing_display
        $apply.sign_allow_replace_existing_display = [bool]$assignment.replace_existing_display
        $result = Invoke-Post $executeUrl $apply
        Start-Sleep -Milliseconds 350
        $after = Wait-World $snapshotUrl 90
        $afterTarget = @($after.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $targetX -and [int]$_.tile_y -eq $targetY
        }) | Select-Object -First 1
        $afterRow = @($afterTarget.sign_state.display_assignment.inventory_rows | Where-Object {
            [int]$_.inventory_slot_index -eq [int]$row.inventory_slot_index
        }) | Select-Object -First 1
        $transparent = [string]$afterTarget.sign_state.display_item.qualified_item_id -eq [string]$row.qualified_item_id -and
            [int]$afterTarget.sign_state.display_item.stack -eq 1 -and
            [int]$afterTarget.sign_state.display_type -eq [int]$row.expected_display_type -and
            [string]$afterRow.source_state_sha256 -eq [string]$row.source_state_sha256 -and [int]$afterRow.stack -eq [int]$row.stack
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $transparent
        $cases += [ordered]@{ family = $family; qualified_item_id = [string]$row.qualified_item_id
            source_runtime_type = [string]$row.source_runtime_type; expected_display_type = [int]$row.expected_display_type
            replacement_case = [bool]$assignment.replace_existing_display; execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status; observed_effect = [string]$result.observed_effect
            block_reasons = @($result.block_reasons); transparent_verified = $transparent
            status = if ($passed) { "passed" } else { "failed" } }
        $cases | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "progress.json") -Encoding utf8
        if (-not $passed) { throw "Native sign display assignment failed for $family." }
        $initial = $after
    }
    $summary = [ordered]@{ schema_version = "stardewai.runtime_sign_display_item_smoke.v1"; status = "passed"
        run_id = $RunId; save_slot = $SaveSlot; passed_case_count = $cases.Count; total_case_count = $cases.Count; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], "Process") }
    if ($null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue }
}
