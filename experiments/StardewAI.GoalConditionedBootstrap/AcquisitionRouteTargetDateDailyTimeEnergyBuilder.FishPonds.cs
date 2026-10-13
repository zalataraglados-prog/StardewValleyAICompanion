using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateFishPondOutputCollection(
            AcquisitionRouteTargetDateStochasticRetry route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionDailyTimeEnergySnapshotState state,
            AcquisitionCurrentRouteCandidateIndex candidates)
    {
        if (!candidates.TryFindCanonical(
                staticRoute.RouteKind,
                staticRoute.SourceId,
                staticRoute.QualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return Result(
                route,
                "native_fish_pond_output",
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
                candidate.OptionId == "fishing.service_fish_ponds" &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles is >= 0 &&
                candidate.EstimatedTicks >
                    candidate.RouteDistanceTiles.Value * 60 &&
                candidate.EnergyCost == 0 &&
                candidate.ExpectedOutputStack >= staticRoute.RequiredAmount &&
                candidate.ExpectedOutputQuality >= staticRoute.MinimumQuality &&
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
                "native_fish_pond_output",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "current_fish_pond_output_budget_candidate_incomplete" });
        }

        return matching.Select(candidate =>
                EvaluateTerminal(
                    route,
                    staticRoute,
                    state,
                    "native_fish_pond_output",
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
                    "current_native_fish_pond_interaction_input_profile",
                    new[]
                    {
                        "candidate:fishing.service_fish_ponds",
                        "candidate.parameters[authoritative_route_sources_json]",
                        "candidate.parameters[route_distance_tiles]",
                        "candidate.parameters[expected_output_items_json]",
                        "candidate.estimated_ticks",
                        "compiler:DailyPlanCompiler.FishPondSteps",
                        "compiler:ActionQueueCompiler.CompileCollectFishPondOutputStep"
                    }))
            .OrderByDescending(value =>
                value.DailyTimeEnergyMatchesTargetDate == true)
            .ThenByDescending(value => value.DailyTimeEnergyAxisResolved)
            .ThenBy(value =>
                value.Evaluation?.GuaranteedCompletionByTime ?? int.MaxValue)
            .First();
    }
}
