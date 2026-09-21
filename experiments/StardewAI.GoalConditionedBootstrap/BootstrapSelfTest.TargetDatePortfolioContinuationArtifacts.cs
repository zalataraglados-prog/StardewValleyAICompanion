namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static AcquisitionRoutePortfolioInputs
        BuildContinuationPortfolioInputs(
            AcquisitionRouteExecutionBindingInputs prior,
            string snapshotPath,
            string ledgerPath,
            string proposalPath,
            string outputRoot)
    {
        var processingPath = Path.Combine(
            outputRoot,
            "target-date-processing.json");
        var processing = BuildProcessingFixtureChain(
            prior.RequirementInventoryPath,
            prior.AcquisitionLoweringPath,
            prior.MasterAnglerWindowsPath,
            prior.CalendarResolutionPath,
            prior.TargetDateCalendarPath,
            snapshotPath,
            prior.RouteTimingCalibrationPath,
            ledgerPath,
            outputRoot);
        Write(processingPath, processing);
        var unlockPath = Path.Combine(outputRoot, "target-date-unlock.json");
        var festivalPath = Path.Combine(
            outputRoot,
            "target-date-festival.json");
        var locationPath = Path.Combine(
            outputRoot,
            "target-date-location.json");
        var facilityPath = Path.Combine(
            outputRoot,
            "target-date-facility.json");
        var resourcePath = Path.Combine(
            outputRoot,
            "target-date-resource.json");
        var currencyPath = Path.Combine(
            outputRoot,
            "target-date-currency.json");
        var reservationPath = Path.Combine(
            outputRoot,
            "target-date-reservation.json");
        var fishingPath = Path.Combine(
            outputRoot,
            "target-date-fishing-probability.json");
        var fishing = AcquisitionRouteTargetDateFishingProbabilityBuilder
            .Build(
                prior.RequirementInventoryPath,
                prior.AcquisitionLoweringPath,
                prior.MasterAnglerWindowsPath,
                prior.CalendarResolutionPath,
                prior.TargetDateCalendarPath,
                unlockPath,
                festivalPath,
                locationPath,
                facilityPath,
                resourcePath,
                currencyPath,
                reservationPath,
                processingPath,
                ledgerPath,
                snapshotPath,
                prior.RouteTimingCalibrationPath,
                prior.FishingForecastManifestPath);
        Write(fishingPath, fishing);
        var retryPath = Path.Combine(
            outputRoot,
            "target-date-stochastic-retry.json");
        var retry = AcquisitionRouteTargetDateStochasticRetryBuilder.Build(
            prior.RequirementInventoryPath,
            prior.AcquisitionLoweringPath,
            prior.MasterAnglerWindowsPath,
            prior.CalendarResolutionPath,
            prior.TargetDateCalendarPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath,
            currencyPath,
            reservationPath,
            processingPath,
            fishingPath,
            prior.FishingForecastManifestPath,
            ledgerPath,
            snapshotPath,
            prior.RouteTimingCalibrationPath);
        Write(retryPath, retry);
        var dailyPath = Path.Combine(
            outputRoot,
            "target-date-daily-time-energy.json");
        var daily = AcquisitionRouteTargetDateDailyTimeEnergyBuilder.Build(
            prior.RequirementInventoryPath,
            prior.AcquisitionLoweringPath,
            prior.MasterAnglerWindowsPath,
            prior.CalendarResolutionPath,
            prior.TargetDateCalendarPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath,
            currencyPath,
            reservationPath,
            processingPath,
            fishingPath,
            retryPath,
            prior.FishingForecastManifestPath,
            ledgerPath,
            snapshotPath,
            prior.RouteTimingCalibrationPath);
        Write(dailyPath, daily);
        var opportunityPath = Path.Combine(
            outputRoot,
            "target-date-opportunity-cost.json");
        var opportunity = AcquisitionRouteTargetDateOpportunityCostBuilder
            .Build(
                prior.RequirementInventoryPath,
                prior.AcquisitionLoweringPath,
                prior.MasterAnglerWindowsPath,
                prior.CalendarResolutionPath,
                prior.TargetDateCalendarPath,
                unlockPath,
                festivalPath,
                locationPath,
                facilityPath,
                resourcePath,
                currencyPath,
                reservationPath,
                processingPath,
                fishingPath,
                retryPath,
                dailyPath,
                prior.FishingForecastManifestPath,
                ledgerPath,
                snapshotPath,
                prior.RouteTimingCalibrationPath);
        Write(opportunityPath, opportunity);
        return new AcquisitionRoutePortfolioInputs
        {
            RequirementInventoryPath = prior.RequirementInventoryPath,
            AcquisitionLoweringPath = prior.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = prior.MasterAnglerWindowsPath,
            CalendarResolutionPath = prior.CalendarResolutionPath,
            TargetDateCalendarPath = prior.TargetDateCalendarPath,
            TargetDateUnlockPath = unlockPath,
            TargetDateFestivalPath = festivalPath,
            TargetDateLocationPath = locationPath,
            TargetDateFacilityPath = facilityPath,
            TargetDateResourcePath = resourcePath,
            TargetDateCurrencyPath = currencyPath,
            TargetDateReservationPath = reservationPath,
            TargetDateProcessingPath = processingPath,
            TargetDateFishingProbabilityPath = fishingPath,
            TargetDateStochasticRetryPath = retryPath,
            TargetDateDailyTimeEnergyPath = dailyPath,
            TargetDateOpportunityCostPath = opportunityPath,
            FishingForecastManifestPath = prior.FishingForecastManifestPath,
            StrategyLedgerPath = ledgerPath,
            SnapshotPath = snapshotPath,
            RouteTimingCalibrationPath = prior.RouteTimingCalibrationPath,
            ProposalPath = proposalPath
        };
    }

    private static AcquisitionRouteExecutionBindingInputs
        ContinuationBindingInputs(
            AcquisitionRoutePortfolioInputs current,
            string admissionPath,
            string requestPath,
            string preferencePath,
            string commitReceiptPath,
            string committedLedgerPath,
            string queuePath,
            string routeOccurrenceId) => new()
        {
            RequirementInventoryPath = current.RequirementInventoryPath,
            AcquisitionLoweringPath = current.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = current.MasterAnglerWindowsPath,
            CalendarResolutionPath = current.CalendarResolutionPath,
            TargetDateCalendarPath = current.TargetDateCalendarPath,
            TargetDateUnlockPath = current.TargetDateUnlockPath,
            TargetDateFestivalPath = current.TargetDateFestivalPath,
            TargetDateLocationPath = current.TargetDateLocationPath,
            TargetDateFacilityPath = current.TargetDateFacilityPath,
            TargetDateResourcePath = current.TargetDateResourcePath,
            TargetDateCurrencyPath = current.TargetDateCurrencyPath,
            TargetDateReservationPath = current.TargetDateReservationPath,
            TargetDateProcessingPath = current.TargetDateProcessingPath,
            TargetDateFishingProbabilityPath =
                current.TargetDateFishingProbabilityPath,
            TargetDateStochasticRetryPath =
                current.TargetDateStochasticRetryPath,
            TargetDateDailyTimeEnergyPath =
                current.TargetDateDailyTimeEnergyPath,
            TargetDateOpportunityCostPath =
                current.TargetDateOpportunityCostPath,
            FishingForecastManifestPath = current.FishingForecastManifestPath,
            StrategyLedgerPath = current.StrategyLedgerPath,
            BeforeSnapshotPath = current.SnapshotPath,
            RouteTimingCalibrationPath = current.RouteTimingCalibrationPath,
            PortfolioProposalPath = current.ProposalPath,
            PortfolioAdmissionPath = admissionPath,
            PortfolioPreferenceRequestPath = requestPath,
            PortfolioTeacherPreferencePath = preferencePath,
            PortfolioCommitReceiptPath = commitReceiptPath,
            CommittedStrategyLedgerPath = committedLedgerPath,
            ActionQueuePath = queuePath,
            RouteOccurrenceId = routeOccurrenceId
        };
}
