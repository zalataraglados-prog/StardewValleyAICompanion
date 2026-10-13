using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateFruitTreeHarvest(
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
                "native_fruit_tree_produce",
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
        var matching = matches.Where(candidate =>
                candidate.OptionId == "foraging.harvest_fruit_tree" &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles.HasValue &&
                candidate.RouteDistanceTiles.Value >= 0 &&
                candidate.EstimatedTicks >
                    candidate.RouteDistanceTiles.Value * 60 &&
                candidate.EnergyCost == 0 &&
                string.Equals(
                    state.RouteState.CurrentLocationId,
                    candidate.LocationId,
                    StringComparison.OrdinalIgnoreCase) &&
                resolvedTargets.Contains((
                    candidate.LocationId,
                    candidate.TargetTileX,
                    candidate.TargetTileY)))
            .ToArray();
        if (matching.Length == 0)
        {
            return Result(
                route,
                "native_fruit_tree_produce",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "current_fruit_tree_budget_candidate_incomplete" });
        }

        return matching.Select(candidate =>
                EvaluateTerminal(
                    route,
                    staticRoute,
                    state,
                    "native_fruit_tree_produce",
                    candidate.LocationId,
                    candidate.TargetTileX!.Value,
                    candidate.TargetTileY!.Value,
                    requireExactTargetTile: false,
                    GameClockBudgetPolicy.TicksToGameMinutes(
                        candidate.EstimatedTicks -
                        candidate.RouteDistanceTiles!.Value * 60),
                    1,
                    null,
                    null,
                    null,
                    null,
                    0d,
                    "current_native_fruit_tree_shake_input_profile",
                    new[]
                    {
                        "candidate:foraging.harvest_fruit_tree",
                        "candidate.parameters[authoritative_route_sources_json]",
                        "candidate.parameters[route_distance_tiles]",
                        "candidate.parameters[expected_output_items_json]",
                        "candidate.estimated_ticks",
                        "compiler:DailyPlanCompiler.HarvestFruitTreeSteps",
                        "compiler:ActionQueueCompiler.CompileHarvestFruitTreeStep"
                    }))
            .OrderByDescending(value =>
                value.DailyTimeEnergyMatchesTargetDate == true)
            .ThenByDescending(value => value.DailyTimeEnergyAxisResolved)
            .ThenBy(value =>
                value.Evaluation?.GuaranteedCompletionByTime ?? int.MaxValue)
            .First();
    }
}
