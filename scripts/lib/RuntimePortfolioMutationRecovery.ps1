function Write-RuntimeMutationJournalFile {
    param([string] $Path, [string] $Value)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

function Get-RuntimePortfolioMutationReply {
    param($Context, [string] $JournalPath, [string] $ObservationPath)
    $journal = Get-Content -LiteralPath $JournalPath -Raw | ConvertFrom-Json
    $requestPath = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $JournalPath) $journal.request_file))
    if ($journal.schema_version -cne 'stardewai.portfolio_rpc_journal.v1' -or
        $journal.backend_url -cne $Context.BackendUrl -or
        $journal.request_file -cne [IO.Path]::GetFileName($requestPath) -or
        $journal.operation -notin @('commit', 'settle-completed-route', 'settle-supporting-transition')) {
        throw 'Portfolio recovery journal identity mismatch.'
    }
    Assert-RuntimeFileIdentity -Path $requestPath -ExpectedSha256 $journal.request_sha256 `
        -Label 'Original portfolio RPC request' | Out-Null
    $rawRequest = Get-Content -LiteralPath $requestPath -Raw
    $lookupBody = '{"operation":' + ($journal.operation | ConvertTo-Json -Compress) +
        ',"request":' + $rawRequest + '}'
    $lookup = Invoke-JsonPost -Url (
        "$($journal.backend_url)/api/v1/strategy/commitments/reservation-portfolios/lookup-mutation") `
        -Body $lookupBody -TimeoutSeconds 15
    Write-RuntimeMutationJournalFile -Path $ObservationPath -Value $lookup.Raw
    if ($lookup.Value.schema_version -cne 'stardewai.portfolio_mutation_lookup.v1' -or
        $lookup.Value.status -cne 'found' -or
        $lookup.Value.execution_performed -isnot [bool] -or $lookup.Value.execution_performed -ne $false -or
        $lookup.Value.ledger_mutation_performed -isnot [bool] -or $lookup.Value.ledger_mutation_performed -ne $false -or
        $lookup.Value.native_outcome_verified -isnot [bool] -or $lookup.Value.native_outcome_verified -ne $false -or
        $lookup.Value.formal_product_training -isnot [bool] -or $lookup.Value.formal_product_training -ne $false -or
        $lookup.Value.requires_independent_verification -ne $true) {
        throw "Portfolio reply unavailable or not observation-only: $ObservationPath"
    }
    $rawReply = Get-RuntimeRawJsonProperty -RawJson $lookup.Raw -PropertyName 'reply'
    return [pscustomobject]@{ Raw = $rawReply; Value = ($rawReply | ConvertFrom-Json) }
}

function Invoke-RuntimePortfolioMutation {
    param($Context, [string] $Operation, $Request, [string] $Directory, [string] $Name)
    if ($Operation -notin @('commit', 'settle-completed-route', 'settle-supporting-transition') -or
        [string]::IsNullOrWhiteSpace($Name) -or $Name -cne [IO.Path]::GetFileName($Name)) {
        throw 'Invalid portfolio mutation operation/name.'
    }
    $rawRequest = if ($Request -is [string]) { $Request } else { $Request | ConvertTo-Json -Depth 96 }
    $requestPath = Join-Path $Directory "$Name-rpc-request.json"
    $journalPath = Join-Path $Directory "$Name-rpc-journal.json"
    Write-RuntimeMutationJournalFile -Path $requestPath -Value $rawRequest
    Write-RuntimeMutationJournalFile -Path $journalPath -Value ([ordered]@{
        schema_version = 'stardewai.portfolio_rpc_journal.v1'
        backend_url = $Context.BackendUrl; operation = $Operation
        request_file = [IO.Path]::GetFileName($requestPath)
        request_sha256 = Get-RuntimeFileSha256 -Path $requestPath
    } | ConvertTo-Json)
    $recovered = $false
    try {
        $result = Invoke-JsonPost -Url (
            "$($Context.BackendUrl)/api/v1/strategy/commitments/reservation-portfolios/$Operation") -Body $rawRequest
    }
    catch {
        $dispatchFailure = $_.Exception.Message
        Write-RuntimeMutationJournalFile -Path (Join-Path $Directory "$Name-rpc-failure.txt") -Value $dispatchFailure
        try {
            $result = Get-RuntimePortfolioMutationReply -Context $Context -JournalPath $journalPath `
                -ObservationPath (Join-Path $Directory "$Name-rpc-lookup.json")
            $recovered = $true
        }
        catch {
            throw "Portfolio mutation remains unknown; no replay. $dispatchFailure; lookup=$($_.Exception.Message); journal=$journalPath"
        }
    }
    $resultPath = Join-Path $Directory "$Name-result.json"
    $ledgerPath = Join-Path $Directory "$Name-ledger.json"
    Write-RuntimeMutationJournalFile -Path $resultPath -Value $result.Raw
    if ($result.Value.accepted -ne $true -or $null -eq $result.Value.ledger) {
        throw "Portfolio ledger mutation rejected: $Operation; result=$resultPath"
    }
    Write-RawJsonProperty -Path $ledgerPath -RawJson $result.Raw -PropertyName 'ledger'
    return [pscustomobject]@{
        ResultPath = $resultPath; LedgerPath = $ledgerPath
        RequestJournalPath = $journalPath; RecoveredReply = $recovered
    }
}
