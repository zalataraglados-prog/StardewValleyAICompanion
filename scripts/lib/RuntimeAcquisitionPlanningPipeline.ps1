function Invoke-RuntimeAcquisitionPlanningPipeline {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $OutputDirectory,
        [Parameter(Mandatory)] [string] $BatchName,
        [Parameter(Mandatory)] [string] $SnapshotPath,
        [Parameter(Mandatory)] [string] $StrategyLedgerPath,
        [Parameter(Mandatory)] [int] $TargetTotalDay,
        [Parameter(Mandatory)] [string] $RequirementInventoryPath,
        [Parameter(Mandatory)] [string] $AcquisitionLoweringPath,
        [Parameter(Mandatory)] [string] $MasterAnglerWindowsPath,
        [Parameter(Mandatory)] [string] $RouteTimingPath,
        [Parameter(Mandatory)] [hashtable] $Paths,
        [switch] $ForceSequential
    )

    $requiredPathKeys = @(
        "calendar", "target_calendar", "unlock", "festival", "location",
        "facility", "resource", "currency", "reservation", "processing",
        "fishing_probability", "stochastic_retry", "daily_time_energy",
        "opportunity_cost", "forecast"
    )
    foreach ($key in $requiredPathKeys) {
        if (-not $Paths.ContainsKey($key) -or
            [string]::IsNullOrWhiteSpace([string]$Paths[$key])) {
            throw "Acquisition planning pipeline is missing path: $key"
        }
    }

    $commands = New-Object System.Collections.Generic.List[object]
    $commands.Add([ordered]@{ arguments = @(
        "build-current-acquisition-route-calendar-resolution",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--snapshot", $SnapshotPath,
        "--output", $Paths.calendar
    ) }) | Out-Null
    $commands.Add([ordered]@{ arguments = @(
        "build-current-acquisition-route-target-date-calendar",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--master-angler-windows", $MasterAnglerWindowsPath,
        "--calendar-resolution", $Paths.calendar,
        "--snapshot", $SnapshotPath,
        "--target-total-day", ([string]$TargetTotalDay),
        "--output", $Paths.target_calendar
    ) }) | Out-Null

    $baseAxes = @(
        @("unlock", "build-acquisition-route-target-date-unlock-state"),
        @("festival", "build-acquisition-route-target-date-festival-state"),
        @("location", "build-acquisition-route-target-date-location-route"),
        @("facility", "build-acquisition-route-target-date-facility-capacity"),
        @("resource", "build-acquisition-route-target-date-resource-inputs"),
        @("currency", "build-acquisition-route-target-date-currency-budget"),
        @("reservation", "build-acquisition-route-target-date-inventory-reservation"),
        @("processing", "build-acquisition-route-target-date-processing-lead-time")
    )
    $rebuiltAxes = [ordered]@{}
    foreach ($row in $baseAxes) {
        $axisName = [string]$row[0]
        $rebuiltAxes[$axisName] = $Paths[$axisName]
        $arguments = @(
            [string]$row[1],
            "--requirement-inventory", $RequirementInventoryPath,
            "--acquisition-lowering", $AcquisitionLoweringPath,
            "--master-angler-windows", $MasterAnglerWindowsPath,
            "--calendar-resolution", $Paths.calendar,
            "--target-date-calendar", $Paths.target_calendar,
            "--snapshot", $SnapshotPath,
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
            "reservation", "processing"
        )) {
            $arguments += @(
                "--route-timing-calibration", $RouteTimingPath)
        }
        if ($axisName -in @("reservation", "processing")) {
            $arguments += @("--strategy-ledger", $StrategyLedgerPath)
        }
        $commands.Add([ordered]@{ arguments = $arguments }) | Out-Null
    }

    $downstreamAxes = @(
        [ordered]@{
            Name = "fishing_probability"
            Parameter = "fishing-probability"
            Command = "build-acquisition-route-target-date-fishing-probability"
            Dependencies = @()
        },
        [ordered]@{
            Name = "stochastic_retry"
            Parameter = "stochastic-retry"
            Command = "build-acquisition-route-target-date-stochastic-retry-budget"
            Dependencies = @("fishing_probability")
        },
        [ordered]@{
            Name = "daily_time_energy"
            Parameter = "daily-time-energy"
            Command = "build-acquisition-route-target-date-daily-time-energy-budget"
            Dependencies = @("fishing_probability", "stochastic_retry")
        },
        [ordered]@{
            Name = "opportunity_cost"
            Parameter = "opportunity-cost"
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
            $dependencyRow = @($downstreamAxes | Where-Object {
                [string]$_.Name -eq [string]$dependency
            })
            if ($dependencyRow.Count -ne 1) {
                throw "Unknown acquisition planning dependency: $dependency"
            }
            $arguments += @(
                "--target-date-$([string]$dependencyRow[0].Parameter)",
                $Paths[[string]$dependency])
        }
        $arguments += @(
            "--fishing-forecast-manifest", $Paths.forecast,
            "--strategy-ledger", $StrategyLedgerPath,
            "--snapshot", $SnapshotPath,
            "--route-timing-calibration", $RouteTimingPath,
            "--output", $Paths[[string]$axis.Name]
        )
        $commands.Add([ordered]@{ arguments = $arguments }) | Out-Null
    }

    if ($ForceSequential) {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        foreach ($command in $commands) {
            Invoke-RuntimeBootstrap `
                -BootstrapDll $BootstrapDll `
                -Arguments (@($command.arguments) + @(
                    "--json-output", "compact"))
        }
        $clock.Stop()
        $batchResult = [pscustomobject][ordered]@{
            schema_version = "stardewai.bootstrap_command_batch_report.v1"
            status = "passed_sequential_oracle"
            command_count = $commands.Count
            elapsed_ms = $clock.Elapsed.TotalMilliseconds
        }
    }
    else {
        $batchResult = Invoke-RuntimeBootstrapBatch `
            -BootstrapDll $BootstrapDll `
            -Commands $commands.ToArray() `
            -OutputDirectory $OutputDirectory `
            -BatchName $BatchName
    }
    $portfolioCommon = @(
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
        "--target-date-processing", $Paths.processing,
        "--target-date-fishing-probability", $Paths.fishing_probability,
        "--target-date-stochastic-retry", $Paths.stochastic_retry,
        "--target-date-daily-time-energy", $Paths.daily_time_energy,
        "--target-date-opportunity-cost", $Paths.opportunity_cost,
        "--fishing-forecast-manifest", $Paths.forecast,
        "--strategy-ledger", $StrategyLedgerPath,
        "--snapshot", $SnapshotPath,
        "--route-timing-calibration", $RouteTimingPath
    )
    return [pscustomobject][ordered]@{
        PortfolioCommon = $portfolioCommon
        BatchResult = $batchResult
    }
}
