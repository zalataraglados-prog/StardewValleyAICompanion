using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteExecutionBindingBuilder
{
    public static AcquisitionRouteExecutionBinding Build(
        AcquisitionRouteExecutionBindingInputs inputs)
    {
        var opportunityPath = Path.GetFullPath(
            inputs.TargetDateOpportunityCostPath);
        var loweringPath = Path.GetFullPath(inputs.AcquisitionLoweringPath);
        var snapshotPath = Path.GetFullPath(inputs.BeforeSnapshotPath);
        var queuePath = Path.GetFullPath(inputs.ActionQueuePath);
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            opportunityPath,
            "Acquisition route target-date opportunity cost");
        var recomputed = RecomputeOpportunityCost(inputs);
        Require(EqualJson(opportunity, recomputed),
            "Target-date opportunity-cost report drifted from deterministic source compilation.");
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
            .Concat(ValidateQueue(
                queue,
                opportunity.GoalId,
                before.StateHash,
                selectedCandidateId,
                requirement,
                lowered))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var optionsBound = !reasons.Any(reason => reason.StartsWith(
            "route_queue_",
            StringComparison.Ordinal));
        var selectedFromFrontier = !reasons.Any(reason => reason.StartsWith(
            "route_selection_",
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

    internal static string SelectedCandidateId(string routeOccurrenceId) =>
        "acquisition-route:" + routeOccurrenceId;

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
