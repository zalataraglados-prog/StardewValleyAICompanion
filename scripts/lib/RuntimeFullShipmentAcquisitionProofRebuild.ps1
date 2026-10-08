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
        [Parameter(Mandatory)] [object[]] $PlanningCommon,
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
    Invoke-JsonPost -Url (
        "$BackendUrl/api/v1/snapshots?profile=$SnapshotProfile") `
        -Body $initialSnapshotRaw | Out-Null
    $sourceTargetCalendar = Get-Content `
        -LiteralPath $Paths.target_calendar -Raw | ConvertFrom-Json
    $targetTotalDay = [int]$sourceTargetCalendar.target_total_day

    Invoke-Bootstrap @(
        "build-current-acquisition-route-calendar-resolution",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--snapshot", $InitialSnapshotPath,
        "--output", $Paths.calendar
    )
    Invoke-Bootstrap @(
        "build-current-acquisition-route-target-date-calendar",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--calendar-resolution", $Paths.calendar,
        "--snapshot", $InitialSnapshotPath,
        "--target-total-day", ([string]$targetTotalDay),
        "--output", $Paths.target_calendar
    )

    $rebuiltAxes = [ordered]@{}
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
        $rebuiltAxes[$axisName] = $Paths[$axisName]
        $arguments = @(
            [string]$row[1],
            "--requirement-inventory", $RequirementInventoryPath,
            "--acquisition-lowering", $AcquisitionLoweringPath,
            "--master-angler-windows", $MasterAnglerWindowsPath,
            "--calendar-resolution", $Paths.calendar,
            "--target-date-calendar", $Paths.target_calendar,
            "--snapshot", $InitialSnapshotPath,
            "--output", $Paths[$axisName]
        )
        foreach ($prior in @(
            "unlock", "festival", "location", "facility", "resource",
            "currency", "reservation", "processing"
        )) {
            if ($rebuiltAxes.Contains($prior) -and $prior -ne $axisName) {
                $arguments += @(
                    "--target-date-$prior", $rebuiltAxes[$prior])
            }
        }
        if ($axisName -in @(
                "location", "facility", "resource", "currency",
                "reservation", "processing")) {
            $arguments += @(
                "--route-timing-calibration", $RouteTimingPath)
        }
        if ($axisName -in @("reservation", "processing")) {
            $arguments += @("--strategy-ledger", $Paths.strategy_ledger)
        }
        Invoke-Bootstrap $arguments
    }

    $downstreamAxes = @(
        [ordered]@{
            Name = "fishing_probability"
            Command = "build-acquisition-route-target-date-fishing-probability"
            Dependencies = @()
        },
        [ordered]@{
            Name = "stochastic_retry"
            Command = "build-acquisition-route-target-date-stochastic-retry-budget"
            Dependencies = @("fishing_probability")
        },
        [ordered]@{
            Name = "daily_time_energy"
            Command = "build-acquisition-route-target-date-daily-time-energy-budget"
            Dependencies = @("fishing_probability", "stochastic_retry")
        },
        [ordered]@{
            Name = "opportunity_cost"
            Command = "build-acquisition-route-target-date-opportunity-cost"
            Dependencies = @(
                "fishing_probability", "stochastic_retry", "daily_time_energy")
        }
    )
    foreach ($axis in $downstreamAxes) {
        $arguments = @(
            [string]$axis.Command,
            "--requirement-inventory", $RequirementInventoryPath,
            "--acquisition-lowering", $AcquisitionLoweringPath,
            "--master-angler-windows", $MasterAnglerWindowsPath,
            "--calendar-resolution", $Paths.calendar,
            "--target-date-calendar", $Paths.target_calendar,
            "--target-date-unlock", $Paths.unlock,
            "--target-date-festival", $Paths.festival,
            "--target-date-location", $Paths.location,
            "--target-date-facility", $Paths.facility,
            "--target-date-resource", $Paths.resource,
            "--target-date-currency", $Paths.currency,
            "--target-date-reservation", $Paths.reservation,
            "--target-date-processing", $Paths.processing
        )
        foreach ($dependency in @($axis.Dependencies)) {
            $parameter = "--target-date-" +
                ([string]$dependency).Replace("_", "-")
            $arguments += @($parameter, $Paths[[string]$dependency])
        }
        $arguments += @(
            "--fishing-forecast-manifest", $Paths.forecast,
            "--strategy-ledger", $Paths.strategy_ledger,
            "--snapshot", $InitialSnapshotPath,
            "--route-timing-calibration", $RouteTimingPath,
            "--output", $Paths[[string]$axis.Name]
        )
        Invoke-Bootstrap $arguments
    }

    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-teacher-preference"
    ) + $PlanningCommon + @(
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
    Write-JsonFile -Path $Paths.committed_ledger `
        -Value $commitResponse.Value.ledger
    Invoke-Bootstrap (@(
        "build-acquisition-route-portfolio-commit-receipt"
    ) + $PlanningCommon + @(
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
