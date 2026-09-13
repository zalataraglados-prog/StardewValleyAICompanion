namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static AcquisitionRouteTargetDateProcessing NotApplicable(
        AcquisitionRouteTargetDateReservation route,
        string status) => Result(
            route,
            status,
            true,
            null,
            "not_applicable",
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateProcessing NotRequired(
        AcquisitionRouteTargetDateReservation route) => Result(
            route,
            "resolved_processing_lead_time_not_required",
            true,
            true,
            NoDeterministicWait,
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateProcessing ResolvedMatch(
        AcquisitionRouteTargetDateReservation route,
        string requirementKind,
        AcquisitionProcessingLeadTimeEvaluation[] evaluations) => Result(
            route,
            "resolved_processing_lead_time_match",
            true,
            true,
            requirementKind,
            evaluations,
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateProcessing ResolvedMiss(
        AcquisitionRouteTargetDateReservation route,
        string requirementKind,
        AcquisitionProcessingLeadTimeEvaluation[] evaluations,
        params string[] reasons) => Result(
            route,
            "resolved_processing_lead_time_miss",
            true,
            false,
            requirementKind,
            evaluations,
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateProcessing Blocked(
        AcquisitionRouteTargetDateReservation route,
        string requirementKind,
        params string[] reasons) => Blocked(
            route,
            requirementKind,
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            reasons);

    private static AcquisitionRouteTargetDateProcessing Blocked(
        AcquisitionRouteTargetDateReservation route,
        string requirementKind,
        AcquisitionProcessingLeadTimeEvaluation[] evaluations,
        params string[] reasons) => Result(
            route,
            "blocked_processing_lead_time_evidence",
            false,
            null,
            requirementKind,
            evaluations,
            Array.Empty<string>(),
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());

    private static AcquisitionProcessingLeadTimeEvaluation EvaluationBlocked(
        string locationId,
        string productionStateKind,
        params string[] reasons) => new(
            locationId,
            productionStateKind,
            "blocked_processing_lead_time_evidence",
            string.Empty,
            null,
            null,
            null,
            null,
            Array.Empty<string>(),
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());

    private static AcquisitionRouteTargetDateProcessing Result(
        AcquisitionRouteTargetDateReservation route,
        string status,
        bool resolved,
        bool? matches,
        string requirementKind,
        AcquisitionProcessingLeadTimeEvaluation[] evaluations,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            status,
            resolved,
            matches,
            requirementKind,
            evaluations,
            nonMatchingReasons,
            blockingReasons);
}
