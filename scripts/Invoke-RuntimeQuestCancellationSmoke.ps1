[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-quest-cancellation-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
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
    throw "Timed out waiting for quest cancellation snapshot. Last status: $lastStatus"
}
function Wait-CancellationRow([string] $QuestId, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.quests.cancellation_candidates
        $row = @($field.value.candidates | Where-Object { $_.quest.id -eq $QuestId }) | Select-Object -First 1
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and $null -ne $row) {
            return [pscustomobject]@{ Snapshot = $snapshot; Row = $row }
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Quest cancellation fixture row did not become readable: $QuestId"
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-quest-cancellation"
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
    $setup = New-Request $Snapshot "debug.setup_quest_cancellation" "$RunId.setup.$CaseName"
    $setup.quest_cancellation_fixture_case = $CaseName
    $result = Invoke-JsonPost $executeUrl $setup
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Quest cancellation fixture setup failed for $CaseName`: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $questId = "stardewai.runtime.cancel.$CaseName"
    $ready = Wait-CancellationRow $questId 30
    [pscustomobject]@{ Setup = $result; Snapshot = $ready.Snapshot; Row = $ready.Row; QuestId = $questId }
}
function New-CancellationRequest($Snapshot, $Row, [string] $CaseName) {
    $projection = $Snapshot.state.quests.cancellation_candidates.value
    $request = New-Request $Snapshot "executor.cancel_quest" "$RunId.execute.$CaseName"
    $request.quest_candidate_id = "quest_cancel:$($Row.cancellation_fingerprint)"
    $request.quest_family = "ordinary"
    $request.quest_id = [string]$Row.quest.id
    $request.quest_runtime_type = [string]$Row.quest.runtime_type
    $request.quest_cancellation_fingerprint = [string]$Row.cancellation_fingerprint
    $request.quest_cancel_reason = "isolated EVD-316 native smoke"
    $request.confirm_quest_cancel = $true
    $request.quest_expected_accepted_before = [bool]$Row.quest.accepted
    $request.quest_expected_completed_before = [bool]$Row.quest.completed
    $request.quest_expected_daily_quest = [bool]$Row.quest.daily_quest
    $request.quest_expected_day_accepted = [int]$Row.quest.day_quest_accepted
    $request.quest_expected_days_left = [int]$Row.quest.days_left
    $request.quest_log_count_before = [int]$projection.quest_log_count_before
    $request.quest_log_count_after = [int]$Row.expected_quest_log_count_after
    $request.quest_accepted_daily_before = [bool]$projection.accepted_daily_quest_before
    $request.quest_accepted_daily_after = [bool]$Row.expected_accepted_daily_quest_after
    $request.quest_resets_accepted_daily_quest = [bool]$Row.resets_accepted_daily_quest
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

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-quest-cancellation\" + $RunId)
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

    $sameDay = Invoke-Setup $snapshot "same_day_daily"
    $sameDayRequest = New-CancellationRequest $sameDay.Snapshot $sameDay.Row "same_day_daily"
    $sameDayResult = Invoke-JsonPost $executeUrl $sameDayRequest
    $sameDayAfter = Wait-Snapshot 30
    $sameDayPresent = @($sameDayAfter.state.quests.cancellation_candidates.value.candidates |
        Where-Object { $_.quest.id -eq $sameDay.QuestId }).Count -gt 0
    $sameDayPassed = $sameDayResult.status -eq "applied" -and
        $sameDayResult.primitive_verification_status -eq "verified" -and
        $sameDayResult.quest_accepted_daily_before -eq $true -and
        $sameDayResult.quest_accepted_daily_after -eq $false -and -not $sameDayPresent
    $cases += [ordered]@{ case = "same_day_daily_native_cancel"; passed = $sameDayPassed; result = $sameDayResult }

    $ordinary = Invoke-Setup $sameDayAfter "ordinary_preserve_daily_flag"
    $ordinaryRequest = New-CancellationRequest $ordinary.Snapshot $ordinary.Row "ordinary_preserve_daily_flag"
    $ordinaryResult = Invoke-JsonPost $executeUrl $ordinaryRequest
    $ordinaryAfter = Wait-Snapshot 30
    $ordinaryPresent = @($ordinaryAfter.state.quests.cancellation_candidates.value.candidates |
        Where-Object { $_.quest.id -eq $ordinary.QuestId }).Count -gt 0
    $ordinaryPassed = $ordinaryResult.status -eq "applied" -and
        $ordinaryResult.primitive_verification_status -eq "verified" -and
        $ordinaryResult.quest_accepted_daily_before -eq $true -and
        $ordinaryResult.quest_accepted_daily_after -eq $true -and -not $ordinaryPresent
    $cases += [ordered]@{ case = "ordinary_preserves_daily_flag"; passed = $ordinaryPassed; result = $ordinaryResult }

    $forged = Invoke-Setup $ordinaryAfter "ordinary_preserve_daily_flag"
    $forgedRequest = New-CancellationRequest $forged.Snapshot $forged.Row "forged_fingerprint"
    $forgedRequest.quest_cancellation_fingerprint = ("f" * 64)
    $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
    $forgedPassed = $forgedResult.status -eq "blocked" -and
        @($forgedResult.block_reasons) -contains "quest_cancellation_exact_quest_missing"
    $cases += [ordered]@{ case = "forged_fingerprint_rejected"; passed = $forgedPassed; result = $forgedResult }

    $nonCancellable = Invoke-Setup (Wait-Snapshot 30) "non_cancellable"
    $nonCancellableRequest = New-CancellationRequest $nonCancellable.Snapshot $nonCancellable.Row "non_cancellable"
    $nonCancellableResult = Invoke-JsonPost $executeUrl $nonCancellableRequest
    $nonCancellablePassed = $nonCancellable.Row.status -eq "blocked" -and
        @($nonCancellable.Row.blocked_diagnostics) -contains "quest_native_cancellation_disabled" -and
        $nonCancellableResult.status -eq "blocked" -and
        @($nonCancellableResult.block_reasons) -contains "quest_cancellation_quest_not_cancellable"
    $cases += [ordered]@{ case = "non_cancellable_rejected"; passed = $nonCancellablePassed; result = $nonCancellableResult }

    $finalSnapshot = Wait-Snapshot 30
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_quest_cancellation_smoke.v1"
        evidence_id = "EVD-316"
        run_id = $RunId
        status = if ($passedCount -eq 4) { "passed" } else { "failed" }
        expected_case_count = 4
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{
        run_id = $RunId
        status = $summary.status
        passed = "$passedCount/4"
        artifact = $runDirectory
    } | ConvertTo-Json -Depth 4
    if ($passedCount -ne 4) { throw "Runtime quest cancellation smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
