using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateArtifactSpot(
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
                "native_location_artifact_spot",
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
        var matchingCandidates = matches
            .Where(candidate =>
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.MaxMovementTiles.HasValue &&
                candidate.MaxMovementTiles.Value >= 0 &&
                candidate.MaxToolSwings.HasValue &&
                candidate.MaxToolSwings.Value > 0 &&
                candidate.TerminalActionTicks > 0 &&
                candidate.EnergyCost >= 0 &&
                string.Equals(
                    state.RouteState.CurrentLocationId,
                    candidate.LocationId,
                    StringComparison.OrdinalIgnoreCase) &&
                resolvedTargets.Contains((
                    candidate.LocationId,
                    candidate.TargetTileX,
                    candidate.TargetTileY)))
            .ToArray();
        if (matchingCandidates.Length == 0)
        {
            return Result(
                route,
                "native_location_artifact_spot",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[]
                {
                    "current_location_artifact_spot_budget_candidate_incomplete"
                });
        }
        if (!state.AvailableEnergy.HasValue)
        {
            return Result(
                route,
                "native_location_artifact_spot",
                "blocked_daily_energy_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                state.EnergyBlockingReasons);
        }

        AcquisitionRouteTargetDateDailyTimeEnergy? best = null;
        foreach (var candidate in matchingCandidates)
        {
            var evaluated = EvaluateTerminal(
                route,
                staticRoute,
                state,
                "native_location_artifact_spot",
                candidate.LocationId,
                candidate.TargetTileX!.Value,
                candidate.TargetTileY!.Value,
                requireExactTargetTile: false,
                GameClockBudgetPolicy.TicksToGameMinutes(
                    candidate.TerminalActionTicks),
                1,
                null,
                state.AvailableEnergy.Value,
                candidate.EnergyCost,
                candidate.EnergyCost,
                0d,
                "existing_native_clear_obstacle_artifact_spot_profile",
                new[]
                {
                    "candidate:foraging.excavate_artifact_spots",
                    "candidate.parameters[route_distance_tiles]",
                    "candidate.parameters[max_tool_swings]",
                    "candidate.estimated_ticks",
                    "candidate.energy_cost",
                    "state.current_location.objects.value[].clear_output_items",
                    "state.current_location.objects.value[].clear_authoritative_route_sources",
                    "compiler:DailyPlanCompiler.ClearPlantSteps",
                    "compiler:ActionQueueCompiler.ClearObstacle"
                },
                acceptDefaultLocationWindow: true,
                exactStandTileX: candidate.StandTileX,
                exactStandTileY: candidate.StandTileY);
            if (evaluated.DailyTimeEnergyMatchesTargetDate == true)
                return evaluated;
            if (best is null ||
                evaluated.DailyTimeEnergyAxisResolved &&
                !best.DailyTimeEnergyAxisResolved ||
                evaluated.DailyTimeEnergyAxisResolved ==
                    best.DailyTimeEnergyAxisResolved &&
                (evaluated.Evaluation?.GuaranteedCompletionByTime ??
                    int.MaxValue) <
                (best.Evaluation?.GuaranteedCompletionByTime ?? int.MaxValue))
            {
                best = evaluated;
            }
        }
        return best ?? throw new InvalidDataException(
            "A non-empty artifact-spot candidate set produced no evaluation.");
    }
}
