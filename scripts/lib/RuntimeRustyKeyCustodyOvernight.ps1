. (Join-Path $PSScriptRoot 'RuntimeRustyKeySleepBoundary.ps1')

function Read-RuntimeRustyKeyCustodyClock {
    param($Snapshot)
    foreach ($name in @('total_days', 'time')) {
        $field = $Snapshot.state.time.$name
        if ($field.status -cnotin @('available', 'derived') -or ($field.value -isnot [int] -and $field.value -isnot [long])) {
            throw 'Custody wait requires a readable native clock and date.'
        }
    }
    $day = [int]$Snapshot.state.time.total_days.value
    $time = [int]$Snapshot.state.time.time.value
    if ($day -lt 0 -or $day -gt 221 -or $time -lt 600 -or $time -gt 2600 -or $time % 100 -ge 60) {
        throw 'Custody wait date/time leaves no legal overnight donation/reward boundary.'
    }
    return [pscustomobject]@{ Day = $day; Time = $time }
}

function Assert-RuntimeRustyKeyStoredCustody {
    param($Snapshot, $Ledger, $Proof)
    $graph = $Snapshot.state.farm.material_inventory_graph
    $rows = @($Ledger.material_reservations | Where-Object { $_.reservation_id -ceq $Proof.custody_reservation_id })
    if ($rows.Count -ne 1 -or $rows[0].status -cne 'active' -or $rows[0].node_id -cne $Proof.chest_node_id -or
        $rows[0].quantity -ne 1 -or $rows[0].output_custody.fresh_terminal_receipt_sha256 -cne $Proof.acquisition_receipt_sha256 -or
        $Snapshot.save_id.status -cnotin @('available', 'derived') -or $Snapshot.player_id.status -cnotin @('available', 'derived') -or
        $Snapshot.save_id.value -cne $Ledger.save_id -or $Snapshot.player_id.value -cne $Ledger.player_id -or
        $graph.status -cne 'available' -or $graph.value.status -cne 'available') { throw 'Stored custody identity/provenance or graph is invalid.' }
    $nodes = @($graph.value.inventory_nodes | Where-Object { $_.node_id -ceq $Proof.chest_node_id })
    $slots = @($nodes | ForEach-Object { $_.slots } | Where-Object { $_.slot_index -eq $rows[0].slot_index -and
        $_.qualified_item_id -ceq $rows[0].qualified_item_id -and $_.stack -eq 1 })
    $stone = @($Ledger.material_reservations | Where-Object { $_.reservation_id -ceq $Proof.protected_reservation_id -and $_.status -ceq 'active' })
    $stoneSlots = @($nodes | ForEach-Object { $_.slots } | Where-Object { $stone.Count -eq 1 -and
        $_.slot_index -eq $stone[0].slot_index -and $_.qualified_item_id -ceq '(O)390' -and $_.stack -eq 3 })
    if ($nodes.Count -ne 1 -or $slots.Count -ne 1 -or $stone.Count -ne 1 -or $stoneSlots.Count -ne 1 -or
        $stone[0].node_id -cne $Proof.chest_node_id -or $stone[0].quantity -ne 3 -or $stone[0].goal_id -cne 'fixture.other_goal_stone') {
        throw 'Stored target or protected other-goal claim/stock is missing.'
    }
    return [pscustomobject]@{
        Custody = ($rows[0] | ConvertTo-Json -Depth 100 -Compress)
        Protected = ($stone[0] | ConvertTo-Json -Depth 100 -Compress)
    }
}

