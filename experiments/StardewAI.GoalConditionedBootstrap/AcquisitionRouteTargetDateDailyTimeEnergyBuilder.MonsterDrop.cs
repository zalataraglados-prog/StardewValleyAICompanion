using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateMonsterDrop(
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
                "native_monster_drop_table",
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
        var candidate = matches.FirstOrDefault(value =>
            resolvedTargets.Contains((
                value.LocationId,
                value.TargetTileX,
                value.TargetTileY)));
        if (candidate is null ||
            !candidate.TargetTileX.HasValue ||
            !candidate.TargetTileY.HasValue ||
            !candidate.StandTileX.HasValue ||
            !candidate.StandTileY.HasValue ||
            !candidate.RouteDistanceTiles.HasValue ||
            candidate.RouteDistanceTiles.Value < 0 ||
            !candidate.MaxAttacks.HasValue ||
            candidate.MaxAttacks.Value <= 0 ||
            !candidate.EstimatedTargetCostMs.HasValue ||
            candidate.EstimatedTargetCostMs.Value <= 0d ||
            !string.Equals(
                state.RouteState.CurrentLocationId,
                candidate.LocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result(
                route,
                "native_monster_drop_table",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[]
                {
                    "current_monster_drop_budget_candidate_incomplete"
                });
        }

        return EvaluateTerminal(
            route,
            staticRoute,
            state,
            "native_monster_drop_table",
            candidate.LocationId,
            candidate.TargetTileX.Value,
            candidate.TargetTileY.Value,
            requireExactTargetTile: false,
            Math.Max(
                1,
                (int)Math.Ceiling(
                    candidate.EstimatedTargetCostMs.Value /
                    GameClockBudgetPolicy.RealMillisecondsPerGameMinute)),
            1,
            null,
            state.AvailableEnergy,
            null,
            null,
            0d,
            "existing_native_mining_combat_profile",
            new[]
            {
                "candidate:mining.reach_depth",
                candidate.RouteDistanceEvidencePath,
                "candidate.parameters[stand_tile_x]",
                "candidate.parameters[stand_tile_y]",
                "candidate.parameters[estimated_target_cost_ms]",
                "candidate.parameters[max_attacks]",
                "state.mining.monsters.value[].melee_attack_projections",
                "compiler:MiningFloorStepCompiler"
            },
            exactStandTileX: candidate.StandTileX,
            exactStandTileY: candidate.StandTileY,
            exactCurrentLocationRouteDistanceTiles:
                candidate.RouteDistanceTiles,
            exactCurrentLocationRouteDistanceEvidencePath:
                candidate.RouteDistanceEvidencePath);
    }
}
