namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static AcquisitionRouteCalendarResolution[] BuildRoutes(
        AcquisitionRouteOptionLoweringReport lowering,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> windowSpecies)
    {
        var result = new List<AcquisitionRouteCalendarResolution>();
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
                        var windows = ResolveWindows(
                            alternative.QualifiedItemId,
                            route,
                            windowSpecies);
                        var supported = SupportedRouteKinds.Contains(route.RouteKind);
                        var resolved = windows.Length > 0;
                        var occurrenceId = string.Join(
                            ":",
                            set.RequirementSetId,
                            group.RequirementId,
                            alternativeIndex,
                            routeIndex);
                        result.Add(new AcquisitionRouteCalendarResolution(
                            occurrenceId,
                            set.RequirementSetId,
                            group.RequirementId,
                            alternativeIndex,
                            routeIndex,
                            alternative.ItemId,
                            alternative.QualifiedItemId,
                            route.RouteKind,
                            route.SourceId,
                            route.SourceAsset,
                            route.SourcePath,
                            resolved
                                ? "resolved_static_source_window_target_date_pending"
                                : supported
                                    ? "blocked_authoritative_fish_window_not_found"
                                    : "blocked_pending_route_kind_calendar_parser",
                            resolved ? EvidenceClass(route.RouteKind) : string.Empty,
                            windows,
                            resolved
                                ? Array.Empty<string>()
                                : new[]
                                {
                                    supported
                                        ? "exact_route_source_has_no_matching_master_angler_window"
                                        : "route_kind_calendar_parser_not_implemented"
                                }));
                    }
                }
            }
        }
        return result.ToArray();
    }

    private static MasterAnglerStageOneSourceWindow[] ResolveWindows(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> speciesById)
    {
        if (!SupportedRouteKinds.Contains(route.RouteKind) ||
            !speciesById.TryGetValue(qualifiedItemId, out var species))
        {
            return Array.Empty<MasterAnglerStageOneSourceWindow>();
        }

        var expectedSourceKind = route.RouteKind switch
        {
            "native_crab_pot_output" => "crab_pot",
            "native_location_fish_spawn" => "location_rule",
            "native_mine_fishing_override" => "mine_override",
            _ => string.Empty
        };
        var sourceKey = route.RouteKind == "native_location_fish_spawn"
            ? NormalizeLocationSourceKey(route.SourceId)
            : string.Empty;
        return species.Windows
            .Where(window => window.SourceKind == expectedSourceKind &&
                (sourceKey.Length == 0 || window.SourceKey == sourceKey))
            .OrderBy(window => window.LastTotalDay)
            .ThenBy(window => window.FirstTotalDay)
            .ThenBy(window => window.SourceKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeLocationSourceKey(string sourceId) =>
        sourceId.StartsWith("location_fish:", StringComparison.Ordinal)
            ? sourceId["location_fish:".Length..]
            : sourceId;

    private static string EvidenceClass(string routeKind) => routeKind switch
    {
        "native_crab_pot_output" => "master_angler_crab_pot_window",
        "native_location_fish_spawn" => "master_angler_location_rule_window",
        "native_mine_fishing_override" => "master_angler_mine_override_window",
        _ => string.Empty
    };
}
