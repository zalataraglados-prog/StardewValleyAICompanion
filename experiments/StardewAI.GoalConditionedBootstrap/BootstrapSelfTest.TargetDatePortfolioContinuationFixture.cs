using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDatePortfolioContinuationFixture(
        AcquisitionRouteExecutionBindingInputs template,
        string shopRouteOccurrenceId,
        string fishRouteOccurrenceId)
    {
        var outputRoot = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "portfolio-continuation-fixture");
        Directory.CreateDirectory(outputRoot);
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            template.TargetDateOpportunityCostPath,
            "Portfolio continuation fixture opportunity cost");
        var shop = opportunity.Routes.Single(route =>
            route.RouteOccurrenceId == shopRouteOccurrenceId);
        var fish = opportunity.Routes.Single(route =>
            route.RouteOccurrenceId == fishRouteOccurrenceId);
        var shopRequirement = TargetDateRequirementRoute(shop);
        var fishRequirement = TargetDateRequirementRoute(fish);
        var requestPath = Path.Combine(outputRoot, "preference-request.json");
        Write(requestPath, new AcquisitionRoutePortfolioTeacherPreferenceRequest
        {
            RequestId = "self-test-shop-fish-teacher",
            GoalId = opportunity.GoalId,
            SnapshotStateHash = opportunity.SnapshotStateHash,
            ExpectedLedgerRevision = 0,
            ScopedRequirements = new[]
            {
                new AcquisitionRoutePortfolioRequirementScope(
                    shopRequirement.RequirementSetId,
                    shopRequirement.RequirementId),
                new AcquisitionRoutePortfolioRequirementScope(
                    fishRequirement.RequirementSetId,
                    fishRequirement.RequirementId)
            }
        });

        var proposalPath = Path.Combine(outputRoot, "proposal.json");
        var portfolioInputs = PortfolioInputs(template, proposalPath);
        var preference = AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .Build(portfolioInputs, requestPath);
        var expectedRoutes = new[]
            {
                shopRouteOccurrenceId,
                fishRouteOccurrenceId
            }
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(preference.TeacherPreferenceLabelEligible &&
                preference.CandidateDenominatorComplete &&
                preference.CandidateDenominatorCount == 1 &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null &&
                preference.SelectedProposal.SelectedRouteOccurrenceIds
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(expectedRoutes, StringComparer.Ordinal),
            "Two-route portfolio Teacher preference drifted.");
        var preferencePath = Path.Combine(outputRoot, "preference.json");
        var admissionPath = Path.Combine(outputRoot, "admission.json");
        Write(preferencePath, preference);
        Write(proposalPath, preference.SelectedProposal!);
        Write(admissionPath, preference.SelectedAdmission!);

        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            template.BeforeSnapshotPath,
            "Portfolio continuation fixture snapshot");
        var baseLedger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            template.StrategyLedgerPath,
            "Portfolio continuation fixture base ledger");
        var commit = new ReservationPortfolioLedgerService().Commit(
            baseLedger,
            snapshot,
            preference.SelectedAdmission!.AtomicCommitRequest!,
            "2026-09-21T00:00:00Z");
        Require(commit.Accepted && commit.Ledger is not null,
            "Two-route portfolio atomic commit failed.");
        var commitResultPath = Path.Combine(outputRoot, "commit-result.json");
        var committedLedgerPath = Path.Combine(
            outputRoot,
            "committed-ledger.json");
        Write(commitResultPath, commit);
        Write(committedLedgerPath, commit.Ledger!);
        var commitReceipt = AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
            portfolioInputs,
            admissionPath,
            committedLedgerPath,
            commitResultPath);
        Require(commitReceipt.PortfolioCommitVerified &&
                commitReceipt.AtomicMutationObserved,
            "Two-route portfolio commit receipt drifted.");
        var commitReceiptPath = Path.Combine(outputRoot, "commit-receipt.json");
        Write(commitReceiptPath, commitReceipt);

        var executionInputs = ExecutionInputs(
            template,
            proposalPath,
            admissionPath,
            requestPath,
            preferencePath,
            commitReceiptPath,
            committedLedgerPath,
            commitResultPath,
            Path.Combine(outputRoot, "queue.json"),
            shopRouteOccurrenceId);
        VerifyTargetDateFreshTerminalReceipt(
            executionInputs,
            Path.Combine(outputRoot, "execution-binding.json"),
            Path.Combine(outputRoot, "after-snapshot.json"),
            Path.Combine(outputRoot, "execution-receipt.json"),
            Path.Combine(outputRoot, "insufficient-after-snapshot.json"),
            expectedPortfolioCompletion: false);
    }

    private static AcquisitionRoutePortfolioInputs PortfolioInputs(
        AcquisitionRouteExecutionBindingInputs source,
        string proposalPath) => new()
        {
            RequirementInventoryPath = source.RequirementInventoryPath,
            AcquisitionLoweringPath = source.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = source.MasterAnglerWindowsPath,
            CalendarResolutionPath = source.CalendarResolutionPath,
            TargetDateCalendarPath = source.TargetDateCalendarPath,
            TargetDateUnlockPath = source.TargetDateUnlockPath,
            TargetDateFestivalPath = source.TargetDateFestivalPath,
            TargetDateLocationPath = source.TargetDateLocationPath,
            TargetDateFacilityPath = source.TargetDateFacilityPath,
            TargetDateResourcePath = source.TargetDateResourcePath,
            TargetDateCurrencyPath = source.TargetDateCurrencyPath,
            TargetDateReservationPath = source.TargetDateReservationPath,
            TargetDateProcessingPath = source.TargetDateProcessingPath,
            TargetDateFishingProbabilityPath =
                source.TargetDateFishingProbabilityPath,
            TargetDateStochasticRetryPath =
                source.TargetDateStochasticRetryPath,
            TargetDateDailyTimeEnergyPath =
                source.TargetDateDailyTimeEnergyPath,
            TargetDateOpportunityCostPath = source.TargetDateOpportunityCostPath,
            FishingForecastManifestPath = source.FishingForecastManifestPath,
            StrategyLedgerPath = source.StrategyLedgerPath,
            SnapshotPath = source.BeforeSnapshotPath,
            RouteTimingCalibrationPath = source.RouteTimingCalibrationPath,
            ProposalPath = proposalPath
        };

    private static AcquisitionRouteExecutionBindingInputs ExecutionInputs(
        AcquisitionRouteExecutionBindingInputs source,
        string proposalPath,
        string admissionPath,
        string requestPath,
        string preferencePath,
        string commitReceiptPath,
        string committedLedgerPath,
        string commitResultPath,
        string queuePath,
        string routeOccurrenceId) => new()
        {
            RequirementInventoryPath = source.RequirementInventoryPath,
            AcquisitionLoweringPath = source.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = source.MasterAnglerWindowsPath,
            CalendarResolutionPath = source.CalendarResolutionPath,
            TargetDateCalendarPath = source.TargetDateCalendarPath,
            TargetDateUnlockPath = source.TargetDateUnlockPath,
            TargetDateFestivalPath = source.TargetDateFestivalPath,
            TargetDateLocationPath = source.TargetDateLocationPath,
            TargetDateFacilityPath = source.TargetDateFacilityPath,
            TargetDateResourcePath = source.TargetDateResourcePath,
            TargetDateCurrencyPath = source.TargetDateCurrencyPath,
            TargetDateReservationPath = source.TargetDateReservationPath,
            TargetDateProcessingPath = source.TargetDateProcessingPath,
            TargetDateFishingProbabilityPath =
                source.TargetDateFishingProbabilityPath,
            TargetDateStochasticRetryPath =
                source.TargetDateStochasticRetryPath,
            TargetDateDailyTimeEnergyPath =
                source.TargetDateDailyTimeEnergyPath,
            TargetDateOpportunityCostPath = source.TargetDateOpportunityCostPath,
            FishingForecastManifestPath = source.FishingForecastManifestPath,
            StrategyLedgerPath = source.StrategyLedgerPath,
            BeforeSnapshotPath = source.BeforeSnapshotPath,
            RouteTimingCalibrationPath = source.RouteTimingCalibrationPath,
            PortfolioProposalPath = proposalPath,
            PortfolioAdmissionPath = admissionPath,
            PortfolioPreferenceRequestPath = requestPath,
            PortfolioTeacherPreferencePath = preferencePath,
            PortfolioCommitReceiptPath = commitReceiptPath,
            CommittedStrategyLedgerPath = committedLedgerPath,
            PortfolioCommitResultPath = commitResultPath,
            ActionQueuePath = queuePath,
            RouteOccurrenceId = routeOccurrenceId
        };
}
