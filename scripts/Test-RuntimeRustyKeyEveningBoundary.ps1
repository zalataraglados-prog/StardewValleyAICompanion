[CmdletBinding()]
param([Parameter(Mandatory)] [string] $OutputRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyEvidence.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyEveningBoundary.ps1')
$passed = [Collections.Generic.List[string]]::new()
$context = [pscustomobject]@{ RunId = 'synthetic-evening'; LoopDll = (Join-Path $OutputRoot 'fake/loop.dll') }
function New-EveningSnapshot([int] $Day, [int] $Count, [int] $Stack, [int] $Clock) {
    $snapshot = @{
        save_id = @{ value = 'synthetic-save'; status = 'available' }; player_id = @{ value = '1'; status = 'available' }
        state = @{
            time = @{ total_days = @{ value = $Day; status = 'available' }; time = @{ value = $Clock; status = 'available' } }
            current_location = @{ home_context = @{ status = 'available'; value = @{
                home_available = $true; home_location_id = 'FarmHouse'; entry_tile_x = 3; entry_tile_y = 9
            } } }
            player = @{
                has_rusty_key = @{ value = $false; status = 'available' }; story_event = @{ value = @{ active = $false }; status = 'available' }
                inventory = @{ status = 'available'; value = @(@{ slot_index = 11; qualified_item_id = '(O)96'; stack = $Stack }) }
            }
            world_progress = @{ museum = @{ status = 'available'; value = @{
                donated_count = $Count; rusty_key_donation_threshold = 60; rusty_key_reward_id = 'museum60'
                rusty_key_reward_action = 'MarkEventSeen Host 295672'; rusty_key_reward_claimed = ($Count -eq 60)
                rusty_key_prerequisite_event_seen = ($Count -eq 60); rusty_key_event_seen = $false; has_rusty_key = $false
            } } }
        }
    }
    # Real HTTP JSON uses Int64 values; do not test only PowerShell literal Int32 scalars.
    return ($snapshot | ConvertTo-Json -Depth 30 | ConvertFrom-Json)
}
function Read-RuntimeLogicalJsonArtifact { param($Path, $CoreAssemblyPath) return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
foreach ($mode in @('normal', 'same_day', 'lost_stock', 'extra_day', 'wrong_actor', 'stale_stock', 'fake_count',
        'premature_key', 'acquisition', 'no_deferral', 'wrong_terminal', 'unverified_identity', 'missing_save_commit',
        'wrong_night_run', 'wrong_night_day', 'negative_night', 'incomplete_night', 'handoff_timeout',
        'sleep_unverified', 'sleep_pending', 'foreign_sleep_run', 'foreign_sleep_queue', 'foreign_sleep_item',
        'foreign_sleep_transaction', 'foreign_sleep_evidence', 'missing_sleep')) {
    $initial = New-EveningSnapshot 5 59 2 1900
    $sleep = New-EveningSnapshot 6 59 2 600
    $donated = New-EveningSnapshot 6 60 1 930
    $night = @{ schema_version = 'end_of_night_transaction.v1'; run_id = $context.RunId; status = 'ready_for_safe_next_day_action'
        transaction_id = 'synthetic-night'; queue_id = 'synthetic-sleep-queue'; sleep_queue_item_id = 'synthetic-sleep-item'; evidence_path = 'synthetic-night.json'
        start_total_days = 5; expected_total_days = 6; saw_native_save_commit = $true; saw_expected_new_day = $true
        saw_safe_new_day_ready_state = $true; debug_negative_fixture_injected = $false }
    $receipt = @{ schema_version = 'queue_execution_receipt.v1'; run_id = $context.RunId; option_id = 'executor.sleep'
        queue_id = $night.queue_id; queue_item_id = $night.sleep_queue_item_id; status = 'applied'; primitive_verification_status = 'verified'
        end_of_night_transaction_id = $night.transaction_id; end_of_night_transaction_evidence_path = $night.evidence_path
        end_of_night_transaction_status = 'ready_for_safe_next_day_action' }
    $directory = Join-Path $OutputRoot $mode
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $sleepPath = Join-Path $directory 'sleep.json'
    $donatedPath = Join-Path $directory 'donation.json'
    $rolling = [pscustomobject]@{
        run_id = $context.RunId; status = 'native_donation_threshold_observed'; input_identity_verified = $true
        steps = @(@{ kind = 'day_recovery'; after_snapshot_path = $sleepPath },
            @{ kind = 'held_donation'; after_snapshot_path = $donatedPath }); last_snapshot_path = $donatedPath
    }
    switch ($mode) {
        'same_day' { $donated.state.time.total_days.value = 5 }
        'lost_stock' { $sleep.state.player.inventory.value[0].stack = 1 }
        'extra_day' { $sleep.state.time.total_days.value = 7 }
        'wrong_actor' { $donated.player_id.value = '2' }
        'stale_stock' { $sleep.state.player.inventory.status = 'unavailable' }
        'fake_count' { $donated.state.world_progress.museum.value.donated_count = '60' }
        'premature_key' { $donated.state.player.has_rusty_key.value = $true }
        'acquisition' { $rolling.steps[1].kind = 'terminal_acquisition' }
        'no_deferral' { $rolling.steps[0].kind = 'held_donation' }
        'wrong_terminal' { $rolling.last_snapshot_path = $sleepPath }
        'unverified_identity' { $rolling.input_identity_verified = 'true' }
        'missing_save_commit' { $night.saw_native_save_commit = $false }
        'wrong_night_run' { $night.run_id = 'foreign-run' }
        'wrong_night_day' { $night.expected_total_days = 7 }
        'negative_night' { $night.debug_negative_fixture_injected = $true }
        'incomplete_night' { $night.status = 'waiting_for_native_save_commit' }
        'handoff_timeout' { $night.status = 'blocked'; $night.failure_reason = 'end_of_night_transition_timeout:controller_handoff' }
        'sleep_unverified' { $receipt.primitive_verification_status = 'unverified' }
        'sleep_pending' { $receipt.end_of_night_transaction_status = 'native_outcomes_pending' }
        'foreign_sleep_run' { $receipt.run_id = 'foreign' }
        'foreign_sleep_queue' { $receipt.queue_id = 'foreign' }
        'foreign_sleep_item' { $receipt.queue_item_id = 'foreign' }
        'foreign_sleep_transaction' { $receipt.end_of_night_transaction_id = 'foreign' }
        'foreign_sleep_evidence' { $receipt.end_of_night_transaction_evidence_path = 'foreign' }
        'missing_sleep' { $receipt = $null }
    }
    Write-JsonFile -Path $sleepPath -Value $sleep
    Write-JsonFile -Path $donatedPath -Value $donated
    $rejected = $false
    $failure = ''
    try {
        $target = Get-RuntimeRustyKeyEveningFixtureTarget -Snapshot $initial
        if ($target.Location -cne 'FarmHouse' -or $target.X -ne 3 -or $target.Y -ne 9) { throw 'Home target drifted.' }
        $result = Assert-RuntimeRustyKeyEveningObservation -Context $context -Rolling $rolling -InitialSnapshot $initial -NativeNight $night -SleepReceipt $receipt
        if ($result.native_review_accepted -or $result.formal_training_authorized -or $result.donation_total_day -ne 6) { throw 'Observation promoted evidence.' }
        if ($mode -ceq 'handoff_timeout' -and (-not $result.native_sleep_boundary_verified -or $result.end_of_night_full_handoff_verified -or
            $result.end_of_night_final_status -cne 'blocked' -or $result.end_of_night_final_failure_reason -cne $night.failure_reason)) {
            throw 'Local sleep evidence must not conceal or promote the broader failed work handoff.'
        }
    } catch { $rejected = $true; $failure = $_.Exception.Message }
    if ($rejected -ne ($mode -cnotin @('normal', 'handoff_timeout'))) { throw "Evening observation boundary mismatch: ${mode}: $failure" }
    $passed.Add("evening_observation_$mode")
}
foreach ($mode in @('unreadable_home', 'string_coordinate', 'negative_coordinate')) {
    $snapshot = New-EveningSnapshot 5 59 2 1900
    switch ($mode) {
        'unreadable_home' { $snapshot.state.current_location.home_context.status = 'unavailable' }
        'string_coordinate' { $snapshot.state.current_location.home_context.value.entry_tile_x = '3' }
        'negative_coordinate' { $snapshot.state.current_location.home_context.value.entry_tile_y = -1 }
    }
    $rejected = $false
    try { Get-RuntimeRustyKeyEveningFixtureTarget -Snapshot $snapshot | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'A guessed or unreadable home cannot authorize fixture positioning.' }
    $passed.Add("evening_target_$mode")
}
$rejected = $false
try { & (Join-Path $PSScriptRoot 'Invoke-RuntimeRustyKeyThresholdSmoke.ps1') -OwnedSupplyEvening -SupplyMode current_geode -NoBuild }
catch { $rejected = $_.Exception.Message -like 'Evening boundary requires held_item supply*' }
if (-not $rejected) { throw 'The evening entrypoint must reject late-geode fabrication before startup.' }
$passed.Add('evening_rejects_geode_mode_before_startup')
Write-JsonFile -Path (Join-Path $OutputRoot 'summary.json') -Value ([ordered]@{
    status = 'passed'; passed_count = $passed.Count; cases = $passed.ToArray(); native_game_run = $false; formal_training_authorized = $false
})
Write-Host "Rusty Key evening boundary checks passed: $($passed.Count); no game launched."
