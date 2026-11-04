. (Join-Path $PSScriptRoot 'RuntimeRustyKeyRollingExecution.ps1')

function Initialize-RuntimeRustyKeyCustodyBoundary {
    param($Context, [string] $Directory)
    if (Test-Path -LiteralPath $Directory) { throw 'Custody boundary already exists; refusing fixture replay.' }
    if ([string]::IsNullOrWhiteSpace($Context.IsolatedSavesPath)) { throw 'Custody boundary requires explicit save isolation.' }
    New-Item -ItemType Directory -Path $Directory | Out-Null
    $before = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 60
    $field = $before.Value.state.farm.material_inventory_graph
    if ($field.status -cne 'available' -or $field.value.status -cne 'available') { throw 'Custody boundary source graph is unreadable.' }
    $beforeIds = @($field.value.inventory_nodes.node_id)
    $request = [ordered]@{
        schema_version = 'training_execution_request.v1'; run_id = $Context.RunId
        queue_id = "$($Context.RunId).fixture"; queue_item_id = "$($Context.RunId).fixture.custody-chest"
        before_state_hash = $before.Value.state_hash; option_id = 'debug.setup_material_transfer_target'
        execution_mode = 'training_singleplayer'; actor = 'training_farmer.main'
        save_isolation_path = $Context.IsolatedSavesPath; request_nonce = [guid]::NewGuid().ToString('N')
        created_at = [DateTimeOffset]::UtcNow.ToString('O'); location_id = 'Blacksmith'
        fixture_material_transfer_profile = 'sparse_source'
    }
    Write-JsonFile -Path (Join-Path $Directory 'chest-request.json') -Value $request
    $setup = Invoke-JsonPost -Url "$($Context.ExecutorRoot)/api/v1/training/execute" -Body $request
    Write-Utf8Text -Path (Join-Path $Directory 'chest-result.json') -Value $setup.Raw
    if ($setup.Value.status -cne 'applied' -or $setup.Value.primitive_verification_status -cne 'verified') {
        throw 'Custody boundary chest setup was not verified.'
    }
    $after = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 60
    $graph = $after.Value.state.farm.material_inventory_graph
    if ($graph.status -cne 'available' -or $graph.value.status -cne 'available' -or
        $after.Value.save_id.value -cne $before.Value.save_id.value -or $after.Value.player_id.value -cne $before.Value.player_id.value) {
        throw 'Custody boundary graph or actor/save identity changed.'
    }
    $chests = @($graph.value.inventory_nodes | Where-Object { $_.node_id -cnotin $beforeIds -and
        $_.inventory_kind -ceq 'chest' -and $_.location_id -ceq 'Blacksmith' -and $_.actor_use_authorized -eq $true })
    if ($chests.Count -ne 1) { throw 'Custody boundary requires one unique new actor-owned ordinary chest.' }
    $chest = $chests[0]
    $stone = @($chest.slots | Where-Object { $_.slot_index -eq 9 -and $_.qualified_item_id -ceq '(O)390' -and $_.stack -eq 3 -and $_.quality -eq 0 })
    if ($stone.Count -ne 1) { throw 'Custody boundary sparse protected stone is missing.' }
    $snapshotPath = Join-Path $Directory 'chest-snapshot.json'
    Save-RuntimeSnapshotAndIngest -Path $snapshotPath -Capture $after -BackendUrl $Context.BackendUrl -SnapshotProfile full
    $ledgerPath = Save-RuntimeRustyKeyLatestLedger -Context $Context -StateHash $after.Value.state_hash -Path (Join-Path $Directory 'source-ledger.json')
    $ledger = Get-Content -LiteralPath $ledgerPath -Raw | ConvertFrom-Json
    if (($ledger.revision -isnot [int] -and $ledger.revision -isnot [long]) -or $ledger.revision -ne 0) {
        throw 'Custody boundary requires an initially empty isolated ledger.'
    }
    $protectionId = "$($Context.RunId).protected-stone"
    $claim = [ordered]@{
        state_hash = $after.Value.state_hash; expected_ledger_revision = $ledger.revision
        reservation_id = $protectionId; source_decision_id = $protectionId; goal_id = 'fixture.other_goal_stone'
        node_id = $chest.node_id; slot_index = 9; qualified_item_id = '(O)390'; quantity = 3; purpose = 'native_round_trip_other_goal_protection'
    }
    $commit = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation commit -Directory $Directory -Name 'stone-protection' `
        -Request ([ordered]@{ state_hash = $after.Value.state_hash; expected_ledger_revision = $ledger.revision
            portfolio_id = $protectionId; source_decision_id = $protectionId; goal_id = 'fixture.other_goal_stone'; material_claims = @($claim) })
    $report = [ordered]@{
        run_id = $Context.RunId; chest_node_id = $chest.node_id; protected_reservation_id = $protectionId
        snapshot_path = $snapshotPath; initial_protected_ledger_path = $commit.LedgerPath
        fixture_only = $true; native_acquisition_performed = $false; formal_training_authorized = $false
    }
    Write-JsonFile -Path (Join-Path $Directory 'boundary.json') -Value $report
    return [pscustomobject]$report
}
