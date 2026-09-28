using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private AcquisitionRouteTargetDateOpportunityCostReport BuildOpportunityCost(
            string dailyPath) =>
            AcquisitionRouteTargetDateOpportunityCostBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                targetDateReservationPath,
                targetDateProcessingPath,
                targetDateFishingProbabilityPath,
                targetDateStochasticRetryPath,
                dailyPath,
                fishingForecastManifestPath,
                strategyLedgerPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);

        private AcquisitionRouteTargetDateDailyTimeEnergyReport BuildDailyBudget(
            string stochasticPath) =>
            AcquisitionRouteTargetDateDailyTimeEnergyBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                targetDateReservationPath,
                targetDateProcessingPath,
                targetDateFishingProbabilityPath,
                stochasticPath,
                fishingForecastManifestPath,
                strategyLedgerPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);

        private AcquisitionRouteTargetDateStochasticRetryReport BuildStochasticRetry(
            string processingPath,
            string? fishingProbabilityPath = null) =>
            AcquisitionRouteTargetDateStochasticRetryBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                targetDateReservationPath,
                processingPath,
                fishingProbabilityPath ?? targetDateFishingProbabilityPath,
                fishingForecastManifestPath,
                strategyLedgerPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);

        private AcquisitionRouteTargetDateFishingProbabilityReport
            BuildFishingProbability() =>
                AcquisitionRouteTargetDateFishingProbabilityBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateCurrencyPath,
                    targetDateReservationPath,
                    targetDateProcessingPath,
                    strategyLedgerPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath,
                    fishingForecastManifestPath);

        private AcquisitionRouteTargetDateProcessingReport BuildProcessing(
            string reservationPath,
            string ledgerPath,
            string snapshotPath) =>
            AcquisitionRouteTargetDateProcessingBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                reservationPath,
                ledgerPath,
                snapshotPath,
                targetDateRouteCalibrationPath);

        private AcquisitionRouteTargetDateReservationReport BuildReservation(
            string ledgerPath) =>
            AcquisitionRouteTargetDateReservationBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                ledgerPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);

    }

    private static AcquisitionRouteTargetDateFacilityReport
        BuildFacilityFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string unlockOutputPath,
            string festivalOutputPath,
            string locationOutputPath)
    {
        var unlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath);
        Write(unlockOutputPath, unlock);
        var festival = AcquisitionRouteTargetDateFestivalBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            snapshotPath);
        Write(festivalOutputPath, festival);
        var location = AcquisitionRouteTargetDateLocationBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
        Write(locationOutputPath, location);
        return AcquisitionRouteTargetDateFacilityBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

    private static AcquisitionRouteTargetDateResourceReport
        BuildResourceFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string unlockOutputPath,
            string festivalOutputPath,
            string locationOutputPath,
            string facilityOutputPath)
    {
        var facility = BuildFacilityFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath,
            routeTimingCalibrationPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath);
        Write(facilityOutputPath, facility);
        return AcquisitionRouteTargetDateResourceBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            facilityOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

    private static AcquisitionRouteTargetDateCurrencyReport
        BuildCurrencyFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string unlockOutputPath,
            string festivalOutputPath,
            string locationOutputPath,
            string facilityOutputPath,
            string resourceOutputPath)
    {
        var resource = BuildResourceFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath,
            routeTimingCalibrationPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            facilityOutputPath);
        Write(resourceOutputPath, resource);
        return AcquisitionRouteTargetDateCurrencyBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            facilityOutputPath,
            resourceOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

    private static AcquisitionRouteTargetDateProcessingReport
        BuildProcessingFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string strategyLedgerPath,
            string outputRoot)
    {
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
        var currency = BuildCurrencyFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath,
            routeTimingCalibrationPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath);
        Write(currencyPath, currency);
        var reservation = AcquisitionRouteTargetDateReservationBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath,
            currencyPath,
            strategyLedgerPath,
            snapshotPath,
            routeTimingCalibrationPath);
        Write(reservationPath, reservation);
        return AcquisitionRouteTargetDateProcessingBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath,
            currencyPath,
            reservationPath,
            strategyLedgerPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }
}
