[CmdletBinding()]
param([string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('portfolio-recovery-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
$originalJsonPost = (Get-Command Invoke-JsonPost).ScriptBlock
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingExecution.ps1')
$passed = [Collections.Generic.List[string]]::new()
$context = [pscustomobject]@{ BackendUrl = 'http://unused.invalid' }
$script:replyRaw = '{"accepted":true,"committed_ledger_revision":1,"errors":[],"ledger":{"revision":1,"updated_at":"2026-11-04T03:04:05.1234567+00:00","precise":1234567890123456789}}'

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 240)
    $script:calls.Add($Url)
    if ($Url.EndsWith('/lookup-mutation')) {
        if ($TimeoutSeconds -ne 15) { throw 'Unbounded recovery timeout.' }
        $decoded = $Body | ConvertFrom-Json
        if ($decoded.operation -cne $script:operation -or $decoded.request.state_hash -cne 'source-state') {
            throw 'Lookup changed original request.'
        }
        if ($script:mode -eq 'unavailable') { throw 'lookup unavailable' }
        $schema = if ($script:mode -eq 'missing-schema') { 'null' } else { '"stardewai.portfolio_mutation_lookup.v1"' }
        $mutated = if ($script:mode -eq 'mutation-claim') { 'true' } else { 'false' }
        $status = if ($script:mode -in @('not_found', 'conflict', 'superseded', 'corrupt')) { $script:mode } else { 'found' }
        $raw = '{"schema_version":' + $schema + ',"status":"' + $status + '","execution_performed":false,' +
            '"ledger_mutation_performed":' + $mutated + ',"native_outcome_verified":false,' +
            '"formal_product_training":false,"requires_independent_verification":true,"reply":' + $script:replyRaw + '}'
        return [pscustomobject]@{ Raw = $raw; Value = ($raw | ConvertFrom-Json) }
    }
    $requestPath = Join-Path $script:directory 'mutation-rpc-request.json'
    $journalPath = Join-Path $script:directory 'mutation-rpc-journal.json'
    if (-not (Test-Path -LiteralPath $journalPath -PathType Leaf) -or
        (Get-Content -LiteralPath $requestPath -Raw) -cne $Body) {
        throw 'RPC was sent before durable exact request/journal.'
    }
    $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
    Assert-RuntimeFileIdentity -Path $requestPath -ExpectedSha256 $journal.request_sha256 -Label 'test' | Out-Null
    if ($script:mode -eq 'normal') {
        return [pscustomobject]@{ Raw = $script:replyRaw; Value = ($script:replyRaw | ConvertFrom-Json) }
    }
    if ($script:mode -eq 'rejected') {
        return [pscustomobject]@{ Raw = '{"accepted":false,"ledger":null}'; Value = [pscustomobject]@{ accepted = $false; ledger = $null } }
    }
    if ($script:mode -in @('http-rejected', 'http-server-error')) {
        $failure = [InvalidOperationException]::new('simulated HTTP failure')
        $failure.Data['RuntimeHttpStatusCode'] = if ($script:mode -eq 'http-rejected') { 409 } else { 500 }
        throw $failure
    }
    if ($script:mode -eq 'request-tamper') { [IO.File]::WriteAllText($requestPath, '{}') }
    throw 'simulated reply lost after mutation'
}

foreach ($operation in @('commit', 'settle-completed-route', 'settle-supporting-transition')) {
    $script:operation = $operation
    $script:mode = 'lost'
    $script:directory = Join-Path $OutputRoot $operation
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $script:calls = [Collections.Generic.List[string]]::new()
    $result = Invoke-RuntimeRustyKeyLedgerMutation -Context $context -Operation $operation `
        -Request '{"state_hash":"source-state","expected_ledger_revision":0}' -Directory $directory -Name 'mutation'
    if ($calls.Count -ne 2 -or -not $result.RecoveredReply -or
        (Get-Content -LiteralPath $result.ResultPath -Raw) -cne $replyRaw -or
        (Get-Content -LiteralPath $result.LedgerPath -Raw) -cne (
            Get-RuntimeRawJsonProperty -RawJson $replyRaw -PropertyName 'ledger')) {
        throw "Exact reply recovery failed: $operation"
    }
    $passed.Add("lost-$operation-one-mutation-one-lookup-exact-raw-reply")
    try {
        Invoke-RuntimeRustyKeyLedgerMutation -Context $context -Operation $operation `
            -Request '{}' -Directory $directory -Name 'mutation' | Out-Null
        throw 'Replay unexpectedly accepted.'
    }
    catch { if ($_.Exception.Message -eq 'Replay unexpectedly accepted.') { throw } }
    if ($calls.Count -ne 2) { throw 'Existing journal allowed mutation replay.' }
    $passed.Add("$operation-no-overwrite-or-replay")
}

