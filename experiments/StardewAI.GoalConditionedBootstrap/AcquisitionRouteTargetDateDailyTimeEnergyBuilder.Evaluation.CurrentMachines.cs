using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateCurrentRouteCollection(
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
                "native_current_route_collection",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                blockingReasons);
        }

        var dispatchableTargets = AcquisitionCurrentMachineOutputTargetSelector
            .SelectDispatchable(
                FacilityRoute(route).TargetEvaluations,
                staticRoute.QualifiedItemId,
                staticRoute.RequiredAmount,
                staticRoute.MinimumQuality)
            .Select(value => (
                value.TargetLocationId,
                value.TargetTileX,
                value.TargetTileY))
            .ToHashSet();
        var candidate = matches.FirstOrDefault(value =>
            dispatchableTargets.Contains((
                value.LocationId,
                value.TargetTileX,
                value.TargetTileY)));
        if (candidate is null ||
            !candidate.TargetTileX.HasValue ||
            !candidate.TargetTileY.HasValue ||
            !candidate.MaxMovementTiles.HasValue ||
            candidate.MaxMovementTiles.Value < 0 ||
            candidate.EstimatedTicks <= 0 ||
            candidate.EnergyCost != 0)
        {
            return Result(
                route,
                "native_current_route_collection",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[]
                {
                    "current_route_collection_budget_candidate_incomplete"
                });
        }

        var terminalTicks = Math.Max(
            1,
            candidate.EstimatedTicks -
                candidate.MaxMovementTiles.Value * 60);
        return EvaluateTerminal(
            route,
            staticRoute,
            state,
            "native_current_route_collection",
            candidate.LocationId,
            candidate.TargetTileX.Value,
            candidate.TargetTileY.Value,
            requireExactTargetTile: false,
            GameClockBudgetPolicy.TicksToGameMinutes(terminalTicks),
            1,
            null,
            null,
            null,
            null,
            0d,
            "existing_native_machine_output_collection_profile",
            new[]
            {
                "candidate:farm.collect_machine_outputs",
                "candidate.estimated_ticks",
                "candidate.parameters[max_movement_tiles]",
                "compiler:DailyPlanCompiler.MachineSteps",
                "compiler:ActionQueueCompiler.CollectMachineOutput"
            });
    }

    private static AcquisitionRouteTargetDateFacility FacilityRoute(
        AcquisitionRouteTargetDateStochasticRetry route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;
}
