. (Join-Path $PSScriptRoot 'RuntimePortfolioMutationRecovery.ps1')

function Save-RuntimeRustyKeyLatestLedger {
    param($Context, [string] $StateHash, [string] $Path)
    $response = Invoke-WebRequest -UseBasicParsing -Uri (
        "$($Context.BackendUrl)/api/v1/strategy/commitments/latest?stateHash=" +
        [Uri]::EscapeDataString($StateHash)) -TimeoutSec 60
    Write-Utf8Text -Path $Path -Value $response.Content
    return $Path
}

function Invoke-RuntimeRustyKeyLedgerMutation {
    param($Context, [string] $Operation, $Request, [string] $Directory, [string] $Name)
    return Invoke-RuntimePortfolioMutation -Context $Context -Operation $Operation `
        -Request $Request -Directory $Directory -Name $Name
}

function Invoke-RuntimeRustyKeySupport {
    param($Context, [string] $BootstrapDll, $Planning, [string] $Name,
        [string] $ExecutionTarget, [string] $ExecutorVersion)
    $directory = $Planning.Directory
    $common = @($Planning.SupportCommon)
    $requestPath = Join-Path $directory 'support-request.json'
    Write-JsonFile -Path $requestPath -Value $Planning.Preparation.support_request
    $commit = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation 'commit' `
        -Request $Planning.Preparation.atomic_commit_request -Directory $directory -Name 'support-commit'
    $commitReceiptPath = Join-Path $directory 'support-commit-receipt.json'
    $commitCommon = @('--support-request', $requestPath, '--committed-ledger', $commit.LedgerPath,
        '--commit-result', $commit.ResultPath)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-supporting-transition-commit-receipt') + $common + $commitCommon +
        @('--output', $commitReceiptPath))
    $compilationPath = Join-Path $directory 'support-compilation.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('compile-acquisition-route-supporting-transition') + $common + $commitCommon +
        @('--support-commit-receipt', $commitReceiptPath, '--queue-output', $Planning.QueuePath, '--output', $compilationPath))
    $execution = Invoke-RuntimePrecompiledQueue -Context $Context -Name $Name -QueuePath $Planning.QueuePath `
        -BeforePath $Planning.SnapshotPath -BeforeSnapshotProfile $Context.SnapshotProfile -ExecutionTarget $ExecutionTarget
    $transitionPath = Join-Path $directory 'support-native-transition.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        'build-acquisition-route-supporting-transition-receipt', '--compilation', $compilationPath,
        '--before-snapshot', $Planning.SnapshotPath, '--execution-receipt', $execution.ExecutionPath,
        '--after-snapshot', $execution.AfterPath, '--run-id', $Context.RunId,
        '--executor-version', $ExecutorVersion, '--output', $transitionPath)
    $proofCommon = $commitCommon + @('--support-commit-receipt', $commitReceiptPath,
        '--compilation', $compilationPath, '--execution-receipt', $execution.ExecutionPath,
        '--after-snapshot', $execution.AfterPath, '--supporting-transition-receipt', $transitionPath,
        '--run-id', $Context.RunId, '--executor-version', $ExecutorVersion)
    $settlementRequestPath = Join-Path $directory 'support-settlement-request.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-supporting-transition-settlement-request') + $common + $proofCommon +
        @('--output', $settlementRequestPath))
    $settlement = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation 'settle-supporting-transition' `
        -Request (Get-Content -LiteralPath $settlementRequestPath -Raw) -Directory $directory -Name 'support-settlement'
    $receiptPath = Join-Path $directory 'support-settlement-receipt.json'
    $proofCommon += @('--settlement-request', $settlementRequestPath, '--settlement-result', $settlement.ResultPath,
        '--settled-ledger', $settlement.LedgerPath)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-supporting-transition-settlement-receipt') + $common + $proofCommon +
        @('--output', $receiptPath))
    $proofCommon += @('--settlement-receipt', $receiptPath)
    $proof = [ordered]@{
        supportRequestPath = $requestPath; supportCommitReceiptPath = $commitReceiptPath
        committedLedgerPath = $commit.LedgerPath; commitResultPath = $commit.ResultPath
        compilationPath = $compilationPath; executionReceiptPath = $execution.ExecutionPath
        afterSnapshotPath = $execution.AfterPath; supportingTransitionReceiptPath = $transitionPath
        runId = $Context.RunId; executorVersion = $ExecutorVersion
        settlementRequestPath = $settlementRequestPath; settlementResultPath = $settlement.ResultPath
        settledLedgerPath = $settlement.LedgerPath; settlementReceiptPath = $receiptPath
    }
    return [pscustomobject]@{
        Execution = $execution; LedgerPath = $settlement.LedgerPath
        Support = [pscustomobject]@{
            Common = $common; ProofCommon = $proofCommon
            Inputs = $Planning.Preparation.support_inputs; Proof = $proof
        }
    }
}

