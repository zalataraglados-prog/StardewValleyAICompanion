[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-multiplayer-chat-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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

function Get-Sha256([string] $Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
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
    throw "Timed out waiting for multiplayer chat snapshot. Last status: $lastStatus"
}

function Wait-ChatReady([int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $chatField = $snapshot.state.player.multiplayer_chat
        $chat = $chatField.value
        if ($chatField.status -in @("available", "derived") -and
            $chat.projection_status -eq "complete_locked_base_1.6.15" -and
            $chat.service_status -eq "ready" -and $chat.network_role -eq "server" -and
            @($chat.online_recipients).Count -eq 1) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Multiplayer chat fixture did not become ready."
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    $isExecutor = $OptionId.StartsWith("executor.", [System.StringComparison]::Ordinal)
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-multiplayer-chat"
        queue_item_id = $ItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = if ($isExecutor) { "dedicated_host_ai" } else { "training_singleplayer" }
        actor = if ($isExecutor) { "ai_host.main" } else { "training_farmer.main" }
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function New-ChatRequest($Snapshot, [string] $Scope, [string] $Message) {
    $chat = $Snapshot.state.player.multiplayer_chat.value
    $recipient = if ($Scope -eq "private") { @($chat.online_recipients) | Select-Object -First 1 } else { $null }
    if ($Scope -eq "private" -and $null -eq $recipient) { throw "No active private chat recipient is projected." }
    $request = New-Request $Snapshot "executor.send_multiplayer_chat" "$RunId.$Scope"
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.target_location = [string]$Snapshot.state.player.location_id.value
    $request.chat_scope = $Scope
    $request.chat_reason = "isolated EVD-311 smoke"
    $request.confirm_chat = $true
    $request.chat_message_text = $Message
    $request.chat_message_sha256 = Get-Sha256 $Message
    $request.chat_message_utf16_length = $Message.Length
    $request.chat_projection_fingerprint = [string]$chat.projection_fingerprint
    $request.chat_sender_player_id = [string]$chat.sender_player_id
    $request.chat_sender_display_name = [string]$chat.sender_display_name
    $request.chat_sender_default_color = [string]$chat.sender_default_chat_color
    $request.chat_language_code = [int]$chat.language_code
    $request.chat_network_role = [string]$chat.network_role
    $request.chat_recipient_player_id = if ($null -eq $recipient) { "" } else { [string]$recipient.player_id }
    $request.chat_recipient_display_name = if ($null -eq $recipient) { "" } else { [string]$recipient.display_name }
    $request.chat_recipient_command_name = if ($null -eq $recipient) { "" } else { [string]$recipient.native_command_name }
    $request.chat_expected_wire_recipient_id = if ($null -eq $recipient) { [string]$chat.all_players_recipient_id } else { [string]$recipient.player_id }
    $request.chat_expected_kind = if ($Scope -eq "private") { [int]$chat.private_chat_kind } else { [int]$chat.global_chat_kind }
    $request.chat_network_message_type = [int]$chat.network_message_type
    $request.chat_message_count_before = [int]$chat.chat_message_count
    $request.chat_message_limit = [int]$chat.chat_message_limit
    $request.chat_input_width_pixels = [int]$chat.input_width_pixels
    $request.chat_input_content_width_pixels = [int]$chat.input_content_width_pixels
    $request.chat_native_route = if ($Scope -eq "private") { "compiler_owned_message_private" } else { "global_all_players" }
    $request.native_contract = [string]$chat.native_contract
    return $request
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-multiplayer-chat\" + $RunId)
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
    $setup = New-Request $initial "debug.setup_multiplayer_chat" "$RunId.setup"
    $setupResult = Invoke-JsonPost $executeUrl $setup
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Multiplayer chat fixture setup failed: $($setupResult | ConvertTo-Json -Depth 32 -Compress)"
    }
    Start-Sleep -Seconds 2

    $cases = @()
    $globalSnapshot = Wait-ChatReady 30
    $globalResult = Invoke-JsonPost $executeUrl (New-ChatRequest $globalSnapshot "global" "EVD311 global hello")
    $globalPassed = $globalResult.status -eq "applied" -and $globalResult.chat_local_receipt_verified -eq $true
    $cases += [ordered]@{ case = "global_all_players"; passed = $globalPassed; result = $globalResult }

    $privateSnapshot = Wait-ChatReady 30
    $privateResult = Invoke-JsonPost $executeUrl (New-ChatRequest $privateSnapshot "private" "EVD311 private hello")
    $privatePassed = $privateResult.status -eq "applied" -and $privateResult.chat_local_receipt_verified -eq $true
    $cases += [ordered]@{ case = "private_exact_active_recipient"; passed = $privatePassed; result = $privateResult }

    $finalSnapshot = Wait-ChatReady 30
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_multiplayer_chat_smoke.v1"
        evidence_id = "EVD-311"
        run_id = $RunId
        status = if ($passedCount -eq 2) { "passed" } else { "failed" }
        expected_case_count = 2
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        remote_delivery_claim = "not_fabricated_sender_dispatch_path_only"
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{
        run_id = $summary.run_id
        status = $summary.status
        passed = "$($summary.passed_case_count)/$($summary.expected_case_count)"
        cases = @($summary.cases | ForEach-Object {
            [pscustomobject]@{
                case = $_.case
                passed = $_.passed
                status = $_.result.status
                reasons = @($_.result.primitive_verification_reasons)
            }
        })
    } | ConvertTo-Json -Depth 6
    if ($passedCount -ne 2) { throw "Runtime multiplayer chat smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