$script:operation = 'commit'
foreach ($mode in @('normal', 'rejected', 'http-rejected', 'not_found', 'conflict', 'superseded', 'corrupt', 'unavailable', 'missing-schema', 'mutation-claim', 'request-tamper')) {
    $script:mode = $mode
    $script:directory = Join-Path $OutputRoot $mode
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $script:calls = [Collections.Generic.List[string]]::new()
    $failed = $false
    try {
        $result = Invoke-RuntimeRustyKeyLedgerMutation -Context $context -Operation 'commit' `
            -Request '{"state_hash":"source-state","expected_ledger_revision":0}' -Directory $directory -Name 'mutation'
    }
    catch { $failed = $true }
    if ($mode -eq 'normal') {
        if ($failed -or $calls.Count -ne 1 -or $result.RecoveredReply) { throw 'Normal result changed.' }
    }
    else {
        $expectedCalls = if ($mode -in @('rejected', 'http-rejected', 'request-tamper')) { 1 } else { 2 }
        if (-not $failed -or $calls.Count -ne $expectedCalls -or
            (Test-Path -LiteralPath (Join-Path $directory 'mutation-ledger.json'))) {
            throw "Unknown/rejected mutation did not stop without settlement: $mode"
        }
    }
    $passed.Add($mode)
}
$script:mode = 'http-server-error'
$script:directory = Join-Path $OutputRoot $mode
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$script:calls = [Collections.Generic.List[string]]::new()
$result = Invoke-RuntimeRustyKeyLedgerMutation -Context $context -Operation 'commit' `
    -Request '{"state_hash":"source-state","expected_ledger_revision":0}' -Directory $directory -Name 'mutation'
if ($calls.Count -ne 2 -or -not $result.RecoveredReply) { throw 'HTTP 500 cannot recover committed reply.' }
$passed.Add('http-server-error-one-lookup-without-resend')

# Exercise the original HTTP helper, not the mutation transport double.
$script:httpResponse = [Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::Conflict)
$script:httpResponse.Content = [Net.Http.StringContent]::new('{"accepted":false,"errors":["ledger_revision_conflict"]}')
function Invoke-WebRequest {
    throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('simulated HTTP conflict', $script:httpResponse)
}
$preserved = $false
try { & $originalJsonPost -Url 'http://unused.invalid' -Body '{}' | Out-Null }
catch {
    $preserved = $_.Exception.Data['RuntimeHttpStatusCode'] -eq 409 -and
        $_.Exception.Message.Contains('ledger_revision_conflict') -and $null -ne $_.Exception.InnerException
}
finally { $script:httpResponse.Dispose() }
if (-not $preserved) { throw 'Original HTTP helper lost explicit status/body/cause.' }
$passed.Add('original-http-helper-preserves-status-body-and-cause')
$summary = [ordered]@{
    schema_version = 'stardewai.portfolio_recovery_tests.v1'; passed = $passed.Count; checks = @($passed)
    game_started = $false; evidence_kind = 'transport_double_not_native'; formal_product_training = $false
}
Write-JsonFile -Path (Join-Path $OutputRoot 'summary.json') -Value $summary
$summary | ConvertTo-Json -Depth 8
