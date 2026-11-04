[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $StepFixtureRoot,
    [string] $CurrentNativeFixtureRoot = '',
    [string] $CapturedControlObservationDirectory = '',
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('rusty-key-runtime-test-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyEvidence.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeAcquisitionPlanningPipeline.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingPlanning.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingExecution.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingLoop.ps1')
$bootstrap = Join-Path $project 'experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll'
$loop = Join-Path $project 'tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'
$passed = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
function Assert-True([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }

# Exercise the actual shared 14-axis batch, source-bound ranking and preparation CLIs without a game or HTTP server.
$stages = @('support','terminal')
if (-not [string]::IsNullOrWhiteSpace($CurrentNativeFixtureRoot)) { $stages += 'geode' }
foreach ($stage in $stages) {
    $fixture = if ($stage -eq 'geode') { $CurrentNativeFixtureRoot } else { Join-Path $StepFixtureRoot $stage }
    $inputPath = Join-Path $fixture 'current-stage-one-collection-teacher-frontier-fixture/runtime-inputs.json'
    $source = Get-Content -LiteralPath $inputPath -Raw | ConvertFrom-Json
    $context = New-RuntimeQueueContext -LoopDll $loop -ArtifactDirectory $OutputRoot `
        -BackendUrl 'http://unused.invalid' -SnapshotUrl 'http://unused.invalid' -SnapshotProfile full `
        -ExecutorRoot 'http://unused.invalid' -ProductUrl 'http://unused.invalid' -RunId 'synthetic-runtime' -IsolatedSavesPath $OutputRoot
    $planning = New-RuntimeRustyKeyPlanning -Context $context -BootstrapDll $bootstrap `
        -Directory (Join-Path $OutputRoot "actual-$stage-planning") -SnapshotPath $source.before_snapshot_path `
        -StrategyLedgerPath $source.strategy_ledger_path -RequirementInventoryPath $source.requirement_inventory_path `
        -AcquisitionLoweringPath $source.acquisition_lowering_path -MasterAnglerWindowsPath $source.master_angler_windows_path `
        -RouteTimingPath $source.route_timing_calibration_path -FishingForecastManifestPath $source.fishing_forecast_manifest_path
    $expected = if ($stage -eq 'support') { 'supporting_transition' } else { 'terminal_acquisition' }
    Assert-True ($planning.Preparation.step_ready -and $planning.Preparation.kind -eq $expected) (
        "Actual $stage planning did not compile: " + (@($planning.Preparation.blocking_reasons) -join ','))
    Assert-True (-not $planning.Preparation.execution_authorized) 'Preparation must not authorize native execution.'
    $passed.Add("actual_source_pipeline_$stage")
}

foreach ($mode in @('held', 'local', 'remote', 'deferred')) {
    $fixture = Join-Path $StepFixtureRoot $(if ($mode -eq 'held') { 'held-donation-remote' } else { "custody-donation-$mode" })
    $context = New-RuntimeQueueContext -LoopDll $loop -ArtifactDirectory $OutputRoot `
        -BackendUrl 'http://unused.invalid' -SnapshotUrl 'http://unused.invalid' -SnapshotProfile full `
        -ExecutorRoot 'http://unused.invalid' -ProductUrl 'http://unused.invalid' -RunId 'synthetic-custody-calibration' -IsolatedSavesPath $OutputRoot
    $planning = New-RuntimeRustyKeyPlanning -Context $context -BootstrapDll $bootstrap `
        -Directory (Join-Path $OutputRoot "actual-custody-$mode-planning") -SnapshotPath (Join-Path $fixture 'snapshot.json') `
        -StrategyLedgerPath (Join-Path $fixture 'ledger.json') -RequirementInventoryPath (Join-Path $fixture 'inventory.json') `
        -AcquisitionLoweringPath 'unused-owned-stock-path' -MasterAnglerWindowsPath 'unused-owned-stock-path' `
        -RouteTimingPath (Join-Path $fixture 'timing.json') -FishingForecastManifestPath 'unused-owned-stock-path'
    $routeReady = if ($mode -eq 'held') { $planning.Control.selected_donation_route_schedule.current_day_route_ready }
        else { $planning.Control.selected_custody_donation_schedule.current_day_custody_donation_route_ready }
    Assert-True ($routeReady -eq $true) `
        "Actual $mode lightweight planning omitted route calibration."
    Assert-True ($planning.Preparation.step_ready -and $planning.Common -contains '--route-timing-calibration' -and
        $null -eq $planning.Control.selected_acquisition_proposal -and
        -not (Test-Path -LiteralPath (Join-Path $planning.Directory 'planning.manifest.json'))) `
        'Owned stock must forward calibration through build and preparation without rebuilding acquisition axes.'
    $passed.Add("actual_owned_stock_light_path_calibration_$mode")
}

foreach ($mode in @('evening', 'sleep_prompt', 'native_settle')) {
    $fixture = Join-Path $StepFixtureRoot "owned-deferral-$mode"
    $planning = New-RuntimeRustyKeyPlanning -Context $context -BootstrapDll $bootstrap `
        -Directory (Join-Path $OutputRoot "actual-deferral-$mode-planning") -SnapshotPath (Join-Path $fixture 'snapshot.json') `
        -StrategyLedgerPath (Join-Path $fixture 'ledger.json') -RequirementInventoryPath (Join-Path $fixture 'inventory.json') `
        -AcquisitionLoweringPath 'unused-owned-stock-path' -MasterAnglerWindowsPath 'unused-owned-stock-path' `
        -RouteTimingPath (Join-Path $fixture 'timing.json') -FishingForecastManifestPath 'unused-owned-stock-path'
    Assert-True ($planning.Preparation.step_ready -and $planning.Preparation.kind -ceq 'owned_supply_deferral' -and
        $planning.Preparation.owned_supply_deferral.future_date_route_verified -eq $false -and
        $null -eq $planning.Preparation.atomic_commit_request -and
        -not (Test-Path -LiteralPath (Join-Path $planning.Directory 'planning.manifest.json'))) `
        'Owned-supply recovery must rebuild its original queue without speculative acquisition or future-date evidence.'
    $passed.Add("actual_owned_supply_deferral_light_path_$mode")
}

$originalReader = (Get-Item Function:Read-RuntimeLoopArtifacts).ScriptBlock
$originalRanking = (Get-Item Function:Invoke-RuntimeRanking).ScriptBlock
$script:optionsParser = ([Reflection.Assembly]::LoadFrom($loop)).GetType('LiveTrainingOptions').GetMethod('Parse')
function Read-RuntimeLoopArtifacts { param($LoopRoot, $RunId, $CoreAssemblyPath, [switch] $AllowVerifiedContinuation)
    return [pscustomobject]@{ VerifiedContinuationPending = $false; VerifiedObjectiveCompleted = $false
        Execution = $script:observationReceipt }
}
$script:observationReceipt = @{ option_id = 'executor.wait_ticks'; status = 'applied'
    primitive_verification_status = 'verified'; selected_candidate_id = 'recovery:wait_for_native_control_settle'
    selected_candidate_completed = $true; queue_id = 'synthetic-queue'; before_state_hash = 'synthetic-before' }
$script:dailyExitCode = 0
$script:observationReport = @{ stop_reason = 'primary_training_target_not_met'; run_id = $context.RunId
    last_queue_id = 'synthetic-queue'; last_state_hash = 'synthetic-before'; attempts_started = 1
    primary_attempts_started = 1; required_verified_actions = 1; verified_actions = 0 }
function dotnet {
    $script:dailyArguments = @($args | ForEach-Object { $_ })
    $parserArguments = [object[]]::new(1)
    $parserArguments[0] = [string[]]$script:dailyArguments[1..($script:dailyArguments.Count - 1)]
    [void]$script:optionsParser.Invoke($null, $parserArguments)
    $rootIndex = [array]::IndexOf($script:dailyArguments, '--root')
    New-Item -ItemType Directory -Path $script:dailyArguments[$rootIndex + 1] -Force | Out-Null
    if ($script:dailyExitCode -eq 2) {
        Write-JsonFile -Path (Join-Path $script:dailyArguments[$rootIndex + 1] "runs/$($context.RunId)/live-training-loop-report.json") `
            -Value $script:observationReport
    }
    $global:LASTEXITCODE = $script:dailyExitCode
}
try {
    $sourceManifest = Join-Path $OutputRoot 'source/relative-manifest.json'
    Write-Utf8Text -Path $sourceManifest -Value '{"schema_version":"stardewai.content_addressed_json.v1","blob_path":"relative.json.gz"}'
    $context.ProductUrl = 'http://product.invalid'
    $context.ExecutorRoot = 'http://harness.invalid'
    foreach ($target in @('product','runtime_harness')) {
        Invoke-RuntimeDailyPlanStep -Context $context -Name "adapter-$target" -OptionId 'recovery.stabilize_day' `
            -CandidateId 'synthetic' -CandidateKind 'synthetic' -SourceSnapshotPath $sourceManifest -ExecutionTarget $target | Out-Null
        $snapshotIndex = [array]::IndexOf($script:dailyArguments, '--snapshot-file')
        $executorIndex = [array]::IndexOf($script:dailyArguments, '--executor-url')
        $expectedUrl = if ($target -eq 'product') { $context.ProductUrl } else { $context.ExecutorRoot }
        Assert-True ($script:dailyArguments[$snapshotIndex + 1] -eq [IO.Path]::GetFullPath($sourceManifest)) `
            'Copying a relative manifest would invalidate its content-addressed blob reference.'
        Assert-True ($script:dailyArguments[$executorIndex + 1] -eq $expectedUrl -and
            ($script:dailyArguments -contains '--use-product-executor') -eq ($target -eq 'product') -and
            $script:dailyArguments -contains '--skip-training') 'Daily-plan adapter changed executor or training semantics.'
        $passed.Add("shared_adapter_manifest_and_executor_$target")
    }
    function Invoke-RuntimeRanking { param($BackendUrl, $GoalId, $SnapshotStateHash, $OutputPath, $OptionId, $Parameters)
        return @{ ranked_event_candidates = @($(if ($OptionId -eq 'recovery.stabilize_day') {
            $script:handoffRecoveryCandidate
        } else { $script:handoffCandidate })) }
    }
    foreach ($candidate in @(
        @{ candidate_id = 'recovery:native_save_boundary'; kind = 'recovery_sleep_immediately'; time = 900 },
        @{ candidate_id = 'recovery:wait_for_native_control_settle'; kind = 'recovery_native_control_settle'; time = 900 },
        @{ candidate_id = 'recovery:wait_for_native_control_settle'; kind = 'recovery_native_control_settle'; time = 600 },
        @{ candidate_id = 'route:farm'; kind = 'route_connector_tile'; time = 600
            parameters = @(@{name='expected_target_location';value='Farm'}) }
    )) {
        $exploration = $candidate.kind -eq 'route_connector_tile'
        $expectedOption = if ($exploration) { 'exploration.visit_location' } else { 'recovery.stabilize_day' }
        $script:handoffCandidate = $candidate + @{ option_id = $expectedOption; available = $true }
        $script:handoffRecoveryCandidate = if ($exploration) {
            @{candidate_id='recovery:native_save_boundary';kind='recovery_sleep_immediately';option_id='recovery.stabilize_day';available=$true}
        } else { $script:handoffCandidate }
        $snapshot = @{ state_hash = 'synthetic-handoff'; state = @{
            time = @{ time = @{ value = $candidate.time } }; player = @{ story_event = @{ value = @{ active = $false } } }
        } }
        Invoke-RuntimeRustyKeyNativeHandoff -Context $context -Name $candidate.kind -SnapshotPath $sourceManifest `
            -Snapshot $snapshot -Directory $OutputRoot -ExecutionTarget 'runtime_harness' | Out-Null
        $idIndex = [array]::IndexOf($script:dailyArguments, '--daily-plan-candidate-id')
        $requiredIndex = [array]::IndexOf($script:dailyArguments, '--required-verified-actions')
        Assert-True ($idIndex -ge 0 -and $script:dailyArguments[$idIndex + 1] -eq $candidate.candidate_id -and
            $requiredIndex -ge 0 -and $script:dailyArguments[$requiredIndex + 1] -eq '1' -and
            ($script:dailyArguments -contains 'control_plane.native_save_boundary=true') -eq (-not $exploration)) `
            'Native handoff must send the actual bounded settle/save candidate through the existing daily-plan adapter.'
        $passed.Add("native_handoff_shared_adapter_$($candidate.kind)_$($candidate.time)")
    }
    $passed.Add('actual_adapter_arguments_pass_actual_cli_parser')
    $zeroArguments = [object[]]::new(1)
    $zeroValues = [string[]]$script:dailyArguments[1..($script:dailyArguments.Count - 1)]
    $zeroValues[[array]::IndexOf($zeroValues, '--required-verified-actions') + 1] = '0'
    $zeroArguments[0] = $zeroValues
    $rejected = $false
    try { [void]$script:optionsParser.Invoke($null, $zeroArguments) }
    catch { $rejected = $_.Exception.ToString().Contains('--emit-queue-execution-receipt requires one attempt and one required verified action.') }
    Assert-True $rejected 'Actual CLI parsing must continue rejecting the incompatible zero-work target.'
    $passed.Add('actual_cli_parser_rejects_zero_receipt_target')
    foreach ($change in @(
        @{ primitive_verification_status = 'observed_mismatch' }, @{ selected_candidate_completed = $false },
        @{ selected_candidate_id = 'foreign-wait' }, @{ option_id = 'executor.sleep' }
    )) {
        $original = $script:observationReceipt
        $script:observationReceipt = $original.Clone()
        foreach ($name in $change.Keys) { $script:observationReceipt[$name] = $change[$name] }
        $rejected = $false
        try {
            Invoke-RuntimeDailyPlanStep -Context $context -Name ('invalid-wait-' + $passed.Count) `
                -OptionId 'recovery.stabilize_day' -CandidateId 'recovery:wait_for_native_control_settle' `
                -CandidateKind 'recovery_native_control_settle' -SourceSnapshotPath $sourceManifest | Out-Null
        } catch { $rejected = $true }
        finally { $script:observationReceipt = $original }
        Assert-True $rejected 'An unverified, incomplete or mismatched observation must not pass.'
        $passed.Add('rejected_control_observation_' + ($change.Keys -join '_'))
    }
    $script:dailyExitCode = 2
    Invoke-RuntimeDailyPlanStep -Context $context -Name 'verified-observation-exit-2' -OptionId 'recovery.stabilize_day' `
        -CandidateId 'recovery:wait_for_native_control_settle' -CandidateKind 'recovery_native_control_settle' `
        -SourceSnapshotPath $sourceManifest | Out-Null
    $passed.Add('exact_observation_exit_2_not_productive_work')
    foreach ($change in @(
        @{ stop_reason = 'unrelated_failure' }, @{ run_id = 'foreign' }, @{ last_queue_id = 'foreign' },
        @{ last_state_hash = 'foreign' }, @{ attempts_started = 2 }, @{ primary_attempts_started = 2 },
        @{ required_verified_actions = 0 }, @{ verified_actions = 1 }
    )) {
        $original = $script:observationReport
        $script:observationReport = $original.Clone()
        foreach ($name in $change.Keys) { $script:observationReport[$name] = $change[$name] }
        $rejected = $false
        try {
            Invoke-RuntimeDailyPlanStep -Context $context -Name ('invalid-exit-report-' + $passed.Count) `
                -OptionId 'recovery.stabilize_day' -CandidateId 'recovery:wait_for_native_control_settle' `
                -CandidateKind 'recovery_native_control_settle' -SourceSnapshotPath $sourceManifest | Out-Null
        } catch { $rejected = $true }
        finally { $script:observationReport = $original }
        Assert-True $rejected 'A foreign or unrelated incomplete CLI report must not authorize observation continuation.'
        $passed.Add('rejected_observation_report_' + ($change.Keys -join '_'))
    }
    foreach ($case in @(
        @{ id = 'recovery:wait_for_native_control_settle'; kind = 'recovery_native_control_settle'; exit = 1 },
        @{ id = 'recovery:native_save_boundary'; kind = 'recovery_sleep_immediately'; exit = 2 }
    )) {
        $script:dailyExitCode = $case.exit
        $rejected = $false
        try {
            Invoke-RuntimeDailyPlanStep -Context $context -Name ('unrelated-exit-' + $passed.Count) `
                -OptionId 'recovery.stabilize_day' -CandidateId $case.id -CandidateKind $case.kind `
                -SourceSnapshotPath $sourceManifest | Out-Null
        } catch { $rejected = $true }
        Assert-True $rejected 'Unrelated exit codes and non-observation target failures must remain failures.'
        $passed.Add('unrelated_exit_not_accepted_' + $case.exit)
    }
    $script:dailyExitCode = 2
    if (-not [string]::IsNullOrWhiteSpace($CapturedControlObservationDirectory)) {
        $savedRunId = $context.RunId
        $script:observationReceipt = Get-Content -LiteralPath (Join-Path $CapturedControlObservationDirectory 'live-snapshots/execution-0001.json') -Raw | ConvertFrom-Json
        $script:observationReport = Get-Content -LiteralPath (Join-Path $CapturedControlObservationDirectory 'live-training-loop-report.json') -Raw | ConvertFrom-Json
        $context.RunId = $script:observationReport.run_id
        try {
            Invoke-RuntimeDailyPlanStep -Context $context -Name 'retained-native-observation-adapter-replay' `
                -OptionId 'recovery.stabilize_day' -CandidateId 'recovery:wait_for_native_control_settle' `
                -CandidateKind 'recovery_native_control_settle' -SourceSnapshotPath $sourceManifest | Out-Null
        } finally { $context.RunId = $savedRunId }
        $passed.Add('retained_native_observation_receipt_and_report_adapter_replay')
    }
}
finally {
    Remove-Item Function:dotnet
    Set-Item Function:Read-RuntimeLoopArtifacts $originalReader
    Set-Item Function:Invoke-RuntimeRanking $originalRanking
    # Expected mocked failures must not leak into the pwsh test host's exit code.
    $global:LASTEXITCODE = 0
}

# The following test doubles cover orchestration only; native transitions are covered separately by the canonical C# verifiers.
$script:mode = 'trace'
$script:executions = 0
$script:planningCalls = 0
$script:seen = [Collections.Generic.List[string]]::new()
function New-TestSnapshot([int] $Index, [bool] $Key = $false) {
    $save = if ($script:mode.StartsWith('drift') -and $Index -gt 0) { 'foreign-save' } else { 'synthetic-save' }
    return [pscustomobject]@{
        save_id = @{ value = $save }; player_id = @{ value = '1' }; state_hash = "synthetic-$Index"
        state = @{ world_progress = @{ museum = @{
            status = $(if ($script:mode -eq 'stale-key' -and $Key) { 'stale' } else { 'available' })
            value = @{ has_rusty_key = $Key; rusty_key_event_seen = $Key }
        } } }
    }
}
function Get-FreshRuntimeSnapshot { param($Url, $TimeoutSeconds)
    $snapshot = New-TestSnapshot 0
    return [pscustomobject]@{ Value = $snapshot; Raw = ($snapshot | ConvertTo-Json -Depth 20) }
}
function Save-RuntimeSnapshotAndIngest { param($Path, $Capture, $BackendUrl, $SnapshotProfile)
    Write-Utf8Text -Path $Path -Value $Capture.Raw
}
function Invoke-WebRequest { param($Uri, $TimeoutSec, [switch] $UseBasicParsing)
    return [pscustomobject]@{ Content = '{"schema_version":"synthetic_loop_only_ledger"}' }
}
function New-RuntimeRustyKeyPlanning {
    param($Context, $BootstrapDll, $Directory, $SnapshotPath, $StrategyLedgerPath, $RequirementInventoryPath,
        $AcquisitionLoweringPath, $MasterAnglerWindowsPath, $RouteTimingPath, $FishingForecastManifestPath, $PriorSupport)
    $script:planningCalls++
    $kind = if ($script:mode -eq 'blocked') { 'blocked' }
        elseif ($script:mode.StartsWith('deferral') -and $script:planningCalls -le 2) { 'owned_supply_deferral' }
        elseif ($script:mode -eq 'custody' -and $script:planningCalls -eq 1) { 'custody_navigation' }
        elseif ($script:mode -eq 'custody' -and $script:planningCalls -eq 2) { 'custody_transfer' }
        elseif ($script:mode -eq 'donation-boundary' -and $script:planningCalls -gt 3) { 'native_key_handoff' }
        elseif ($script:planningCalls -eq 1) { 'supporting_transition' }
        elseif ($script:planningCalls -eq 2) { 'terminal_acquisition' } else { 'held_donation' }
    if ($script:planningCalls -eq 2 -and $script:mode -ne 'custody' -and -not $script:mode.StartsWith('deferral')) {
        Assert-True ($PriorSupport.Token -eq 'retained-support' -and $StrategyLedgerPath -eq 'exact-settled-ledger-path') `
            'A supporting transition lost its exact retained proof/ledger before replanning.'
    }
    $deferral = if ($script:mode -eq 'deferral-invalid') { $null } else { @{
        recovery_candidate_id = $(if ($script:planningCalls -eq 1) { 'recovery:return_home' } else { 'recovery:wait_for_native_control_settle' })
        recovery_candidate_kind = $(if ($script:planningCalls -eq 1) { 'recovery_return_home' } else { 'recovery_native_control_settle' })
    } }
    return [pscustomobject]@{
        Preparation = @{ kind = $kind; step_ready = ($kind -ne 'blocked'); blocking_reasons = @('synthetic_block'); owned_supply_deferral = $deferral }
        ControlPath = 'synthetic-control'; QueuePath = 'synthetic-queue'; Directory = $Directory
        Control = @{ threshold_satisfied = ($kind -ceq 'native_key_handoff') }
        SnapshotPath = $SnapshotPath
    }
}
function New-TestExecution($Context, [string] $Name, [string] $Kind) {
    $script:executions++
    $script:seen.Add($Kind)
    if ($script:mode -in @('error', 'deferral-error')) { throw 'synthetic ambiguous native/network failure' }
    $path = Join-Path $Context.ArtifactDirectory "$Name-after.json"
    $after = New-TestSnapshot $script:executions ($Kind -eq 'held' -and $script:mode -ne 'donation-boundary')
    Write-JsonFile -Path $path -Value $after
    return [pscustomobject]@{ QueuePath = 'synthetic-queue'; ExecutionPath = 'synthetic-receipt'; AfterPath = $path; After = $after }
}
function Invoke-RuntimeRustyKeySupport {
    param($Context, $BootstrapDll, $Planning, $Name, $ExecutionTarget, $ExecutorVersion)
    Assert-True ($ExecutionTarget -eq 'product' -and $ExecutorVersion -eq 'product_executor.v1') 'Executor identity changed.'
    return [pscustomobject]@{ Execution = (New-TestExecution $Context $Name 'support')
        LedgerPath = 'exact-settled-ledger-path'; Support = @{ Token = 'retained-support' } }
}
function Invoke-RuntimeRustyKeyTerminal {
    param($Context, $BootstrapDll, $Planning, $Name, $ExecutionTarget, $ExecutorVersion)
    return [pscustomobject]@{ Execution = (New-TestExecution $Context $Name 'terminal'); LedgerPath = 'settled-custody'; Support = $null }
}
function Invoke-RuntimePrecompiledQueue {
    param($Context, $Name, $QueuePath, $BeforePath, $BeforeSnapshotProfile, $ExecutionTarget)
    if ($script:mode -eq 'custody' -and $script:planningCalls -eq 1) { return New-TestExecution $Context $Name 'navigation' }
    return New-TestExecution $Context $Name 'held'
}
function Invoke-RuntimeRustyKeyCustodyTransfer {
    param($Context, $BootstrapDll, $Planning, $Name, $ExecutionTarget)
    Assert-True ($ExecutionTarget -eq 'product') 'Custody retrieval changed executor selection.'
    return [pscustomobject]@{ Execution = (New-TestExecution $Context $Name 'transfer'); LedgerPath = 'settled-retrieval'; Support = $null }
}
function Invoke-RuntimeDailyPlanStep {
    param($Context, $Name, $OptionId, $CandidateId, $CandidateKind, $SourceSnapshotPath,
        [switch] $AllowVerifiedContinuation, $ExecutionTarget)
    Assert-True ($OptionId -ceq 'recovery.stabilize_day' -and $AllowVerifiedContinuation -and $ExecutionTarget -ceq 'product') `
        'Deferral must use the original exact native continuation adapter, without forced save parameters.'
    $before = Get-Content -LiteralPath $SourceSnapshotPath -Raw | ConvertFrom-Json
    Assert-True ($before.state_hash -ceq "synthetic-$($script:executions)") 'Recovery reused a stale source instead of the latest observation.'
    $expected = if ($script:executions -eq 0) { 'recovery:return_home' } else { 'recovery:wait_for_native_control_settle' }
    Assert-True ($CandidateId -ceq $expected) 'Recovery lost its rebuilt candidate identity.'
    return New-TestExecution $Context $Name 'recovery'
}
function Invoke-TestLoop([string] $Mode, [int] $Budget) {
    $script:mode = $Mode; $script:executions = 0; $script:planningCalls = 0; $script:seen.Clear()
    $directory = Join-Path $OutputRoot "loop-$Mode"
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $context = New-RuntimeQueueContext -LoopDll $loop -ArtifactDirectory $directory `
        -BackendUrl 'http://unused.invalid' -SnapshotUrl 'http://unused.invalid' -SnapshotProfile full `
        -ExecutorRoot 'http://unused.invalid' -ProductUrl 'http://unused.invalid' -RunId "synthetic-$Mode" -IsolatedSavesPath $directory
    return Invoke-RuntimeRustyKeyRollingLoop -Context $context -BootstrapDll $bootstrap `
        -RequirementInventoryPath 'unused' -AcquisitionLoweringPath 'unused' -MasterAnglerWindowsPath 'unused' `
        -RouteTimingPath 'unused' -FishingForecastManifestPath 'unused' -MaxSteps $Budget -MinFreeSpaceMb 1024 `
        -StopAfterDonationThreshold:($Mode -eq 'donation-boundary')
}
$trace = Invoke-TestLoop 'trace' 3
Assert-True ($trace.status -eq 'native_key_observed' -and ($script:seen -join ',') -eq 'support,terminal,held') 'Rolling stages did not advance in order.'
Assert-True (-not $trace.formal_training_authorized -and -not $trace.criterion_promoted) 'Test orchestration promoted training/coverage.'
Assert-True ($trace.last_ledger_path -eq $trace.steps[-1].ledger_path -and
    $trace.last_ledger_path.EndsWith('donation-settled-ledger.json') -and (Test-Path -LiteralPath $trace.last_ledger_path)) `
    'The final donation must retain its post-settlement ledger rather than the pre-donation lease.'
$passed.Add('support_terminal_donation_order_and_retained_proof')
$custodyTrace = Invoke-TestLoop 'custody' 3
Assert-True ($custodyTrace.status -eq 'native_key_observed' -and ($script:seen -join ',') -eq 'navigation,transfer,held' -and
    ($custodyTrace.steps.kind -join ',') -eq 'custody_navigation,custody_transfer,held_donation') `
    'Stored custody must navigate, transfer and return to the original donation stage with fresh replanning.'
$passed.Add('custody_navigation_transfer_donation_order')
$deferralTrace = Invoke-TestLoop 'deferral' 3
Assert-True ($deferralTrace.status -ceq 'native_key_observed' -and ($script:seen -join ',') -ceq 'recovery,recovery,held' -and
    ($deferralTrace.steps.kind -join ',') -ceq 'owned_supply_deferral,owned_supply_deferral,held_donation' -and
    $deferralTrace.steps[0].ledger_path.EndsWith('owned-supply-recovery-ledger.json')) `
    'Recovery must refresh the exact source and ledger before returning to ordinary donation; no duplicate acquisition.'
$passed.Add('owned_supply_deferral_fresh_replan_then_donation')
foreach ($mode in @('deferral-invalid', 'deferral-error')) {
    $rejected = $false
    try { Invoke-TestLoop $mode 8 | Out-Null } catch { $rejected = $true }
    $expected = if ($mode -ceq 'deferral-invalid') { 0 } else { 1 }
    Assert-True ($rejected -and $script:executions -eq $expected) 'Invalid or failed recovery must stop without blind retry.'
    $passed.Add("owned_supply_no_retry_$mode")
}
$donationBoundary = Invoke-TestLoop 'donation-boundary' 8
Assert-True ($donationBoundary.status -ceq 'native_donation_threshold_observed' -and
    $donationBoundary.donation_threshold_observed -eq $true -and -not $donationBoundary.actual_key_observed -and
    $script:executions -eq 3 -and $script:planningCalls -eq 4) `
    'The explicit local donation boundary must stop before dispatching the already accepted night/key flow.'
$passed.Add('local_donation_boundary_does_not_reopen_key_gate')
$replayRejected = $false
try { Invoke-TestLoop 'trace' 3 | Out-Null } catch { $replayRejected = $true }
Assert-True ($replayRejected -and $script:executions -eq 0) 'An existing run must not replay any native action.'
$passed.Add('existing_run_no_native_replay')
$blocked = Invoke-TestLoop 'blocked' 32
Assert-True ($script:executions -eq 0 -and $blocked.status.StartsWith('blocked:')) 'Blocked preparation must not dispatch.'
$passed.Add('blocked_preparation_no_dispatch')
foreach ($mode in @('error','drift','drift-final')) {
    $rejected = $false
    $limit = if ($mode -eq 'drift-final') { 1 } else { 8 }
    try { Invoke-TestLoop $mode $limit | Out-Null } catch { $rejected = $true }
    Assert-True ($rejected -and $script:executions -eq 1 -and (Test-Path -LiteralPath (Join-Path $OutputRoot "loop-$mode/failure.json"))) `
        'Failure/identity drift must retain diagnostics without retrying native action.'
    $passed.Add("no_native_retry_$mode")
}
$budget = Invoke-TestLoop 'budget' 1
Assert-True ($budget.status -eq 'step_budget_reached' -and $script:executions -eq 1 -and -not $budget.actual_key_observed) 'A step budget is not completion.'
$passed.Add('bounded_loop_not_completion')
$stale = Invoke-TestLoop 'stale-key' 3
Assert-True ($stale.status -eq 'step_budget_reached' -and -not $stale.actual_key_observed) 'A stale key projection is not a verified observation.'
$passed.Add('stale_key_not_completion')
Write-JsonFile -Path (Join-Path $OutputRoot 'test-summary.json') -Value ([ordered]@{
    status = 'passed'; passed_count = $passed.Count; cases = $passed.ToArray()
    evidence_scope = 'actual_source_cli_and_synthetic_orchestration_only'; native_game_run = $false
    formal_training_authorized = $false
})
Write-Host "Rusty Key runtime checks passed: $($passed.Count); no game launched."