function Invoke-RuntimeRustyKeyTerminal {
    param($Context, [string] $BootstrapDll, $Planning, [string] $Name,
        [string] $ExecutionTarget, [string] $ExecutorVersion)
    $directory = $Planning.Directory
    $commit = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation 'commit' `
        -Request $Planning.Preparation.atomic_commit_request -Directory $directory -Name 'terminal-commit'
    $commitReceiptPath = Join-Path $directory 'terminal-commit-receipt.json'
    $common = $Planning.Common + $Planning.ProofArgs + @(
        '--portfolio-control-selection', $Planning.ControlPath, '--portfolio-proposal', $Planning.ProposalPath,
        '--portfolio-admission', $Planning.AdmissionPath, '--portfolio-commit-result', $commit.ResultPath,
        '--portfolio-commit-receipt', $commitReceiptPath, '--committed-strategy-ledger', $commit.LedgerPath,
        '--action-queue', $Planning.QueuePath, '--route-occurrence-id', $Planning.Control.selected_candidate_id)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-portfolio-commit-receipt') + $common + @('--output', $commitReceiptPath))
    $rankingPath = Join-Path $directory 'committed-ranking.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-rusty-key-current-ranking') + $Planning.Common + $Planning.ProofArgs +
        @('--portfolio-control-selection', $Planning.ControlPath, '--current-ranking-ledger', $commit.LedgerPath,
            '--output', $rankingPath))
    $dispatchPath = Join-Path $directory 'terminal-dispatch.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('compile-acquisition-route-dispatch') + $common + @('--ranking', $rankingPath,
            '--queue-output', $Planning.QueuePath, '--output', $dispatchPath))
    $bindingPath = Join-Path $directory 'terminal-binding.json'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-execution-binding') + $common + @('--output', $bindingPath))
    $execution = Invoke-RuntimePrecompiledQueue -Context $Context -Name $Name -QueuePath $Planning.QueuePath `
        -BeforePath $Planning.SnapshotPath -BeforeSnapshotProfile $Context.SnapshotProfile -ExecutionTarget $ExecutionTarget
    $freshPath = Join-Path $directory 'terminal-native-receipt.json'
    $nativeCommon = @('--execution-binding', $bindingPath, '--execution-receipt', $execution.ExecutionPath,
        '--after-snapshot', $execution.AfterPath, '--run-id', $Context.RunId, '--executor-version', $ExecutorVersion)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-fresh-terminal-receipt') + $common + $nativeCommon + @('--output', $freshPath))
    $requestPath = Join-Path $directory 'terminal-settlement-request.json'
    $nativeCommon += @('--fresh-terminal-receipt', $freshPath)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-portfolio-settlement-request') + $common + $nativeCommon + @('--output', $requestPath))
    $settlement = Invoke-RuntimeRustyKeyLedgerMutation -Context $Context -Operation 'settle-completed-route' `
        -Request (Get-Content -LiteralPath $requestPath -Raw) -Directory $directory -Name 'terminal-settlement'
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
        @('build-acquisition-route-portfolio-settlement-receipt') + $common + $nativeCommon +
        @('--settlement-request', $requestPath, '--settlement-result', $settlement.ResultPath,
            '--settled-ledger', $settlement.LedgerPath, '--output', (Join-Path $directory 'terminal-settlement-receipt.json')))
    return [pscustomobject]@{ Execution = $execution; LedgerPath = $settlement.LedgerPath; Support = $null }
}
