namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static AcquisitionRouteTargetDateProcessing EvaluateCrabPot(
        AcquisitionRouteTargetDateReservation route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        var facility = route.UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var result = AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
            state.CrabPotNetwork,
            facility.TargetEvaluations.Select(target =>
                target.TargetLocationId),
            staticRoute.QualifiedItemId,
            targetTotalDay);
        if (!result.EvidenceAvailable)
        {
            return Blocked(
                route,
                CrabPotProduction,
                result.Evaluations,
                result.BlockingReasons);
        }
        var provenReadyQuantity = AcquisitionOutputProof.ReadyQuantity(
            result.Evaluations,
            staticRoute.MinimumQuality);
        if (provenReadyQuantity >= staticRoute.RequiredAmount)
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
            provenReadyQuantity > 0
                ? "crab_pot_ready_output_quantity_or_quality_shortfall:" +
                    provenReadyQuantity + ":" + staticRoute.RequiredAmount
                : "crab_pot_output_not_ready_until_next_day");
    }
}
