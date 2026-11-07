function Select-RuntimeNativeSaveBoundaryCandidate {
    param(
        [Parameter(Mandatory)] $Ranking,
        [string] $OperationName = 'Native save boundary'
    )
    $candidates = @($Ranking.ranked_event_candidates | Where-Object {
        $_.available -is [bool] -and $_.available -and
        $_.option_id -eq 'recovery.stabilize_day' -and
        ($_.candidate_id -eq 'recovery:native_save_boundary' -or
            ($_.candidate_id -eq 'recovery:wait_for_native_control_settle' -and
                $_.kind -eq 'recovery_native_control_settle'))
    })
    if ($candidates.Count -ne 1) {
        throw "$OperationName needs one safe save-boundary or native-control-settle candidate; found $($candidates.Count)."
    }
    return $candidates[0]
}

function Wait-RuntimeNativeSaveBoundaryControl {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [int] $TotalDay,
        [Parameter(Mandatory)] [string] $Name,
        [ValidateRange(1, 8)] [int] $MaximumObservations = 4
    )
    $currentPath = $SourceSnapshotPath
    $observations = [Collections.Generic.List[object]]::new()
    for ($stage = 0; $stage -le $MaximumObservations; $stage++) {
        $current = Import-RuntimeSnapshotFile -Path $currentPath `
            -BackendUrl $Context.BackendUrl -SnapshotProfile $SnapshotProfile `
            -CoreAssemblyPath (Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll')
        if ([int](Read-StateValue $current.Value 'time' 'total_days') -ne $TotalDay) {
            throw "$Name native-control observation crossed its day boundary."
        }
        $ranking = Invoke-RuntimeRanking -BackendUrl $Context.BackendUrl `
            -GoalId 'daily.closed_loop' -SnapshotStateHash $current.Value.state_hash `
            -OutputPath (Join-Path $Context.ArtifactDirectory "$Name-ranking-$stage.json") `
            -OptionId 'recovery.stabilize_day' -Parameters @(
                @{ name = 'control_plane.native_save_boundary'; value = 'true' })
        $candidate = Select-RuntimeNativeSaveBoundaryCandidate `
            -Ranking $ranking -OperationName $Name
        if ($candidate.candidate_id -eq 'recovery:native_save_boundary') {
            if ($candidate.kind -ne 'recovery_sleep_immediately') {
                throw "$Name native save candidate has an invalid kind."
            }
            return [pscustomobject]@{
                SourcePath = $current.Path
                Observations = $observations.ToArray()
            }
        }
        if ($stage -eq $MaximumObservations) {
            throw "$Name exceeded its bounded native-control observations."
        }
        $observation = Invoke-RuntimeDailyPlanStep -Context $Context `
            -Name "$Name-observation-$stage" -OptionId 'recovery.stabilize_day' `
            -CandidateId $candidate.candidate_id -CandidateKind $candidate.kind `
            -CandidateParameters @('control_plane.native_save_boundary=true') `
            -SourceSnapshotPath $current.Path -GoalId 'daily.closed_loop'
        if ($observation.Before.state_hash -cne $current.Value.state_hash -or
            [string]::IsNullOrWhiteSpace([string]$observation.After.state_hash) -or
            $observation.After.state_hash -ceq $current.Value.state_hash) {
            throw "$Name native-control observation lost its source boundary."
        }
        $observations.Add($observation)
        $currentPath = $observation.AfterPath
    }
}
