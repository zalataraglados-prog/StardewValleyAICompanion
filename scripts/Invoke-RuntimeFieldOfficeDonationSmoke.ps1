param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-field-office-donation-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
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
                $snapshot.state.world_progress.island_field_office.status -in @("available", "derived")) { return $snapshot }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world-ready Field Office snapshot. Last error: $lastError"
}

function Wait-FieldOfficeState([int] $PieceIndex, [bool] $Donated, [int] $DonatedCount, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $snapshot = Wait-WorldSnapshot 10
        $office = $snapshot.state.world_progress.island_field_office.value
        $piece = @($office.pieces) | Where-Object { $_.piece_index -eq $PieceIndex } | Select-Object -First 1
        if ($null -ne $piece -and [bool]$piece.donated -eq $Donated -and [int]$office.donated_piece_count -eq $DonatedCount) {
            return $snapshot
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for Field Office piece $PieceIndex donated=$Donated count=$DonatedCount."
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-field-office-donation-smoke"
        queue_item_id = $QueueItemId; before_state_hash = $Snapshot.state_hash; option_id = $OptionId
        execution_mode = "training_singleplayer"; actor = "training_farmer.main"; save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Convert-ArrayJson($Values) {
    ConvertTo-Json -InputObject @($Values) -Compress -Depth 16
}

function Setup-FieldOfficeFixture([int] $PieceIndex, [bool] $CompletesSet, [int] $WalnutsFound, [string] $CaseName) {
    $snapshot = Wait-WorldSnapshot 30
    $request = New-BaseRequest $snapshot "debug.setup_field_office_donation" ("setup-" + $CaseName)
    $request.field_office_target_piece_index = $PieceIndex
    $request.field_office_completes_set = $CompletesSet
    $request.field_office_golden_walnuts_found_before = $WalnutsFound
    $request.inventory_slot_index = 11
    $result = Invoke-JsonPost $executorUrl $request
    if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
        throw "Field Office fixture setup failed: $($result | ConvertTo-Json -Depth 32 -Compress)"
    }
    $expectedBefore = if ($CompletesSet) {
        if ($PieceIndex -le 5) { 5 } elseif ($PieceIndex -le 8) { 2 } else { 0 }
    } elseif ($PieceIndex -in @(2, 6)) { 1 } else { 0 }
    return Wait-FieldOfficeState $PieceIndex $false $expectedBefore 30
}

function FieldOffice-RestoredFlag($Office, [string] $SetKind) {
    switch ($SetKind) {
        "center_skeleton" { return [bool]$Office.center_skeleton_restored }
        "snake" { return [bool]$Office.snake_restored }
        "bat" { return [bool]$Office.bat_restored }
        "frog" { return [bool]$Office.frog_restored }
        default { return $false }
    }
}

function Invoke-FieldOfficeCase([string] $CaseName, [int] $PieceIndex, [bool] $FixtureCompletesSet, [int] $WalnutsFound) {
    $before = Setup-FieldOfficeFixture $PieceIndex $FixtureCompletesSet $WalnutsFound $CaseName
    $before | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-before-snapshot.json")) -Encoding utf8
    $office = $before.state.world_progress.island_field_office.value
    $candidate = @($office.donation_candidates) | Where-Object { $_.slot_index -eq 11 -and $_.target_piece_index -eq $PieceIndex } | Select-Object -First 1
    if ($null -eq $candidate -or $candidate.action_status -ne "ready") {
        throw "Transparent Field Office candidate was not ready for $CaseName."
    }
    $desk = @($office.desk_action_tiles) | Where-Object { $_.action_raw -eq "FieldOfficeDesk" } | Select-Object -First 1
    if ($null -eq $desk) { throw "Field Office desk endpoint missing for $CaseName." }

    $request = New-BaseRequest $before "executor.donate_field_office_piece" ("donate-" + $CaseName)
    $request.location_id = [string]$office.location_id; $request.target_location = [string]$office.location_id
    $request.target_tile_x = [int]$desk.tile_x; $request.target_tile_y = [int]$desk.tile_y
    $request.stand_tile_x = [int]$before.state.player.tile_x.value; $request.stand_tile_y = [int]$before.state.player.tile_y.value
    $request.field_office_desk_action_raw = [string]$desk.action_raw
    $request.inventory_slot_index = [int]$candidate.slot_index; $request.item_id = [string]$candidate.item_id
    $request.qualified_item_id = [string]$candidate.qualified_item_id; $request.target_runtime_type = [string]$candidate.runtime_type
    $request.expected_stack_before = [int]$candidate.stack_before; $request.expected_stack_after = [int]$candidate.stack_after
    $request.field_office_target_piece_index = [int]$candidate.target_piece_index
    $request.field_office_target_piece_kind = [string]$candidate.target_piece_kind
    $request.field_office_target_set_kind = [string]$candidate.target_set_kind
    $request.field_office_donated_piece_count_before = [int]$candidate.donated_piece_count_before
    $request.field_office_donated_piece_count_after = [int]$candidate.donated_piece_count_after
    $request.field_office_completes_set = [bool]$candidate.completes_set
    $request.field_office_new_reward_items_json = Convert-ArrayJson $candidate.new_reward_items
    $request.field_office_rewards_before_json = Convert-ArrayJson $candidate.uncollected_rewards_before
    $request.field_office_rewards_after_json = Convert-ArrayJson $candidate.uncollected_rewards_after
    $request.field_office_collected_nut_key = [string]$candidate.expected_collected_nut_key
    $request.field_office_collected_nut_before = [bool]$candidate.collected_nut_before
    $request.field_office_finale_ready_after = [bool]$candidate.expected_finale_ready_after
    $request.field_office_plants_restored_left_before = [bool]$office.plants_restored_left
    $request.field_office_plants_restored_right_before = [bool]$office.plants_restored_right
    $request.field_office_finale_received_before = [bool]$office.finale_received_or_pending
    $request.field_office_golden_walnuts_found_before = [int]$office.golden_walnuts_found
    $request.field_office_projection_status = [string]$office.projection_status
    $request.native_contract = "FieldOfficeDesk_mutex_then_Safari_Donate_then_FieldOfficeMenu_inventory_and_exact_piece_holder_then_native_ok_exit"
    $request.max_movement_tiles = 512

    $result = Invoke-JsonPost $executorUrl $request
    $result | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-result.json")) -Encoding utf8
    $after = Wait-FieldOfficeState $PieceIndex $true ([int]$candidate.donated_piece_count_after) 30
    $after | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath (Join-Path $artifactDirectory ($CaseName + "-after-snapshot.json")) -Encoding utf8
    $afterOffice = $after.state.world_progress.island_field_office.value
    $rewardsAfterJson = Convert-ArrayJson $afterOffice.uncollected_rewards
    $restoredAfter = FieldOffice-RestoredFlag $afterOffice ([string]$candidate.target_set_kind)
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
        $rewardsAfterJson -eq $request.field_office_rewards_after_json -and
        [int]$afterOffice.golden_walnuts_found -eq $WalnutsFound -and
        ([int]$after.state.player.inventory.value[11].stack -eq [int]$candidate.stack_after) -and
        (-not [bool]$candidate.completes_set -or $restoredAfter)
    [ordered]@{
        case = $CaseName; passed = $passed; status = $result.status; verification = $result.primitive_verification_status
        piece_index = $PieceIndex; qualified_item_id = [string]$candidate.qualified_item_id
        set_kind = [string]$candidate.target_set_kind; completes_set = [bool]$candidate.completes_set
        walnuts_found = $WalnutsFound; restored_after = $restoredAfter
        donated_before = [int]$candidate.donated_piece_count_before; donated_after = [int]$afterOffice.donated_piece_count
        new_rewards = @($candidate.new_reward_items); uncollected_rewards_after = @($afterOffice.uncollected_rewards)
        block_reasons = @($result.block_reasons)
    }
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"; $smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"; $savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"; $executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach ($port in @(8765, 8767)) { if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already listening. Refusing to attach." } }
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) { throw "StardewModdingAPI is already running. Refusing to attach or start." }

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-field-office-donation-smoke\" + $RunId)
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
    $cases = @()
    foreach ($piece in 0..10) { $cases += Invoke-FieldOfficeCase ("slot-" + $piece.ToString("00")) $piece $false 0 }
    $cases += Invoke-FieldOfficeCase "center-set-complete" 5 $true 0
    $cases += Invoke-FieldOfficeCase "snake-set-complete" 8 $true 0
    $cases += Invoke-FieldOfficeCase "bat-walnut-cap-fallback" 9 $false 130
    $cases += Invoke-FieldOfficeCase "frog-walnut-cap-fallback" 10 $false 130
    $expectedCount = 15; $passedCount = @($cases | Where-Object { $_.passed }).Count
    $summary = [ordered]@{
        status = if ($passedCount -eq $expectedCount) { "passed" } else { "failed" }; evidence_id = "EVD-302"; run_id = $RunId; save_slot = $SaveSlot
        expected_case_count = $expectedCount; passed_case_count = $passedCount; cases = $cases
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if ($passedCount -ne $expectedCount) { throw "Runtime Field Office donation smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process") }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
