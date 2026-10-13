using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateAnimalProductHarvest(
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
                "native_farm_animal_product",
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
                candidate.OptionId == "farm.collect_animal_products" &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles is >= 0 &&
                candidate.EstimatedTicks >
                    candidate.RouteDistanceTiles.Value * 60 &&
                candidate.EnergyCost >= 0 &&
                !string.IsNullOrWhiteSpace(candidate.RequiredToolKind) &&
                candidate.ToolSlotIndex is >= 0 &&
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
                "native_farm_animal_product",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "current_animal_product_budget_candidate_incomplete" });
        }

        return matching.Select(candidate =>
                EvaluateTerminal(
                    route,
                    staticRoute,
                    state,
                    "native_farm_animal_product",
                    candidate.LocationId,
                    candidate.TargetTileX!.Value,
                    candidate.TargetTileY!.Value,
                    requireExactTargetTile: false,
                    GameClockBudgetPolicy.TicksToGameMinutes(
                        candidate.EstimatedTicks -
                        candidate.RouteDistanceTiles!.Value * 60),
                    1,
                    null,
                    state.AvailableEnergy,
                    candidate.EnergyCost,
                    candidate.EnergyCost,
                    0d,
                    "current_native_animal_product_tool_input_profile",
                    new[]
                    {
                        "candidate:farm.collect_animal_products",
                        "candidate.parameters[authoritative_route_sources_json]",
                        "candidate.parameters[required_tool_kind]",
                        "candidate.parameters[tool_slot_index]",
                        "candidate.parameters[route_distance_tiles]",
                        "candidate.estimated_ticks",
                        "candidate.energy_cost",
                        "compiler:DailyPlanCompiler.CollectAnimalProductSteps",
                        "compiler:ActionQueueCompiler.CompileCollectAnimalProductStep"
                    }))
            .OrderByDescending(value =>
                value.DailyTimeEnergyMatchesTargetDate == true)
            .ThenByDescending(value => value.DailyTimeEnergyAxisResolved)
            .ThenBy(value =>
                value.Evaluation?.GuaranteedCompletionByTime ?? int.MaxValue)
            .First();
    }
}
