namespace StardewAI.GoalConditionedBootstrap;

using StardewAI.Core.Execution;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateCommunityCenterReward(
            AcquisitionRouteTargetDateStochasticRetry route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionDailyTimeEnergySnapshotState state,
            AcquisitionCurrentRouteCandidateIndex candidates)
    {
        if (!candidates.TryFind(
                staticRoute.RouteKind,
                staticRoute.SourceId,
                staticRoute.QualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return Result(
                route,
                "native_community_center_reward_claim",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                blockingReasons);
        }

        var resolvedTargets = LocationRoute(route).TargetEvaluations
            .Where(value => value.Status == "resolved_location_route_match")
            .Select(value => (
                value.TargetLocationId,
                value.TargetTileX,
                value.TargetTileY))
            .ToHashSet();
        var candidate = matches.SingleOrDefault(value =>
            value.OptionId == "community_center.donate_bundle_items" &&
            value.LocationId == "CommunityCenter" &&
            value.TargetTileX.HasValue &&
            value.TargetTileY.HasValue &&
            value.StandTileX.HasValue &&
            value.StandTileY.HasValue &&
            value.EstimatedTicks > 0 &&
            value.EnergyCost == 0 &&
            resolvedTargets.Contains((
                value.LocationId,
                value.TargetTileX,
                value.TargetTileY)));
        if (candidate is null)
        {
            return Result(
                route,
                "native_community_center_reward_claim",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[]
                {
                    "current_community_center_reward_budget_candidate_incomplete"
                });
        }

        return EvaluateTerminal(
            route,
            staticRoute,
            state,
            "native_community_center_reward_claim",
            candidate.LocationId,
            candidate.TargetTileX!.Value,
            candidate.TargetTileY!.Value,
            requireExactTargetTile: false,
            GameClockBudgetPolicy.TicksToGameMinutes(
                candidate.TerminalActionTicks),
            1,
            null,
            null,
            null,
            null,
            0d,
            "current_native_community_center_reward_input_profile",
            new[]
            {
                "candidate:community_center.donate_bundle_items",
                "candidate.parameters[authoritative_route_sources_json]",
                "candidate.estimated_ticks",
                "compiler:DailyPlanCompiler.CommunityCenterSteps",
                "compiler:ActionQueueCompiler.ClaimCommunityCenterBundleReward"
            },
            exactStandTileX: candidate.StandTileX,
            exactStandTileY: candidate.StandTileY);
    }
}