function Invoke-RuntimeRustyKeyCustodyEveningWait {
    param($Context, $Proof)
    $directory = Join-Path $Context.ArtifactDirectory 'custody-native-wait'
    if (Test-Path -LiteralPath $directory) { throw 'Existing custody wait must not be blindly replayed.' }
    New-Item -ItemType Directory -Path $directory | Out-Null
    $core = Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll'
    $snapshot = Read-RuntimeLogicalJsonArtifact -Path $Proof.storage_after_snapshot_path -CoreAssemblyPath $core
    $ledger = Get-Content -LiteralPath $Proof.storage_ledger_path -Raw | ConvertFrom-Json
    $stored = Assert-RuntimeRustyKeyStoredCustody -Snapshot $snapshot -Ledger $ledger -Proof $Proof
    $start = Read-RuntimeRustyKeyCustodyClock $snapshot
    $steps = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt 16; $index++) {
        $before = Read-RuntimeRustyKeyCustodyClock $snapshot
        if ($before.Day -ne $start.Day) { throw 'Native evening wait crossed a day without ordinary sleep.' }
        if ($before.Time -ge 1900) { break }
        $name = 'wait-{0:D2}' -f $index
        $request = [ordered]@{
            schema_version = 'training_execution_request.v1'; run_id = $Context.RunId
            queue_id = "$($Context.RunId).$name"; queue_item_id = "$($Context.RunId).$name.item"
            before_state_hash = $snapshot.state_hash; option_id = 'executor.wait_ticks'; source_planning_option_id = 'executor.wait_ticks'
            execution_mode = 'training_singleplayer'; actor = 'training_farmer.main'; save_isolation_path = $Context.IsolatedSavesPath
            request_nonce = [guid]::NewGuid().ToString('N'); created_at = [DateTimeOffset]::UtcNow.ToString('O')
            wait_ticks = 600; wait_until_time = 1900; wait_max_slices = 3
        }
        Write-JsonFile -Path (Join-Path $directory "$name-request.json") -Value $request
        $response = Invoke-JsonPost -Url "$($Context.ExecutorRoot)/api/v1/training/execute" -Body $request
        Write-Utf8Text -Path (Join-Path $directory "$name-result.json") -Value $response.Raw
        if ($response.Value.run_id -cne $Context.RunId -or $response.Value.queue_id -cne $request.queue_id -or
            $response.Value.queue_item_id -cne $request.queue_item_id -or $response.Value.option_id -cne 'executor.wait_ticks' -or
            $response.Value.status -cne 'applied' -or $response.Value.primitive_verification_status -cne 'verified' -or
            $response.Value.before_state_hash -cne $request.before_state_hash -or $response.Value.actual_ticks -lt 1 -or $response.Value.actual_ticks -gt 1800) {
            throw 'Native custody wait failed or became ambiguous; do not retry.'
        }
        $capture = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 60
        $path = Join-Path $directory "$name-after.json"
        Save-RuntimeSnapshotAndIngest -Path $path -Capture $capture -BackendUrl $Context.BackendUrl -SnapshotProfile $Context.SnapshotProfile
        $snapshot = $capture.Value
        $after = Read-RuntimeRustyKeyCustodyClock $snapshot
        $ledgerPath = Save-RuntimeRustyKeyLatestLedger -Context $Context -StateHash $snapshot.state_hash -Path (Join-Path $directory "$name-ledger.json")
        $ledger = Get-Content -LiteralPath $ledgerPath -Raw | ConvertFrom-Json
        $current = Assert-RuntimeRustyKeyStoredCustody -Snapshot $snapshot -Ledger $ledger -Proof $Proof
        if ($after.Day -ne $start.Day -or $after.Time -le $before.Time -or $current.Custody -cne $stored.Custody -or
            $current.Protected -cne $stored.Protected) { throw 'Native wait did not advance safely with the same stored claim.' }
        $steps.Add([ordered]@{ before_time = $before.Time; after_time = $after.Time; after_snapshot_path = $path
            request_path = (Join-Path $directory "$name-request.json"); result_path = (Join-Path $directory "$name-result.json") })
    }
    $end = Read-RuntimeRustyKeyCustodyClock $snapshot
    if ($end.Day -ne $start.Day -or $end.Time -lt 1900) { throw 'Native custody wait exhausted its bounded step budget; preserve diagnostics without retry.' }
    $result = [ordered]@{ source_total_day = $start.Day; evening_time = $end.Time; steps = $steps.ToArray()
        save_id = $snapshot.save_id.value; player_id = $snapshot.player_id.value
        stored_custody = $stored; native_wait_only = $true; formal_training_authorized = $false }
    Write-JsonFile -Path (Join-Path $directory 'summary.json') -Value $result
    return [pscustomobject]$result
}

