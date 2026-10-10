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

        var terminalMinutes = Math.Max(
            1,
            (int)Math.Ceiling(
                candidate.EstimatedTargetCostMs.Value /
                GameClockBudgetPolicy.RealMillisecondsPerGameMinute));
        var start = state.RouteState.CurrentTime;
        var window = RequirementRoute(route).MatchingWindows
            .Where(value => string.IsNullOrWhiteSpace(value.LocationId) ||
                string.Equals(
                    value.LocationId,
                    candidate.LocationId,
                    StringComparison.OrdinalIgnoreCase))
            .SelectMany(value => value.TimeWindows)
            .Select(value => new
            {
                Window = value,
                Start = Math.Max(start, value.StartTime),
                AvailableMinutes = GameClockBudgetPolicy.ClockMinutesBetween(
                    Math.Max(start, value.StartTime),
                    value.EndTime)
            })
            .Where(value => value.AvailableMinutes >= terminalMinutes)
            .OrderBy(value => value.Start)
            .ThenBy(value => value.Window.EndTime)
            .FirstOrDefault();
        var evaluation = new AcquisitionDailyTimeEnergyEvaluation(
            candidate.LocationId,
            candidate.TargetTileX,
            candidate.TargetTileY,
            candidate.StandTileX,
            candidate.StandTileY,
            start,
            start,
            window?.Window.StartTime,
            window?.Window.EndTime,
            terminalMinutes,
            window is null
                ? null
                : GameClockBudgetPolicy.AddClockMinutes(
                    window.Start,
                    terminalMinutes),
            1,
            null,
            state.AvailableEnergy,
            null,
            null,
            0d,
            window is not null,
            true,
            "existing_native_mining_combat_profile",
            state.RouteState.Timing?.EvidenceId ?? string.Empty,
            new[]
            {
                "candidate:mining.reach_depth",
                "candidate.parameters[estimated_target_cost_ms]",
                "candidate.parameters[max_attacks]",
                "state.mining.monsters.value[].melee_attack_projections",
                "compiler:MiningFloorStepCompiler"
            });
        if (window is null)
        {
            return Result(
                route,
                "native_monster_drop_table",
                "resolved_daily_time_budget_miss",
                true,
                false,
                evaluation,
                new[] { "terminal_duration_does_not_fit_source_window" },
                Array.Empty<string>());
        }
        return Result(
            route,
            "native_monster_drop_table",
            "resolved_daily_time_energy_budget_match",
            true,
            true,
            evaluation,
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
