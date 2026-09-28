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
        private const string stateHash = "stage-one-collection-frontier-state";

        private StageOneFishFixture[] fish = Array.Empty<StageOneFishFixture>();
        private readonly string root;
        private readonly string inventoryPath;
        private readonly string loweringPath;
        private readonly string catalogPath;
        private readonly string staleProbabilityCatalogPath;
        private readonly string windowsPath;
        private readonly string locationsPath;
        private readonly string cropsPath;
        private readonly string shopsPath;
        private readonly string accessConstraintPath;
        private readonly string cropGrowthSourcePath;
        private readonly string cropPlantingSourcePath;
        private readonly string shopStockSourcePath;
        private readonly string shopOpenSourcePath;
        private readonly string shopPurchaseSourcePath;
        private readonly string gameStateQuerySourcePath;
        private readonly string routeCalendarPath;
        private readonly string currentRouteCalendarPath;
        private readonly string currentTargetDateCalendarPath;
        private readonly string tamperedCurrentRouteCalendarPath;
        private readonly string targetDateCalendarPath;
        private readonly string targetDateUnlockPath;
        private readonly string targetDateFestivalPath;
        private readonly string targetDateLocationPath;
        private readonly string targetDateFacilityPath;
        private readonly string targetDateResourcePath;
        private readonly string targetDateCurrencyPath;
        private readonly string targetDateReservationPath;
        private readonly string targetDateProcessingPath;
        private readonly string fishingForecastSnapshotPath;
        private readonly string fishingForecastManifestPath;
        private readonly string targetDateFishingProbabilityPath;
        private readonly string targetDateStochasticRetryPath;
        private readonly string targetDateDailyTimeEnergyPath;
        private readonly string targetDateOpportunityCostPath;
        private readonly string targetDatePortfolioProposalPath;
        private readonly string targetDatePortfolioPreferenceRequestPath;
        private readonly string targetDatePortfolioPreferencePath;
        private readonly string targetDatePortfolioTeacherProposalPath;
        private readonly string targetDatePortfolioTeacherAdmissionPath;
        private readonly string targetDatePortfolioAdmissionPath;
        private readonly string targetDatePortfolioCommitResultPath;
        private readonly string targetDatePortfolioCommitReceiptPath;
        private readonly string targetDatePortfolioCommittedLedgerPath;
        private readonly string targetDatePortfolioTamperedLedgerPath;
        private readonly string targetDatePortfolioCoupledTamperedLedgerPath;
        private readonly string targetDatePortfolioCoupledTamperedResultPath;
        private readonly string targetDatePortfolioExistingAdmissionPath;
        private readonly string targetDateRouteQueuePath;
        private readonly string targetDateExecutionBindingPath;
        private readonly string targetDateAfterSnapshotPath;
        private readonly string targetDateExecutionReceiptPath;
        private readonly string targetDateInsufficientAfterSnapshotPath;
        private readonly string tamperedTargetDateDailyTimeEnergyPath;
        private readonly string tamperedTargetDateReservationPath;
        private readonly string tamperedTargetDateProcessingPath;
        private readonly string tamperedTargetDateFishingProbabilityPath;
        private readonly string tamperedTargetDateStochasticRetryPath;
        private readonly string strategyLedgerPath;
        private readonly string materialReservedLedgerPath;
        private readonly string currencyReservedLedgerPath;
        private readonly string cancelledReservationLedgerPath;
        private readonly string committedReservationLedgerPath;
        private readonly string mismatchedStrategyLedgerPath;
        private readonly string missingShopQuoteSnapshotPath;
        private readonly string missingCurrencySnapshotPath;
        private readonly string insufficientCurrencySnapshotPath;
        private readonly string currencyVariantUnlockPath;
        private readonly string currencyVariantFestivalPath;
        private readonly string currencyVariantLocationPath;
        private readonly string currencyVariantFacilityPath;
        private readonly string currencyVariantResourcePath;
        private readonly string targetDateRouteCalibrationPath;
        private readonly string targetDateUnlockSnapshotPath;
        private readonly string missingUnlockStateSnapshotPath;
        private readonly string mismatchedUnlockDateSnapshotPath;
        private readonly string missingCalendarStateSnapshotPath;
        private readonly string missingCalendarStateUnlockPath;
        private readonly string mismatchedCalendarTimeSnapshotPath;
        private readonly string tamperedTargetDateUnlockPath;
        private readonly string tamperedTargetDateFestivalPath;
        private readonly string tamperedTargetDateLocationPath;
        private readonly string tamperedTargetDateFacilityPath;
        private readonly string missingResourceSnapshotPath;
        private readonly string missingResourceUnlockPath;
        private readonly string missingResourceFestivalPath;
        private readonly string missingResourceLocationPath;
        private readonly string missingResourceFacilityPath;
        private readonly string insufficientSeedSnapshotPath;
        private readonly string insufficientSeedUnlockPath;
        private readonly string insufficientSeedFestivalPath;
        private readonly string insufficientSeedLocationPath;
        private readonly string insufficientSeedFacilityPath;
        private readonly string missingFacilitySnapshotPath;
        private readonly string missingFacilityUnlockPath;
        private readonly string missingFacilityFestivalPath;
        private readonly string missingFacilityLocationPath;
        private readonly string zeroFacilitySnapshotPath;
        private readonly string zeroFacilityUnlockPath;
        private readonly string zeroFacilityFestivalPath;
        private readonly string zeroFacilityLocationPath;
        private readonly string missingRouteSnapshotPath;
        private readonly string missingRouteUnlockPath;
        private readonly string missingRouteFestivalPath;
        private readonly string deniedRouteSnapshotPath;
        private readonly string deniedRouteUnlockPath;
        private readonly string deniedRouteFestivalPath;
        private readonly string calibrationPath;
        private readonly string snapshotPath;
        private readonly string intentsPath;
        private readonly string rankingPath;
        private readonly string preferencePath;
        private readonly string afterSnapshotPath;
        private readonly string receiptPath;

        public StageOneCollectionFrontierFixture(string outputRoot)
        {
            root = Path.Combine(
                outputRoot,
                "current-stage-one-collection-teacher-frontier-fixture");
            Directory.CreateDirectory(root);
            inventoryPath = Path.Combine(root, "requirements.json");
            loweringPath = Path.Combine(root, "lowering.json");
            catalogPath = Path.Combine(root, "master-angler-catalog.json");
            staleProbabilityCatalogPath = Path.Combine(
                root,
                "master-angler-catalog-missing-probability-inputs.json");
            windowsPath = Path.Combine(root, "master-angler-windows.json");
            locationsPath = Path.Combine(root, "data-locations.json");
            cropsPath = Path.Combine(root, "data-crops.json");
            shopsPath = Path.Combine(root, "data-shops.json");
            accessConstraintPath = Path.Combine(root, "access-constraint-index.json");
            cropGrowthSourcePath = Path.Combine(root, "Crop.cs");
            cropPlantingSourcePath = Path.Combine(root, "HoeDirt.cs");
            shopStockSourcePath = Path.Combine(root, "ShopBuilder.cs");
            shopOpenSourcePath = Path.Combine(root, "Utility.cs");
            shopPurchaseSourcePath = Path.Combine(root, "ShopMenu.cs");
            gameStateQuerySourcePath = Path.Combine(root, "GameStateQuery.cs");
            routeCalendarPath = Path.Combine(root, "route-calendar-resolution.json");
            currentRouteCalendarPath = Path.Combine(
                root,
                "current-route-calendar-resolution.json");
            currentTargetDateCalendarPath = Path.Combine(
                root,
                "current-target-date-calendar.json");
            tamperedCurrentRouteCalendarPath = Path.Combine(
                root,
                "tampered-current-route-calendar-resolution.json");
            targetDateCalendarPath = Path.Combine(root, "target-date-calendar.json");
            targetDateUnlockPath = Path.Combine(root, "target-date-unlock.json");
            targetDateFestivalPath = Path.Combine(root, "target-date-festival.json");
            targetDateLocationPath = Path.Combine(root, "target-date-location.json");
            targetDateFacilityPath = Path.Combine(root, "target-date-facility.json");
            targetDateResourcePath = Path.Combine(root, "target-date-resource.json");
            targetDateCurrencyPath = Path.Combine(root, "target-date-currency.json");
            targetDateReservationPath = Path.Combine(
                root,
                "target-date-inventory-reservation.json");
            targetDateProcessingPath = Path.Combine(
                root,
                "target-date-processing-lead-time.json");
            fishingForecastSnapshotPath = Path.Combine(
                root,
                "fishing-forecast-beach-slot-2.json");
            fishingForecastManifestPath = Path.Combine(
                root,
                "fishing-forecast-manifest.json");
            targetDateFishingProbabilityPath = Path.Combine(
                root,
                "target-date-fishing-probability.json");
            targetDateStochasticRetryPath = Path.Combine(
                root,
                "target-date-stochastic-retry-budget.json");
            targetDateDailyTimeEnergyPath = Path.Combine(
                root,
                "target-date-daily-time-energy-budget.json");
            targetDateOpportunityCostPath = Path.Combine(
                root,
                "target-date-opportunity-cost.json");
            targetDatePortfolioProposalPath = Path.Combine(
                root,
                "target-date-portfolio-proposal.json");
            targetDatePortfolioPreferenceRequestPath = Path.Combine(
                root,
                "target-date-portfolio-preference-request.json");
            targetDatePortfolioPreferencePath = Path.Combine(
                root,
                "target-date-portfolio-preference.json");
            targetDatePortfolioTeacherProposalPath = Path.Combine(
                root,
                "target-date-portfolio-teacher-proposal.json");
            targetDatePortfolioTeacherAdmissionPath = Path.Combine(
                root,
                "target-date-portfolio-teacher-admission.json");
            targetDatePortfolioAdmissionPath = Path.Combine(
                root,
                "target-date-portfolio-admission.json");
            targetDatePortfolioCommitResultPath = Path.Combine(
                root,
                "target-date-portfolio-commit-result.json");
            targetDatePortfolioCommitReceiptPath = Path.Combine(
                root,
                "target-date-portfolio-commit-receipt.json");
            targetDatePortfolioCommittedLedgerPath = Path.Combine(
                root,
                "target-date-portfolio-committed-ledger.json");
            targetDatePortfolioTamperedLedgerPath = Path.Combine(
                root,
                "target-date-portfolio-tampered-ledger.json");
            targetDatePortfolioCoupledTamperedLedgerPath = Path.Combine(
                root,
                "target-date-portfolio-coupled-tampered-ledger.json");
            targetDatePortfolioCoupledTamperedResultPath = Path.Combine(
                root,
                "target-date-portfolio-coupled-tampered-result.json");
            targetDatePortfolioExistingAdmissionPath = Path.Combine(
                root,
                "target-date-portfolio-existing-admission.json");
            targetDateRouteQueuePath = Path.Combine(
                root,
                "target-date-route-queue.json");
            targetDateExecutionBindingPath = Path.Combine(
                root,
                "target-date-route-execution-binding.json");
            targetDateAfterSnapshotPath = Path.Combine(
                root,
                "target-date-route-after-snapshot.json");
            targetDateExecutionReceiptPath = Path.Combine(
                root,
                "target-date-route-execution-receipt.json");
            targetDateInsufficientAfterSnapshotPath = Path.Combine(
                root,
                "target-date-route-insufficient-after-snapshot.json");
            tamperedTargetDateDailyTimeEnergyPath = Path.Combine(
                root,
                "tampered-target-date-daily-time-energy-budget.json");
            tamperedTargetDateReservationPath = Path.Combine(
                root,
                "tampered-target-date-inventory-reservation.json");
            tamperedTargetDateProcessingPath = Path.Combine(
                root,
                "tampered-target-date-processing-lead-time.json");
            tamperedTargetDateFishingProbabilityPath = Path.Combine(
                root,
                "tampered-target-date-fishing-probability.json");
            tamperedTargetDateStochasticRetryPath = Path.Combine(
                root,
                "tampered-target-date-stochastic-retry-budget.json");
            strategyLedgerPath = Path.Combine(root, "strategy-ledger.json");
            materialReservedLedgerPath = Path.Combine(
                root,
                "material-reserved-strategy-ledger.json");
            currencyReservedLedgerPath = Path.Combine(
                root,
                "currency-reserved-strategy-ledger.json");
            cancelledReservationLedgerPath = Path.Combine(
                root,
                "cancelled-reservation-strategy-ledger.json");
            committedReservationLedgerPath = Path.Combine(
                root,
                "committed-reservation-strategy-ledger.json");
            mismatchedStrategyLedgerPath = Path.Combine(
                root,
                "mismatched-strategy-ledger.json");
            missingShopQuoteSnapshotPath = Path.Combine(
                root,
                "missing-shop-quote-snapshot.json");
            missingCurrencySnapshotPath = Path.Combine(
                root,
                "missing-currency-snapshot.json");
            insufficientCurrencySnapshotPath = Path.Combine(
                root,
                "insufficient-currency-snapshot.json");
            currencyVariantUnlockPath = Path.Combine(
                root,
                "currency-variant-unlock.json");
            currencyVariantFestivalPath = Path.Combine(
                root,
                "currency-variant-festival.json");
            currencyVariantLocationPath = Path.Combine(
                root,
                "currency-variant-location.json");
            currencyVariantFacilityPath = Path.Combine(
                root,
                "currency-variant-facility.json");
            currencyVariantResourcePath = Path.Combine(
                root,
                "currency-variant-resource.json");
            targetDateRouteCalibrationPath = Path.Combine(
                root,
                "target-date-route-timing.json");
            targetDateUnlockSnapshotPath = Path.Combine(
                root,
                "target-date-unlock-snapshot.json");
            missingUnlockStateSnapshotPath = Path.Combine(
                root,
                "missing-unlock-state-snapshot.json");
            mismatchedUnlockDateSnapshotPath = Path.Combine(
                root,
                "mismatched-unlock-date-snapshot.json");
            missingCalendarStateSnapshotPath = Path.Combine(
                root,
                "missing-calendar-state-snapshot.json");
            missingCalendarStateUnlockPath = Path.Combine(
                root,
                "missing-calendar-state-unlock.json");
            mismatchedCalendarTimeSnapshotPath = Path.Combine(
                root,
                "mismatched-calendar-time-snapshot.json");
            tamperedTargetDateUnlockPath = Path.Combine(
                root,
                "tampered-target-date-unlock.json");
            tamperedTargetDateFestivalPath = Path.Combine(
                root,
                "tampered-target-date-festival.json");
            tamperedTargetDateLocationPath = Path.Combine(
                root,
                "tampered-target-date-location.json");
            tamperedTargetDateFacilityPath = Path.Combine(
                root,
                "tampered-target-date-facility.json");
            missingResourceSnapshotPath = Path.Combine(
                root,
                "missing-resource-evidence-snapshot.json");
            missingResourceUnlockPath = Path.Combine(
                root,
                "missing-resource-evidence-unlock.json");
            missingResourceFestivalPath = Path.Combine(
                root,
                "missing-resource-evidence-festival.json");
            missingResourceLocationPath = Path.Combine(
                root,
                "missing-resource-evidence-location.json");
            missingResourceFacilityPath = Path.Combine(
                root,
                "missing-resource-evidence-facility.json");
            insufficientSeedSnapshotPath = Path.Combine(
                root,
                "insufficient-seed-snapshot.json");
            insufficientSeedUnlockPath = Path.Combine(
                root,
                "insufficient-seed-unlock.json");
            insufficientSeedFestivalPath = Path.Combine(
                root,
                "insufficient-seed-festival.json");
            insufficientSeedLocationPath = Path.Combine(
                root,
                "insufficient-seed-location.json");
            insufficientSeedFacilityPath = Path.Combine(
                root,
                "insufficient-seed-facility.json");
            missingFacilitySnapshotPath = Path.Combine(
                root,
                "missing-facility-evidence-snapshot.json");
            missingFacilityUnlockPath = Path.Combine(
                root,
                "missing-facility-evidence-unlock.json");
            missingFacilityFestivalPath = Path.Combine(
                root,
                "missing-facility-evidence-festival.json");
            missingFacilityLocationPath = Path.Combine(
                root,
                "missing-facility-evidence-location.json");
            zeroFacilitySnapshotPath = Path.Combine(
                root,
                "zero-facility-capacity-snapshot.json");
            zeroFacilityUnlockPath = Path.Combine(
                root,
                "zero-facility-capacity-unlock.json");
            zeroFacilityFestivalPath = Path.Combine(
                root,
                "zero-facility-capacity-festival.json");
            zeroFacilityLocationPath = Path.Combine(
                root,
                "zero-facility-capacity-location.json");
            missingRouteSnapshotPath = Path.Combine(
                root,
                "missing-route-evidence-snapshot.json");
            missingRouteUnlockPath = Path.Combine(
                root,
                "missing-route-evidence-unlock.json");
            missingRouteFestivalPath = Path.Combine(
                root,
                "missing-route-evidence-festival.json");
            deniedRouteSnapshotPath = Path.Combine(
                root,
                "denied-route-snapshot.json");
            deniedRouteUnlockPath = Path.Combine(
                root,
                "denied-route-unlock.json");
            deniedRouteFestivalPath = Path.Combine(
                root,
                "denied-route-festival.json");
            calibrationPath = Path.Combine(root, "route-timing.json");
            snapshotPath = Path.Combine(root, "snapshot.json");
            intentsPath = Path.Combine(root, "master-angler-intents.json");
            rankingPath = Path.Combine(root, "ranking.json");
            preferencePath = Path.Combine(root, "teacher-preference.json");
            afterSnapshotPath = Path.Combine(root, "after-snapshot.json");
            receiptPath = Path.Combine(root, "execution-receipt.json");
        }

        public void Run()
        {
            var routeCalendar = BuildStaticInputs();
            var axes = VerifyTargetDateAxes(routeCalendar);
            var portfolioRequirement = VerifyPortfolio(
                axes.OpportunityCost,
                axes.OpportunityShop,
                axes.OpportunityFish);
            VerifyPortfolioSemantics(portfolioRequirement);
            VerifyNegativeCases(
                routeCalendar,
                axes.CurrencyShop,
                axes.ReservationShop);
            VerifyCurrentFrontier(
                routeCalendar,
                axes.TargetDateCalendar);
        }

        private sealed record TargetDateAxes(
            AcquisitionRouteTargetDateCalendarReport TargetDateCalendar,
            AcquisitionRouteTargetDateOpportunityCostReport OpportunityCost,
            AcquisitionRouteTargetDateOpportunityCost OpportunityShop,
            AcquisitionRouteTargetDateOpportunityCost OpportunityFish,
            AcquisitionRouteTargetDateCurrency CurrencyShop,
            AcquisitionRouteTargetDateReservation ReservationShop);
    }
}
