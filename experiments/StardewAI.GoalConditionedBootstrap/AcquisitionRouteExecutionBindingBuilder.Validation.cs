using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteExecutionBindingBuilder
{
    private static IEnumerable<string> ValidateSelection(
        AcquisitionRouteTargetDateOpportunityCostReport report,
        AcquisitionRouteTargetDateOpportunityCost selected,
        AcquisitionRouteTargetDateUnlock requirement,
        SnapshotEnvelope before,
        string snapshotPath)
    {
        if (!string.Equals(
                report.SchemaVersion,
                "acquisition_route_target_date_opportunity_cost.v1",
                StringComparison.Ordinal) ||
            !string.Equals(
                report.Status,
                "complete_target_date_opportunity_cost_axis_downstream_pending",
                StringComparison.Ordinal) ||
            !report.RouteOccurrenceInventoryComplete ||
            !report.OpportunityCostAxisResolutionComplete ||
            report.TrainingLabelEligible ||
            report.RouteOccurrenceCount != report.Routes.Length ||
            report.OpportunityCostAxisResolvedCount != report.Routes.Count(
                route => route.OpportunityCostAxisResolved) ||
            report.ParetoFrontierCount != report.Routes.Count(route =>
                route.OpportunityCostAxisStatus ==
                    "resolved_opportunity_cost_pareto_frontier") ||
            report.ParetoDominatedCount != report.Routes.Count(route =>
                route.OpportunityCostAxisStatus ==
                    "resolved_opportunity_cost_pareto_dominated"))
        {
            yield return "route_selection_opportunity_frontier_incomplete";
        }
        if (!string.Equals(
                selected.OpportunityCostAxisStatus,
                "resolved_opportunity_cost_pareto_frontier",
                StringComparison.Ordinal) ||
            !selected.OpportunityCostAxisResolved ||
            selected.OpportunityCostMatchesTargetDate != true ||
            selected.CostVector is null ||
            selected.DominatedByRouteOccurrenceIds.Length != 0 ||
            selected.NonMatchingReasons.Length != 0 ||
            selected.BlockingReasons.Length != 0)
        {
            yield return "route_selection_not_on_resolved_pareto_frontier";
        }
        if (!string.Equals(
                selected.RouteOccurrenceId,
                requirement.RouteOccurrenceId,
                StringComparison.Ordinal) ||
            requirement.AlternativeIndex < 0 ||
            requirement.RouteIndex < 0 ||
            requirement.RequiredAmount <= 0 ||
            requirement.MinimumQuality < 0 ||
            string.IsNullOrWhiteSpace(requirement.RequirementSetId) ||
            string.IsNullOrWhiteSpace(requirement.RequirementId) ||
            string.IsNullOrWhiteSpace(requirement.RouteKind) ||
            string.IsNullOrWhiteSpace(requirement.SourceId))
        {
            yield return "route_selection_requirement_identity_invalid";
        }
        if (!string.Equals(before.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            !string.Equals(before.GameVersion, report.GameVersion,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(before.SaveId.Value) ||
            string.IsNullOrWhiteSpace(before.PlayerId.Value) ||
            string.IsNullOrWhiteSpace(before.StateHash) ||
            !string.Equals(before.StateHash, report.SnapshotStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(
                CurrentTeacherFrontierSupport.HashFile(snapshotPath),
                report.SnapshotSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            yield return "route_selection_before_snapshot_identity_mismatch";
        }
    }

    private static IEnumerable<string> ValidateQueue(
        ActionQueueEnvelope queue,
        string goalId,
        string beforeStateHash,
        string selectedCandidateId,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering route)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        if (!string.Equals(queue.SchemaVersion, "action_queue.v1",
                StringComparison.Ordinal) ||
            !string.Equals(queue.Status, "pending", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(queue.QueueId) ||
            !string.Equals(queue.GoalId, goalId, StringComparison.Ordinal) ||
            !string.Equals(queue.StateHash, beforeStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(
                queue.ExecutionMode,
                ExecutionTargetProfiles.TrainingSingleplayer,
                StringComparison.Ordinal) ||
            items.Length is < 1 or > TeacherEvidenceRolloutLimits.MaxQueueItems ||
            (queue.CompilerDiagnostics?.Length ?? 0) != 0)
        {
            yield return "route_queue_envelope_invalid";
        }
        if (string.IsNullOrWhiteSpace(selectedCandidateId) ||
            (queue.CandidateAudit ?? Array.Empty<SmallModelPlanCandidateAudit>())
                .Count(candidate =>
                    string.Equals(candidate.CandidateId, selectedCandidateId,
                        StringComparison.Ordinal) &&
                    string.Equals(candidate.Decision, "accepted",
                        StringComparison.Ordinal)) != 1)
        {
            yield return "route_queue_selected_candidate_not_accepted";
        }
        if (items.Select(item => item.QueueItemId)
                .Distinct(StringComparer.Ordinal).Count() != items.Length ||
            items.Any(item => string.IsNullOrWhiteSpace(item.QueueItemId) ||
                string.IsNullOrWhiteSpace(item.SourceActionId) ||
                string.IsNullOrWhiteSpace(item.OptionId)))
        {
            yield return "route_queue_item_identity_invalid";
        }

        var allowed = route.EndpointOptionIds
            .Concat(route.SupportingOptionIds)
            .ToHashSet(StringComparer.Ordinal);
        if (route.EndpointOptionIds.Length == 0 ||
            allowed.Count == 0 ||
            items.Any(item => !allowed.Contains(item.OptionId)) ||
            !items.Any(item => route.EndpointOptionIds.Contains(
                item.OptionId,
                StringComparer.Ordinal)))
        {
            yield return "route_queue_option_outside_authoritative_route";
        }
        if (items.Any(item =>
                !string.Equals(item.Status, "pending", StringComparison.Ordinal) ||
                (item.MissingStateFactors?.Length ?? 0) != 0 ||
                (item.BlockingReasons?.Length ?? 0) != 0 ||
                item.NormalizedCommand is null ||
                !string.Equals(item.NormalizedCommand.CommandType,
                    "option_request", StringComparison.Ordinal) ||
                !string.Equals(item.NormalizedCommand.OptionId, item.OptionId,
                    StringComparison.Ordinal) ||
                !string.Equals(item.NormalizedCommand.StateHash,
                    beforeStateHash, StringComparison.Ordinal) ||
                !string.Equals(item.NormalizedCommand.ExecutionMode,
                    queue.ExecutionMode, StringComparison.Ordinal) ||
                item.NormalizedCommand.Steps is not { Length: 1 } ||
                !RouteParametersMatch(
                    item.NormalizedCommand.Parameters,
                    requirement) ||
                !SameActor(item.NormalizedCommand.Actor, queue.Actor)))
        {
            yield return "route_queue_command_binding_invalid";
        }
        if (queue.Actor is null ||
            string.IsNullOrWhiteSpace(queue.Actor.ActorId) ||
            string.IsNullOrWhiteSpace(queue.Actor.ActorType) ||
            string.IsNullOrWhiteSpace(queue.Actor.ControlSurface))
        {
            yield return "route_queue_actor_invalid";
        }
    }

    private static AcquisitionRequirementRouteLowering LoweredRoute(
        AcquisitionRouteOptionLoweringReport lowering,
        AcquisitionRouteTargetDateUnlock requirement)
    {
        var set = lowering.RequirementSets.SingleOrDefault(value =>
            string.Equals(value.RequirementSetId, requirement.RequirementSetId,
                StringComparison.Ordinal));
        var group = set?.Groups.SingleOrDefault(value => string.Equals(
            value.RequirementId,
            requirement.RequirementId,
            StringComparison.Ordinal));
        Require(group is not null &&
                requirement.AlternativeIndex >= 0 &&
                requirement.AlternativeIndex < group.Alternatives.Length,
            "Selected route alternative is absent from acquisition lowering.");
        var alternative = group!.Alternatives[requirement.AlternativeIndex];
        Require(requirement.RouteIndex >= 0 &&
                requirement.RouteIndex < alternative.Routes.Length,
            "Selected route index is absent from acquisition lowering.");
        return alternative.Routes[requirement.RouteIndex];
    }

    private static void ValidateLoweredIdentity(
        AcquisitionRouteOptionLoweringReport lowering,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering route)
    {
        var set = lowering.RequirementSets.Single(value => string.Equals(
            value.RequirementSetId,
            requirement.RequirementSetId,
            StringComparison.Ordinal));
        var group = set.Groups.Single(value => string.Equals(
            value.RequirementId,
            requirement.RequirementId,
            StringComparison.Ordinal));
        var alternative = group.Alternatives[requirement.AlternativeIndex];
        Require(string.Equals(lowering.SchemaVersion,
                    "acquisition_route_option_lowering.v1",
                    StringComparison.Ordinal) &&
                lowering.DependencyAxisInventoryComplete &&
                StageOneCollectionRouteDependencyAxes.IsComplete(
                    lowering.RequiredDownstreamDependencyAxes) &&
                string.Equals(alternative.QualifiedItemId,
                    requirement.QualifiedItemId, StringComparison.Ordinal) &&
                string.Equals(alternative.MatchKind,
                    requirement.MatchKind, StringComparison.Ordinal) &&
                alternative.Amount == requirement.RequiredAmount &&
                alternative.MinimumQuality == requirement.MinimumQuality &&
                string.Equals(route.RouteKind, requirement.RouteKind,
                    StringComparison.Ordinal) &&
                string.Equals(route.SourceId, requirement.SourceId,
                    StringComparison.Ordinal) &&
                route.RuntimeAdmissionReady &&
                route.TeacherAdmissionReady,
            "Selected route identity or lowering admission drifted.");
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateOpportunityCost route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;

    private static string TerminalReceiptKind(string matchKind) => matchKind switch
    {
        "item_id" => "exact_player_inventory_quantity_increase",
        "money_payment" => "native_community_center_payment_completion",
        _ => string.Empty
    };

    internal static SmallModelActionParameter[] RouteBindingParameters(
        AcquisitionRouteTargetDateUnlock requirement) => new[]
    {
        Parameter("acquisition_route_occurrence_id",
            requirement.RouteOccurrenceId),
        Parameter("acquisition_requirement_set_id",
            requirement.RequirementSetId),
        Parameter("acquisition_requirement_id", requirement.RequirementId),
        Parameter("acquisition_alternative_index",
            requirement.AlternativeIndex.ToString(CultureInfo.InvariantCulture)),
        Parameter("acquisition_route_index",
            requirement.RouteIndex.ToString(CultureInfo.InvariantCulture)),
        Parameter("acquisition_qualified_item_id",
            requirement.QualifiedItemId),
        Parameter("acquisition_match_kind", requirement.MatchKind),
        Parameter("acquisition_required_amount",
            requirement.RequiredAmount.ToString(CultureInfo.InvariantCulture)),
        Parameter("acquisition_minimum_quality",
            requirement.MinimumQuality.ToString(CultureInfo.InvariantCulture)),
        Parameter("acquisition_route_kind", requirement.RouteKind),
        Parameter("acquisition_source_id", requirement.SourceId)
    };

    private static bool RouteParametersMatch(
        SmallModelActionParameter[]? actual,
        AcquisitionRouteTargetDateUnlock requirement)
    {
        var values = actual ?? Array.Empty<SmallModelActionParameter>();
        return RouteBindingParameters(requirement).All(expected =>
        {
            var matches = values.Where(value => string.Equals(
                    value.Name,
                    expected.Name,
                    StringComparison.Ordinal))
                .ToArray();
            return matches.Length == 1 && string.Equals(
                matches[0].Value,
                expected.Value,
                StringComparison.Ordinal);
        });
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private static bool SameActor(ActionActorRef? left, ActionActorRef? right) =>
        left is not null && right is not null &&
        string.Equals(left.ActorId, right.ActorId, StringComparison.Ordinal) &&
        string.Equals(left.ActorType, right.ActorType, StringComparison.Ordinal) &&
        string.Equals(left.ControlSurface, right.ControlSurface,
            StringComparison.Ordinal);
}
