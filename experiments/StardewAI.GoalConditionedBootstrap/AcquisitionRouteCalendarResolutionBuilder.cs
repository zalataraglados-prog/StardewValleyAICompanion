using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static readonly HashSet<string> SupportedRouteKinds = new(StringComparer.Ordinal)
    {
        "native_crab_pot_output",
        "native_location_fish_spawn",
        "native_mine_fishing_override"
    };

    public static AcquisitionRouteCalendarResolutionReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath)
    {
        var inventoryFullPath = Path.GetFullPath(inventoryPath);
        var loweringFullPath = Path.GetFullPath(loweringPath);
        var windowFullPath = Path.GetFullPath(masterAnglerWindowIndexPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Authoritative requirement inventory");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringFullPath,
            "Acquisition route lowering");
        CurrentTeacherFrontierSupport.ValidateAuthority(
            inventoryFullPath,
            inventory,
            lowering,
            "Acquisition route calendar resolution");

        var windows = CurrentTeacherFrontierSupport.Read<MasterAnglerStageOneWindowIndex>(
            windowFullPath,
            "Master Angler window index");
        var masterSet = inventory.RequirementSets.SingleOrDefault(set =>
            set.RequirementSetId == "master_angler")
            ?? throw new InvalidDataException(
                "Authoritative inventory is missing the Master Angler requirement set.");
        var expectedFishIds = masterSet.Groups
            .Select(group => group.Alternatives.Single().QualifiedItemId)
            .ToHashSet(StringComparer.Ordinal);
        Require(windows.SchemaVersion == "master_angler_stage_one_window_index.v1" &&
                windows.Status == "complete_static_windows_dynamic_execution_pending" &&
                windows.StaticWindowCoverageComplete &&
                !windows.TrainingLabelEligible &&
                windows.NativeDenominatorCount == 72 &&
                windows.Species.Length == 72 &&
                windows.UnresolvedSpeciesIds.Length == 0 &&
                windows.GoalId == inventory.GoalId &&
                expectedFishIds.SetEquals(windows.Species.Select(species =>
                    species.QualifiedItemId)),
            "Master Angler window index does not bind the authoritative inventory.");

        var catalogFullPath = Path.GetFullPath(windows.CatalogPath);
        Require(File.Exists(catalogFullPath) &&
                string.Equals(
                    CurrentTeacherFrontierSupport.HashFile(catalogFullPath),
                    windows.CatalogSha256,
                    StringComparison.OrdinalIgnoreCase),
            "Master Angler opportunity catalog hash drifted.");
        var catalog = CurrentTeacherFrontierSupport.Read<
            MasterAnglerOpportunityCatalogReport>(
            catalogFullPath,
            "Master Angler opportunity catalog");
        Require(catalog.SchemaVersion == "master_angler_opportunity_catalog.v1" &&
                catalog.Status == "complete" &&
                catalog.SourceInventoryComplete &&
                catalog.StaticCalendarConstraintComplete &&
                catalog.NativeDenominatorCount == 72 &&
                catalog.Species.Length == 72 &&
                catalog.UnresolvedSpeciesIds.Length == 0 &&
                catalog.UnresolvedCalendarRuleIds.Length == 0 &&
                catalog.GoalId == inventory.GoalId &&
                string.Equals(catalog.GameVersion, inventory.GameVersion,
                    StringComparison.Ordinal) &&
                string.Equals(
                    catalog.RequirementInventorySha256,
                    CurrentTeacherFrontierSupport.HashFile(inventoryFullPath),
                    StringComparison.OrdinalIgnoreCase) &&
                expectedFishIds.SetEquals(catalog.Species.Select(species =>
                    species.QualifiedItemId)),
            "Master Angler opportunity catalog does not bind the authoritative inventory.");

        var recomputedWindows = MasterAnglerStageOneWindowIndexBuilder.Build(
            catalogFullPath,
            windows.DeadlineYear);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Require(string.Equals(
                JsonSerializer.Serialize(windows, jsonOptions),
                JsonSerializer.Serialize(recomputedWindows, jsonOptions),
                StringComparison.Ordinal),
            "Master Angler window index drifted from deterministic catalog compilation.");
        windows = recomputedWindows;

        var windowSpecies = windows.Species.ToDictionary(
            species => species.QualifiedItemId,
            StringComparer.Ordinal);
        var routes = BuildRoutes(lowering, windowSpecies);
        var expectedRouteCount = lowering.RequirementSets
            .SelectMany(set => set.Groups)
            .SelectMany(group => group.Alternatives)
            .Sum(alternative => alternative.Routes.Length);
        Require(routes.Length == expectedRouteCount &&
                routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() == routes.Length,
            "Calendar resolution route occurrence inventory drifted.");

        var resolvedCount = routes.Count(route => route.Status ==
            "resolved_static_source_window_target_date_pending");
        var blockedCount = routes.Length - resolvedCount;
        return new AcquisitionRouteCalendarResolutionReport
        {
            Status = blockedCount == 0
                ? "complete_static_sources_target_date_pending"
                : "partial_static_sources_explicitly_blocked",
            GoalId = inventory.GoalId,
            GameVersion = inventory.GameVersion,
            RequirementInventorySha256 =
                CurrentTeacherFrontierSupport.HashFile(inventoryFullPath),
            AcquisitionLoweringSha256 =
                CurrentTeacherFrontierSupport.HashFile(loweringFullPath),
            MasterAnglerWindowIndexSha256 =
                CurrentTeacherFrontierSupport.HashFile(windowFullPath),
            MasterAnglerOpportunityCatalogSha256 =
                CurrentTeacherFrontierSupport.HashFile(catalogFullPath),
            DeadlineTotalDayExclusive = windows.DeadlineTotalDayExclusive,
            RouteOccurrenceCount = routes.Length,
            ResolvedStaticSourceCount = resolvedCount,
            BlockedStaticSourceCount = blockedCount,
            RouteOccurrenceInventoryComplete = true,
            StaticCalendarSourceResolutionComplete = blockedCount == 0,
            TrainingLabelEligible = false,
            SupportedRouteKinds = SupportedRouteKinds.Order(StringComparer.Ordinal).ToArray(),
            UnresolvedRouteKinds = routes
                .Where(route => route.Status !=
                    "resolved_static_source_window_target_date_pending")
                .Select(route => route.RouteKind)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Routes = routes
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
