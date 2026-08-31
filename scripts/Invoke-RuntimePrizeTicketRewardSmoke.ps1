[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-prize-ticket-reward-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
    throw "Timed out waiting for Prize Ticket reward snapshot. Last status: $lastStatus"
}
function Wait-PrizeTicketReady([string] $Stage, [int] $Level, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-Snapshot 30
        $field = $snapshot.state.player.prize_ticket_reward
        if ($field.status -in @("available", "derived") -and
            $field.value.projection_status -eq "complete_locked_base_1.6.15" -and
            $field.value.stage -eq $Stage -and
            [int]$field.value.current_prize_level -eq $Level -and
            $field.value.service_status -eq "ready" -and
            @($field.value.preview_track).Count -eq 4) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Prize Ticket fixture did not become ready for stage=$Stage level=$Level."
}
function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-prize-ticket-reward"
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
function Invoke-Setup($Snapshot, [string] $FixtureCase, [string] $Stage, [int] $Level) {
    $request = New-Request $Snapshot "debug.setup_prize_ticket_reward" "$RunId.setup.$FixtureCase"
    $request.prize_ticket_fixture_case = $FixtureCase
    $result = Invoke-JsonPost $executeUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Prize Ticket fixture setup failed for $FixtureCase`: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    [pscustomobject]@{ Setup = $result; Snapshot = (Wait-PrizeTicketReady $Stage $Level 30) }
}
function New-PrizeTicketRequest($Snapshot, [string] $CaseName) {
    $projection = $Snapshot.state.player.prize_ticket_reward.value
    $tiles = if ($projection.stage -eq "redeem_prize") {
        @($projection.prize_machine_action_tiles)
    } else {
        @($projection.special_order_ticket_action_tiles)
    }
    $target = $tiles | Select-Object -First 1
    if ($null -eq $target -or $null -eq $projection.current_reward) {
        throw "Typed Prize Ticket endpoint or reward missing for $CaseName."
    }
    $reward = $projection.current_reward
    $request = New-Request $Snapshot "executor.claim_prize_ticket" "$RunId.execute.$CaseName"
    $request.location_id = [string]$projection.target_location_id
    $request.target_location = [string]$projection.target_location_id
    $request.target_tile_x = [int]$target.tile_x
    $request.target_tile_y = [int]$target.tile_y
    $request.stand_tile_x = [int]$Snapshot.state.player.tile_x.value
    $request.stand_tile_y = [int]$Snapshot.state.player.tile_y.value
    $request.max_movement_tiles = 512
    $request.prize_ticket_stage = [string]$projection.stage
    $request.prize_ticket_projection_fingerprint = [string]$projection.projection_fingerprint
    $request.prize_ticket_current_reward_fingerprint = [string]$projection.current_reward_fingerprint
    $request.prize_ticket_preview_json = ConvertTo-Json -InputObject @($projection.preview_track) -Depth 32 -Compress
    $request.prize_ticket_inventory_count_before = [int]$projection.inventory_ticket_count
    $request.prize_ticket_pending_count_before = [int]$projection.pending_special_order_ticket_count
    $request.prize_ticket_claimed_count_before = [int]$projection.ticket_prizes_claimed
    $request.prize_ticket_prize_level = [int]$projection.current_prize_level
    $request.prize_ticket_reward_qualified_item_id = [string]$reward.qualified_item_id
    $request.prize_ticket_reward_item_id = [string]$reward.item_id
    $request.prize_ticket_reward_stack = [int]$reward.stack
    $request.prize_ticket_reward_quality = [int]$reward.quality
    $request.prize_ticket_reward_runtime_type = [string]$reward.runtime_type
    $request.prize_ticket_inventory_max_items = [int]$projection.inventory_max_items
    $request.prize_ticket_inventory_occupied_slots = [int]$projection.inventory_occupied_slots
    $request.prize_ticket_pending_capacity_sufficient = [bool]$projection.pending_ticket_capacity_sufficient
    $request.prize_ticket_action_raw = [string]$target.action_raw
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

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-prize-ticket-reward\" + $RunId)
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

    $forged = Invoke-Setup $snapshot "redeem_space_level_0" "redeem_prize" 0
    $forgedRequest = New-PrizeTicketRequest $forged.Snapshot "forged-fingerprint"
    $forgedRequest.prize_ticket_projection_fingerprint = ("f" * 64)
    $forgedResult = Invoke-JsonPost $executeUrl $forgedRequest
    $forgedPassed = $forgedResult.status -eq "blocked" -and
        @($forgedResult.block_reasons) -contains "prize_ticket_reward_projection_drifted"
    $cases += [ordered]@{ case = "forged_projection_fingerprint_rejected"; passed = $forgedPassed; result = $forgedResult }

    $collect = Invoke-Setup (Wait-Snapshot 30) "collect_pending" "collect_pending_ticket" 0
    $collectRequest = New-PrizeTicketRequest $collect.Snapshot "collect-pending"
    $collectResult = Invoke-JsonPost $executeUrl $collectRequest
    $collectPassed = $collectResult.status -eq "applied" -and $collectResult.primitive_verification_status -eq "verified" -and
        [int]$collectResult.prize_ticket_inventory_count_after -eq 1 -and
        [int]$collectResult.prize_ticket_pending_count_after -eq 0 -and
        [int]$collectResult.prize_ticket_claimed_count_after -eq 0
    $cases += [ordered]@{ case = "native_pending_ticket_collection"; passed = $collectPassed; result = $collectResult }

    $redemptionCases = @(
        [pscustomobject]@{ Fixture = "redeem_space_level_0"; Name = "native_level_0"; Level = 0; Full = $false },
        [pscustomobject]@{ Fixture = "redeem_space_level_5_upgraded"; Name = "native_level_5_upgraded_house"; Level = 5; Full = $false },
        [pscustomobject]@{ Fixture = "redeem_full_level_21"; Name = "native_level_21_full_inventory_debris"; Level = 21; Full = $true },
        [pscustomobject]@{ Fixture = "redeem_cycle_level_22"; Name = "native_level_22_cycle"; Level = 22; Full = $false }
    )
    foreach ($case in $redemptionCases) {
        $setup = Invoke-Setup (Wait-Snapshot 30) $case.Fixture "redeem_prize" $case.Level
        $request = New-PrizeTicketRequest $setup.Snapshot $case.Name
        $result = Invoke-JsonPost $executeUrl $request
        $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
            [int]$result.prize_ticket_inventory_count_after -eq 0 -and
            [int]$result.prize_ticket_claimed_count_after -eq ($case.Level + 1) -and
            [int]$result.prize_ticket_reward_total_delta -eq [int]$request.prize_ticket_reward_stack
        $cases += [ordered]@{ case = $case.Name; passed = $passed; full_inventory = $case.Full; result = $result }
    }

    $finalSnapshot = Wait-Snapshot 30
    Write-Json (Join-Path $runDirectory "full-snapshot.json") $finalSnapshot
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_prize_ticket_reward_smoke.v1"
        evidence_id = "EVD-318"
        run_id = $RunId
        status = if ($passedCount -eq 6) { "passed" } else { "failed" }
        expected_case_count = 6
        passed_case_count = $passedCount
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $cases
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    [pscustomobject]@{ run_id = $RunId; status = $summary.status; passed = "$passedCount/6"; artifact = $runDirectory } |
        ConvertTo-Json -Depth 4
    if ($passedCount -ne 6) { throw "Runtime Prize Ticket reward smoke failed: $runDirectory" }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
        $gameProcess.WaitForExit(10000) | Out-Null
    }
}
