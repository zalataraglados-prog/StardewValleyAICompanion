param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-text-sign-editing-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
    throw "Timed out waiting for text-sign snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-text-sign-editing"
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

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-text-sign-editing\" + $RunId)
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
    $utf16Text = [string][char]0x6625 + [char]0x5B63 + [char]0x79CD + [char]0x5B50
    $caseSpecs = @(
        [ordered]@{ name = "first_write"; initial = ""; requested = "Seed storage"; expected = "Seed storage" },
        [ordered]@{ name = "trim_replacement"; initial = "Old label"; requested = "  Crop machines  "; expected = "Crop machines" },
        [ordered]@{ name = "clear_replacement"; initial = "Clear me"; requested = ""; expected = "" },
        [ordered]@{ name = "utf16_text"; initial = ""; requested = $utf16Text; expected = $utf16Text },
        [ordered]@{ name = "limit_60"; initial = "Old"; requested = ("x" * 60); expected = ("x" * 60) }
    )
    $cases = @(); $targetX = $null; $targetY = $null
    for ($caseIndex = 0; $caseIndex -lt $caseSpecs.Count; $caseIndex++) {
        $spec = $caseSpecs[$caseIndex]
        $fixtureRequest = New-Request $initial "debug.setup_text_sign_edit_target" "$RunId.fixture.$caseIndex"
        $fixtureRequest.text_sign_fixture_initial_text = [string]$spec.initial
        if ($null -ne $targetX) { $fixtureRequest.target_tile_x = $targetX; $fixtureRequest.target_tile_y = $targetY }
        $fixture = Invoke-Post $executeUrl $fixtureRequest
        if ($fixture.status -ne "applied") { throw "Text-sign fixture failed for $($spec.name)." }
        $targetX = [int]$fixture.target_tile_x; $targetY = [int]$fixture.target_tile_y
        Start-Sleep -Milliseconds 350
        $before = Wait-World $snapshotUrl 90
        $target = @($before.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $targetX -and [int]$_.tile_y -eq $targetY
        }) | Select-Object -First 1
        $editing = $target.sign_state.text_editing
        if ([string]$editing.status -ne "ready") { throw "Text-sign projection unavailable for $($spec.name)." }

        $apply = New-Request $before "executor.edit_text_sign" "$RunId.apply.$caseIndex"
        $apply.location_id = [string]$editing.target_location; $apply.target_tile_x = $targetX; $apply.target_tile_y = $targetY
        $apply.target_runtime_type = [string]$editing.target_runtime_type; $apply.max_movement_tiles = 512
        $apply.native_contract = [string]$editing.native_contract
        $apply.text_sign_target_projection_fingerprint = [string]$editing.target_projection_fingerprint
        $apply.text_sign_target_qualified_item_id = [string]$editing.target_qualified_item_id
        $apply.text_sign_target_state_sha256 = [string]$editing.target_state_sha256
        $apply.text_sign_raw_before = [string]$editing.raw_sign_text_before
        $apply.text_sign_display_before = [string]$editing.display_sign_text_before
        $apply.text_sign_show_next_index_before = [bool]$editing.show_next_index_before
        $apply.text_sign_replaces_existing_text = [bool]$editing.replaces_existing_text
        $apply.text_sign_allow_replace_existing_text = [bool]$editing.replaces_existing_text
        $apply.text_sign_requested_text = [string]$spec.requested
        $result = Invoke-Post $executeUrl $apply
        Start-Sleep -Milliseconds 350
        $after = Wait-World $snapshotUrl 90
        $afterTarget = @($after.state.current_location.objects.value | Where-Object {
            [int]$_.tile_x -eq $targetX -and [int]$_.tile_y -eq $targetY
        }) | Select-Object -First 1
        $afterEditing = $afterTarget.sign_state.text_editing
        $expectedShowNext = [string]::IsNullOrEmpty([string]$spec.expected)
        $transparent = [string]$afterEditing.raw_sign_text_before -eq [string]$spec.expected -and
            [string]$afterTarget.sign_state.sign_text -eq [string]$spec.expected -and
            [bool]$afterTarget.sign_state.show_next_index -eq $expectedShowNext -and
            [string]$afterEditing.target_state_sha256 -ne [string]$editing.target_state_sha256
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and $transparent
        $cases += [ordered]@{ name = [string]$spec.name; initial = [string]$spec.initial; requested = [string]$spec.requested
            expected = [string]$spec.expected; execution_status = [string]$result.status
            verification_status = [string]$result.primitive_verification_status; observed_effect = [string]$result.observed_effect
            block_reasons = @($result.block_reasons); transparent_verified = $transparent
            status = if ($passed) { "passed" } else { "failed" } }
        $cases | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "progress.json") -Encoding utf8
        if (-not $passed) { throw "Native text-sign editing failed for $($spec.name)." }
        $initial = $after
    }
    $summary = [ordered]@{ schema_version = "stardewai.runtime_text_sign_editing_smoke.v1"; status = "passed"
        run_id = $RunId; save_slot = $SaveSlot; passed_case_count = $cases.Count; total_case_count = $cases.Count; cases = $cases }
    $summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 32
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], "Process") }
    if ($null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue }
}
