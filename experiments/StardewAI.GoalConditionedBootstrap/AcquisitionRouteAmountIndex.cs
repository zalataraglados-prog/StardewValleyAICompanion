namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionRouteAmountIndex
{
    public static IReadOnlyDictionary<string, AcquisitionRouteAmount> Build(
        AcquisitionRouteOptionLoweringReport lowering,
        AcquisitionRouteTargetDateResourceReport source)
    {
        Require(lowering.SchemaVersion ==
                    "acquisition_route_option_lowering.v1" &&
                lowering.Status == "complete" &&
                lowering.GoalId == source.GoalId &&
                lowering.DependencyAxisInventoryComplete &&
                StageOneCollectionRouteDependencyAxes.IsComplete(
                    lowering.RequiredDownstreamDependencyAxes) &&
                lowering.RouteOccurrenceCount == source.RouteOccurrenceCount,
            "Acquisition lowering metadata is incomplete for currency binding.");
        var result = new Dictionary<string, AcquisitionRouteAmount>(
            StringComparer.Ordinal);
        foreach (var set in lowering.RequirementSets)
        {
            foreach (var group in set.Groups)
            {
                for (var alternativeIndex = 0;
                     alternativeIndex < group.Alternatives.Length;
                     alternativeIndex++)
                {
                    var alternative = group.Alternatives[alternativeIndex];
                    for (var routeIndex = 0;
                         routeIndex < alternative.Routes.Length;
                         routeIndex++)
                    {
                        var route = alternative.Routes[routeIndex];
                        var id = string.Join(
                            ":",
                            set.RequirementSetId,
                            group.RequirementId,
                            alternativeIndex,
                            routeIndex);
                        Require(result.TryAdd(id, new AcquisitionRouteAmount(
                                alternative.Amount,
                                alternative.MatchKind,
                                alternative.QualifiedItemId,
                                route.RouteKind,
                                route.SourceId)),
                            "Acquisition lowering contains duplicate route occurrences.");
                    }
                }
            }
        }
        Require(result.Count == source.RouteOccurrenceCount,
            "Acquisition lowering route occurrence count drifted.");
        foreach (var route in source.Routes)
        {
            Require(result.TryGetValue(route.RouteOccurrenceId, out var amount) &&
                    amount.RouteKind == RouteKind(route) &&
                    amount.SourceId == SourceId(route) &&
                    amount.QualifiedItemId == QualifiedItemId(route),
                "Acquisition lowering route identity drifted.");
        }
        return result;
    }

    private static string RouteKind(
        AcquisitionRouteTargetDateResource route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind;

    private static string QualifiedItemId(
        AcquisitionRouteTargetDateResource route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .QualifiedItemId;

    private static string SourceId(
        AcquisitionRouteTargetDateResource route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute.SourceId;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}

internal sealed record AcquisitionRouteAmount(
    int Amount,
    string MatchKind,
    string QualifiedItemId,
    string RouteKind,
    string SourceId);
