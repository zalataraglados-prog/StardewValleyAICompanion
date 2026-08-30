[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-jukebox-selection-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}
function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}
function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Wait-Snapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $snapshotUrl 30
            if ($snapshot.save_id.status -in @("available", "derived")) { return $snapshot }
            $lastStatus = "save=$($snapshot.save_id.status)"
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for jukebox snapshot. Last status: $lastStatus"
}
function Wait-JukeboxReady([int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.player.jukebox_selection
        $jukebox = $field.value
        if ($field.status -in @("available", "derived") -and
            $jukebox.projection_status -eq "complete_locked_base_1.6.15" -and
            $jukebox.service_status -eq "ready" -and
            @($jukebox.action_tiles).Count -gt 0 -and @($jukebox.tracks).Count -gt 0) { return $snapshot }
        Start-Sleep -Milliseconds 300
    }
    throw "Jukebox fixture did not become ready."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-jukebox-selection"
        queue_item_id = $ItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}
function Invoke-Setup($Snapshot) {
    $setup = New-Request $Snapshot "debug.setup_jukebox_selection" "$RunId.setup"
    $result = Invoke-JsonPost $executeUrl $setup
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Jukebox fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    Start-Sleep -Seconds 1
}
function New-JukeboxRequest($Snapshot, $Track) {
    $jukebox = $Snapshot.state.player.jukebox_selection.value
    $target = @($jukebox.action_tiles) | Select-Object -First 1
    $request = New-Request $Snapshot "executor.choose_jukebox_track" "$RunId.track.$($Track.track_index)"
    $request.location_id = [string]$jukebox.location_id
    $request.target_location = [string]$jukebox.location_id
    $request.target_tile_x = [int]$target.tile_x
    $request.target_tile_y = [int]$target.tile_y
    $request.stand_tile_x = [int]$Snapshot.state.player.tile_x.value
    $request.stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.jukebox_track_id = [string]$Track.track_id
    $request.jukebox_reason = "isolated EVD-313 smoke"
    $request.confirm_jukebox_track = $true
    $request.jukebox_projection_fingerprint = [string]$jukebox.projection_fingerprint
    $request.jukebox_track_index = [int]$Track.track_index
    $request.jukebox_unlocked_track_count = [int]$jukebox.unlocked_track_count
    $request.jukebox_default_track_before = [string]$jukebox.default_music_track
    $request.jukebox_requested_track_before = [string]$jukebox.requested_music_track
    $request.jukebox_current_song_before = [string]$jukebox.current_song_name
    $request.jukebox_green_rain_override = [bool]$jukebox.green_rain_native_override_active
    $request.jukebox_action_raw = [string]$target.action_raw
    $request.expected_menu_type_after = "ChooseFromListMenu"
    $request.expected_menu_kind = "jukebox"
    $request.native_contract = [string]$jukebox.native_contract
    $request.max_movement_tiles = 512
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-jukebox-selection\" + $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
if (-not (Test-Path -LiteralPath (Join-Path $savesPath $SaveSlot) -PathType Container)) { throw "Isolated save not found: $SaveSlot" }
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
$loadedModAllowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_DEDICATED_HOST_MODE", "STARDEWAI_DEDICATED_HOST_RUN_ID", "STARDEWAI_DEDICATED_HOST_ACTOR_ID",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_DEDICATED_HOST_MODE = "1"
    $env:STARDEWAI_DEDICATED_HOST_RUN_ID = $RunId
    $env:STARDEWAI_DEDICATED_HOST_ACTOR_ID = "ai_host.main"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Wait-Snapshot $StartupTimeoutSeconds
    Invoke-Setup $initial

    $firstSnapshot = Wait-JukeboxReady
    $tracks = @($firstSnapshot.state.player.jukebox_selection.value.tracks | Where-Object { $_.selectable_now })
    if ($tracks.Count -lt 1) { throw "No selectable native jukebox track exists." }
    $firstTrack = $tracks | Select-Object -First 1
    $firstResult = Invoke-JsonPost $executeUrl (New-JukeboxRequest $firstSnapshot $firstTrack)
    $firstPassed = $firstResult.status -eq "applied" -and
        $firstResult.observed_effect -match ("default_music_track=" + [regex]::Escape([string]$firstTrack.track_id) + "(?:;|$)")

    $lastSnapshot = Wait-JukeboxReady
    $lastTrack = @($lastSnapshot.state.player.jukebox_selection.value.tracks | Where-Object { $_.selectable_now }) | Select-Object -Last 1
    $lastResult = Invoke-JsonPost $executeUrl (New-JukeboxRequest $lastSnapshot $lastTrack)
    $lastPassed = $lastResult.status -eq "applied" -and
        $lastResult.observed_effect -match ("default_music_track=" + [regex]::Escape([string]$lastTrack.track_id) + "(?:;|$)")

    $driftSnapshot = Wait-JukeboxReady
    $driftTrack = @($driftSnapshot.state.player.jukebox_selection.value.tracks | Where-Object { $_.selectable_now }) | Select-Object -First 1
    $driftRequest = New-JukeboxRequest $driftSnapshot $driftTrack
    $driftRequest.jukebox_track_index = [int]$driftRequest.jukebox_track_index + 1
    $driftResult = Invoke-JsonPost $executeUrl $driftRequest
    $driftPassed = $driftResult.status -eq "blocked" -and
        @($driftResult.block_reasons) -contains "jukebox_selection_track_catalog_or_index_drifted"

    $cases = @(
        [ordered]@{ case = "first_native_track_index_zero"; passed = $firstPassed; result = $firstResult },
        [ordered]@{ case = "last_native_track_forward_click_sequence"; passed = $lastPassed; result = $lastResult },
        [ordered]@{ case = "forged_track_index_rejected"; passed = $driftPassed; result = $driftResult }
    )
    $finalSnapshot = Wait-JukeboxReady
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_jukebox_selection_smoke.v1"
        evidence_id = "EVD-313"
        run_id = $RunId
        status = if ($passedCount -eq 3) { "passed" } else { "failed" }
        expected_case_count = 3
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/3"; artifact = $runDirectory } | ConvertTo-Json -Depth 4
    if ($passedCount -ne 3) { throw "Runtime jukebox selection smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
