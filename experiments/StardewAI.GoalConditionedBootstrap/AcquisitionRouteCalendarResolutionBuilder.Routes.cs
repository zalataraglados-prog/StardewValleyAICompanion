using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private const string ResolvedStatus =
        "resolved_static_source_window_target_date_pending";

    private static bool UsesMachineRoutes(
        AcquisitionRouteOptionLoweringReport lowering,
        CurrentCommunityCenterRequirementAuthority? communityCenterAuthority) =>
        lowering.RequirementSets
            .SelectMany(set => set.Groups)
            .SelectMany(group => group.Alternatives)
            .SelectMany(alternative => alternative.Routes)
            .Any(route => IsMachineRouteKind(route.RouteKind)) ||
        communityCenterAuthority?.AlternativesByKey.Values
            .SelectMany(alternative => alternative.AcceptedTargets)
            .SelectMany(target => target.Routes)
            .Any(route => IsMachineRouteKind(route.RouteKind)) == true;

    private static bool IsMachineRouteKind(string routeKind) => routeKind is
        "machine_output" or
        "native_machine_flavored_output" or
        "native_machine_item_query_output";

    private static AcquisitionRouteCalendarResolution[] BuildRoutes(
        AcquisitionRouteOptionLoweringReport lowering,
        CurrentCommunityCenterRequirementAuthority? communityCenterAuthority,
        IReadOnlyDictionary<string, MasterAnglerStageOneSpeciesWindow> windowSpecies,
        JsonElement locations,
        JsonElement crops,
        JsonElement shops,
        JsonElement machines,
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
                    machines,
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
                            machines,
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
        JsonElement machines,
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
                        machines,
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
        JsonElement machines,
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
            machines,
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
            resolution.ShopSource,
            resolution.MachineSource));
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
        JsonElement machines,
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

        if (IsMachineRouteKind(route.RouteKind))
        {
            return ResolveMachineWindows(
                qualifiedItemId,
                route,
                machines,
                deadlineTotalDayExclusive);
        }

        if (route.RouteKind is "native_wild_tree_chop_drop" or
            "native_wild_tree_tapper_output")
        {
            return ResolveWildTreeWindow(
                route,
                deadlineTotalDayExclusive);
        }

        if (route.RouteKind == "native_monster_drop_table")
        {
            return ResolveMonsterDropWindow(
                route,
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

    private static CalendarSourceResolution ResolveWildTreeWindow(
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        const string sourcePrefix = "wild_tree:";
        if (route.SourceAsset != "Data/WildTrees" ||
            !route.SourceId.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_wild_tree_source_invalid",
                "wild_tree_authoritative_source_identity_invalid");
        }

        var sourceParts = route.SourceId[sourcePrefix.Length..].Split(':');
        var rowName = route.RouteKind == "native_wild_tree_chop_drop"
            ? "ChopItems"
            : "TapItems";
        if (sourceParts.Length != 2 ||
            sourceParts.Any(string.IsNullOrWhiteSpace) ||
            route.SourcePath != "payload." + sourceParts[0] +
                "." + rowName + "[" + sourceParts[1] + "].ItemId")
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_wild_tree_source_invalid",
                "wild_tree_authoritative_source_path_mismatch");
        }
        Require(deadlineTotalDayExclusive > 0,
            "Wild-tree calendar deadline must be positive.");
        var sourceKind = route.RouteKind == "native_wild_tree_chop_drop"
            ? "wild_tree_chop_row"
            : "wild_tree_tapper_row";
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_" + sourceKind + "_calendar_invariant",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = sourceKind,
                    SourceKey = route.SourceId,
                    RuleId = sourceParts[1],
                    FirstTotalDay = 0,
                    LastTotalDay = deadlineTotalDayExclusive - 1,
                    TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
                    WeatherModes =
                        NativeCalendarConstraintNormalizer.AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = true
                }
            },
            Array.Empty<string>());
    }

    private static CalendarSourceResolution ResolveMonsterDropWindow(
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        const string sourcePrefix = "monster:";
        if (route.SourceAsset != "Data/Monsters" ||
            !route.SourceId.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_monster_source_invalid",
                "monster_authoritative_source_identity_invalid");
        }

        var monsterName = route.SourceId[sourcePrefix.Length..];
        var sourcePathPrefix = "payload." + monsterName + "[6:drop_pair:";
        if (string.IsNullOrWhiteSpace(monsterName) ||
            !route.SourcePath.StartsWith(
                sourcePathPrefix,
                StringComparison.Ordinal) ||
            !route.SourcePath.EndsWith(']') ||
            !int.TryParse(
                route.SourcePath[sourcePathPrefix.Length..^1],
                out var dropPairIndex) ||
            dropPairIndex < 0)
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_monster_source_invalid",
                "monster_authoritative_source_path_mismatch");
        }
        Require(deadlineTotalDayExclusive > 0,
            "Monster-drop calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_monster_drop_row_calendar_invariant",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = "monster_drop_row",
                    SourceKey = route.SourceId,
                    RuleId = dropPairIndex.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    FirstTotalDay = 0,
                    LastTotalDay = deadlineTotalDayExclusive - 1,
                    TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
                    WeatherModes =
                        NativeCalendarConstraintNormalizer.AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = true
                }
            },
            Array.Empty<string>());
    }

    private static CalendarSourceResolution BlockLiveSourceCalendar(
        string status,
        string reason) => new(
            status,
            string.Empty,
            Array.Empty<AuthoritativeCalendarSourceWindow>(),
            new[] { reason });

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

    internal sealed record CalendarSourceResolution(
        string Status,
        string EvidenceClass,
        AuthoritativeCalendarSourceWindow[] Windows,
        string[] BlockingReasons,
        AcquisitionCropSourceEvidence? CropSource = null,
        AcquisitionShopSourceEvidence? ShopSource = null,
        AcquisitionMachineSourceEvidence? MachineSource = null);
}
