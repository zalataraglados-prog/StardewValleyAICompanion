namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static AcquisitionRouteTargetDateProcessing EvaluateCrabPot(
        AcquisitionRouteTargetDateReservation route,
        string targetItem,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        var facility = route.UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var result = AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
            state.CrabPotNetwork,
            facility.TargetEvaluations.Select(target =>
                target.TargetLocationId),
            targetItem,
            targetTotalDay);
        if (!result.EvidenceAvailable)
        {
            return Blocked(
                route,
                CrabPotProduction,
                result.Evaluations,
                result.BlockingReasons);
        }
        if (result.Evaluations.Any(value =>
                value.OutputReadyOnTargetDate == true))
        {
            return ResolvedMatch(
                route,
                CrabPotProduction,
                result.Evaluations);
        }
        return ResolvedMiss(
            route,
            CrabPotProduction,
            result.Evaluations,
            "crab_pot_output_not_ready_until_next_day");
    }
}
