function Get-RuntimeRustyKeyEveningFixtureTarget {
    param($Snapshot)
    $field = $Snapshot.state.current_location.home_context
    $homeContext = $field.value
    if ($field.status -cnotin @('available', 'derived') -or $homeContext.home_available -isnot [bool] -or -not $homeContext.home_available -or
        [string]::IsNullOrWhiteSpace($homeContext.home_location_id) -or
        ($homeContext.entry_tile_x -isnot [int] -and $homeContext.entry_tile_x -isnot [long]) -or
        ($homeContext.entry_tile_y -isnot [int] -and $homeContext.entry_tile_y -isnot [long]) -or
        $homeContext.entry_tile_x -lt 0 -or $homeContext.entry_tile_y -lt 0) { throw 'Evening fixture requires the actual native home/entry projection.' }
    return @{ Location = [string]$homeContext.home_location_id; X = $homeContext.entry_tile_x; Y = $homeContext.entry_tile_y }
}

function Assert-RuntimeRustyKeyEveningObservation {
    param($Context, $Rolling, $InitialSnapshot, $NativeNight, $SleepReceipt)
    function Read-EveningDay($Snapshot) {
        $field = $Snapshot.state.time.total_days
        if ($field.status -cnotin @('available', 'derived') -or
            ($field.value -isnot [int] -and $field.value -isnot [long]) -or $field.value -lt 0) { throw 'Evening boundary date is unreadable.' }
        return [int]$field.value
    }
    function Read-EveningStock($Snapshot) {
        $field = $Snapshot.state.player.inventory
        if ($field.status -cnotin @('available', 'derived')) { throw 'Evening player stock is unreadable.' }
        $rows = @($field.value | Where-Object { $_.qualified_item_id -ceq '(O)96' })
        if ($rows.Count -ne 1 -or ($rows[0].stack -isnot [int] -and $rows[0].stack -isnot [long])) { throw 'Evening target stack is missing or ambiguous.' }
        return [int]$rows[0].stack
    }
    $day = Read-EveningDay $InitialSnapshot
    $clock = $InitialSnapshot.state.time.time
    if ($clock.status -cnotin @('available', 'derived') -or ($clock.value -isnot [int] -and $clock.value -isnot [long]) -or
        $clock.value -lt 1900 -or $clock.value -ge 2400 -or $clock.value % 100 -ge 60 -or
        (Read-EveningStock $InitialSnapshot) -ne 2 -or $day -gt 221) { throw 'Evening boundary must start with two owned units and legal next-morning reward slack.' }
    Assert-RustyKeySnapshot $InitialSnapshot 59 $false $false
    if ($NativeNight.schema_version -cne 'end_of_night_transaction.v1' -or $NativeNight.run_id -cne $Context.RunId -or
        $NativeNight.status -cnotin @('ready_for_safe_next_day_action', 'complete', 'blocked') -or
        ($NativeNight.start_total_days -isnot [int] -and $NativeNight.start_total_days -isnot [long]) -or $NativeNight.start_total_days -ne $day -or
        ($NativeNight.expected_total_days -isnot [int] -and $NativeNight.expected_total_days -isnot [long]) -or $NativeNight.expected_total_days -ne $day + 1 -or
        $NativeNight.debug_negative_fixture_injected -isnot [bool] -or $NativeNight.debug_negative_fixture_injected) {
        throw 'Evening boundary has no same-run, exact-date native night transaction.'
    }
    # The immutable sleep receipt proves save/control readiness; the later work-handoff observer has a broader scope.
    if ($SleepReceipt.schema_version -cne 'queue_execution_receipt.v1' -or $SleepReceipt.run_id -cne $Context.RunId -or
        $SleepReceipt.option_id -cne 'executor.sleep' -or $SleepReceipt.status -cne 'applied' -or
        $SleepReceipt.primitive_verification_status -cne 'verified' -or
        $SleepReceipt.end_of_night_transaction_status -cnotin @('ready_for_safe_next_day_action', 'complete') -or
        [string]::IsNullOrWhiteSpace($NativeNight.transaction_id) -or
        $SleepReceipt.end_of_night_transaction_id -cne $NativeNight.transaction_id -or
        [string]::IsNullOrWhiteSpace($NativeNight.queue_id) -or $SleepReceipt.queue_id -cne $NativeNight.queue_id -or
        [string]::IsNullOrWhiteSpace($NativeNight.sleep_queue_item_id) -or $SleepReceipt.queue_item_id -cne $NativeNight.sleep_queue_item_id -or
        [string]::IsNullOrWhiteSpace($NativeNight.evidence_path) -or
        $SleepReceipt.end_of_night_transaction_evidence_path -cne $NativeNight.evidence_path) {
        throw 'Evening boundary requires the exact immutable verified sleep-ready receipt, not a later aggregate status.'
    }
    foreach ($name in @('saw_native_save_commit', 'saw_expected_new_day', 'saw_safe_new_day_ready_state')) {
        if ($NativeNight.$name -isnot [bool] -or -not $NativeNight.$name) { throw "Native night fact was not observed: $name" }
    }
    foreach ($name in @('save_id', 'player_id')) {
        if ($InitialSnapshot.$name.status -cnotin @('available', 'derived') -or [string]::IsNullOrWhiteSpace($InitialSnapshot.$name.value)) {
            throw 'Evening source actor/save is unreadable.'
        }
    }
    if ($Rolling.run_id -cne $Context.RunId -or $Rolling.status -cne 'native_donation_threshold_observed' -or
        $Rolling.input_identity_verified -isnot [bool] -or -not $Rolling.input_identity_verified -or
        @($Rolling.steps).Count -eq 0 -or $Rolling.steps[0].kind -cne 'day_recovery' -or
        @($Rolling.steps | Where-Object { $_.kind -cnotin @('day_recovery', 'held_donation') }).Count -ne 0) {
        throw 'Evening boundary did not use original recovery then donation, or repeated acquisition/key gates.'
    }
    $core = Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll'
    $last = $InitialSnapshot
    $donated = $false
    foreach ($step in $Rolling.steps) {
        $after = Read-RuntimeLogicalJsonArtifact -Path $step.after_snapshot_path -CoreAssemblyPath $core
        if ($after.save_id.status -cnotin @('available', 'derived') -or $after.player_id.status -cnotin @('available', 'derived') -or
            $after.save_id.value -cne $InitialSnapshot.save_id.value -or $after.player_id.value -cne $InitialSnapshot.player_id.value) {
            throw 'Evening boundary changed actor/save.'
        }
        $afterDay = Read-EveningDay $after
        if ($afterDay -lt (Read-EveningDay $last) -or $afterDay -gt $day + 1) { throw 'Evening boundary skipped or reversed a native day.' }
        $count = (Read-StateValue $after 'world_progress' 'museum').donated_count
        if ($count -isnot [int] -and $count -isnot [long]) { throw 'Evening donation progress is not a native integer.' }
        if ($count -eq 60) {
            if ($donated -or $step.kind -cne 'held_donation' -or $afterDay -ne $day + 1 -or (Read-EveningStock $after) -ne 1) {
                throw 'Evening donation must consume exactly one original unit on the next native day.'
            }
            $donated = $true
        } elseif ($count -ne 59 -or $donated -or (Read-EveningStock $after) -ne 2) { throw 'Owned stock or museum progress changed before donation.' }
        $last = $after
    }
    if (-not $donated -or $Rolling.last_snapshot_path -cne $Rolling.steps[-1].after_snapshot_path) { throw 'Evening boundary did not retain its exact terminal observation.' }
    Assert-RustyKeySnapshot $last 60 $true $false
    return [ordered]@{
        source_total_day = $day; donation_total_day = (Read-EveningDay $last); qualified_item_id = '(O)96'
        source_quantity = 2; final_quantity = 1; future_date_route_verified = $false
        native_sleep_boundary_verified = $true
        end_of_night_full_handoff_verified = ($NativeNight.status -ceq 'complete')
        end_of_night_final_status = $NativeNight.status; end_of_night_final_failure_reason = $NativeNight.failure_reason
        native_review_accepted = $false; teacher_preference_label_eligible = $false; formal_training_authorized = $false
    }
}
