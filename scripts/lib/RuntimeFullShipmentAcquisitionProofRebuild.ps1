function Invoke-RuntimeFullShipmentAcquisitionPlanningRebuild {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $InitialSnapshotPath,
        [Parameter(Mandatory)] [string] $RequirementInventoryPath,
        [Parameter(Mandatory)] [string] $AcquisitionLoweringPath,
        [Parameter(Mandatory)] [string] $MasterAnglerWindowsPath,
        [Parameter(Mandatory)] [string] $RouteTimingPath,
        [Parameter(Mandatory)] [hashtable] $Paths,
        [Parameter(Mandatory)] [object[]] $ExecutionCommon,
        [Parameter(Mandatory)] [string] $RouteOccurrenceId,
        [Parameter(Mandatory)] [string] $RequirementId,
        [Parameter(Mandatory)] [string] $QualifiedItemId,
        [Parameter(Mandatory)] [string] $RouteKind,
        [Parameter(Mandatory)] [string] $ExecutionReceiptPath,
        [Parameter(Mandatory)] [string] $AfterSnapshotPath,
        [Parameter(Mandatory)] [string] $RunId
    )

    $initialSnapshotRaw = Get-Content -LiteralPath $InitialSnapshotPath -Raw
    $initialSnapshot = $initialSnapshotRaw | ConvertFrom-Json
    Invoke-JsonPost -Url (
        "$BackendUrl/api/v1/snapshots?profile=$SnapshotProfile") `
        -Body $initialSnapshotRaw | Out-Null
    $sourceRanking = Get-Content -LiteralPath $Paths.ranking -Raw |
        ConvertFrom-Json
    $sourceDispatch = Get-Content -LiteralPath $Paths.dispatch -Raw |
        ConvertFrom-Json
    $sourceCandidateId = [string]$sourceDispatch.source_candidate_id
    $rankedOptionIds = @($sourceRanking.prediction.ranked_options |
        ForEach-Object { [string]$_.option_id } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique)
    $sourceAvailability = @()
    if ($rankedOptionIds.Count -eq 1) {
        $sourceAvailability = @($sourceRanking.availability.options |
            Where-Object {
                [string]$_.option_id -eq [string]$rankedOptionIds[0]
            })
    }
    elseif ($rankedOptionIds.Count -eq 0) {
        $sourceAvailability = @($sourceRanking.availability.options)
    }
    else {
        throw "Source acquisition ranking must identify at most one option."
    }
    if ($sourceAvailability.Count -ne 1) {
        throw "Source acquisition ranking has no unique availability request."
    }
    $sourceOptionId = [string]$sourceAvailability[0].option_id
    [object[]]$rankingCandidates = @()
    [string[]]$candidateOptionIds = @($sourceOptionId)
    $sourceParameters = @($sourceAvailability[0].parameters)
    $availableCurrentCandidates = @(
        @($sourceAvailability[0].economic_candidates) +
        @($sourceAvailability[0].event_candidates) +
        @($sourceAvailability[0].social_candidates) |
            Where-Object { [bool]$_.available })
    if ($rankedOptionIds.Count -eq 0) {
        $sourceCandidates = @(if ([string]::IsNullOrWhiteSpace(
                $sourceCandidateId)) {
            $availableCurrentCandidates
        }
        else {
            $availableCurrentCandidates | Where-Object {
                [string]$_.candidate_id -eq $sourceCandidateId
            }
        })
        if ($sourceCandidates.Count -ne 1) {
            throw "Unbound source ranking has no unique source dispatch " +
                "candidate '$sourceCandidateId'."
        }
        $sourceCandidateId = [string]$sourceCandidates[0].candidate_id
        # Preserve the source request shape. The exact dispatch identity above
        # proves that the old native target remains in the unbound option set;
        # downstream route binding must still select that same identity.
        $sourceParameters = @()
    }
    if ($sourceParameters.Count -gt 0) {
        $candidateOptionIds = @()
        $rankingCandidates = @([ordered]@{
            option_id = $sourceOptionId
            parameters = $sourceParameters
            explicit_confirmation_granted = $false
            invocation_source = 0
            actor_is_host = $true
            ownership_authorized = $true
            adapter_id = "vanilla_native"
        })
    }
    $preferenceRequest = Get-Content `
        -LiteralPath $Paths.preference_request -Raw | ConvertFrom-Json
    $rankingRequest = [ordered]@{
        goal_id = [string]$preferenceRequest.goal_id
        execution_mode = "training_singleplayer"
        state_hash = [string]$initialSnapshot.state_hash
        candidate_option_ids = $candidateOptionIds
        candidates = $rankingCandidates
        include_blocked_options = $false
        training_report = [ordered]@{}
        policy_checkpoint_path = $null
        require_structured_policy = $false
    }
    $rankingResponse = Invoke-JsonPost -Url (
        "$BackendUrl/api/v1/planner/baseline/rank-options") `
        -Body $rankingRequest
    if ([string]$rankingResponse.Value.schema_version -ne
            "availability_policy_prediction.v1") {
        throw "Rebuilt acquisition ranking returned an unexpected schema."
    }
    if ($rankedOptionIds.Count -eq 0) {
        $rebuiltSourceCandidates = @(
            @($rankingResponse.Value.availability.options | Where-Object {
                [string]$_.option_id -eq $sourceOptionId
            } | ForEach-Object {
                @($_.economic_candidates) + @($_.event_candidates) +
                    @($_.social_candidates)
            }) | Where-Object {
                [bool]$_.available -and
                [string]$_.candidate_id -eq $sourceCandidateId
            })
        if ($rebuiltSourceCandidates.Count -ne 1) {
            throw "Rebuilt unbound ranking lost source dispatch candidate " +
                "'$sourceCandidateId'."
        }
    }
    Write-Utf8Text -Path $Paths.ranking -Value $rankingResponse.Raw
    $sourceTargetCalendar = Get-Content `
        -LiteralPath $Paths.target_calendar -Raw | ConvertFrom-Json
    $targetTotalDay = [int]$sourceTargetCalendar.target_total_day

    $rebuiltPlanning = Invoke-RuntimeAcquisitionPlanningPipeline `
        -BootstrapDll $script:bootstrapDll `
        -OutputDirectory ([IO.Path]::GetDirectoryName($Paths.calendar)) `
        -BatchName "target-date-planning-rebuild" `
        -SnapshotPath $InitialSnapshotPath `
        -StrategyLedgerPath $Paths.strategy_ledger `
        -TargetTotalDay $targetTotalDay `
        -RequirementInventoryPath $RequirementInventoryPath `
        -AcquisitionLoweringPath $AcquisitionLoweringPath `
        -MasterAnglerWindowsPath $MasterAnglerWindowsPath `
        -RouteTimingPath $RouteTimingPath `
        -Paths $Paths

    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-teacher-preference"
    ) + @($rebuiltPlanning.PortfolioCommon) + @(
        "--preference-request", $Paths.preference_request,
        "--selected-proposal-output", $Paths.proposal,
        "--selected-admission-output", $Paths.admission,
        "--output", $Paths.teacher_preference
    ))
    $proposal = Get-Content -LiteralPath $Paths.proposal -Raw |
        ConvertFrom-Json
    $selectedRouteIds = @($proposal.selected_route_occurrence_ids)
    if ($selectedRouteIds.Count -ne 1 -or
        [string]$selectedRouteIds[0] -ne $RouteOccurrenceId) {
        throw "Rebuilt teacher preference did not preserve the source route."
    }

    $admission = Get-Content -LiteralPath $Paths.admission -Raw |
        ConvertFrom-Json
    $commitResponse = Invoke-JsonPost -Url (
        "$BackendUrl/api/v1/strategy/commitments/" +
        "reservation-portfolios/commit") `
        -Body $admission.atomic_commit_request
    Write-Utf8Text -Path $Paths.commit_result -Value $commitResponse.Raw
    Write-RawJsonProperty -Path $Paths.committed_ledger `
        -RawJson $commitResponse.Raw -PropertyName "ledger"
    $postCommitRankingResponse = Invoke-JsonPost -Url (
        "$BackendUrl/api/v1/planner/baseline/rank-options") `
        -Body $rankingRequest
    if ([string]$postCommitRankingResponse.Value.schema_version -ne
            "availability_policy_prediction.v1") {
        throw "Post-commit acquisition ranking returned an unexpected schema."
    }
    Write-Utf8Text -Path $Paths.ranking `
        -Value $postCommitRankingResponse.Raw
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-commit-receipt"
    ) + @($rebuiltPlanning.PortfolioCommon) + @(
        "--proposal", $Paths.proposal,
        "--portfolio-admission", $Paths.admission,
        "--committed-ledger", $Paths.committed_ledger,
        "--commit-result", $Paths.commit_result,
        "--output", $Paths.commit_receipt
    ))
    Invoke-Bootstrap (@("compile-acquisition-route-dispatch") +
        $ExecutionCommon + @(
            "--ranking", $Paths.ranking,
            "--queue-output", $Paths.action_queue,
            "--output", $Paths.dispatch
        ))
    Invoke-Bootstrap (@("build-acquisition-route-execution-binding") +
        $ExecutionCommon + @(
            "--action-queue", $Paths.action_queue,
            "--output", $Paths.execution_binding
        ))
    $rebuiltBinding = Get-Content -LiteralPath $Paths.execution_binding `
        -Raw | ConvertFrom-Json
    if ([string]$rebuiltBinding.route_occurrence_id -ne $RouteOccurrenceId -or
        [string]$rebuiltBinding.requirement_id -ne $RequirementId -or
        [string]$rebuiltBinding.qualified_item_id -ne $QualifiedItemId -or
        [string]$rebuiltBinding.route_kind -ne $RouteKind) {
        throw "Rebuilt execution binding drifted from source acquisition identity."
    }
    Invoke-Bootstrap (@(
        "build-acquisition-route-fresh-terminal-receipt"
    ) + $ExecutionCommon + @(
        "--action-queue", $Paths.action_queue,
        "--execution-binding", $Paths.execution_binding,
        "--execution-receipt", $ExecutionReceiptPath,
        "--after-snapshot", $AfterSnapshotPath,
        "--run-id", $RunId,
        "--executor-version", "product_executor.v1",
        "--output", $Paths.fresh_terminal
    ))
}
