function New-RuntimeRustyKeyPlanning {
    param($Context, [string] $BootstrapDll, [string] $Directory,
        [string] $SnapshotPath, [string] $StrategyLedgerPath,
        [string] $RequirementInventoryPath, [string] $AcquisitionLoweringPath,
        [string] $MasterAnglerWindowsPath, [string] $RouteTimingPath,
        [string] $FishingForecastManifestPath, $PriorSupport)

    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $core = Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll'
    $snapshot = Read-RuntimeLogicalJsonArtifact -Path $SnapshotPath -CoreAssemblyPath $core
    $controlPath = Join-Path $Directory 'control.json'
    $proposalPath = Join-Path $Directory 'proposal.json'
    $admissionPath = Join-Path $Directory 'admission.json'
    $queuePath = Join-Path $Directory 'queue.json'
    $common = @('--requirement-inventory', $RequirementInventoryPath,
        '--snapshot', $SnapshotPath, '--strategy-ledger', $StrategyLedgerPath)
    $exports = @('--output', $controlPath, '--selected-queue-output', $queuePath,
        '--selected-proposal-output', $proposalPath, '--selected-admission-output', $admissionPath)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll `
        -Arguments (@('build-rusty-key-rolling-supply') + $common + $exports) -AllowedExitCodes @(0, 2)
    $control = Get-Content -LiteralPath $controlPath -Raw | ConvertFrom-Json
    $proofArgs = @()
    $supportCommon = @()
    if ($control.blocking_reasons -contains 'rusty_key_rolling_acquisition_source_chain_required') {
        $paths = @{}
        foreach ($name in @('calendar','target_calendar','unlock','festival','location','facility',
                'resource','currency','reservation','processing','fishing_probability','stochastic_retry',
                'daily_time_energy','opportunity_cost')) {
            $paths[$name] = Join-Path $Directory "$name.json"
        }
        $paths.forecast = $FishingForecastManifestPath
        $pipeline = Invoke-RuntimeAcquisitionPlanningPipeline -BootstrapDll $BootstrapDll `
            -OutputDirectory $Directory -BatchName 'planning' -SnapshotPath $SnapshotPath `
            -StrategyLedgerPath $StrategyLedgerPath -TargetTotalDay ([int](Read-StateValue $snapshot 'time' 'total_days')) `
            -RequirementInventoryPath $RequirementInventoryPath -AcquisitionLoweringPath $AcquisitionLoweringPath `
            -MasterAnglerWindowsPath $MasterAnglerWindowsPath -RouteTimingPath $RouteTimingPath -Paths $paths -CalendarMode static
        $common = @($pipeline.PortfolioCommon)
        if ($null -ne $PriorSupport) {
            $next = @()
            foreach ($value in $common) {
                $next += if ([string]$value -like '--*') { '--next-' + ([string]$value).Substring(2) } else { $value }
            }
            $replanPath = Join-Path $Directory 'support-replan.json'
            Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
                @('build-acquisition-route-supporting-transition-replan') + $PriorSupport.Common +
                $PriorSupport.ProofCommon + $next + @('--output', $replanPath,
                    '--preference-request-output', (Join-Path $Directory 'support-scoped-request.json')))
            $proofPath = Join-Path $Directory 'control-support-proof.json'
            Write-JsonFile -Path $proofPath -Value ([ordered]@{
                request_inputs = $PriorSupport.Inputs
                settlement_proof = $PriorSupport.Proof
                replan_admission_path = $replanPath
            })
            $proofArgs = @('--control-supporting-transition-proof', $proofPath)
        }
        Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll `
            -Arguments (@('build-rusty-key-rolling-supply') + $common + $proofArgs + $exports) -AllowedExitCodes @(0, 2)
        $control = Get-Content -LiteralPath $controlPath -Raw | ConvertFrom-Json
    }
    $rankingPath = Join-Path $Directory 'ranking.json'
    if ($null -ne $control.selected_acquisition_proposal) {
        Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments (
            @('build-rusty-key-current-ranking') + $common + $proofArgs +
            @('--portfolio-control-selection', $controlPath, '--output', $rankingPath))
        $supportCommon = $common + @('--ranking', $rankingPath, '--route-occurrence-id', $control.selected_candidate_id,
            '--support-deadline-total-day', ([string]([int]$control.source_total_day + 1)), '--use-control-selection', 'true')
    }
    $preparePath = Join-Path $Directory 'preparation.json'
    $prepareArgs = @('prepare-rusty-key-rolling-step') + $common + $proofArgs +
        @('--portfolio-control-selection', $controlPath, '--output', $preparePath)
    if (Test-Path -LiteralPath $rankingPath) { $prepareArgs += @('--ranking', $rankingPath) }
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments $prepareArgs -AllowedExitCodes @(0, 2)
    return [pscustomobject]@{
        Directory = $Directory; Common = $common; ProofArgs = $proofArgs; Control = $control
        ControlPath = $controlPath; ProposalPath = $proposalPath; AdmissionPath = $admissionPath
        QueuePath = $queuePath; RankingPath = $rankingPath; SnapshotPath = $SnapshotPath
        SupportCommon = $supportCommon; Preparation = (Get-Content -LiteralPath $preparePath -Raw | ConvertFrom-Json)
    }
}
