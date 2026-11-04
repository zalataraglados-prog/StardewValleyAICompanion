function Invoke-RuntimeRustyKeyCustodyTransfer {
    param($Context, [string] $BootstrapDll, $Planning, [string] $Name, [string] $ExecutionTarget)

    $selection = $Planning.Control.selected_custody_transfer
    if ($null -eq $selection -or $selection.kind -cne 'custody_transfer' -or
        $Planning.Preparation.kind -cne 'custody_transfer' -or $null -eq $Planning.Preparation.atomic_commit_request) {
        throw 'Rusty Key custody transfer has no verified original control request.'
    }
    $directory = $Planning.Directory
    $common = @('--snapshot', $Planning.SnapshotPath, '--strategy-ledger', $Planning.StrategyLedgerPath,
        '--reservation-id', $selection.reservation_id, '--destination-node', $selection.destination_node_id)
    $commit = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation 'commit' `
        -Request $Planning.Preparation.atomic_commit_request -Directory $directory -Name 'custody-commit'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('compile-museum-custody-transfer') + $common + @('--commit-result', $commit.ResultPath,
            '--selected-queue-output', $Planning.QueuePath, '--output', (Join-Path $directory 'custody-compilation.json')))
    $execution = Invoke-RuntimePrecompiledQueue -Context $Context -Name $Name -QueuePath $Planning.QueuePath `
        -BeforePath $Planning.SnapshotPath -BeforeSnapshotProfile $Context.SnapshotProfile -ExecutionTarget $ExecutionTarget
    $transfers = @($execution.Execution.step_results | Where-Object { $_.option_id -ceq 'executor.transfer_material' })
    if ($transfers.Count -ne 1) { throw 'Custody transfer requires exactly one actual material-transfer receipt.' }
    $receipt = $transfers[0]
    foreach ($field in @('effective_before_snapshot_path', 'after_snapshot_path')) {
        if ([string]::IsNullOrWhiteSpace([string]$receipt.$field) -or -not (Test-Path -LiteralPath $receipt.$field -PathType Leaf)) {
            throw "Custody transfer receipt is missing its actual $field."
        }
    }
    if ($null -eq $receipt.effective_queue_item) { throw 'Custody transfer receipt is missing its actual dispatched command.' }
    $itemPath = Join-Path $directory 'custody-dispatched-item.json'
    $receiptPath = Join-Path $directory 'custody-native-receipt.json'
    Write-JsonFile -Path $itemPath -Value $receipt.effective_queue_item
    Write-JsonFile -Path $receiptPath -Value $receipt
    $proof = @('--queue', $Planning.QueuePath, '--dispatched-item', $itemPath, '--receipt', $receiptPath,
        '--before-snapshot', $receipt.effective_before_snapshot_path, '--after-snapshot', $receipt.after_snapshot_path,
        '--strategy-ledger', $commit.LedgerPath)
    $requestPath = Join-Path $directory 'custody-settlement-request.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-museum-custody-transfer-settlement') + $proof + @('--settlement-request-output', $requestPath,
            '--output', (Join-Path $directory 'custody-settlement-build.json')))
    $settlement = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation 'settle-supporting-transition' `
        -Request (Get-Content -LiteralPath $requestPath -Raw) -Directory $directory -Name 'custody-settlement'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-museum-custody-transfer-settlement') + $proof + @('--settlement-result', $settlement.ResultPath,
            '--output', (Join-Path $directory 'custody-settlement-verified.json')))
    return [pscustomobject]@{ Execution = $execution; LedgerPath = $settlement.LedgerPath; Support = $null }
}
