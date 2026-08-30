[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-multiplayer-wallet-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
    throw "Timed out waiting for multiplayer wallet snapshot. Last status: $lastStatus"
}

function Wait-Wallet([string] $Mode, [bool] $Pending, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $wallet = $snapshot.state.player.multiplayer_wallet.value
        if ($snapshot.state.player.multiplayer_wallet.status -in @("available", "derived") -and
            $wallet.projection_status -eq "complete_locked_base_1.6.15" -and
            $wallet.wallet_mode -eq $Mode -and [bool]$wallet.change_wallet_type_tonight -eq $Pending -and
            $wallet.service_status -eq "ready") {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Wallet did not reach mode=$Mode pending=$Pending."
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-multiplayer-wallet"
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

function Get-BalanceCsv($Participants) {
    (@($Participants) | Sort-Object { [long]$_.player_id } | ForEach-Object {
        ([string]$_.player_id) + ":" + ([int]$_.effective_balance)
    }) -join ","
}

function Get-TransferredBalanceCsv($Wallet, [string] $RecipientId, [int] $Amount) {
    (@($Wallet.participants) | Sort-Object { [long]$_.player_id } | ForEach-Object {
        $balance = [int]$_.effective_balance
        if ([string]$_.player_id -eq [string]$Wallet.local_player_id) { $balance -= $Amount }
        if ([string]$_.player_id -eq $RecipientId) { $balance += $Amount }
        ([string]$_.player_id) + ":" + $balance
    }) -join ","
}

function New-WalletRequest($Snapshot, [string] $Operation, [int] $Amount = 0) {
    $wallet = $Snapshot.state.player.multiplayer_wallet.value
    $command = @($wallet.commands | Where-Object { $_.operation -eq $Operation }) | Select-Object -First 1
    if ($null -eq $command -or $command.gate_status -ne "ready") {
        throw "Wallet command $Operation is not ready: $($command.gate_status)"
    }
    $ledger = @($wallet.ledger_action_tiles) | Select-Object -First 1
    if ($null -eq $ledger) { throw "Wallet ledger endpoint is missing." }
    $standX = [int]$Snapshot.state.player.tile_x.value
    $standY = [int]$Snapshot.state.player.tile_y.value
    if ([Math]::Abs([int]$ledger.tile_x - $standX) + [Math]::Abs([int]$ledger.tile_y - $standY) -ne 1) {
        throw "Fixture did not place player beside LedgerBook."
    }
    $recipient = $null
    if ($Operation -eq "transfer") {
        $recipient = @($wallet.recipients) | Select-Object -First 1
        if ($null -eq $recipient) { throw "No wallet transfer recipient is available." }
    }
    $beforeCsv = Get-BalanceCsv $wallet.participants
    $recipientId = if ($null -eq $recipient) { "" } else { [string]$recipient.player_id }
    $afterCsv = if ($Operation -eq "transfer") {
        Get-TransferredBalanceCsv $wallet $recipientId $Amount
    } else { $beforeCsv }
    $changeAfter = if ($Operation -in @("schedule_separate", "schedule_merge")) { $true } `
        elseif ($Operation -in @("cancel_separate", "cancel_merge")) { $false } `
        else { [bool]$wallet.change_wallet_type_tonight }
    $pendingAfter = switch ($Operation) {
        "schedule_separate" { "separate_tonight" }
        "schedule_merge" { "merge_tonight" }
        "cancel_separate" { "none" }
        "cancel_merge" { "none" }
        default { [string]$wallet.pending_transition }
    }
    $senderBefore = [int]$wallet.local_effective_money
    $recipientBefore = if ($null -eq $recipient) { 0 } else { [int]$recipient.balance }
    $giftedBefore = [uint32]$wallet.total_money_gifted
    $request = New-Request $Snapshot "executor.manage_multiplayer_wallet" "$RunId.$Operation"
    $request.location_id = [string]$wallet.location_id
    $request.target_location = [string]$wallet.location_id
    $request.target_tile_x = [int]$ledger.tile_x
    $request.target_tile_y = [int]$ledger.tile_y
    $request.stand_tile_x = $standX
    $request.stand_tile_y = $standY
    $request.max_movement_tiles = 512
    $request.wallet_operation = $Operation
    $request.wallet_reason = "isolated EVD-310 smoke"
    $request.confirm_wallet_operation = $true
    $request.confirm_wallet_transfer = $Operation -eq "transfer"
    $request.wallet_projection_fingerprint = [string]$wallet.projection_fingerprint
    $request.wallet_mode_before = [string]$wallet.wallet_mode
    $request.wallet_change_tonight_before = [bool]$wallet.change_wallet_type_tonight
    $request.wallet_change_tonight_after = $changeAfter
    $request.wallet_pending_transition_before = [string]$wallet.pending_transition
    $request.wallet_pending_transition_after = $pendingAfter
    $request.wallet_local_player_id = [string]$wallet.local_player_id
    $request.wallet_actor_is_host = [bool]$wallet.is_host
    $request.wallet_participant_count = [int]$wallet.claimed_participant_count
    $request.wallet_shared_money_before = [int]$wallet.shared_money
    $request.wallet_individual_balances_before_csv = $beforeCsv
    $request.wallet_expected_individual_balances_after_csv = $afterCsv
    $request.wallet_separation_each_balance = [int]$wallet.separation_settlement.each_balance
    $request.wallet_separation_resulting_total = [int]$wallet.separation_settlement.resulting_total
    $request.wallet_separation_discarded_remainder = [int]$wallet.separation_settlement.discarded_integer_remainder
    $request.wallet_merge_resulting_shared_money = [int]$wallet.merge_settlement.resulting_shared_money
    $request.wallet_recipient_player_id = $recipientId
    $request.wallet_recipient_response_key = if ($null -eq $recipient) { "" } else { [string]$recipient.response_key }
    $request.wallet_transfer_amount = $Amount
    $request.wallet_sender_money_before = $senderBefore
    $request.wallet_sender_money_after = $senderBefore - $Amount
    $request.wallet_recipient_money_before = $recipientBefore
    $request.wallet_recipient_money_after = $recipientBefore + $Amount
    $request.wallet_total_money_gifted_before = $giftedBefore
    $request.wallet_total_money_gifted_after = $giftedBefore + [uint32]$Amount
    $request.wallet_ledger_action_raw = [string]$ledger.action_raw
    $request.native_contract = [string]$wallet.native_contract
    return $request
}

function Invoke-Setup([string] $Mode, $Snapshot) {
    $request = New-Request $Snapshot "debug.setup_multiplayer_wallet" "$RunId.setup.$Mode.$([guid]::NewGuid().ToString('N'))"
    $request.wallet_operation = $Mode
    $result = Invoke-JsonPost $executeUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Wallet fixture setup $Mode failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    Start-Sleep -Seconds 2
    Wait-Wallet $Mode $false
}

function Invoke-WalletCommand($Snapshot, [string] $Operation, [int] $Amount = 0) {
    $result = Invoke-JsonPost $executeUrl (New-WalletRequest $Snapshot $Operation $Amount)
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Wallet command $Operation failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    return $result
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-multiplayer-wallet\" + $RunId)
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
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Wait-Snapshot $StartupTimeoutSeconds
    $cases = @()

    $shared = Invoke-Setup "shared" $initial
    $scheduleSeparate = Invoke-WalletCommand $shared "schedule_separate"
    $cases += [ordered]@{ case = "schedule_separate"; passed = $true; result = $scheduleSeparate }
    $sharedPending = Wait-Wallet "shared" $true
    $cancelSeparate = Invoke-WalletCommand $sharedPending "cancel_separate"
    $cases += [ordered]@{ case = "cancel_separate"; passed = $true; result = $cancelSeparate }

    $separate = Invoke-Setup "separate" (Wait-Wallet "shared" $false)
    $scheduleMerge = Invoke-WalletCommand $separate "schedule_merge"
    $cases += [ordered]@{ case = "schedule_merge"; passed = $true; result = $scheduleMerge }
    $separatePending = Wait-Wallet "separate" $true
    $cancelMerge = Invoke-WalletCommand $separatePending "cancel_merge"
    $cases += [ordered]@{ case = "cancel_merge"; passed = $true; result = $cancelMerge }

    $separate = Invoke-Setup "separate" (Wait-Wallet "separate" $false)
    $transfer = Invoke-WalletCommand $separate "transfer" 50
    $cases += [ordered]@{ case = "transfer"; passed = $true; result = $transfer }

    $shared = Invoke-Setup "shared" (Wait-Snapshot 30)
    $null = Invoke-WalletCommand $shared "schedule_separate"
    $scheduled = Wait-Wallet "shared" $true
    $settleRequest = New-Request $scheduled "debug.settle_multiplayer_wallet" "$RunId.settle.separate"
    $settleRequest.wallet_operation = "settle_separate"
    $settleSeparate = Invoke-JsonPost $executeUrl $settleRequest
    $afterSeparate = Wait-Wallet "separate" $false
    $separatePassed = $settleSeparate.status -eq "applied" -and [int]$afterSeparate.state.player.multiplayer_wallet.value.current_individual_total -eq 999
    $cases += [ordered]@{ case = "next_day_settle_separate"; passed = $separatePassed; result = $settleSeparate }

    $separate = Invoke-Setup "separate" $afterSeparate
    $null = Invoke-WalletCommand $separate "schedule_merge"
    $scheduled = Wait-Wallet "separate" $true
    $settleRequest = New-Request $scheduled "debug.settle_multiplayer_wallet" "$RunId.settle.merge"
    $settleRequest.wallet_operation = "settle_merge"
    $settleMerge = Invoke-JsonPost $executeUrl $settleRequest
    $afterMerge = Wait-Wallet "shared" $false
    $mergePassed = $settleMerge.status -eq "applied" -and [int]$afterMerge.state.player.multiplayer_wallet.value.shared_money -eq 1001
    $cases += [ordered]@{ case = "next_day_settle_merge"; passed = $mergePassed; result = $settleMerge }

    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_multiplayer_wallet_smoke.v1"
        evidence_id = "EVD-310"
        run_id = $RunId
        status = if ($passedCount -eq 7) { "passed" } else { "failed" }
        expected_case_count = 7
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne 7) { throw "Runtime multiplayer wallet smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