function Assert-RuntimeRustyKeyCustodyOvernight {
    param($Context, $Rolling, $Proof, $WaitProof, $NativeNight)
    if ($Rolling.run_id -cne $Context.RunId -or $Rolling.status -cne 'native_donation_threshold_observed' -or
        $Rolling.input_identity_verified -isnot [bool] -or -not $Rolling.input_identity_verified -or
        ($WaitProof.source_total_day -isnot [int] -and $WaitProof.source_total_day -isnot [long]) -or
        $WaitProof.native_wait_only -isnot [bool] -or -not $WaitProof.native_wait_only) { throw 'Overnight control/wait identities are unverified.' }
    Assert-RuntimeRustyKeyCustodyRoundTrip -Rolling $Rolling -Proof $Proof
    $core = Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll'
    $sleep = @($Rolling.steps | ForEach-Object { Get-Content -LiteralPath $_.execution_path -Raw | ConvertFrom-Json } |
        Where-Object { $_.option_id -ceq 'executor.sleep' })
    if ($sleep.Count -ne 1) { throw 'Custody overnight requires exactly one original native sleep receipt.' }
    Assert-RuntimeRustyKeySleepBoundary -Context $Context -NativeNight $NativeNight -SleepReceipt $sleep[0] -SourceTotalDay $WaitProof.source_total_day
    $nextDayVerified = $false
    $lastDay = $WaitProof.source_total_day
    foreach ($step in $Rolling.steps) {
        $snapshot = Read-RuntimeLogicalJsonArtifact -Path $step.after_snapshot_path -CoreAssemblyPath $core
        $day = Read-StateValue $snapshot 'time' 'total_days'
        if ($snapshot.state.time.total_days.status -cnotin @('available', 'derived') -or
            ($day -isnot [int] -and $day -isnot [long]) -or $day -lt $lastDay -or $day -gt $WaitProof.source_total_day + 1 -or
            $snapshot.save_id.value -cne $WaitProof.save_id -or $snapshot.player_id.value -cne $WaitProof.player_id) {
            throw 'Custody overnight skipped or reversed the native date.'
        }
        if (-not $nextDayVerified -and $day -eq $WaitProof.source_total_day + 1) {
            $ledger = Get-Content -LiteralPath $step.ledger_path -Raw | ConvertFrom-Json
            $stored = Assert-RuntimeRustyKeyStoredCustody -Snapshot $snapshot -Ledger $ledger -Proof $Proof
            if ($stored.Custody -cne $WaitProof.stored_custody.Custody -or $stored.Protected -cne $WaitProof.stored_custody.Protected) {
                throw 'Midnight rewrote acquisition/position history or another goal claim.'
            }
            $nextDayVerified = $true
        }
        $lastDay = $day
    }
    if (-not $nextDayVerified -or $day -ne $WaitProof.source_total_day + 1) { throw 'Stored custody was never observed across the actual native night.' }
    Assert-RustyKeySnapshot $snapshot 60 $true $false
    return [ordered]@{ native_output_custody_overnight_observed = $true; source_total_day = $WaitProof.source_total_day
        donation_total_day = $day; end_of_night_final_status = $NativeNight.status; end_of_night_final_failure_reason = $NativeNight.failure_reason
        full_end_of_night_handoff_verified = $false; full_criterion_verified = $false; formal_training_authorized = $false }
}
