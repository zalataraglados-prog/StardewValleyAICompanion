param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-museum-donation-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 180
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try { $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5; if ($null -ne $result) { return $result } }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot([int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds); $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 5
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.world_progress.museum.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready museum snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-museum-donation-smoke"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Convert-ArrayJson($Values) {
    ConvertTo-Json -InputObject @($Values) -Compress
}

function Setup-MuseumFixture([int] $DonatedCount, [bool] $QuestPresent, [string] $CaseName) {
    $snapshot = Wait-WorldSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_museum_donation" ("setup-" + $CaseName)
    $request.expected_donated_count_before = $DonatedCount
    $request.inventory_slot_index = 11
    $request.field_guide_quest_present_before = $QuestPresent
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Museum fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    Start-Sleep -Milliseconds 750
    return Wait-WorldSnapshot 30
}

function Invoke-MuseumCase([string] $CaseName, [int] $DonatedCount, [bool] $QuestPresent) {
    $before = Setup-MuseumFixture $DonatedCount $QuestPresent $CaseName
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $museum = $before.state.world_progress.museum.value
    $candidate = @($museum.donation_candidates) | Where-Object { $_.slot_index -eq 11 -and $_.qualified_item_id -eq "(O)96" } | Select-Object -First 1
    if ($null -eq $candidate -or $candidate.action_status -ne "ready" -or $candidate.reward_projection_status -ne "ready") {
        throw "Transparent museum candidate was not ready for $CaseName."
    }

    $request = New-BaseRequest $before "executor.donate_museum_item" ("donate-" + $CaseName)
    $request.location_id = [string]$museum.museum_location_id; $request.target_location = [string]$museum.museum_location_id
    $request.target_tile_x = [int]$museum.gunther_action_tile_x; $request.target_tile_y = [int]$museum.gunther_action_tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.donation_tile_x = [int]$museum.free_donation_tile_x; $request.donation_tile_y = [int]$museum.free_donation_tile_y
    $request.inventory_slot_index = [int]$candidate.slot_index; $request.item_id = [string]$candidate.item_id
    $request.qualified_item_id = [string]$candidate.qualified_item_id; $request.target_runtime_type = [string]$candidate.runtime_type
    $request.expected_stack_before = [int]$candidate.stack_before; $request.expected_stack_after = [int]$candidate.stack_after
    $request.expected_donated_count_before = [int]$candidate.donated_count_before; $request.expected_donated_count_after = [int]$candidate.donated_count_after
    $request.museum_total_donatable_items = [int]$museum.total_donatable_items
    $request.expected_collection_complete_after = [bool]$candidate.completes_collection
    $request.expected_complete_collection_achievement_after = [bool]$candidate.expected_complete_collection_achievement_after
    $request.field_guide_quest_present_before = [bool]$candidate.field_guide_quest_present_before
    $request.field_guide_quest_completed_before = [bool]$candidate.field_guide_quest_completed_before
    $request.expected_field_guide_quest_completed_after = [bool]$candidate.expected_field_guide_quest_completed_after
    $request.pending_reward_ids_before_json = Convert-ArrayJson $candidate.pending_reward_ids_before
    $request.pending_reward_ids_after_json = Convert-ArrayJson $candidate.pending_reward_ids_after
    $request.newly_pending_reward_ids_json = Convert-ArrayJson $candidate.newly_pending_reward_ids
    $request.auto_applied_reward_ids_json = Convert-ArrayJson $candidate.auto_applied_reward_ids
    $request.auto_applied_reward_actions_json = Convert-ArrayJson $candidate.auto_applied_reward_actions
    $request.reward_projection_status = [string]$candidate.reward_projection_status
    $request.rusty_key_donation_threshold = [int]$museum.rusty_key_donation_threshold
    $request.reaches_rusty_key_threshold = [bool]$candidate.reaches_rusty_key_threshold
    $request.rusty_key_reward_action = [string]$museum.rusty_key_reward_action
    $request.native_contract = "LibraryMuseum.OpenDonationMenu_then_MuseumMenu_fade_then_receiveLeftClick_inventory_and_display_then_okButton_native_exit"
    $request.max_movement_tiles = 512
    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $after = Wait-WorldSnapshot 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $afterMuseum = $after.state.world_progress.museum.value
    $pendingAfterJson = Convert-ArrayJson $afterMuseum.pending_reward_ids
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified_native_museum_menu_lifecycle" -and
        [int]$afterMuseum.donated_count -eq ($DonatedCount + 1) -and $pendingAfterJson -eq $request.pending_reward_ids_after_json -and
        [bool]$afterMuseum.complete_collection_achievement_received -eq [bool]$candidate.expected_complete_collection_achievement_after -and
        [bool]$afterMuseum.field_guide_quest_completed -eq [bool]$candidate.expected_field_guide_quest_completed_after -and
        (-not [bool]$candidate.reaches_rusty_key_threshold -or
            ([bool]$afterMuseum.rusty_key_reward_claimed -and [bool]$afterMuseum.rusty_key_prerequisite_event_seen))
    [ordered]@{
        case = $CaseName; passed = $passed; status = $result.status; verification = $result.primitive_verification_status
        donated_before = $DonatedCount; donated_after = [int]$afterMuseum.donated_count
        quest24_expected_after = [bool]$candidate.expected_field_guide_quest_completed_after
        quest24_after = [bool]$afterMuseum.field_guide_quest_completed
        achievement_after = [bool]$afterMuseum.complete_collection_achievement_received
        pending_rewards_after = @($afterMuseum.pending_reward_ids)
        newly_pending_rewards = @($candidate.newly_pending_reward_ids)
        auto_applied_rewards = @($candidate.auto_applied_reward_ids)
        block_reasons = @($result.block_reasons)
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-museum-donation-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS")
$savedEnvironment = @{}; foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath; $env:STARDEWAI_TEST_SLOT = $SaveSlot; $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId; $env:STARDEWAI_TRAINING_MODE = "1"; $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"; $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 30 | Out-Null; Wait-WorldSnapshot 120 | Out-Null
    $cases = @(
        (Invoke-MuseumCase "ordinary-with-quest24" 0 $true),
        (Invoke-MuseumCase "rusty-key-threshold" 59 $false),
        (Invoke-MuseumCase "complete-collection" 94 $false)
    )
    $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq 3) { "passed" } else { "failed" }; evidence_id = "EVD-224"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = 3; passed_case_count = $passedCount; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne 3) { throw "Runtime museum donation smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
