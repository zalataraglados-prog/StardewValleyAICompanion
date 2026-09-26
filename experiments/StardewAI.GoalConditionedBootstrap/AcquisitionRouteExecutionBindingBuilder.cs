using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteExecutionBindingBuilder
{
    public static AcquisitionRouteExecutionBinding Build(
        AcquisitionRouteExecutionBindingInputs inputs) =>
        BuildCore(inputs, null);

    public static AcquisitionRouteExecutionBinding BuildInitialContinuation(
        AcquisitionRoutePortfolioInitialCheckpointProof proof,
        string checkpointPath,
        string continuationRequestPath,
        AcquisitionRouteExecutionBindingInputs inputs) =>
        BuildCore(
            inputs,
            AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                proof,
                checkpointPath),
            continuationRequestPath);

    public static AcquisitionRouteExecutionBinding BuildContinuation(
        string rolloutProofManifestPath,
        string continuationRequestPath,
        AcquisitionRouteExecutionBindingInputs inputs) => BuildCore(
            inputs,
            AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                rolloutProofManifestPath),
            continuationRequestPath);

    internal static AcquisitionRouteExecutionBinding BuildContinuation(
        AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
        string continuationRequestPath,
        AcquisitionRouteExecutionBindingInputs inputs) => BuildCore(
            inputs,
            verifiedPrior,
            continuationRequestPath);

    private static AcquisitionRouteExecutionBinding BuildCore(
        AcquisitionRouteExecutionBindingInputs inputs,
        AcquisitionRoutePortfolioVerifiedCheckpoint? continuation,
        string continuationRequestPath = "")
    {
        var opportunityPath = Path.GetFullPath(
            inputs.TargetDateOpportunityCostPath);
        var loweringPath = Path.GetFullPath(inputs.AcquisitionLoweringPath);
        var snapshotPath = Path.GetFullPath(inputs.BeforeSnapshotPath);
        var portfolioPreferencePath = Path.GetFullPath(
            inputs.PortfolioTeacherPreferencePath);
        var portfolioReceiptPath = Path.GetFullPath(
            inputs.PortfolioCommitReceiptPath);
        var committedLedgerPath = Path.GetFullPath(
            inputs.CommittedStrategyLedgerPath);
        var queuePath = Path.GetFullPath(inputs.ActionQueuePath);
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            opportunityPath,
            "Acquisition route target-date opportunity cost");
        var recomputed = RecomputeOpportunityCost(inputs);
        Require(EqualJson(opportunity, recomputed),
            "Target-date opportunity-cost report drifted from deterministic source compilation.");
        var portfolioPreference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            portfolioPreferencePath,
            "Acquisition route portfolio Teacher preference");
        var recomputedPreference = continuation is null
            ? AcquisitionRoutePortfolioTeacherPreferenceBuilder.Build(
                PortfolioInputs(inputs),
                inputs.PortfolioPreferenceRequestPath)
            : AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .BuildContinuation(
                    continuation,
                    PortfolioInputs(inputs),
                    continuationRequestPath);
        Require(EqualJson(portfolioPreference, recomputedPreference),
            "Route portfolio Teacher preference drifted from deterministic source compilation.");
        var portfolioProposal = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioProposal>(
            inputs.PortfolioProposalPath,
            "Teacher-selected acquisition route portfolio proposal");
        var portfolioAdmission = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioAdmission>(
            inputs.PortfolioAdmissionPath,
            "Teacher-selected acquisition route portfolio admission");
        var portfolioReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioCommitReceipt>(
            portfolioReceiptPath,
            "Acquisition route portfolio commit receipt");
        var commitResultPath = string.IsNullOrWhiteSpace(
                inputs.PortfolioCommitResultPath)
            ? null
            : inputs.PortfolioCommitResultPath;
        var recomputedPortfolioReceipt = continuation is null
            ? AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
                PortfolioInputs(inputs),
                inputs.PortfolioAdmissionPath,
                committedLedgerPath,
                commitResultPath)
            : AcquisitionRoutePortfolioCommitReceiptBuilder
                .BuildContinuation(
                    continuation,
                    PortfolioInputs(inputs),
                    continuationRequestPath,
                    inputs.PortfolioTeacherPreferencePath,
                    inputs.PortfolioAdmissionPath,
                    committedLedgerPath,
                    commitResultPath);
        Require(EqualJson(portfolioReceipt, recomputedPortfolioReceipt),
            "Route portfolio commit receipt drifted from deterministic source compilation.");
        Require(AcquisitionRouteCommunityCenterProvenanceSupport.Equal(
                    portfolioPreference.CommunityCenterProvenance,
                    portfolioAdmission.CommunityCenterProvenance) &&
                AcquisitionRouteCommunityCenterProvenanceSupport.Equal(
                    portfolioPreference.CommunityCenterProvenance,
                    portfolioReceipt.CommunityCenterProvenance),
            "Route portfolio Community Center provenance drifted before execution binding.");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringPath,
            "Acquisition route option lowering");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Before snapshot");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queuePath,
            "Selected route action queue");
        var selected = opportunity.Routes.SingleOrDefault(route =>
            string.Equals(
                route.RouteOccurrenceId,
                inputs.RouteOccurrenceId,
                StringComparison.Ordinal));
        if (selected is null)
        {
            throw new InvalidDataException(
                "Selected route occurrence is not present exactly once in the opportunity-cost report.");
        }
        var requirement = RequirementRoute(selected);
        var lowered = LoweredRoute(lowering, requirement);
        ValidateLoweredIdentity(lowering, requirement, lowered);
        var selectedCandidateId = SelectedCandidateId(
            selected.RouteOccurrenceId);

        var reasons = ValidateSelection(
                opportunity,
                selected,
                requirement,
                before,
                snapshotPath)
            .Concat(ValidatePortfolioTeacherPreference(
                portfolioPreference,
                portfolioProposal,
                portfolioAdmission,
                portfolioReceipt,
                opportunity,
                selected.RouteOccurrenceId,
                before.StateHash))
            .Concat(ValidatePortfolioCommit(
                portfolioReceipt,
                opportunity,
                selected.RouteOccurrenceId,
                before.StateHash))
            .Concat(ValidateQueue(
                queue,
                opportunity.GoalId,
                before.StateHash,
                selectedCandidateId,
                requirement,
                lowered,
                portfolioReceipt.PortfolioId,
                portfolioReceipt.CommittedLedgerRevision))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var optionsBound = !reasons.Any(reason => reason.StartsWith(
            "route_queue_",
            StringComparison.Ordinal));
        var selectedFromFrontier = !reasons.Any(reason => reason.StartsWith(
            "route_selection_",
            StringComparison.Ordinal));
        var portfolioCommitVerified = !reasons.Any(reason =>
            reason.StartsWith(
                "route_portfolio_",
                StringComparison.Ordinal) &&
            !reason.StartsWith(
                "route_portfolio_teacher_",
                StringComparison.Ordinal));
        var portfolioPreferenceVerified = !reasons.Any(reason =>
            reason.StartsWith(
                "route_portfolio_teacher_",
                StringComparison.Ordinal));
        var terminalKind = TerminalReceiptKind(requirement.MatchKind);
        if (string.IsNullOrWhiteSpace(terminalKind))
        {
            reasons = reasons
                .Append("route_selection_terminal_receipt_kind_unsupported")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            selectedFromFrontier = false;
        }

        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        return new AcquisitionRouteExecutionBinding
        {
            Status = reasons.Length == 0
                ? "ready_for_exact_route_dispatch"
                : "blocked_route_execution_binding",
            GoalId = opportunity.GoalId,
            GameVersion = opportunity.GameVersion,
            TargetTotalDay = opportunity.TargetTotalDay,
            CommunityCenterProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.Clone(
                    portfolioReceipt.CommunityCenterProvenance),
            RouteOccurrenceId = selected.RouteOccurrenceId,
            RequirementSetId = requirement.RequirementSetId,
            RequirementId = requirement.RequirementId,
            AlternativeIndex = requirement.AlternativeIndex,
            RouteIndex = requirement.RouteIndex,
            QualifiedItemId = requirement.QualifiedItemId,
            MatchKind = requirement.MatchKind,
            RequiredAmount = requirement.RequiredAmount,
            MinimumQuality = requirement.MinimumQuality,
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            OpportunityCostSha256 = CurrentTeacherFrontierSupport.HashFile(
                opportunityPath),
            PortfolioCommitReceiptSha256 =
                CurrentTeacherFrontierSupport.HashFile(portfolioReceiptPath),
            PortfolioTeacherPreferenceSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    portfolioPreferencePath),
            ReservationPortfolioId = portfolioReceipt.PortfolioId,
            CommittedStrategyLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(committedLedgerPath),
            CommittedStrategyLedgerRevision =
                portfolioReceipt.CommittedLedgerRevision,
            PriorRolloutCheckpointSha256 =
                portfolioReceipt.PriorRolloutCheckpointSha256,
            CompletedAlternatives = (portfolioReceipt.CompletedAlternatives ??
                    Array.Empty<
                        AcquisitionRoutePortfolioCompletedAlternatives>())
                .Where(value => value is not null)
                .Select(AcquisitionRoutePortfolioBuilder
                    .CloneCompletedAlternatives)
                .ToArray(),
            AcquisitionLoweringSha256 = CurrentTeacherFrontierSupport.HashFile(
                loweringPath),
            BeforeSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                snapshotPath),
            BeforeStateHash = before.StateHash,
            ActionQueueSha256 = CurrentTeacherFrontierSupport.HashFile(queuePath),
            QueueId = queue.QueueId,
            SelectedCandidateId = selectedCandidateId,
            QueueItemIds = items.Select(item => item.QueueItemId).ToArray(),
            PrimitiveOptionIds = items.Select(item => item.OptionId).ToArray(),
            EndpointOptionIds = lowered.EndpointOptionIds,
            SupportingOptionIds = lowered.SupportingOptionIds,
            TerminalReceiptKind = terminalKind,
            SelectedFromCompleteParetoFrontier = selectedFromFrontier,
            QueueOptionsBoundToRoute = optionsBound,
            PortfolioReservationCommitVerified = portfolioCommitVerified,
            PortfolioTeacherPreferenceVerified =
                portfolioPreferenceVerified,
            DispatchBindingReady = reasons.Length == 0,
            FormalTrainingAuthorized = false,
            BlockingReasons = reasons
        };
    }

    internal static AcquisitionRouteTargetDateOpportunityCostReport
        RecomputeOpportunityCost(AcquisitionRouteExecutionBindingInputs inputs) =>
        AcquisitionRouteTargetDateOpportunityCostBuilder.Build(
            inputs.RequirementInventoryPath,
            inputs.AcquisitionLoweringPath,
            inputs.MasterAnglerWindowsPath,
            inputs.CalendarResolutionPath,
            inputs.TargetDateCalendarPath,
            inputs.TargetDateUnlockPath,
            inputs.TargetDateFestivalPath,
            inputs.TargetDateLocationPath,
            inputs.TargetDateFacilityPath,
            inputs.TargetDateResourcePath,
            inputs.TargetDateCurrencyPath,
            inputs.TargetDateReservationPath,
            inputs.TargetDateProcessingPath,
            inputs.TargetDateFishingProbabilityPath,
            inputs.TargetDateStochasticRetryPath,
            inputs.TargetDateDailyTimeEnergyPath,
            inputs.FishingForecastManifestPath,
            inputs.StrategyLedgerPath,
            inputs.BeforeSnapshotPath,
            inputs.RouteTimingCalibrationPath);

    internal static AcquisitionRoutePortfolioInputs PortfolioInputs(
        AcquisitionRouteExecutionBindingInputs inputs) => new()
        {
            RequirementInventoryPath = inputs.RequirementInventoryPath,
            AcquisitionLoweringPath = inputs.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = inputs.MasterAnglerWindowsPath,
            CalendarResolutionPath = inputs.CalendarResolutionPath,
            TargetDateCalendarPath = inputs.TargetDateCalendarPath,
            TargetDateUnlockPath = inputs.TargetDateUnlockPath,
            TargetDateFestivalPath = inputs.TargetDateFestivalPath,
            TargetDateLocationPath = inputs.TargetDateLocationPath,
            TargetDateFacilityPath = inputs.TargetDateFacilityPath,
            TargetDateResourcePath = inputs.TargetDateResourcePath,
            TargetDateCurrencyPath = inputs.TargetDateCurrencyPath,
            TargetDateReservationPath = inputs.TargetDateReservationPath,
            TargetDateProcessingPath = inputs.TargetDateProcessingPath,
            TargetDateFishingProbabilityPath =
                inputs.TargetDateFishingProbabilityPath,
            TargetDateStochasticRetryPath =
                inputs.TargetDateStochasticRetryPath,
            TargetDateDailyTimeEnergyPath =
                inputs.TargetDateDailyTimeEnergyPath,
            TargetDateOpportunityCostPath =
                inputs.TargetDateOpportunityCostPath,
            FishingForecastManifestPath = inputs.FishingForecastManifestPath,
            StrategyLedgerPath = inputs.StrategyLedgerPath,
            SnapshotPath = inputs.BeforeSnapshotPath,
            RouteTimingCalibrationPath = inputs.RouteTimingCalibrationPath,
            ProposalPath = inputs.PortfolioProposalPath
        };

    internal static string SelectedCandidateId(string routeOccurrenceId) =>
        "acquisition-route:" + routeOccurrenceId;


}
