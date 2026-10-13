function Read-RuntimeUtf8Json {
    param([Parameter(Mandatory)] [string] $Path)

    return [IO.File]::ReadAllText(
        [IO.Path]::GetFullPath($Path),
        [Text.Encoding]::UTF8) | ConvertFrom-Json
}

function Wait-RuntimeShopConnectorStableSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $ExpectedLocationId,
        [int] $TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_observed"
    while ((Get-Date) -lt $deadline) {
        $remaining = [Math]::Max(
            1,
            [int][Math]::Ceiling(($deadline - (Get-Date)).TotalSeconds))
        try {
            $capture = Get-FreshSnapshot `
                -TimeoutSeconds ([Math]::Min(10, $remaining))
        }
        catch {
            $lastState = $_.Exception.Message
            continue
        }
        $locationId = [string](Read-StateValue `
            $capture.Value "player" "location_id")
        $movement = Read-StateValue `
            $capture.Value "player" "movement_timing_context"
        $routeReady = $null -ne $movement -and
            [bool]$movement.runtime_calibration_compatible -and
            [bool]$movement.route_timing_ready_now
        $lastState = "location=$locationId;route_timing_ready_now=$routeReady"
        if ($locationId -eq $ExpectedLocationId -and $routeReady) {
            return $capture
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for stable shop connector snapshot. $lastState"
}

function New-RuntimeShopPurchasePlanningState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $SnapshotPath,
        [Parameter(Mandatory)] [string] $StrategyLedgerPath,
        [Parameter(Mandatory)] [int] $TargetTotalDay,
        [Parameter(Mandatory)] [string] $RequirementInventoryPath,
        [Parameter(Mandatory)] [string] $AcquisitionLoweringPath,
        [Parameter(Mandatory)] [string] $MasterAnglerWindowsPath,
        [Parameter(Mandatory)] [string] $RouteTimingPath,
        [Parameter(Mandatory)] [string] $RankingOptionId,
        [Parameter(Mandatory)] [object[]] $RankingParameters
    )

    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $snapshot = Read-RuntimeUtf8Json -Path $SnapshotPath
    $rankingPath = Join-Path $Directory "ranking-acquisition.json"
    Invoke-Ranking -SnapshotStateHash ([string]$snapshot.state_hash) `
        -OutputPath $rankingPath -OptionId $RankingOptionId `
        -Parameters $RankingParameters | Out-Null

    $calendarPath = Join-Path $Directory "calendar-resolution.json"
    Invoke-Bootstrap @(
        "build-current-acquisition-route-calendar-resolution",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--snapshot", $SnapshotPath,
        "--output", $calendarPath
    )
    $targetCalendarPath = Join-Path $Directory "target-date-calendar.json"
    Invoke-Bootstrap @(
        "build-current-acquisition-route-target-date-calendar",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--calendar-resolution", $calendarPath,
        "--snapshot", $SnapshotPath,
        "--target-total-day", ([string]$TargetTotalDay),
        "--output", $targetCalendarPath
    )

    $axis = [ordered]@{}
    foreach ($row in @(
        @("unlock", "build-acquisition-route-target-date-unlock-state"),
        @("festival", "build-acquisition-route-target-date-festival-state"),
        @("location", "build-acquisition-route-target-date-location-route"),
        @("facility", "build-acquisition-route-target-date-facility-capacity"),
        @("resource", "build-acquisition-route-target-date-resource-inputs"),
        @("currency", "build-acquisition-route-target-date-currency-budget"),
        @("reservation", "build-acquisition-route-target-date-inventory-reservation"),
        @("processing", "build-acquisition-route-target-date-processing-lead-time")
    )) {
        $axisName = [string]$row[0]
        $axis[$axisName] = Join-Path $Directory `
            ("target-date-" + $axisName + ".json")
        $arguments = @(
            [string]$row[1],
            "--requirement-inventory", $RequirementInventoryPath,
            "--acquisition-lowering", $AcquisitionLoweringPath,
            "--master-angler-windows", $MasterAnglerWindowsPath,
            "--calendar-resolution", $calendarPath,
            "--target-date-calendar", $targetCalendarPath,
            "--snapshot", $SnapshotPath,
            "--output", $axis[$axisName]
        )
        foreach ($prior in @(
            "unlock", "festival", "location", "facility", "resource",
            "currency", "reservation", "processing"
        )) {
            if ($axis.Contains($prior) -and $prior -ne $axisName) {
                $arguments += @("--target-date-$prior", $axis[$prior])
            }
        }
        if ($axisName -in @(
                "location", "facility", "resource", "currency",
                "reservation", "processing")) {
            $arguments += @("--route-timing-calibration", $RouteTimingPath)
        }
        if ($axisName -in @("reservation", "processing")) {
            $arguments += @("--strategy-ledger", $StrategyLedgerPath)
        }
        Invoke-Bootstrap $arguments
    }

    $forecastPath = Join-Path $Directory "fishing-forecast-manifest.json"
    Write-JsonFile -Path $forecastPath -Value ([ordered]@{
        schema_version = "fishing_forecast_snapshot_manifest.v1"
        snapshots = @()
    })
    $downstream = @(
        [ordered]@{
            Name = "fishing-probability"
            Command = "build-acquisition-route-target-date-fishing-probability"
            Dependencies = @()
        },
        [ordered]@{
            Name = "stochastic-retry"
            Command = "build-acquisition-route-target-date-stochastic-retry-budget"
            Dependencies = @("fishing-probability")
        },
        [ordered]@{
            Name = "daily-time-energy"
            Command = "build-acquisition-route-target-date-daily-time-energy-budget"
            Dependencies = @("fishing-probability", "stochastic-retry")
        },
        [ordered]@{
            Name = "opportunity-cost"
            Command = "build-acquisition-route-target-date-opportunity-cost"
            Dependencies = @(
                "fishing-probability", "stochastic-retry",
                "daily-time-energy")
        }
    )
    foreach ($row in $downstream) {
        $axis[[string]$row.Name] = Join-Path $Directory `
            ("target-date-" + [string]$row.Name + ".json")
        $arguments = @(
            [string]$row.Command,
            "--requirement-inventory", $RequirementInventoryPath,
            "--acquisition-lowering", $AcquisitionLoweringPath,
            "--master-angler-windows", $MasterAnglerWindowsPath,
            "--calendar-resolution", $calendarPath,
            "--target-date-calendar", $targetCalendarPath,
            "--target-date-unlock", $axis["unlock"],
            "--target-date-festival", $axis["festival"],
            "--target-date-location", $axis["location"],
            "--target-date-facility", $axis["facility"],
            "--target-date-resource", $axis["resource"],
            "--target-date-currency", $axis["currency"],
            "--target-date-reservation", $axis["reservation"],
            "--target-date-processing", $axis["processing"]
        )
        foreach ($dependency in @($row.Dependencies)) {
            $arguments += @(
                "--target-date-$dependency",
                $axis[[string]$dependency])
        }
        $arguments += @(
            "--fishing-forecast-manifest", $forecastPath,
            "--strategy-ledger", $StrategyLedgerPath,
            "--snapshot", $SnapshotPath,
            "--route-timing-calibration", $RouteTimingPath,
            "--output", $axis[[string]$row.Name]
        )
        Invoke-Bootstrap $arguments
    }

    $portfolioCommon = @(
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--calendar-resolution", $calendarPath,
        "--target-date-calendar", $targetCalendarPath,
        "--target-date-unlock", $axis["unlock"],
        "--target-date-festival", $axis["festival"],
        "--target-date-location", $axis["location"],
        "--target-date-facility", $axis["facility"],
        "--target-date-resource", $axis["resource"],
        "--target-date-currency", $axis["currency"],
        "--target-date-reservation", $axis["reservation"],
        "--target-date-processing", $axis["processing"],
        "--target-date-fishing-probability", $axis["fishing-probability"],
        "--target-date-stochastic-retry", $axis["stochastic-retry"],
        "--target-date-daily-time-energy", $axis["daily-time-energy"],
        "--target-date-opportunity-cost", $axis["opportunity-cost"],
        "--fishing-forecast-manifest", $forecastPath,
        "--strategy-ledger", $StrategyLedgerPath,
        "--snapshot", $SnapshotPath,
        "--route-timing-calibration", $RouteTimingPath
    )
    return [pscustomobject][ordered]@{
        Directory = $Directory
        SnapshotPath = $SnapshotPath
        StrategyLedgerPath = $StrategyLedgerPath
        RankingPath = $rankingPath
        CalendarPath = $calendarPath
        TargetCalendarPath = $targetCalendarPath
        Axis = $axis
        ForecastPath = $forecastPath
        PortfolioCommon = $portfolioCommon
    }
}

function Invoke-RuntimeShopPurchaseAcquisitionPreparation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [pscustomobject] $InitialPlanning,
        [Parameter(Mandatory)] [string] $PlanningDirectory,
        [Parameter(Mandatory)] [string] $RequirementInventoryPath,
        [Parameter(Mandatory)] [string] $AcquisitionLoweringPath,
        [Parameter(Mandatory)] [string] $MasterAnglerWindowsPath,
        [Parameter(Mandatory)] [string] $RouteTimingPath,
        [Parameter(Mandatory)] [string] $RequirementId,
        [Parameter(Mandatory)] [string] $QualifiedItemId,
        [Parameter(Mandatory)] [string] $ShopId,
        [Parameter(Mandatory)] [string] $RouteOccurrenceId,
        [Parameter(Mandatory)] [string] $RankingOptionId,
        [Parameter(Mandatory)] [object[]] $RankingParameters,
        [Parameter(Mandatory)] [string] $GoalId,
        [Parameter(Mandatory)] [string] $RunId,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [int] $MaxSupportingTransitions = 8
    )

    $current = $InitialPlanning
    $initialTargetCalendar = Read-RuntimeUtf8Json `
        -Path $InitialPlanning.TargetCalendarPath
    $targetTotalDay = [int]$initialTargetCalendar.target_total_day
    $transitions = New-Object System.Collections.Generic.List[object]
    for ($stage = 1; $stage -le $MaxSupportingTransitions; $stage++) {
        $ranking = Read-RuntimeUtf8Json -Path $current.RankingPath
        $candidates = @($ranking.ranked_event_candidates | Where-Object {
            [string]$_.option_id -eq $RankingOptionId -and
            [string]$_.qualified_item_id -eq $QualifiedItemId -and
            [string]$_.shop_id -eq $ShopId -and
            [bool]$_.available
        })
        if ($candidates.Count -ne 1) {
            throw "Shop acquisition stage has no unique current candidate."
        }
        $candidateKind = [string]$candidates[0].kind
        if ($candidateKind -eq "buy_shop_item") {
            $ledger = Read-RuntimeUtf8Json -Path $current.StrategyLedgerPath
            $snapshot = Read-RuntimeUtf8Json -Path $current.SnapshotPath
            $preferenceRequestPath = Join-Path $current.Directory `
                "portfolio-preference-request.json"
            Write-JsonFile -Path $preferenceRequestPath -Value ([ordered]@{
                schema_version =
                    "acquisition_route_portfolio_teacher_preference_request.v1"
                request_id = "$RunId.shop-terminal"
                goal_id = $GoalId
                snapshot_state_hash = [string]$snapshot.state_hash
                expected_ledger_revision = [int]$ledger.revision
                scoped_requirements = @([ordered]@{
                    requirement_set_id = "full_shipment"
                    requirement_id = $RequirementId
                })
            })
            $teacherPath = Join-Path $current.Directory `
                "portfolio-teacher-preference.json"
            $proposalPath = Join-Path $current.Directory `
                "portfolio-selected-proposal.json"
            $admissionPath = Join-Path $current.Directory `
                "portfolio-selected-admission.json"
            Invoke-Bootstrap (@(
                "build-acquisition-route-portfolio-teacher-preference"
            ) + $current.PortfolioCommon + @(
                "--preference-request", $preferenceRequestPath,
                "--selected-proposal-output", $proposalPath,
                "--selected-admission-output", $admissionPath,
                "--output", $teacherPath
            ))
            $proposal = Read-RuntimeUtf8Json -Path $proposalPath
            $routeIds = @($proposal.selected_route_occurrence_ids)
            if ($routeIds.Count -ne 1 -or
                [string]$routeIds[0] -ne $RouteOccurrenceId) {
                throw "Shop terminal Teacher selection drifted from the route."
            }
            $admission = Read-RuntimeUtf8Json -Path $admissionPath
            $commitResponse = Invoke-JsonPost -Url (
                "$BackendUrl/api/v1/strategy/commitments/" +
                "reservation-portfolios/commit") `
                -Body $admission.atomic_commit_request
            $commitResultPath = Join-Path $current.Directory `
                "portfolio-commit-result.json"
            $committedLedgerPath = Join-Path $current.Directory `
                "committed-strategy-ledger.json"
            Write-Utf8Text -Path $commitResultPath -Value $commitResponse.Raw
            Write-JsonFile -Path $committedLedgerPath `
                -Value $commitResponse.Value.ledger
            $commitReceiptPath = Join-Path $current.Directory `
                "portfolio-commit-receipt.json"
            Invoke-Bootstrap (@(
                "build-acquisition-route-portfolio-commit-receipt"
            ) + $current.PortfolioCommon + @(
                "--proposal", $proposalPath,
                "--portfolio-admission", $admissionPath,
                "--committed-ledger", $committedLedgerPath,
                "--commit-result", $commitResultPath,
                "--output", $commitReceiptPath
            ))
            $queuePath = Join-Path $current.Directory "action-queue.json"
            $dispatchPath = Join-Path $current.Directory `
                "dispatch-compilation.json"
            $executionCommon = $current.PortfolioCommon + @(
                "--portfolio-proposal", $proposalPath,
                "--portfolio-admission", $admissionPath,
                "--portfolio-preference-request", $preferenceRequestPath,
                "--portfolio-teacher-preference", $teacherPath,
                "--portfolio-commit-receipt", $commitReceiptPath,
                "--committed-strategy-ledger", $committedLedgerPath,
                "--portfolio-commit-result", $commitResultPath,
                "--route-occurrence-id", $RouteOccurrenceId
            )
            Invoke-Bootstrap (@("compile-acquisition-route-dispatch") +
                $executionCommon + @(
                    "--ranking", $current.RankingPath,
                    "--queue-output", $queuePath,
                    "--output", $dispatchPath
                ))
            $dispatch = Read-RuntimeUtf8Json -Path $dispatchPath
            if ([string]$dispatch.selected_route_option_role -ne
                    "terminal_transition" -or
                -not [bool]$dispatch.terminal_receipt_eligible -or
                [bool]$dispatch.fresh_replan_required_after_success) {
                throw "Shop purchase did not compile as the terminal stage."
            }
            $bindingPath = Join-Path $current.Directory `
                "execution-binding.json"
            Invoke-Bootstrap (@(
                "build-acquisition-route-execution-binding"
            ) + $executionCommon + @(
                "--action-queue", $queuePath,
                "--output", $bindingPath
            ))
            $manifestPath = Join-Path $PlanningDirectory `
                "shop-navigation-proof-manifest.json"
            Write-JsonFile -Path $manifestPath -Value ([ordered]@{
                schema_version =
                    "runtime_shop_navigation_proof_manifest.v1"
                route_occurrence_id = $RouteOccurrenceId
                supporting_transition_count = $transitions.Count
                supporting_transitions = $transitions.ToArray()
                terminal_before_snapshot_path = $current.SnapshotPath
                terminal_ranking_path = $current.RankingPath
                terminal_dispatch_path = $dispatchPath
                formal_training_authorized = $false
            })
            return [pscustomobject][ordered]@{
                BeforeSnapshotPath = $current.SnapshotPath
                QueuePath = $queuePath
                DispatchPath = $dispatchPath
                BindingPath = $bindingPath
                ExecutionCommon = $executionCommon
                PortfolioCommon = $current.PortfolioCommon
                Planning = $current
                ProposalPath = $proposalPath
                AdmissionPath = $admissionPath
                PreferenceRequestPath = $preferenceRequestPath
                TeacherPreferencePath = $teacherPath
                CommitReceiptPath = $commitReceiptPath
                CommittedLedgerPath = $committedLedgerPath
                CommitResultPath = $commitResultPath
                NavigationManifestPath = $manifestPath
            }
        }
        if ($candidateKind -notin @(
                "route_connector_tile", "interact_endpoint")) {
            throw "Unsupported shop acquisition stage: $candidateKind"
        }

        $stageDirectory = Join-Path $PlanningDirectory `
            ("shop-support-{0:D2}" -f $stage)
        New-Item -ItemType Directory -Force -Path $stageDirectory | Out-Null
        $targetCalendar = Read-RuntimeUtf8Json `
            -Path $current.TargetCalendarPath
        $supportDeadline = [int]$targetCalendar.target_total_day + 1
        $supportCommon = @(
            "--requirement-inventory", $RequirementInventoryPath,
            "--acquisition-lowering", $AcquisitionLoweringPath,
            "--master-angler-windows", $MasterAnglerWindowsPath,
            "--calendar-resolution", $current.CalendarPath,
            "--target-date-calendar", $current.TargetCalendarPath,
            "--target-date-unlock", $current.Axis["unlock"],
            "--target-date-festival", $current.Axis["festival"],
            "--target-date-location", $current.Axis["location"],
            "--target-date-facility", $current.Axis["facility"],
            "--target-date-resource", $current.Axis["resource"],
            "--target-date-currency", $current.Axis["currency"],
            "--target-date-reservation", $current.Axis["reservation"],
            "--target-date-processing", $current.Axis["processing"],
            "--strategy-ledger", $current.StrategyLedgerPath,
            "--snapshot", $current.SnapshotPath,
            "--route-timing-calibration", $RouteTimingPath,
            "--ranking", $current.RankingPath,
            "--route-occurrence-id", $RouteOccurrenceId,
            "--support-deadline-total-day", ([string]$supportDeadline)
        )
        $supportRequestPath = Join-Path $stageDirectory `
            "support-request.json"
        Invoke-Bootstrap (@(
            "build-acquisition-route-supporting-transition-request"
        ) + $supportCommon + @("--output", $supportRequestPath))
        $supportRequest = Read-RuntimeUtf8Json -Path $supportRequestPath
        if ([string]$supportRequest.support_transition_kind -ne
                "shop_purchase_navigation" -or
            [string]$supportRequest.purchase_stage -notin @(
                "route_connector", "shop_interaction")) {
            throw "Shop support request did not bind the current stage."
        }
        $commitResponse = Invoke-JsonPost -Url (
            "$BackendUrl/api/v1/strategy/commitments/" +
            "reservation-portfolios/commit") `
            -Body $supportRequest.atomic_commit_request
        $supportCommitResultPath = Join-Path $stageDirectory `
            "support-commit-result.json"
        $supportCommittedLedgerPath = Join-Path $stageDirectory `
            "support-committed-ledger.json"
        Write-Utf8Text -Path $supportCommitResultPath `
            -Value $commitResponse.Raw
        Write-JsonFile -Path $supportCommittedLedgerPath `
            -Value $commitResponse.Value.ledger
        $supportCommitReceiptPath = Join-Path $stageDirectory `
            "support-commit-receipt.json"
        Invoke-Bootstrap (@(
            "build-acquisition-route-supporting-transition-commit-receipt"
        ) + $supportCommon + @(
            "--support-request", $supportRequestPath,
            "--committed-ledger", $supportCommittedLedgerPath,
            "--commit-result", $supportCommitResultPath,
            "--output", $supportCommitReceiptPath
        ))
        $supportCompilationPath = Join-Path $stageDirectory `
            "support-compilation.json"
        $supportQueuePath = Join-Path $stageDirectory `
            "support-action-queue.json"
        Invoke-Bootstrap (@(
            "compile-acquisition-route-supporting-transition"
        ) + $supportCommon + @(
            "--support-request", $supportRequestPath,
            "--support-commit-receipt", $supportCommitReceiptPath,
            "--committed-ledger", $supportCommittedLedgerPath,
            "--commit-result", $supportCommitResultPath,
            "--queue-output", $supportQueuePath,
            "--output", $supportCompilationPath
        ))
        $supportCompilation = Read-RuntimeUtf8Json `
            -Path $supportCompilationPath
        if ([string]$supportCompilation.selected_route_option_role -ne
                "supporting_transition" -or
            [bool]$supportCompilation.terminal_receipt_eligible -or
            -not [bool]$supportCompilation.fresh_replan_required_after_success) {
            throw "Shop navigation compiled with invalid receipt semantics."
        }
        $execution = Invoke-PrecompiledQueue `
            -Name ("shop-support-{0:D2}" -f $stage) `
            -QueuePath $supportQueuePath -BeforePath $current.SnapshotPath
        $transitionReceiptPath = Join-Path $stageDirectory `
            "supporting-transition-receipt.json"
        Invoke-Bootstrap @(
            "build-acquisition-route-supporting-transition-receipt",
            "--compilation", $supportCompilationPath,
            "--before-snapshot", $current.SnapshotPath,
            "--execution-receipt", $execution.ExecutionPath,
            "--after-snapshot", $execution.AfterPath,
            "--run-id", $RunId,
            "--executor-version", "product_executor.v1",
            "--output", $transitionReceiptPath
        )
        $afterRaw = [IO.File]::ReadAllText(
            [IO.Path]::GetFullPath($execution.AfterPath),
            [Text.Encoding]::UTF8)
        Invoke-JsonPost -Url (
            "$BackendUrl/api/v1/snapshots?profile=$SnapshotProfile") `
            -Body $afterRaw | Out-Null
        $settlementRequestPath = Join-Path $stageDirectory `
            "support-settlement-request.json"
        $proofCommon = @(
            "--support-request", $supportRequestPath,
            "--support-commit-receipt", $supportCommitReceiptPath,
            "--committed-ledger", $supportCommittedLedgerPath,
            "--commit-result", $supportCommitResultPath,
            "--compilation", $supportCompilationPath,
            "--execution-receipt", $execution.ExecutionPath,
            "--after-snapshot", $execution.AfterPath,
            "--supporting-transition-receipt", $transitionReceiptPath,
            "--run-id", $RunId,
            "--executor-version", "product_executor.v1"
        )
        Invoke-Bootstrap (@(
            "build-acquisition-route-supporting-transition-settlement-request"
        ) + $supportCommon + $proofCommon + @(
            "--output", $settlementRequestPath
        ))
        $settlementRequest = Read-RuntimeUtf8Json `
            -Path $settlementRequestPath
        $settlementResponse = Invoke-JsonPost -Url (
            "$BackendUrl/api/v1/strategy/commitments/" +
            "reservation-portfolios/settle-supporting-transition") `
            -Body $settlementRequest
        $settlementResultPath = Join-Path $stageDirectory `
            "support-settlement-result.json"
        $settledLedgerPath = Join-Path $stageDirectory `
            "support-settled-ledger.json"
        Write-Utf8Text -Path $settlementResultPath `
            -Value $settlementResponse.Raw
        Write-JsonFile -Path $settledLedgerPath `
            -Value $settlementResponse.Value.ledger
        $settlementReceiptPath = Join-Path $stageDirectory `
            "support-settlement-receipt.json"
        Invoke-Bootstrap (@(
            "build-acquisition-route-supporting-transition-settlement-receipt"
        ) + $supportCommon + $proofCommon + @(
            "--settlement-request", $settlementRequestPath,
            "--settlement-result", $settlementResultPath,
            "--settled-ledger", $settledLedgerPath,
            "--output", $settlementReceiptPath
        ))
        $transition = Read-RuntimeUtf8Json -Path $transitionReceiptPath
        $settlement = Read-RuntimeUtf8Json -Path $settlementReceiptPath
        if (-not [bool]$transition.supporting_transition_verified -or
            -not [bool]$settlement.reservation_lifecycle_verified) {
            throw "Shop navigation support proof was not verified."
        }
        $transitions.Add([ordered]@{
            stage_index = $stage
            candidate_kind = $candidateKind
            purchase_stage = [string]$supportRequest.purchase_stage
            before_snapshot_path = $current.SnapshotPath
            action_queue_path = $supportQueuePath
            execution_receipt_path = $execution.ExecutionPath
            after_snapshot_path = $execution.AfterPath
            supporting_transition_receipt_path = $transitionReceiptPath
            settlement_receipt_path = $settlementReceiptPath
        }) | Out-Null
        $replanSnapshotPath = $execution.AfterPath
        if ($candidateKind -eq "route_connector_tile") {
            $afterSnapshot = Read-RuntimeUtf8Json -Path $execution.AfterPath
            $expectedLocationId = [string](Read-StateValue `
                $afterSnapshot "player" "location_id")
            if ([string]::IsNullOrWhiteSpace($expectedLocationId)) {
                throw "Shop connector after-snapshot has no location identity."
            }
            $stableCapture = Wait-RuntimeShopConnectorStableSnapshot `
                -ExpectedLocationId $expectedLocationId
            $replanSnapshotPath = Join-Path $stageDirectory `
                "stable-replan-snapshot.json"
            Save-SnapshotAndIngest -Path $replanSnapshotPath `
                -Capture $stableCapture
            $transitions[$transitions.Count - 1][
                "replan_snapshot_path"] = $replanSnapshotPath
        }
        $nextDirectory = Join-Path $stageDirectory "next-planning"
        $current = New-RuntimeShopPurchasePlanningState `
            -Directory $nextDirectory `
            -SnapshotPath $replanSnapshotPath `
            -StrategyLedgerPath $settledLedgerPath `
            -TargetTotalDay $targetTotalDay `
            -RequirementInventoryPath $RequirementInventoryPath `
            -AcquisitionLoweringPath $AcquisitionLoweringPath `
            -MasterAnglerWindowsPath $MasterAnglerWindowsPath `
            -RouteTimingPath $RouteTimingPath `
            -RankingOptionId $RankingOptionId `
            -RankingParameters $RankingParameters
    }
    throw "Shop acquisition exceeded the bounded supporting-transition count."
}
