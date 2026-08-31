[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-player-emote-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "save=$($snapshot.save_id.status)"
        } catch { $lastStatus = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for player emote snapshot. Last status: $lastStatus"
}
function Wait-EmoteReady([int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.player.emote
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and
            $field.value.service_status -eq "ready" -and
            @($field.value.emotes).Count -eq 22 -and
            @($field.value.emotes | Where-Object { $_.hidden }).Count -eq 4) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Player emote fixture did not become ready."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-player-emote"
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
function Invoke-Setup($Snapshot, [string] $CaseName) {
    $request = New-Request $Snapshot "debug.setup_player_emote" "$RunId.setup.$CaseName"
    $result = Invoke-JsonPost $executeUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Player emote fixture setup failed for $CaseName`: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    [pscustomobject]@{ Setup = $result; Snapshot = (Wait-EmoteReady 30) }
}
function New-EmoteRequest($Snapshot, [string] $EmoteKey, [string] $CaseName) {
    $projection = $Snapshot.state.player.emote.value
    $option = @($projection.emotes) | Where-Object { $_.emote_key -eq $EmoteKey } | Select-Object -First 1
    if ($null -eq $option) { throw "Typed player emote option missing for $CaseName." }
    $request = New-Request $Snapshot "executor.perform_emote" "$RunId.execute.$CaseName"
    $request.emote_key = [string]$option.emote_key
    $request.emote_reason = "EVD-320 isolated native runtime verification"
    $request.confirm_emote = $true
    $request.emote_projection_fingerprint = [string]$projection.projection_fingerprint
    $request.emote_option_fingerprint = [string]$option.option_fingerprint
    $request.emote_index = [int]$option.emote_index
    $request.emote_icon_index = [int]$option.icon_index
    $request.emote_has_animation = [bool]$option.has_animation
    $request.emote_animation_facing_direction = [int]$option.animation_facing_direction
    $request.emote_animation_duration_milliseconds = [int]$option.animation_duration_milliseconds
    $request.emote_hidden = [bool]$option.hidden
    $request.emote_performed_entry_before = [bool]$option.performed_entry_present
    $request.emote_performed_value_before = [bool]$option.performed_value
    $request.emote_player_id = [long]$projection.player_id
    $request.emote_language_code = [int]$projection.language_code
    $request.emote_network_role = [string]$projection.network_role
    $request.emote_chat_input_width_pixels = [int]$projection.chat_input_width_pixels
    $request.emote_chat_input_content_width_pixels = [int]$projection.chat_input_content_width_pixels
    $request.emote_native_input = "/emote " + [string]$option.emote_key
    $request.native_contract = [string]$projection.native_contract
    $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
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
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach or start."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-player-emote\" + $RunId)
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

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
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
    $snapshot = Wait-Snapshot $StartupTimeoutSeconds
    $cases = @()

    $forged = Invoke-Setup $snapshot "forged-fingerprint"
    $forgedRequest = New-EmoteRequest $forged.Snapshot "happy" "forged-fingerprint"
    $forgedRequest.emote_projection_fingerprint = ("f" * 64)
    $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
    $forgedPassed = $forgedResult.status -eq "blocked" -and @($forgedResult.block_reasons) -contains "emote_projection_fingerprint_drifted"
    $cases += [ordered]@{ case = "forged_projection_fingerprint_rejected"; passed = $forgedPassed; result = $forgedResult }

    $emoteKeys = @("happy", "sad", "heart", "exclamation", "note", "sleep", "game", "question", "x", "pause", "blush", "angry", "yes", "no", "sick", "laugh", "surprised", "hi", "taunt", "uh", "music", "jar")
    foreach ($key in $emoteKeys) {
        $setup = Invoke-Setup (Wait-Snapshot 30) $key
        $request = New-EmoteRequest $setup.Snapshot $key $key
        $result = Invoke-JsonPost $executeUrl $request
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [string]$result.emote_key -eq $key -and [bool]$result.emote_performed_entry_after -and
            [bool]$result.emote_performed_value_after -and [bool]$result.emote_icon_receipt_observed -and
            [bool]$result.emote_animation_receipt_observed -and [bool]$result.emote_native_command_receipt_verified
        $cases += [ordered]@{ case = "native_emote_$key"; passed = $passed; result = $result }
    }

    $finalSnapshot = Wait-Snapshot 30
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_player_emote_smoke.v1"
        evidence_id = "EVD-320"
        run_id = $RunId
        status = if ($passedCount -eq 23) { "passed" } else { "failed" }
        expected_case_count = 23
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/23"; artifact = $runDirectory } |
        ConvertTo-Json -Depth 4
    if ($passedCount -ne 23) { throw "Runtime player emote smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
