using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy EvaluateGeodeDrop(
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
                "native_geode_processing",
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
        var candidate = matches
            .Where(value => value.OptionId == "processing.crack_geode" &&
                value.TargetTileX.HasValue &&
                value.TargetTileY.HasValue &&
                value.ExpectedOutputStack >= staticRoute.RequiredAmount &&
                value.ExpectedOutputQuality >= staticRoute.MinimumQuality &&
                resolvedTargets.Contains((
                    value.LocationId,
                    value.TargetTileX,
                    value.TargetTileY)))
            .OrderBy(value => value.EstimatedTicks)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is null || candidate.EstimatedTicks <= 0)
        {
            return Result(
                route,
                "native_geode_processing",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "matching_current_geode_budget_candidate_not_present" });
        }

        return EvaluateTerminal(
            route,
            staticRoute,
            state,
            "native_geode_processing",
            candidate.LocationId,
            candidate.TargetTileX.GetValueOrDefault(),
            candidate.TargetTileY.GetValueOrDefault(),
            requireExactTargetTile: false,
            GameClockBudgetPolicy.TicksToGameMinutes(
                candidate.EstimatedTicks),
            1,
            null,
            state.AvailableEnergy,
            0d,
            0d,
            0d,
            "existing_native_blacksmith_geode_processing_profile",
            new[]
            {
                "candidate:processing.crack_geode",
                "candidate.estimated_ticks",
                "state.player.geode_processing.value",
                "compiler:DailyPlanCompiler.GeodeProcessingSteps",
                "compiler:ActionQueueCompiler.CompileGeodeProcessingStep"
            });
    }
}
