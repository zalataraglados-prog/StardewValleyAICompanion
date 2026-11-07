function Initialize-RuntimeMoneyConcreteDenominator {
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $AlternativePath,
        [Parameter(Mandatory)] [string] $OutputDirectory,
        [Parameter(Mandatory)] [ValidateRange(0, [int]::MaxValue)] [int] $FreshRebindAlternativeCount,
        [string] $ComparisonCollectionPlanPath = '',
        [object[]] $RolloutProofs = @()
    )
    $manifestPath = Join-Path $OutputDirectory 'money-revenue-concrete-alternative-denominator-manifest.json'
    $catalogPath = Join-Path $OutputDirectory 'money-revenue-concrete-alternative-denominator.json'
    $assemblyPath = Join-Path $OutputDirectory 'money-revenue-terminal-rebind-manifest-assembly-request.json'
    $manifestRebindOutput = Join-Path $OutputDirectory 'money-revenue-terminal-rebind-manifest.json'
    $receiptRebindOutput = Join-Path $OutputDirectory 'money-revenue-terminal-rebind-receipt.json'
    $outputs = @($manifestPath, $catalogPath, $assemblyPath, $manifestRebindOutput, $receiptRebindOutput)
    foreach ($inputPath in @($AlternativePath, $ComparisonCollectionPlanPath)) {
        if ([string]::IsNullOrWhiteSpace($inputPath)) { continue }
        foreach ($output in $outputs) {
            if ([string]::Equals([IO.Path]::GetFullPath($inputPath), [IO.Path]::GetFullPath($output), [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Money concrete initialization output aliases a source input.'
            }
        }
    }
    $rebindManifestPath = ''
    $rebindReceiptPath = ''
    if ($FreshRebindAlternativeCount -gt 0) {
        if ([string]::IsNullOrWhiteSpace($ComparisonCollectionPlanPath)) {
            throw 'Required Money rebind comparison collection plan is missing.'
        }
        $rebindManifestPath = $manifestRebindOutput
        $rebindReceiptPath = $receiptRebindOutput
        Write-JsonFile $assemblyPath ([ordered]@{
            schema_version = 'money_revenue_terminal_rebind_manifest_assembly_request.v1'
            comparison_collection_plan_path = $ComparisonCollectionPlanPath
            rollout_proofs = $RolloutProofs
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
            'build-money-revenue-terminal-rebind-manifest', '--request', $assemblyPath, '--output', $rebindManifestPath)
        Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
            'build-money-revenue-terminal-candidate-rebind', '--manifest', $rebindManifestPath, '--output', $rebindReceiptPath)
    }
    elseif ($RolloutProofs.Count -gt 0) {
        throw 'Direct-only Money initialization must not ignore supplied rollout proofs.'
    }
    Write-JsonFile $manifestPath ([ordered]@{
        schema_version = 'money_revenue_concrete_alternative_denominator_manifest.v1'
        alternative_denominator_catalog_path = $AlternativePath
        terminal_rebind_manifest_path = $rebindManifestPath
        terminal_rebind_receipt_path = $rebindReceiptPath
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        'build-money-revenue-concrete-alternative-denominator', '--manifest', $manifestPath, '--output', $catalogPath)
    return [pscustomobject]@{
        RebindManifestPath = $rebindManifestPath
        RebindReceiptPath = $rebindReceiptPath
        ConcreteCatalogPath = $catalogPath
    }
}
