namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private static AcquisitionRouteTargetDateResource EvaluateGeodeInput(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionResourceInputSnapshotState state,
        AcquisitionCurrentRouteCandidateIndex? candidates)
    {
        if (candidates is null)
        {
            return Blocked(
                route,
                CurrentGeodeInput,
                "current_geode_candidate_index_missing");
        }
        if (!candidates.TryFind(
                staticRoute.RouteKind,
                staticRoute.SourceId,
                staticRoute.QualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return Blocked(
                route,
                CurrentGeodeInput,
                blockingReasons);
        }

        var candidate = matches
            .Where(value => value.OptionId == "processing.crack_geode" &&
                !string.IsNullOrWhiteSpace(value.InputQualifiedItemId) &&
                value.InputSlotIndex.HasValue &&
                value.InputStackBefore >= 1 &&
                value.ExpectedOutputStack >= staticRoute.RequiredAmount &&
                value.ExpectedOutputQuality >= staticRoute.MinimumQuality)
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is null)
        {
            return Blocked(
                route,
                CurrentGeodeInput,
                "matching_current_geode_input_candidate_not_present");
        }

        return EvaluateMaterial(
            route,
            CurrentGeodeInput,
            "geode_input",
            candidate.InputQualifiedItemId,
            1,
            state);
    }
}
