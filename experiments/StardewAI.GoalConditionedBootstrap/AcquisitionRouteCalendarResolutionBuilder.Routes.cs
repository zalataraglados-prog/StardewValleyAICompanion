using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private const string ResolvedStatus =
        "resolved_static_source_window_target_date_pending";

    private static AcquisitionRouteCalendarResolution[] BuildRoutes(
        AcquisitionRouteOptionLoweringReport lowering,
        CurrentCommunityCenterRequirementAuthority? communityCenterAuthority,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> windowSpecies,
        JsonElement locations,
        JsonElement crops,
        JsonElement shops,
        JsonElement accessConstraints,
        int deadlineTotalDayExclusive)
    {
        var result = new List<AcquisitionRouteCalendarResolution>();
        foreach (var set in lowering.RequirementSets)
        {
            if (set.RequirementSetId ==
                    CurrentCommunityCenterRequirementAuthorityBuilder.RequirementSetId &&
                communityCenterAuthority is not null)
            {
                AddCurrentCommunityCenterRoutes(
                    result,
                    communityCenterAuthority,
                    windowSpecies,
                    locations,
                    crops,
                    shops,
                    accessConstraints,
                    deadlineTotalDayExclusive);
                continue;
            }
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
                        AddRoute(
                            result,
                            set.RequirementSetId,
                            group.RequirementId,
                            alternativeIndex,
                            routeIndex,
                            alternative.ItemId,
                            alternative.QualifiedItemId,
                            alternative.MatchKind,
                            alternative.Amount,
                            alternative.MinimumQuality,
                            route,
                            windowSpecies,
                            locations,
                            crops,
                            shops,
                            accessConstraints,
                            deadlineTotalDayExclusive);
                    }
                }
            }
        }
        return result.ToArray();
    }

    private static void AddCurrentCommunityCenterRoutes(
        ICollection<AcquisitionRouteCalendarResolution> result,
        CurrentCommunityCenterRequirementAuthority authority,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> windowSpecies,
        JsonElement locations,
        JsonElement crops,
        JsonElement shops,
        JsonElement accessConstraints,
        int deadlineTotalDayExclusive)
    {
        foreach (var group in authority.Inventory.Groups)
        {
            for (var alternativeIndex = 0;
                 alternativeIndex < group.Alternatives.Length;
                 alternativeIndex++)
            {
                var alternative = group.Alternatives[alternativeIndex];
                var key = CurrentCommunityCenterRequirementAuthorityBuilder
                    .AlternativeKey(group.RequirementId, alternativeIndex);
                if (!authority.AlternativesByKey.TryGetValue(key, out var accepted))
                {
                    throw new InvalidDataException(
                        "A current Community Center route alternative has no concrete target authority: " +
                        key);
                }

                var targetRoutes = accepted.AcceptedTargets
                    .SelectMany(target => target.Routes.Select(route => new
                    {
                        Target = target,
                        Route = route
                    }))
                    .GroupBy(value => string.Join(
                            "\u001f",
                            value.Target.ItemId,
                            value.Target.QualifiedItemId,
                            value.Route.RouteKind,
                            value.Route.SourceId,
                            value.Route.SourceAsset,
                            value.Route.SourcePath),
                        StringComparer.Ordinal)
                    .Select(value => value.First())
                    .OrderBy(value => value.Target.QualifiedItemId,
                        StringComparer.Ordinal)
                    .ThenBy(value => value.Target.ItemId, StringComparer.Ordinal)
                    .ThenBy(value => value.Route.RouteKind, StringComparer.Ordinal)
                    .ThenBy(value => value.Route.SourceId, StringComparer.Ordinal)
                    .ThenBy(value => value.Route.SourceAsset, StringComparer.Ordinal)
                    .ThenBy(value => value.Route.SourcePath, StringComparer.Ordinal)
                    .ToArray();
                for (var routeIndex = 0;
                     routeIndex < targetRoutes.Length;
                     routeIndex++)
                {
                    var targetRoute = targetRoutes[routeIndex];
                    AddRoute(
                        result,
                        authority.Inventory.RequirementSetId,
                        group.RequirementId,
                        alternativeIndex,
                        routeIndex,
                        targetRoute.Target.ItemId,
                        targetRoute.Target.QualifiedItemId,
                        alternative.MatchKind,
                        alternative.Amount,
                        alternative.MinimumQuality,
                        targetRoute.Route,
                        windowSpecies,
                        locations,
                        crops,
                        shops,
                        accessConstraints,
                        deadlineTotalDayExclusive);
                }
            }
        }
    }

    private static void AddRoute(
        ICollection<AcquisitionRouteCalendarResolution> result,
        string requirementSetId,
        string requirementId,
        int alternativeIndex,
        int routeIndex,
        string itemId,
        string qualifiedItemId,
        string matchKind,
        int amount,
        int minimumQuality,
        AcquisitionRequirementRouteLowering route,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> windowSpecies,
        JsonElement locations,
        JsonElement crops,
        JsonElement shops,
        JsonElement accessConstraints,
        int deadlineTotalDayExclusive)
    {
        var resolution = ResolveSource(
            qualifiedItemId,
            route,
            windowSpecies,
            locations,
            crops,
            shops,
            accessConstraints,
            deadlineTotalDayExclusive);
        var occurrenceId = string.Join(
            ":",
            requirementSetId,
            requirementId,
            alternativeIndex,
            routeIndex);
        result.Add(new AcquisitionRouteCalendarResolution(
            occurrenceId,
            requirementSetId,
            requirementId,
            alternativeIndex,
            routeIndex,
            itemId,
            qualifiedItemId,
            matchKind,
            amount,
            minimumQuality,
            route.RouteKind,
            route.UncertaintyMode,
            route.SourceId,
            route.SourceAsset,
            route.SourcePath,
            resolution.Status,
            resolution.EvidenceClass,
            resolution.Windows,
            resolution.BlockingReasons,
            resolution.CropSource,
            resolution.ShopSource));
    }

    private static int ExpectedRouteCount(
        AcquisitionRouteOptionLoweringReport lowering,
        CurrentCommunityCenterRequirementAuthority? communityCenterAuthority)
    {
        var staticCount = lowering.RequirementSets
            .Where(set => communityCenterAuthority is null ||
                set.RequirementSetId !=
                    CurrentCommunityCenterRequirementAuthorityBuilder.RequirementSetId)
            .SelectMany(set => set.Groups)
            .SelectMany(group => group.Alternatives)
            .Sum(alternative => alternative.Routes.Length);
        if (communityCenterAuthority is null)
            return staticCount;
        return staticCount + communityCenterAuthority.AlternativesByKey.Values.Sum(
            alternative => alternative.AcceptedTargets
                .SelectMany(target => target.Routes.Select(route => string.Join(
                    "\u001f",
                    target.ItemId,
                    target.QualifiedItemId,
                    route.RouteKind,
                    route.SourceId,
                    route.SourceAsset,
                    route.SourcePath)))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static CalendarSourceResolution ResolveSource(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> speciesById,
        JsonElement locations,
        JsonElement crops,
        JsonElement shops,
        JsonElement accessConstraints,
        int deadlineTotalDayExclusive)
    {
        if (route.RouteKind == "harvests_as")
        {
            return ResolveCropWindows(
                qualifiedItemId,
                route,
                crops,
                deadlineTotalDayExclusive);
        }

        if (route.RouteKind == "sells")
        {
            return ResolveShopWindows(
                qualifiedItemId,
                route,
                shops,
                accessConstraints,
                deadlineTotalDayExclusive);
        }

        if (route.RouteKind is "native_crab_pot_output" or
            "native_location_fish_spawn" or
            "native_mine_fishing_override")
        {
            var fishResolution = ResolveFishWindows(
                qualifiedItemId,
                route,
                speciesById);
            if (fishResolution.Status == ResolvedStatus ||
                route.RouteKind != "native_location_fish_spawn" ||
                speciesById.ContainsKey(qualifiedItemId))
            {
                return fishResolution;
            }
        }

        if (route.RouteKind is "native_location_artifact_spot" or
            "native_location_fish_spawn" or
            "native_location_forage_spawn")
        {
            return ResolveLocationWindows(
                route,
                locations,
                deadlineTotalDayExclusive);
        }

        return new CalendarSourceResolution(
            "blocked_pending_route_kind_calendar_parser",
            string.Empty,
            Array.Empty<AuthoritativeCalendarSourceWindow>(),
            new[] { "route_kind_calendar_parser_not_implemented" });
    }

    private static CalendarSourceResolution ResolveFishWindows(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> speciesById)
    {
        if (!speciesById.TryGetValue(qualifiedItemId, out var species))
        {
            return new CalendarSourceResolution(
                "blocked_authoritative_fish_window_not_found",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                new[] { "target_is_not_in_master_angler_window_index" });
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
        var windows = species.Windows
            .Where(window => window.SourceKind == expectedSourceKind &&
                (sourceKey.Length == 0 || window.SourceKey == sourceKey))
            .OrderBy(window => window.LastTotalDay)
            .ThenBy(window => window.FirstTotalDay)
            .ThenBy(window => window.SourceKey, StringComparer.Ordinal)
            .ToArray();
        return windows.Length > 0
            ? new CalendarSourceResolution(
                ResolvedStatus,
                FishEvidenceClass(route.RouteKind),
                windows,
                Array.Empty<string>())
            : new CalendarSourceResolution(
                "blocked_authoritative_fish_window_not_found",
                string.Empty,
                windows,
                new[] { "exact_route_source_has_no_matching_master_angler_window" });
    }

    private static string NormalizeLocationSourceKey(string sourceId) =>
        sourceId.StartsWith("location_fish:", StringComparison.Ordinal)
            ? sourceId["location_fish:".Length..]
            : sourceId;

    private static string FishEvidenceClass(string routeKind) => routeKind switch
    {
        "native_crab_pot_output" => "master_angler_crab_pot_window",
        "native_location_fish_spawn" => "master_angler_location_rule_window",
        "native_mine_fishing_override" => "master_angler_mine_override_window",
        _ => string.Empty
    };

    private sealed record CalendarSourceResolution(
        string Status,
        string EvidenceClass,
        AuthoritativeCalendarSourceWindow[] Windows,
        string[] BlockingReasons,
        AcquisitionCropSourceEvidence? CropSource = null,
        AcquisitionShopSourceEvidence? ShopSource = null);
}
