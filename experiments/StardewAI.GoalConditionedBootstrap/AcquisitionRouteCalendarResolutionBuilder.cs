using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static readonly HashSet<string> SupportedRouteKinds = new(StringComparer.Ordinal)
    {
        "harvests_as",
        "machine_output",
        "sells",
        "native_crab_pot_output",
        "native_location_artifact_spot",
        "native_location_fish_spawn",
        "native_location_forage_spawn",
        "native_mine_fishing_override",
        "native_machine_flavored_output",
        "native_machine_item_query_output"
    };

    public static AcquisitionRouteCalendarResolutionReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath) => BuildCore(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            null,
            null);

    public static AcquisitionRouteCalendarResolutionReport BuildCurrent(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string snapshotPath) => BuildCore(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            CurrentCommunityCenterDenominatorBuilder.Build(
                inventoryPath,
                snapshotPath),
            snapshotPath);

    internal static AcquisitionRouteCalendarResolutionReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string snapshotPath,
        CurrentCommunityCenterDenominatorReport denominator) => BuildCore(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            denominator,
            snapshotPath);

    private static AcquisitionRouteCalendarResolutionReport BuildCore(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        CurrentCommunityCenterDenominatorReport? communityCenterDenominator,
        string? snapshotPath)
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
        CurrentCommunityCenterRequirementAuthority? communityCenterAuthority = null;
        if (communityCenterDenominator is not null)
        {
            if (string.IsNullOrWhiteSpace(snapshotPath))
            {
                throw new InvalidDataException(
                    "A current Community Center calendar build requires its snapshot path.");
            }
            var snapshotFullPath = Path.GetFullPath(snapshotPath);
            CurrentCommunityCenterRequirementAuthorityBuilder.ValidateIdentity(
                communityCenterDenominator,
                inventory,
                inventoryFullPath,
                snapshotFullPath);
            communityCenterAuthority =
                CurrentCommunityCenterRequirementAuthorityBuilder.Build(
                    communityCenterDenominator,
                    lowering);
        }

        var locationEvidence = VerifyEvidence(
            inventory,
            "runtime_data_locations",
            "Runtime Data/Locations");
        var cropEvidence = VerifyEvidence(
            inventory,
            "runtime_data_crops",
            "Runtime Data/Crops");
        var cropGrowthEvidence = VerifyEvidence(
            inventory,
            "native_crop_growth_rule",
            "Native crop growth rule");
        var cropPlantingEvidence = VerifyEvidence(
            inventory,
            "native_crop_planting_rule",
            "Native crop planting rule");
        var shopEvidence = VerifyEvidence(
            inventory,
            "runtime_data_shops",
            "Runtime Data/Shops");
        var requiresMachineEvidence = UsesMachineRoutes(
            lowering,
            communityCenterAuthority);
        var machineEvidence = requiresMachineEvidence
            ? VerifyEvidence(
                inventory,
                "runtime_data_machines",
                "Runtime Data/Machines")
            : null;
        var machineSelectionEvidence = requiresMachineEvidence
            ? VerifyEvidence(
                inventory,
                "native_machine_output_selection_rule",
                "Native machine output selection rule")
            : null;
        var accessConstraintEvidence = VerifyEvidence(
            inventory,
            "access_constraint_index",
            "Access constraint index");
        var shopStockEvidence = VerifyEvidence(
            inventory,
            "native_shop_stock_rule",
            "Native shop stock rule");
        var shopOpenEvidence = VerifyEvidence(
            inventory,
            "native_shop_open_rule",
            "Native shop open rule");
        var shopPurchaseEvidence = VerifyEvidence(
            inventory,
            "native_shop_purchase_rule",
            "Native shop purchase rule");
        var gameStateQueryEvidence = VerifyEvidence(
            inventory,
            "native_game_state_query_rule",
            "Native game-state query rule");
        var locations = ReadPayloadEvidence(locationEvidence, "Runtime Data/Locations");
        var crops = ReadPayloadEvidence(cropEvidence, "Runtime Data/Crops");
        var shops = ReadPayloadEvidence(shopEvidence, "Runtime Data/Shops");
        var machines = machineEvidence is null
            ? default
            : ReadPayloadEvidence(machineEvidence, "Runtime Data/Machines");
        var accessConstraints = ReadRootEvidence(
            accessConstraintEvidence,
            "Access constraint index");

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
                catalog.LocationRuleSpawnChanceInputInventoryComplete &&
                catalog.NativeDenominatorCount == 72 &&
                catalog.Species.Length == 72 &&
                catalog.UnresolvedSpeciesIds.Length == 0 &&
                catalog.UnresolvedCalendarRuleIds.Length == 0 &&
                catalog.UnresolvedSpawnChanceInputRuleIds.Length == 0 &&
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
        var routes = BuildRoutes(
            lowering,
            communityCenterAuthority,
            windowSpecies,
            locations,
            crops,
            shops,
            machines,
            accessConstraints,
            windows.DeadlineTotalDayExclusive);
        var expectedRouteCount = ExpectedRouteCount(
            lowering,
            communityCenterAuthority);
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
            UsesCurrentCommunityCenterDenominator =
                communityCenterDenominator is not null,
            CommunityCenterBundleMode =
                communityCenterDenominator?.BundleMode ?? string.Empty,
            CommunityCenterDenominatorSha256 =
                communityCenterDenominator?.DenominatorSha256 ?? string.Empty,
            CommunityCenterSourceStateHash =
                communityCenterDenominator?.SourceStateHash ?? string.Empty,
            CommunityCenterSnapshotSha256 =
                communityCenterDenominator?.SnapshotSha256 ?? string.Empty,
            MasterAnglerWindowIndexSha256 =
                CurrentTeacherFrontierSupport.HashFile(windowFullPath),
            MasterAnglerOpportunityCatalogSha256 =
                CurrentTeacherFrontierSupport.HashFile(catalogFullPath),
            LocationDataSha256 = locationEvidence.Sha256,
            CropDataSha256 = cropEvidence.Sha256,
            ShopDataSha256 = shopEvidence.Sha256,
            MachineDataSha256 = machineEvidence?.Sha256 ?? string.Empty,
            NativeMachineOutputSelectionSourceSha256 =
                machineSelectionEvidence?.Sha256 ?? string.Empty,
            AccessConstraintIndexSha256 = accessConstraintEvidence.Sha256,
            NativeCropGrowthSourceSha256 = cropGrowthEvidence.Sha256,
            NativeCropPlantingSourceSha256 = cropPlantingEvidence.Sha256,
            NativeShopStockSourceSha256 = shopStockEvidence.Sha256,
            NativeShopOpenSourceSha256 = shopOpenEvidence.Sha256,
            NativeShopPurchaseSourceSha256 = shopPurchaseEvidence.Sha256,
            NativeGameStateQuerySourceSha256 = gameStateQueryEvidence.Sha256,
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

}
