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

$originalReader = (Get-Item Function:Read-RuntimeLoopArtifacts).ScriptBlock
$originalRanking = (Get-Item Function:Invoke-RuntimeRanking).ScriptBlock
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
        return @{ ranked_event_candidates = @($script:handoffCandidate) }
    }
    foreach ($candidate in @(
        @{ candidate_id = 'recovery:native_save_boundary'; kind = 'recovery_sleep_immediately' },
        @{ candidate_id = 'recovery:wait_for_native_control_settle'; kind = 'recovery_native_control_settle' }
    )) {
        $script:handoffCandidate = $candidate + @{ option_id = 'recovery.stabilize_day'; available = $true }
        $snapshot = @{ state_hash = 'synthetic-handoff'; state = @{
            time = @{ time = @{ value = 900 } }; player = @{ story_event = @{ value = @{ active = $false } } }
        } }
        Invoke-RuntimeRustyKeyNativeHandoff -Context $context -Name $candidate.kind -SnapshotPath $sourceManifest `
            -Snapshot $snapshot -Directory $OutputRoot -ExecutionTarget 'runtime_harness' | Out-Null
        $idIndex = [array]::IndexOf($script:dailyArguments, '--daily-plan-candidate-id')
        $requiredIndex = [array]::IndexOf($script:dailyArguments, '--required-verified-actions')
        Assert-True ($idIndex -ge 0 -and $script:dailyArguments[$idIndex + 1] -eq $candidate.candidate_id -and
            $requiredIndex -ge 0 -and $script:dailyArguments[$requiredIndex + 1] -eq '1' -and
            $script:dailyArguments -contains 'control_plane.native_save_boundary=true') `
            'Native handoff must send the actual bounded settle/save candidate through the existing daily-plan adapter.'
        $passed.Add("native_handoff_shared_adapter_$($candidate.kind)")
    }
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
        elseif ($script:planningCalls -eq 1) { 'supporting_transition' }
        elseif ($script:planningCalls -eq 2) { 'terminal_acquisition' } else { 'held_donation' }
    if ($script:planningCalls -eq 2) {
        Assert-True ($PriorSupport.Token -eq 'retained-support' -and $StrategyLedgerPath -eq 'exact-settled-ledger-path') `
            'A supporting transition lost its exact retained proof/ledger before replanning.'
    }
    return [pscustomobject]@{
        Preparation = @{ kind = $kind; step_ready = ($kind -ne 'blocked'); blocking_reasons = @('synthetic_block') }
        ControlPath = 'synthetic-control'; QueuePath = 'synthetic-queue'; Directory = $Directory
        SnapshotPath = $SnapshotPath
    }
}
function New-TestExecution($Context, [string] $Name, [string] $Kind) {
    $script:executions++
    $script:seen.Add($Kind)
    if ($script:mode -eq 'error') { throw 'synthetic ambiguous native/network failure' }
    $path = Join-Path $Context.ArtifactDirectory "$Name-after.json"
    $after = New-TestSnapshot $script:executions ($Kind -eq 'held')
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
    return New-TestExecution $Context $Name 'held'
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
        -RouteTimingPath 'unused' -FishingForecastManifestPath 'unused' -MaxSteps $Budget -MinFreeSpaceMb 1024
}
$trace = Invoke-TestLoop 'trace' 3
Assert-True ($trace.status -eq 'native_key_observed' -and ($script:seen -join ',') -eq 'support,terminal,held') 'Rolling stages did not advance in order.'
Assert-True (-not $trace.formal_training_authorized -and -not $trace.criterion_promoted) 'Test orchestration promoted training/coverage.'
Assert-True ($trace.last_ledger_path -eq $trace.steps[-1].ledger_path -and
    $trace.last_ledger_path.EndsWith('donation-settled-ledger.json') -and (Test-Path -LiteralPath $trace.last_ledger_path)) `
    'The final donation must retain its post-settlement ledger rather than the pre-donation lease.'
$passed.Add('support_terminal_donation_order_and_retained_proof')
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
