[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $FixtureRoot,
    [Parameter(Mandatory)] [string] $BootstrapDll,
    [Parameter(Mandatory)] [string] $OutputRoot
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingExecution.ps1')
$passed = [Collections.Generic.List[string]]::new()
$scope = Get-Content -LiteralPath (Join-Path $FixtureRoot 'scope.json') -Raw | ConvertFrom-Json
$source = Get-Content -LiteralPath (Join-Path $FixtureRoot 'source-ledger.json') -Raw | ConvertFrom-Json
$prepared = Get-Content -LiteralPath (Join-Path $FixtureRoot 'prepared-ledger.json') -Raw | ConvertFrom-Json
$settled = Get-Content -LiteralPath (Join-Path $FixtureRoot 'settlement-result.json') -Raw | ConvertFrom-Json
if ($scope.synthetic_only -ne $true) { throw 'This game-free test accepts only an explicitly synthetic fixture.' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'Custody adapter test requires a new output root.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$context = [pscustomobject]@{ SnapshotProfile = 'full'; RunId = 'synthetic-custody-adapter' }

function Invoke-RuntimeRustyKeyLedgerMutation {
    param($Context, $Operation, $Request, $Directory, $Name)
    $script:mutations.Add($Operation)
    if ($script:case -eq 'commit-failed') { throw 'Synthetic commit transport failure; do not retry.' }
    $actual = if ($Request -is [string]) { $Request | ConvertFrom-Json } else { $Request }
    $expectedName = if ($Operation -eq 'commit') { 'commit-request.json' } else { 'settlement-request.json' }
    $expected = Get-Content -LiteralPath (Join-Path $FixtureRoot $expectedName) -Raw | ConvertFrom-Json
    if (($actual | ConvertTo-Json -Depth 100 -Compress) -cne ($expected | ConvertTo-Json -Depth 100 -Compress)) {
        throw 'Adapter changed the independently built original mutation request.'
    }
    if ($Operation -eq 'commit') {
        return [pscustomobject]@{ LedgerPath = (Join-Path $FixtureRoot 'prepared-ledger.json'); ResultPath = (Join-Path $FixtureRoot 'commit-result.json') }
    }
    if ($Operation -cne 'settle-supporting-transition') { throw 'Unexpected mutation operation.' }
    return [pscustomobject]@{ LedgerPath = (Join-Path $Directory 'settled-ledger.json'); ResultPath = (Join-Path $FixtureRoot 'settlement-result.json') }
}
function Invoke-RuntimePrecompiledQueue {
    param($Context, $Name, $QueuePath, $BeforePath, $BeforeSnapshotProfile, $ExecutionTarget)
    $script:executions++
    if ($ExecutionTarget -cne 'product' -or $BeforeSnapshotProfile -cne 'full') { throw 'Executor selection changed.' }
    $receipt = Get-Content -LiteralPath (Join-Path $FixtureRoot 'receipt.json') -Raw | ConvertFrom-Json
    $receipt | Add-Member -NotePropertyName effective_before_snapshot_path -NotePropertyValue (Join-Path $FixtureRoot 'before.json')
    $receipt.after_snapshot_path = Join-Path $FixtureRoot 'after.json'
    switch ($script:case) {
        'missing-before' { $receipt.effective_before_snapshot_path = '' }
        'missing-command' { $receipt.effective_queue_item = $null }
        'changed-command' { $receipt.effective_queue_item.normalized_command.actor.actor_id = 'foreign-actor' }
    }
    $rows = if ($script:case -eq 'duplicate') { @($receipt, $receipt) } else { @($receipt) }
    return [pscustomobject]@{ Execution = [pscustomobject]@{ step_results = $rows }; QueuePath = $QueuePath
        BeforePath = $BeforePath; AfterPath = (Join-Path $FixtureRoot 'after.json') }
}

foreach ($script:case in @('valid', 'missing-before', 'missing-command', 'changed-command', 'duplicate', 'commit-failed')) {
    $directory = Join-Path $OutputRoot $script:case
    New-Item -ItemType Directory -Path $directory | Out-Null
    Write-JsonFile -Path (Join-Path $directory 'settled-ledger.json') -Value $settled.ledger
    $planning = [pscustomobject]@{
        Directory = $directory; SnapshotPath = (Join-Path $FixtureRoot 'before.json')
        StrategyLedgerPath = (Join-Path $FixtureRoot 'source-ledger.json'); QueuePath = (Join-Path $directory 'queue.json')
        Control = [pscustomobject]@{ selected_custody_transfer = [pscustomobject]@{
            kind = 'custody_transfer'; reservation_id = $scope.reservation_id; destination_node_id = $scope.destination_node } }
        Preparation = [pscustomobject]@{ kind = 'custody_transfer'
            atomic_commit_request = (Get-Content -LiteralPath (Join-Path $FixtureRoot 'commit-request.json') -Raw | ConvertFrom-Json) }
    }
    $script:mutations = [Collections.Generic.List[string]]::new()
    $script:executions = 0
    $rejected = $false
    try {
        $result = Invoke-RuntimeRustyKeyCustodyTransfer -Context $context -BootstrapDll $BootstrapDll `
            -Planning $planning -Name 'synthetic-transfer' -ExecutionTarget product
        if ($script:case -ne 'valid' -or $result.LedgerPath -cne (Join-Path $directory 'settled-ledger.json')) {
            throw 'Invalid adapter result.'
        }
        $verified = Get-Content -LiteralPath (Join-Path $directory 'custody-settlement-verified.json') -Raw | ConvertFrom-Json
        if (-not $verified.ready -or $verified.execution_performed -or $verified.formal_training_authorized) {
            throw 'Actual CLI settlement replay did not verify the original result.'
        }
    }
    catch {
        if ($script:case -eq 'valid') { throw }
        $rejected = $true
    }
    $expectedMutations = if ($script:case -eq 'valid') { 'commit,settle-supporting-transition' } else { 'commit' }
    $expectedExecutions = if ($script:case -eq 'commit-failed') { 0 } else { 1 }
    if (($script:mutations -join ',') -cne $expectedMutations -or $script:executions -ne $expectedExecutions -or
        ($script:case -ne 'valid' -and -not $rejected)) { throw "Unsafe repeat or premature settlement in $script:case." }
    $passed.Add($script:case)
}
Write-JsonFile -Path (Join-Path $OutputRoot 'summary.json') -Value ([ordered]@{
    status = 'passed'; passed_count = $passed.Count; passed = $passed.ToArray()
    native_game_run = $false; real_http_mutations = $false; actual_original_compiler_and_settlement_cli = $true
    source_ledger_revision = $source.revision; prepared_ledger_revision = $prepared.revision
    formal_training_authorized = $false
})
Write-Output "Custody runtime adapter checks passed: $($passed.Count)"
