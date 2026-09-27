using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionMachineCapacitySupport
{
    internal static AcquisitionRouteDispatchCandidateMatch[] BuildCandidates(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        string goalId,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionRequirementRouteLowering lowered,
        string requestedCandidateKind,
        out AcquisitionMachineCapacitySupportBinding? binding,
        out string[] blockingReasons)
    {
        binding = null;
        var reasons = new List<string>();
        var location = reservation.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;
        var source = staticRoute.MachineSource;
        if (requirement.RouteKind is not (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output") ||
            staticRoute.RouteOccurrenceId != requirement.RouteOccurrenceId ||
            staticRoute.RouteKind != requirement.RouteKind ||
            staticRoute.SourceId != requirement.SourceId ||
            staticRoute.QualifiedItemId != requirement.QualifiedItemId ||
            staticRoute.Status !=
                "resolved_static_source_window_target_date_pending" ||
            staticRoute.BlockingReasons.Length != 0 ||
            source is null ||
            string.IsNullOrWhiteSpace(source.MachineQualifiedItemId))
        {
            reasons.Add("acquisition_machine_capacity_static_source_invalid");
        }
        if (!location.LocationRouteAxisResolved ||
            location.LocationRouteMatchesTargetDate != false ||
            location.LocationRouteAxisStatus != "resolved_location_route_miss" ||
            location.TargetEvaluations.Length != 0 ||
            location.BlockingReasons.Length != 0 ||
            !location.NonMatchingReasons.SequenceEqual(
                new[] { "matching_machine_runtime_location_not_present" },
                StringComparer.Ordinal))
        {
            reasons.Add(
                "acquisition_machine_capacity_missing_runtime_fleet_not_proven");
        }
        if (!lowered.SupportingOptionIds.Contains(
                "farm.establish_supported_machine_capacity",
                StringComparer.Ordinal))
        {
            reasons.Add("acquisition_machine_capacity_option_not_lowered");
        }
        if (requestedCandidateKind is not (
                "" or "craft_machine_item" or "place_machine_item"))
        {
            reasons.Add("acquisition_machine_capacity_candidate_kind_invalid");
        }
        if (reasons.Count > 0)
        {
            blockingReasons = reasons.ToArray();
            return Array.Empty<AcquisitionRouteDispatchCandidateMatch>();
        }

        var support = new MachineCapacitySupportSource(
            goalId,
            requirement.RouteOccurrenceId,
            requirement.RouteKind,
            requirement.SourceId,
            requirement.QualifiedItemId,
            source!.MachineQualifiedItemId);
        var supportJson = JsonSerializer.Serialize(support);
        var intentId = "machine-support:acquisition-route:" +
            requirement.RouteOccurrenceId + ":" +
            source.MachineQualifiedItemId;
        binding = new AcquisitionMachineCapacitySupportBinding(
            source.MachineQualifiedItemId,
            intentId,
            supportJson,
            "complete_exact_static_machine_source_and_missing_runtime_capacity");
        var optionCandidate = new OptionAvailabilityCandidate
        {
            OptionId = "farm.establish_supported_machine_capacity",
            Parameters = new[]
            {
                Parameter("machine_capacity_support_kind", "acquisition_route"),
                Parameter("machine_capacity_goal_id", goalId),
                Parameter(
                    "machine_capacity_route_occurrence_id",
                    requirement.RouteOccurrenceId),
                Parameter("machine_capacity_route_kind", requirement.RouteKind),
                Parameter("machine_capacity_source_id", requirement.SourceId),
                Parameter(
                    "machine_capacity_output_qualified_item_id",
                    requirement.QualifiedItemId),
                Parameter(
                    "machine_capacity_machine_qualified_item_id",
                    source.MachineQualifiedItemId),
                Parameter("machine_capacity_intent_id", intentId),
                Parameter(
                    "machine_capacity_requested_candidate_kind",
                    requestedCandidateKind)
            }
        };
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { optionCandidate },
            includeExecutorCalibrationOptions: true,
            commitmentLedger: ledger);
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            goalId);
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowered,
                ranked,
                source.MachineQualifiedItemId);
        if (matches.Length == 0)
        {
            reasons.AddRange(availability.Options
                .SelectMany(option => option.BlockingReasons)
                .Where(reason => !string.IsNullOrWhiteSpace(reason)));
            reasons.AddRange(availability.Options
                .SelectMany(option => option.EventCandidates)
                .SelectMany(candidate => candidate.BlockReasons)
                .Where(reason => !string.IsNullOrWhiteSpace(reason)));
            reasons.Add("no_current_exact_machine_capacity_support_candidate");
        }

        blockingReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return matches;
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private sealed record MachineCapacitySupportSource(
        [property: System.Text.Json.Serialization.JsonPropertyName("goal_id")]
        string GoalId,
        [property: System.Text.Json.Serialization.JsonPropertyName("route_occurrence_id")]
        string RouteOccurrenceId,
        [property: System.Text.Json.Serialization.JsonPropertyName("route_kind")]
        string RouteKind,
        [property: System.Text.Json.Serialization.JsonPropertyName("source_id")]
        string SourceId,
        [property: System.Text.Json.Serialization.JsonPropertyName("output_qualified_item_id")]
        string OutputQualifiedItemId,
        [property: System.Text.Json.Serialization.JsonPropertyName("machine_qualified_item_id")]
        string MachineQualifiedItemId);
}
