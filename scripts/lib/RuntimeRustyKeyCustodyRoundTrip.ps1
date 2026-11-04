function Get-RuntimeRustyKeyCustodyPrefixArguments {
    param($Context, [string] $ProjectRoot, $AcquisitionInputs)
    $arguments = [Collections.Generic.List[string]]::new()
    foreach ($value in @('-NoProfile', '-File', (Join-Path $ProjectRoot 'scripts/Invoke-RuntimeRustyKeyRollingSupply.ps1'),
        '-ProjectRoot', $ProjectRoot, '-OutputDirectory', (Join-Path $Context.ArtifactDirectory 'acquisition-prefix'), '-RunId', $Context.RunId,
        '-BackendUrl', $Context.BackendUrl, '-SnapshotUrl', $Context.SnapshotUrl, '-ExecutorUrl', $Context.ExecutorRoot,
        '-IsolatedSavesPath', $Context.IsolatedSavesPath, '-ExecutionTarget', 'runtime_harness', '-MaxSteps', '1')) {
        $arguments.Add([string]$value)
    }
    foreach ($name in $AcquisitionInputs.Keys) {
        $arguments.Add('-' + [string]$name)
        $arguments.Add([string]$AcquisitionInputs[$name])
    }
    return $arguments.ToArray()
}

function Invoke-RuntimeRustyKeyCustodyRoundTripPrefix {
    param($Context, [string] $ProjectRoot, $AcquisitionInputs, $Boundary, $CustodyBoundary)
    $prefixOutput = Join-Path $Context.ArtifactDirectory 'acquisition-prefix'
    $arguments = @(Get-RuntimeRustyKeyCustodyPrefixArguments -Context $Context -ProjectRoot $ProjectRoot -AcquisitionInputs $AcquisitionInputs)
    & pwsh @arguments > (Join-Path $Context.ArtifactDirectory 'acquisition-prefix.stdout.log')
    if ($LASTEXITCODE -ne 2) { throw 'Custody prefix must stop at its declared one-step boundary, not restart or finish the key.' }
    $prefixPath = Join-Path (Join-Path $prefixOutput $Context.RunId) 'summary.json'
    $prefix = Get-Content -LiteralPath $prefixPath -Raw | ConvertFrom-Json
    if ($prefix.run_id -cne $Context.RunId -or $prefix.status -cne 'step_budget_reached' -or $prefix.input_identity_verified -ne $true -or
        @($prefix.steps).Count -ne 1 -or $prefix.steps[0].kind -cne 'terminal_acquisition') {
        throw 'Custody prefix did not complete one original native acquisition stage.'
    }
    $ledger = Get-Content -LiteralPath $prefix.last_ledger_path -Raw | ConvertFrom-Json
    $terminalPath = Join-Path (Split-Path -Parent $prefix.last_ledger_path) 'terminal-settlement-result.json'
    $terminal = Get-Content -LiteralPath $terminalPath -Raw | ConvertFrom-Json
    $custodyIds = @($terminal.output_custody_reservation_ids)
    if ($terminal.accepted -ne $true -or $custodyIds.Count -ne 1) { throw 'Original terminal settlement did not identify one custody row.' }
    $rows = @($ledger.material_reservations | Where-Object { $_.reservation_id -ceq $custodyIds[0] -and $_.status -ceq 'active' -and
        $null -ne $_.output_custody -and $_.qualified_item_id -ceq $Boundary.target_qualified_item_id -and
        $_.node_id -ceq ('player:' + $ledger.player_id) -and $_.quantity -eq 1 })
    if ($rows.Count -ne 1) { throw 'Custody prefix did not retain one exact acquired-output claim.' }
    $snapshot = Read-RuntimeLogicalJsonArtifact -Path $prefix.last_snapshot_path `
        -CoreAssemblyPath (Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll')
    $graph = $snapshot.state.farm.material_inventory_graph
    $quantity = (@($graph.value.inventory_nodes | Where-Object { $_.node_id -ceq ('player:' + $ledger.player_id) } |
        ForEach-Object { $_.slots } | Where-Object { $_.qualified_item_id -ceq $Boundary.target_qualified_item_id } |
        Measure-Object -Property stack -Sum)).Sum
    if ($graph.status -cne 'available' -or $graph.value.status -cne 'available' -or $quantity -ne 1) {
        throw 'Round-trip proof requires exactly one acquired player unit, with no free duplicate to donate instead.'
    }
    $directory = Join-Path $Context.ArtifactDirectory 'custody-storage'
    New-Item -ItemType Directory -Path $directory | Out-Null
    $bootstrap = Join-Path $ProjectRoot 'experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll'
    $preparedPath = Join-Path $directory 'preparation.json'
    $requestPath = Join-Path $directory 'commit-request.json'
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrap -Arguments @('build-museum-custody-transfer-request',
        '--snapshot', $prefix.last_snapshot_path, '--strategy-ledger', $prefix.last_ledger_path,
        '--reservation-id', $rows[0].reservation_id, '--destination-node', $CustodyBoundary.chest_node_id,
        '--output', $preparedPath, '--commit-request-output', $requestPath)
    $planning = [pscustomobject]@{
        Directory = $directory; SnapshotPath = $prefix.last_snapshot_path; StrategyLedgerPath = $prefix.last_ledger_path
        QueuePath = (Join-Path $directory 'queue.json')
        Control = [pscustomobject]@{ selected_custody_transfer = [pscustomobject]@{ kind = 'custody_transfer'
            reservation_id = $rows[0].reservation_id; destination_node_id = $CustodyBoundary.chest_node_id } }
        Preparation = [pscustomobject]@{ kind = 'custody_transfer'; atomic_commit_request = (Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json) }
    }
    $stored = Invoke-RuntimeRustyKeyCustodyTransfer -Context $Context -BootstrapDll $bootstrap -Planning $planning `
        -Name 'custody-storage-execution' -ExecutionTarget runtime_harness
    $result = [ordered]@{
        run_id = $Context.RunId; prefix_summary_path = $prefixPath; storage_directory = $directory
        terminal_settlement_result_path = $terminalPath
        storage_execution_path = $stored.Execution.ExecutionPath; storage_after_snapshot_path = $stored.Execution.AfterPath
        storage_ledger_path = $stored.LedgerPath; custody_reservation_id = $rows[0].reservation_id
        acquisition_receipt_sha256 = $rows[0].output_custody.fresh_terminal_receipt_sha256
        chest_node_id = $CustodyBoundary.chest_node_id; protected_reservation_id = $CustodyBoundary.protected_reservation_id
        scope = 'same_process_native_storage_probe_not_policy_preference'; formal_training_authorized = $false
    }
    Write-JsonFile -Path (Join-Path $directory 'storage-proof-inputs.json') -Value $result
    return [pscustomobject]$result
}

