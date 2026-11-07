[CmdletBinding()]
param([Parameter(Mandatory)] [string] $OutputRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeMoneyConcreteInitialization.ps1')
if (Test-Path -LiteralPath $OutputRoot) { throw 'Direct-concrete test output already exists.' }
$root = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $root | Out-Null
$cli = Join-Path $repo 'experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll'
$fixture = Join-Path $root 'fixture'
Invoke-RuntimeBootstrap -BootstrapDll $cli -Arguments @('self-test-money-direct-concrete', '--output-root', $fixture)
$alternative = Join-Path $fixture 'alternative-catalog.json'
$before = Get-DirectoryContentHash $fixture
$directory = Join-Path $root 'direct-initialization'
$result = Initialize-RuntimeMoneyConcreteDenominator -BootstrapDll $cli -AlternativePath $alternative `
    -OutputDirectory $directory -FreshRebindAlternativeCount 0
$catalog = Get-Content -LiteralPath $result.ConcreteCatalogPath -Raw | ConvertFrom-Json
if ($result.RebindManifestPath -cne '' -or $result.RebindReceiptPath -cne '' -or
    $catalog.current_concrete_denominator_authoritative -ne $true -or $catalog.direct_alternative_count -ne 2 -or
    $catalog.rebound_alternative_count -ne 0 -or $catalog.production_teacher_preference_label_eligible -ne $false -or
    $catalog.long_horizon_denominator_authoritative -ne $false -or $catalog.formal_product_training_authorized -ne $false) {
    throw 'Direct-only initializer lost bounded concrete authority or invented a receipt.'
}
foreach ($name in @('money-revenue-terminal-rebind-manifest-assembly-request.json',
    'money-revenue-terminal-rebind-manifest.json', 'money-revenue-terminal-rebind-receipt.json')) {
    if (Test-Path -LiteralPath (Join-Path $directory $name)) { throw 'Direct-only initializer emitted unnecessary rebind evidence.' }
}
$rejected = 0
foreach ($case in @('negative-count', 'unused-rollout', 'missing-required-plan', 'source-output-alias')) {
    try {
        $parameters = @{ BootstrapDll = $cli; AlternativePath = $alternative;
            OutputDirectory = (Join-Path $root $case); FreshRebindAlternativeCount = 0 }
        switch ($case) {
            'negative-count' { $parameters.FreshRebindAlternativeCount = -1 }
            'unused-rollout' { $parameters.RolloutProofs = @(@{ alternative_id = 'not-required' }) }
            'missing-required-plan' { $parameters.FreshRebindAlternativeCount = 1 }
            'source-output-alias' {
                $parameters.AlternativePath = Join-Path $parameters.OutputDirectory 'money-revenue-concrete-alternative-denominator-manifest.json'
                New-Item -ItemType Directory -Path $parameters.OutputDirectory | Out-Null
                Copy-Item -LiteralPath $alternative -Destination $parameters.AlternativePath
                $aliasHash = Get-RuntimeFileSha256 $parameters.AlternativePath
            }
        }
        $null = Initialize-RuntimeMoneyConcreteDenominator @parameters
    }
    catch {
        $failure = $_
        $expected = switch ($case) {
            'negative-count' { $failure.FullyQualifiedErrorId -ceq 'ParameterArgumentValidationError,Initialize-RuntimeMoneyConcreteDenominator' }
            'unused-rollout' { $failure.Exception.Message -like '*must not ignore supplied rollout proofs*' }
            'missing-required-plan' { $failure.Exception.Message -like '*rebind comparison collection plan is missing*' }
            'source-output-alias' {
                $failure.Exception.Message -like '*output aliases a source input*' -and
                    (Get-RuntimeFileSha256 $parameters.AlternativePath) -ceq $aliasHash
            }
        }
        if (-not $expected) { throw }
        $rejected++; continue
    }
    throw "Initializer accepted $case."
}
if ((Get-DirectoryContentHash $fixture) -cne $before) { throw 'Initializer changed the original public fixture source.' }
Write-JsonFile (Join-Path $root 'summary.json') @{ status = 'passed'; rejected_cases = $rejected
    original_sources_unchanged = $true; unnecessary_rebind_files_created = $false; game_started = $false }
$global:LASTEXITCODE = 0
'PASS: original direct-concrete initializer, absent rebind files and four rejection scenarios.'
