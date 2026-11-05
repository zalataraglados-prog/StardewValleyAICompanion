function Invoke-RuntimeRustyKeyRollingLoop {
    [CmdletBinding()]
    param($Context, [string] $BootstrapDll, [string] $RequirementInventoryPath,
        [string] $AcquisitionLoweringPath, [string] $MasterAnglerWindowsPath,
        [string] $RouteTimingPath, [string] $FishingForecastManifestPath,
        [ValidateRange(1, 256)] [int] $MaxSteps = 32,
        [ValidateRange(1024, 1048576)] [int] $MinFreeSpaceMb = 8192,
        [ValidateSet('product', 'runtime_harness')] [string] $ExecutionTarget = 'product',
        [switch] $StopAfterDonationThreshold)

    foreach ($name in @('initial-snapshot.json','progress.json','summary.json','failure.json')) {
        if (Test-Path -LiteralPath (Join-Path $Context.ArtifactDirectory $name)) {
            throw "Existing Rusty Key run cannot be replayed blindly: $name"
        }
    }
    $core = Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll'
    $version = if ($ExecutionTarget -eq 'product') { 'product_executor.v1' } else { 'runtime_test_harness_executor.v1' }
    $steps = [Collections.Generic.List[object]]::new()
    $support = $null
    $ledgerPath = ''
    $status = 'step_budget_reached'
    $snapshotPath = Join-Path $Context.ArtifactDirectory 'initial-snapshot.json'
    try {
        $capture = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 60
        Save-RuntimeSnapshotAndIngest -Path $snapshotPath -Capture $capture `
            -BackendUrl $Context.BackendUrl -SnapshotProfile $Context.SnapshotProfile
        $identity = @([string]$capture.Value.save_id.value, [string]$capture.Value.player_id.value)
        if ($identity.Where({ [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) { throw 'Rusty Key source identity is missing.' }
        for ($index = 0; $index -lt $MaxSteps; $index++) {
            $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Context.ArtifactDirectory)))
            if ($drive.AvailableFreeSpace -lt ([long]$MinFreeSpaceMb * 1MB)) { throw 'Rusty Key runtime disk floor reached.' }
            $snapshot = Read-RuntimeLogicalJsonArtifact -Path $snapshotPath -CoreAssemblyPath $core
            if ([string]$snapshot.save_id.value -ne $identity[0] -or [string]$snapshot.player_id.value -ne $identity[1]) {
                throw 'Rusty Key actor/save identity changed; refusing to continue.'
            }
            if (Test-RuntimeRustyKeyObservedKey -Snapshot $snapshot) {
                $status = 'native_key_observed'
                break
            }
            $directory = Join-Path $Context.ArtifactDirectory ('planning-{0:D4}' -f $index)
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            if ($null -eq $support) {
                $ledgerPath = Save-RuntimeRustyKeyLatestLedger -Context $Context -StateHash $snapshot.state_hash `
                    -Path (Join-Path $directory 'source-ledger.json')
            }
            $planning = New-RuntimeRustyKeyPlanning -Context $Context -BootstrapDll $BootstrapDll `
                -Directory $directory -SnapshotPath $snapshotPath -StrategyLedgerPath $ledgerPath `
                -RequirementInventoryPath $RequirementInventoryPath -AcquisitionLoweringPath $AcquisitionLoweringPath `
                -MasterAnglerWindowsPath $MasterAnglerWindowsPath -RouteTimingPath $RouteTimingPath `
                -FishingForecastManifestPath $FishingForecastManifestPath -PriorSupport $support
            $kind = [string]$planning.Preparation.kind
            if ($StopAfterDonationThreshold -and $kind -ceq 'native_key_handoff' -and
                $planning.Control.threshold_satisfied -is [bool] -and $planning.Control.threshold_satisfied -eq $true) {
                $status = 'native_donation_threshold_observed'
                break
            }
            $name = 'execution-{0:D4}' -f $index
            Write-JsonFile -Path (Join-Path $directory 'dispatch-intent.json') -Value ([ordered]@{
                run_id = $Context.RunId; kind = $kind; preparation = $planning.Preparation
                resume_policy = 'retain_receipts_no_blind_native_retry'; formal_training_authorized = $false
            })
            if (-not [bool]$planning.Preparation.step_ready -and $kind -ne 'native_key_handoff') {
                $status = 'blocked:' + (@($planning.Preparation.blocking_reasons) -join ',')
                break
            }
            $arguments = @{ Context = $Context; BootstrapDll = $BootstrapDll; Planning = $planning
                Name = $name; ExecutionTarget = $ExecutionTarget; ExecutorVersion = $version }
            if ($kind -eq 'supporting_transition') {
                $result = Invoke-RuntimeRustyKeySupport @arguments
                $support = $result.Support
                $ledgerPath = $result.LedgerPath
                $execution = $result.Execution
            }
            elseif ($kind -eq 'terminal_acquisition') {
                $result = Invoke-RuntimeRustyKeyTerminal @arguments
                $support = $null
                $ledgerPath = $result.LedgerPath
                $execution = $result.Execution
            }
            elseif ($kind -eq 'custody_transfer') {
                $result = Invoke-RuntimeRustyKeyCustodyTransfer -Context $Context -BootstrapDll $BootstrapDll `
                    -Planning $planning -Name $name -ExecutionTarget $ExecutionTarget
                $support = $null
                $ledgerPath = $result.LedgerPath
                $execution = $result.Execution
            }
            elseif ($kind -in @('held_donation', 'custody_navigation')) {
                $execution = Invoke-RuntimePrecompiledQueue -Context $Context -Name $name -QueuePath $planning.QueuePath `
                    -BeforePath $snapshotPath -BeforeSnapshotProfile $Context.SnapshotProfile -ExecutionTarget $ExecutionTarget
                $support = $null
                $latestLedgerName = if ($kind -eq 'held_donation') { 'donation-settled-ledger.json' } else { 'custody-navigation-latest-ledger.json' }
                $ledgerPath = Save-RuntimeRustyKeyLatestLedger -Context $Context -StateHash $execution.After.state_hash `
                    -Path (Join-Path $directory $latestLedgerName)
            }
            elseif ($kind -eq 'day_recovery') {
                $recovery = $planning.Preparation.day_recovery
                if ($null -eq $recovery -or [string]::IsNullOrWhiteSpace($recovery.recovery_candidate_id) -or
                    [string]::IsNullOrWhiteSpace($recovery.recovery_candidate_kind)) {
                    throw 'Rusty Key day recovery is missing its rebuilt native candidate.'
                }
                $execution = Invoke-RuntimeDailyPlanStep -Context $Context -Name $name -OptionId 'recovery.stabilize_day' `
                    -CandidateId $recovery.recovery_candidate_id -CandidateKind $recovery.recovery_candidate_kind `
                    -SourceSnapshotPath $snapshotPath -AllowVerifiedContinuation -ExecutionTarget $ExecutionTarget
                $support = $null
                $ledgerPath = Save-RuntimeRustyKeyLatestLedger -Context $Context -StateHash $execution.After.state_hash `
                    -Path (Join-Path $directory 'day-recovery-ledger.json')
            }
            elseif ($kind -eq 'native_key_handoff') {
                $execution = Invoke-RuntimeRustyKeyNativeHandoff -Context $Context -Name $name `
                    -SnapshotPath $snapshotPath -Snapshot $snapshot -Directory $directory -ExecutionTarget $ExecutionTarget
                $support = $null
                $ledgerPath = Save-RuntimeRustyKeyLatestLedger -Context $Context -StateHash $execution.After.state_hash `
                    -Path (Join-Path $directory 'handoff-ledger.json')
            }
            else { throw "Unknown Rusty Key stage: $kind" }
            $steps.Add([ordered]@{
                index = $index; kind = $kind; control_path = $planning.ControlPath
                before_snapshot_path = $snapshotPath; queue_path = $execution.QueuePath
                execution_path = $execution.ExecutionPath; after_snapshot_path = $execution.AfterPath
                ledger_path = $ledgerPath
            })
            $snapshotPath = $execution.AfterPath
            Write-JsonFile -Path (Join-Path $Context.ArtifactDirectory 'progress.json') -Value ([ordered]@{
                schema_version = 'stardewai.rusty_key_rolling_runtime_progress.v1'; run_id = $Context.RunId
                status = 'running'; steps = $steps.ToArray(); last_snapshot_path = $snapshotPath
                formal_training_authorized = $false
            })
        }
        $final = Read-RuntimeLogicalJsonArtifact -Path $snapshotPath -CoreAssemblyPath $core
        if ([string]$final.save_id.value -ne $identity[0] -or [string]$final.player_id.value -ne $identity[1]) {
            throw 'Rusty Key final actor/save identity changed.'
        }
        if (Test-RuntimeRustyKeyObservedKey -Snapshot $final) { $status = 'native_key_observed' }
        $summary = [ordered]@{
            schema_version = 'stardewai.rusty_key_rolling_runtime.v1'; run_id = $Context.RunId; status = $status
            steps = $steps.ToArray(); last_snapshot_path = $snapshotPath; last_ledger_path = $ledgerPath
            actual_key_observed = ($status -eq 'native_key_observed'); criterion_promoted = $false
            donation_threshold_observed = ($status -eq 'native_donation_threshold_observed')
            evidence_scope = 'runtime_control_observation_only_not_criterion_evidence'
            input_identity_verified = $false
            teacher_preference_label_eligible = $false; formal_training_authorized = $false
        }
        Write-JsonFile -Path (Join-Path $Context.ArtifactDirectory 'summary.json') -Value $summary
        return [pscustomobject]$summary
    }
    catch {
        Write-JsonFile -Path (Join-Path $Context.ArtifactDirectory 'failure.json') -Value ([ordered]@{
            run_id = $Context.RunId; status = 'failed'; error = $_.Exception.Message
            steps = $steps.ToArray(); last_snapshot_path = $snapshotPath
            native_retry_performed = $false; formal_training_authorized = $false
        })
        throw
    }
}