function Assert-RuntimeRustyKeyCustodyRoundTrip {
    param($Rolling, $Proof)
    if ($Rolling.run_id -cne $Proof.run_id -or
        @($Rolling.steps | Where-Object { $_.kind -ceq 'custody_transfer' }).Count -ne 1 -or
        @($Rolling.steps | Where-Object { $_.kind -ceq 'terminal_acquisition' }).Count -ne 0 -or
        @($Rolling.steps | Where-Object { $_.kind -ceq 'held_donation' }).Count -eq 0) {
        throw 'Stored output was not retrieved and donated by the original rolling selector without reacquisition.'
    }
    $ledger = Get-Content -LiteralPath $Rolling.last_ledger_path -Raw | ConvertFrom-Json
    $custody = @($ledger.material_reservations | Where-Object { $_.reservation_id -ceq $Proof.custody_reservation_id })
    $stone = @($ledger.material_reservations | Where-Object { $_.reservation_id -ceq $Proof.protected_reservation_id })
    if ($custody.Count -ne 1 -or $custody[0].status -cne 'completed' -or
        $custody[0].output_custody.fresh_terminal_receipt_sha256 -cne $Proof.acquisition_receipt_sha256 -or
        $stone.Count -ne 1 -or $stone[0].status -cne 'active' -or $stone[0].node_id -cne $Proof.chest_node_id -or
        $stone[0].slot_index -ne 1 -or $stone[0].quantity -ne 3 -or $stone[0].goal_id -cne 'fixture.other_goal_stone') {
        throw 'Round-trip custody provenance, original consumption or passive other-goal protection was lost.'
    }
}
