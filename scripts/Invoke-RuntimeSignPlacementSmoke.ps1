param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-sign-placement-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
            if ($snapshot.state.player.sign_placement.status -in @("available", "derived") -and
                $snapshot.state.current_location.objects.status -in @("available", "derived")) { return $snapshot }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for sign snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-sign-placement"
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-sign-placement\" + $RunId)
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
    $catalog = @($initial.state.player.sign_placement.value.sign_catalog | Where-Object {
        [string]$_.placement_kind -in @("display_item_sign", "text_sign") -and [bool]$_.is_placeable
    })
    if ($catalog.Count -eq 0) { throw "Live sign catalog is empty." }

    $cases = @()
    for ($caseIndex = 0; $caseIndex -lt $catalog.Count; $caseIndex++) {
        $catalogRow = $catalog[$caseIndex]; $qid = [string]$catalogRow.qualified_item_id
        $fixtureRequest = New-Request $initial "debug.setup_sign_placement_target" "$RunId.fixture.$caseIndex"
        $fixtureRequest.qualified_item_id = $qid
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied") { throw "Sign fixture failed for $qid." }
        Start-Sleep -Milliseconds 350
        $before = Wait-World $snapshotUrl 90; $placement = $before.state.player.sign_placement.value
        $row = @($placement.rows | Where-Object { [string]$_.qualified_item_id -eq $qid -and [int]$_.stack -gt 0 }) | Select-Object -First 1
        $location = @($row.locations | Where-Object { [bool]$_.location_is_current }) | Select-Object -First 1
        $targetX = [int]$fixture.target_tile_x; $targetY = [int]$fixture.target_tile_y
        $range = @($location.static_legal_tile_ranges | Where-Object {
            [int]$_.y -eq $targetY -and [int]$_.start_x -le $targetX -and [int]$_.end_x -ge $targetX
        }) | Select-Object -First 1
        if ($null -eq $row -or $null -eq $location -or $null -eq $range) { throw "Transparent sign binding missing for $qid." }

        $apply = New-Request $before "executor.place_sign" "$RunId.place.$caseIndex"
        $apply.location_id = [string]$location.location_id; $apply.target_tile_x = $targetX; $apply.target_tile_y = $targetY
        $apply.inventory_slot_index = [int]$row.inventory_slot_index; $apply.expected_stack_before = [int]$row.stack
        $apply.item_id = [string]$row.item_id; $apply.qualified_item_id = $qid
        $apply.target_runtime_type = [string]$row.expected_placed_runtime_type; $apply.native_contract = [string]$placement.native_runtime_contract
        $apply.sign_placement_kind = [string]$row.placement_kind; $apply.sign_expected_passable = [bool]$row.expected_passable
        $apply.sign_expected_display_item_empty = [bool]$row.expected_display_item_empty
        $apply.sign_expected_display_type = [int]$row.expected_display_type; $apply.sign_expected_text = [string]$row.expected_sign_text
        $apply.sign_expected_show_next_index = [bool]$row.expected_show_next_index; $apply.max_movement_tiles = 512
        $result = Invoke-Post $executeUrl $apply
        Start-Sleep -Milliseconds 350
        $after = Wait-World $snapshotUrl 90
        $placed = @($after.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $targetX -and [int]$_.tile_y -eq $targetY -and [string]$_.qualified_item_id -eq $qid
        }) | Select-Object -First 1
        $transparent = $null -ne $placed -and [string]$placed.type -eq [string]$row.expected_placed_runtime_type -and
            [string]$placed.sign_state.placement_kind -eq [string]$row.placement_kind -and
            [bool]$placed.sign_state.is_passable -eq $false
        if ([string]$row.placement_kind -eq "display_item_sign") {
            $transparent = $transparent -and $null -eq $placed.sign_state.display_item -and [int]$placed.sign_state.display_type -eq 0
        } else {
            $transparent = $transparent -and [string]::IsNullOrEmpty([string]$placed.sign_state.sign_text) -and [bool]$placed.sign_state.show_next_index
        }
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $transparent
        $cases += [ordered]@{ qualified_item_id = $qid; placement_kind = [string]$row.placement_kind
            runtime_type = [string]$row.expected_placed_runtime_type; target_tile = "$targetX,$targetY"
            execution_status = [string]$result.status; verification_status = [string]$result.primitive_verification_status
            observed_effect = [string]$result.observed_effect; block_reasons = @($result.block_reasons)
            transparent_verified = $transparent; status = if ($passed) { "passed" } else { "failed" } }
        $cases | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "progress.json") -Encoding utf8
        if (-not $passed) { throw "Native sign placement failed for $qid." }
        $initial = $after
    }
    $summary = [ordered]@{ schema_version = "stardewai.runtime_sign_placement_smoke.v1"; status = "passed"
        run_id = $RunId; save_slot = $SaveSlot; live_catalog_count = $catalog.Count
        passed_case_count = $cases.Count; total_case_count = $cases.Count; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], "Process") }
    if ($null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue }
}