function Test-RuntimeRustyKeyObservedKey {
    param($Snapshot)
    $field = $Snapshot.state.world_progress.museum
    return ($field.status -eq 'available' -and $field.value.has_rusty_key -is [bool] -and
        $field.value.has_rusty_key -and $field.value.rusty_key_event_seen -is [bool] -and $field.value.rusty_key_event_seen)
}

function Invoke-RuntimeRustyKeyNativeHandoff {
    param($Context, [string] $Name, [string] $SnapshotPath, $Snapshot, [string] $Directory,
        [string] $ExecutionTarget)
    $story = Read-StateValue $Snapshot 'player' 'story_event'
    $option = if ($story.active -eq $true) { 'story.advance_event' }
        elseif ([int](Read-StateValue $Snapshot 'time' 'time') -lt 700) { 'exploration.visit_location' }
        else { 'recovery.stabilize_day' }
    $parameters = if ($option -eq 'recovery.stabilize_day') {
        @([ordered]@{ name = 'control_plane.native_save_boundary'; value = 'true' })
    } else { @() }
    $ranking = $null
    if ($option -eq 'exploration.visit_location') {
        $recoveryParameters = @([ordered]@{ name = 'control_plane.native_save_boundary'; value = 'true' })
        $recoveryRanking = Invoke-RuntimeRanking -BackendUrl $Context.BackendUrl -GoalId 'daily.closed_loop' `
            -SnapshotStateHash $Snapshot.state_hash -OutputPath (Join-Path $Directory 'native-handoff-control-ranking.json') `
            -OptionId 'recovery.stabilize_day' -Parameters $recoveryParameters
        if (@($recoveryRanking.ranked_event_candidates | Where-Object {
            $_.candidate_id -ceq 'recovery:wait_for_native_control_settle' -and $_.available -eq $true
        }).Count -gt 0) {
            $option = 'recovery.stabilize_day'
            $parameters = $recoveryParameters
            $ranking = $recoveryRanking
        }
    }
    if ($null -eq $ranking) {
        $ranking = Invoke-RuntimeRanking -BackendUrl $Context.BackendUrl -GoalId 'daily.closed_loop' `
            -SnapshotStateHash $Snapshot.state_hash -OutputPath (Join-Path $Directory 'native-handoff-ranking.json') `
            -OptionId $option -Parameters $parameters
    }
    $candidates = @($ranking.ranked_event_candidates | Where-Object { $_.available -eq $true })
    if ($option -eq 'exploration.visit_location') {
        $candidates = @($candidates | Where-Object { $_.kind -eq 'route_connector_tile' -and
            @($_.parameters | Where-Object { $_.name -eq 'expected_target_location' -and $_.value -eq 'Farm' }).Count -eq 1 })
    }
    elseif ($option -eq 'recovery.stabilize_day') {
        $candidates = @(Select-RustyKeySaveBoundaryCandidate -Ranking $ranking)
    }
    else { $candidates = @($candidates | Where-Object { $_.kind -eq 'advance_story_event_automatic' }) }
    if ($candidates.Count -ne 1) { throw "Rusty Key native handoff has no unique safe $option candidate." }
    return Invoke-RuntimeDailyPlanStep -Context $Context -Name $Name -OptionId $option `
        -CandidateId $candidates[0].candidate_id -CandidateKind $candidates[0].kind `
        -CandidateParameters @($parameters | ForEach-Object { "$($_.name)=$($_.value)" }) `
        -SourceSnapshotPath $SnapshotPath -AllowVerifiedContinuation -ExecutionTarget $ExecutionTarget
}
