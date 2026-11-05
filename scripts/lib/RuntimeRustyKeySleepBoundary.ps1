function Assert-RuntimeRustyKeySleepBoundary {
    param($Context, $NativeNight, $SleepReceipt, [int] $SourceTotalDay)
    if ($NativeNight.schema_version -cne 'end_of_night_transaction.v1' -or $NativeNight.run_id -cne $Context.RunId -or
        $NativeNight.status -cnotin @('ready_for_safe_next_day_action', 'complete', 'blocked') -or
        ($NativeNight.start_total_days -isnot [int] -and $NativeNight.start_total_days -isnot [long]) -or $NativeNight.start_total_days -ne $SourceTotalDay -or
        ($NativeNight.expected_total_days -isnot [int] -and $NativeNight.expected_total_days -isnot [long]) -or $NativeNight.expected_total_days -ne $SourceTotalDay + 1 -or
        $NativeNight.debug_negative_fixture_injected -isnot [bool] -or $NativeNight.debug_negative_fixture_injected) {
        throw 'Boundary has no same-run, exact-date native night transaction.'
    }
    # Save/control readiness is an immutable sleep fact, not the later planning-work handoff verdict.
    if ($SleepReceipt.schema_version -cne 'queue_execution_receipt.v1' -or $SleepReceipt.run_id -cne $Context.RunId -or
        $SleepReceipt.option_id -cne 'executor.sleep' -or $SleepReceipt.status -cne 'applied' -or
        $SleepReceipt.primitive_verification_status -cne 'verified' -or
        $SleepReceipt.end_of_night_transaction_status -cnotin @('ready_for_safe_next_day_action', 'complete') -or
        [string]::IsNullOrWhiteSpace($NativeNight.transaction_id) -or $SleepReceipt.end_of_night_transaction_id -cne $NativeNight.transaction_id -or
        [string]::IsNullOrWhiteSpace($NativeNight.queue_id) -or $SleepReceipt.queue_id -cne $NativeNight.queue_id -or
        [string]::IsNullOrWhiteSpace($NativeNight.sleep_queue_item_id) -or $SleepReceipt.queue_item_id -cne $NativeNight.sleep_queue_item_id -or
        [string]::IsNullOrWhiteSpace($NativeNight.evidence_path) -or $SleepReceipt.end_of_night_transaction_evidence_path -cne $NativeNight.evidence_path) {
        throw 'Boundary requires the exact immutable verified sleep-ready receipt, not a later aggregate status.'
    }
    foreach ($name in @('saw_native_save_commit', 'saw_expected_new_day', 'saw_safe_new_day_ready_state')) {
        if ($NativeNight.$name -isnot [bool] -or -not $NativeNight.$name) { throw "Native night fact was not observed: $name" }
    }
}
