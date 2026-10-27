using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static partial class MasterAnglerWindowIntentValidator
{
    public const string HardwoodReserveStageParameter =
        "master_angler_hardwood_reserve_stage";
    public const string HardwoodReserveInventoryParameter =
        "master_angler_hardwood_inventory_quantity";
    public const string HardwoodReserveSourceKindParameter =
        "master_angler_hardwood_source_kind";
    public const string HardwoodReserveSourceLocationParameter =
        "master_angler_hardwood_source_location_id";
    public const string HardwoodReserveSourceXParameter =
        "master_angler_hardwood_source_tile_x";
    public const string HardwoodReserveSourceYParameter =
        "master_angler_hardwood_source_tile_y";
    public const string HardwoodReserveSourceQuantityParameter =
        "master_angler_hardwood_source_guaranteed_quantity";
    public const string HardwoodReserveRequiredAxeUpgradeParameter =
        "master_angler_hardwood_required_axe_upgrade_level";
    public const string HardwoodReserveParentSheetIndexParameter =
        "master_angler_hardwood_source_parent_sheet_index";
    public const string HardwoodReserveSourceStatusParameter =
        "master_angler_hardwood_source_status";

    internal const string HardwoodReserveValidationStatus =
        "authoritative_future_rain_window_hardwood_reserve";
    private const string HardwoodQualifiedItemId = "(O)709";

    private sealed record HardwoodReserveSource(
        string Stage,
        string Kind,
        string LocationId,
        int TileX,
        int TileY,
        int GuaranteedQuantity,
        int RequiredAxeUpgradeLevel,
        int ParentSheetIndex,
        string Status);

    private static bool TryBuildRainTotemHardwoodReserveIntent(
        SnapshotEnvelope snapshot,
        ParsedArtifact artifact,
        string indexPath,
        string indexHash,
        out SmallModelActionParameter[] parameters,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        parameters = [];
        if (!TryProjectRainTotemHardwoodReserve(
                snapshot,
                artifact,
                indexPath,
                indexHash,
                out validation,
                out rejectionReason))
        {
            return false;
        }

        parameters = CanonicalRainTotemHardwoodReserveParameters(validation);
        if (TryValidateRainTotemHardwoodReserveIntent(
                snapshot,
                parameters,
                out validation,
                out rejectionReason))
        {
            return true;
        }
        parameters = [];
        return false;
    }

    private static bool TryValidateRainTotemHardwoodReserveIntent(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        validation = new();
        rejectionReason = string.Empty;
        if (!TryRead(parameters, WeatherReserveStatusParameter, out var status) ||
            !TryReadInt(parameters, WeatherReserveTargetDayParameter, out var targetDay) ||
            !TryReadInt(parameters, WeatherReserveUseDayParameter, out var useDay) ||
            !TryRead(parameters, WeatherReserveTargetIdsParameter, out var targetIdsJson) ||
            !TryRead(parameters, WeatherReserveAffectedContextParameter, out var affectedContext) ||
            !TryRead(parameters, WeatherReserveWindowIndexPathParameter, out var indexPath) ||
            !TryRead(parameters, WeatherReserveWindowIndexSha256Parameter, out var expectedHash) ||
            !TryRead(parameters, WeatherReserveMaterialIdParameter, out var materialId) ||
            !TryReadInt(parameters, WeatherReserveRequiredQuantityParameter, out var requiredQuantity) ||
            !TryRead(parameters, HardwoodReserveStageParameter, out var stage) ||
            !TryReadInt(parameters, HardwoodReserveInventoryParameter, out var inventoryQuantity) ||
            !TryRead(parameters, HardwoodReserveSourceKindParameter, out var sourceKind) ||
            !TryRead(parameters, HardwoodReserveSourceLocationParameter, out var sourceLocation) ||
            !TryReadInt(parameters, HardwoodReserveSourceXParameter, out var sourceX) ||
            !TryReadInt(parameters, HardwoodReserveSourceYParameter, out var sourceY) ||
            !TryReadInt(parameters, HardwoodReserveSourceQuantityParameter, out var sourceQuantity) ||
            !TryReadInt(parameters, HardwoodReserveRequiredAxeUpgradeParameter, out var requiredAxeUpgrade) ||
            !TryReadInt(parameters, HardwoodReserveParentSheetIndexParameter, out var parentSheetIndex) ||
            !TryRead(parameters, HardwoodReserveSourceStatusParameter, out var sourceStatus) ||
            !TryRead(parameters, WeatherReserveReplanRequiredParameter, out var replanRequired) ||
            !TryReadQualifiedItemIds(targetIdsJson, out var targetIds))
        {
            rejectionReason =
                "master_angler_hardwood_reserve_contract_incomplete_or_duplicated";
            return false;
        }
        if (!string.Equals(status, HardwoodReserveValidationStatus, StringComparison.Ordinal) ||
            !string.Equals(materialId, HardwoodQualifiedItemId, StringComparison.Ordinal) ||
            requiredQuantity != 1 || inventoryQuantity < 0 || sourceQuantity < 0 ||
            requiredAxeUpgrade < 0 || !IsSha256(expectedHash) ||
            !string.Equals(replanRequired, "true", StringComparison.Ordinal))
        {
            rejectionReason = "master_angler_hardwood_reserve_contract_invalid";
            return false;
        }
        if (!TryLoadArtifact(indexPath, expectedHash, out var artifact, out rejectionReason) ||
            !TryProjectRainTotemHardwoodReserve(
                snapshot,
                artifact,
                Path.GetFullPath(indexPath),
                expectedHash,
                out var projected,
                out rejectionReason))
        {
            return false;
        }

        if (targetDay != projected.TargetTotalDay || useDay != projected.UseTotalDay ||
            !targetIds.SequenceEqual(projected.TargetQualifiedItemIds, StringComparer.Ordinal) ||
            !string.Equals(affectedContext, projected.AffectedLocationContextId, StringComparison.Ordinal) ||
            !string.Equals(stage, projected.ReserveStage, StringComparison.Ordinal) ||
            inventoryQuantity != projected.InventoryHardwoodQuantity ||
            !string.Equals(sourceKind, projected.HardwoodSourceKind, StringComparison.Ordinal) ||
            !string.Equals(sourceLocation, projected.HardwoodSourceLocationId, StringComparison.Ordinal) ||
            sourceX != projected.HardwoodSourceTileX || sourceY != projected.HardwoodSourceTileY ||
            sourceQuantity != projected.HardwoodSourceGuaranteedQuantity ||
            requiredAxeUpgrade != projected.HardwoodRequiredAxeUpgradeLevel ||
            parentSheetIndex != projected.HardwoodSourceParentSheetIndex ||
            !string.Equals(sourceStatus, projected.HardwoodSourceStatus, StringComparison.Ordinal))
        {
            rejectionReason = "master_angler_hardwood_reserve_projection_drifted";
            return false;
        }

        validation = projected;
        return true;
    }

    private static SmallModelActionParameter[] CanonicalRainTotemHardwoodReserveParameters(
        MasterAnglerRainTotemReserveIntentValidation value) =>
    [
        Parameter(WeatherReserveStatusParameter, HardwoodReserveValidationStatus),
        Parameter(WeatherReserveTargetDayParameter, value.TargetTotalDay.ToString(CultureInfo.InvariantCulture)),
        Parameter(WeatherReserveUseDayParameter, value.UseTotalDay.ToString(CultureInfo.InvariantCulture)),
        Parameter(WeatherReserveTargetIdsParameter, JsonSerializer.Serialize(value.TargetQualifiedItemIds)),
        Parameter(WeatherReserveAffectedContextParameter, value.AffectedLocationContextId),
        Parameter(WeatherReserveWindowIndexPathParameter, value.WindowIndexPath),
        Parameter(WeatherReserveWindowIndexSha256Parameter, value.WindowIndexSha256),
        Parameter(WeatherReserveMaterialIdParameter, HardwoodQualifiedItemId),
        Parameter(WeatherReserveRequiredQuantityParameter, "1"),
        Parameter(HardwoodReserveStageParameter, value.ReserveStage),
        Parameter(HardwoodReserveInventoryParameter, value.InventoryHardwoodQuantity.ToString(CultureInfo.InvariantCulture)),
        Parameter(HardwoodReserveSourceKindParameter, value.HardwoodSourceKind),
        Parameter(HardwoodReserveSourceLocationParameter, value.HardwoodSourceLocationId),
        Parameter(HardwoodReserveSourceXParameter, value.HardwoodSourceTileX.ToString(CultureInfo.InvariantCulture)),
        Parameter(HardwoodReserveSourceYParameter, value.HardwoodSourceTileY.ToString(CultureInfo.InvariantCulture)),
        Parameter(HardwoodReserveSourceQuantityParameter, value.HardwoodSourceGuaranteedQuantity.ToString(CultureInfo.InvariantCulture)),
        Parameter(HardwoodReserveRequiredAxeUpgradeParameter, value.HardwoodRequiredAxeUpgradeLevel.ToString(CultureInfo.InvariantCulture)),
        Parameter(HardwoodReserveParentSheetIndexParameter, value.HardwoodSourceParentSheetIndex.ToString(CultureInfo.InvariantCulture)),
        Parameter(HardwoodReserveSourceStatusParameter, value.HardwoodSourceStatus),
        Parameter(WeatherReserveReplanRequiredParameter, "true")
    ];

    private static bool TryProjectRainTotemHardwoodReserve(
        SnapshotEnvelope snapshot,
        ParsedArtifact artifact,
        string indexPath,
        string indexHash,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        validation = new();
        var inventoryQuantity = InventoryQuantity(snapshot, HardwoodQualifiedItemId);
        if (inventoryQuantity >= 1)
        {
            rejectionReason = "master_angler_hardwood_inventory_already_sufficient";
            return false;
        }
        if (!TryProjectRainTotemReserveWindow(
                snapshot,
                artifact,
                indexPath,
                indexHash,
                1,
                out var window,
                out rejectionReason))
        {
            return false;
        }

        var source = ProjectHardwoodReserveSource(snapshot);
        validation = new MasterAnglerRainTotemReserveIntentValidation
        {
            ReserveKind = "hardwood_reserve",
            ReserveStage = source.Stage,
            TargetTotalDay = window.TargetTotalDay,
            UseTotalDay = window.UseTotalDay,
            TargetQualifiedItemIds = window.TargetQualifiedItemIds,
            AffectedLocationContextId = window.AffectedLocationContextId,
            WindowIndexPath = window.WindowIndexPath,
            WindowIndexSha256 = window.WindowIndexSha256,
            InventoryHardwoodQuantity = inventoryQuantity,
            HardwoodSourceKind = source.Kind,
            HardwoodSourceLocationId = source.LocationId,
            HardwoodSourceTileX = source.TileX,
            HardwoodSourceTileY = source.TileY,
            HardwoodSourceGuaranteedQuantity = source.GuaranteedQuantity,
            HardwoodRequiredAxeUpgradeLevel = source.RequiredAxeUpgradeLevel,
            HardwoodSourceParentSheetIndex = source.ParentSheetIndex,
            HardwoodSourceStatus = source.Status
        };
        rejectionReason = string.Empty;
        return true;
    }

    private static HardwoodReserveSource ProjectHardwoodReserveSource(
        SnapshotEnvelope snapshot)
    {
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var farmRows = HardwoodFarmResourceClumps(snapshot).ToArray();
        if (string.Equals(currentLocation, "Farm", StringComparison.OrdinalIgnoreCase))
        {
            var readyFarm = farmRows.FirstOrDefault(row => row.Status == "ready");
            if (readyFarm is not null)
                return readyFarm with { Stage = "clear_loaded_farm_resource_clump" };
        }

        var currentTree = HardwoodCurrentLocationWildTrees(snapshot)
            .FirstOrDefault(row => row.Status == "ready");
        if (currentTree is not null)
            return currentTree with { Stage = "chop_loaded_wild_tree" };

        var remoteFarm = farmRows.FirstOrDefault(row => row.Status == "ready");
        if (remoteFarm is not null)
            return remoteFarm with { Stage = "route_to_farm_resource_clump" };

        var blockedUpgrade = farmRows.FirstOrDefault(row =>
            row.Status == "blocked_required_axe_upgrade_missing");
        if (blockedUpgrade is not null)
        {
            var upgrade = ReadStateFieldValue(snapshot, "player", "tool_upgrade");
            var lifecycleStage = upgrade.HasValue &&
                upgrade.Value.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(upgrade.Value, "projection_status"),
                    "complete_live_native_contract",
                    StringComparison.Ordinal)
                    ? ReadString(upgrade.Value, "lifecycle_stage")
                    : "blocked_tool_upgrade_projection_missing";
            return blockedUpgrade with { Stage = lifecycleStage };
        }

        return new HardwoodReserveSource(
            "blocked_no_exact_hardwood_source",
            "none",
            "not_applicable",
            -1,
            -1,
            0,
            0,
            -1,
            "blocked_no_exact_hardwood_source");
    }

    private static System.Collections.Generic.IEnumerable<HardwoodReserveSource>
        HardwoodFarmResourceClumps(SnapshotEnvelope snapshot)
    {
        var clumps = ReadStateFieldValue(snapshot, "farm", "resource_clumps");
        if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var clump in clumps.Value.EnumerateArray()
                     .Where(row => row.ValueKind == JsonValueKind.Object))
        {
            var parent = ReadInt(clump, "parent_sheet_index");
            var exactMinimum = parent switch { 600 => 2, 602 => 8, _ => 0 };
            var projectedMinimum = HardwoodMinimumFromJson(
                ReadString(clump, "clear_expected_output_items_json"),
                quantityProperty: "quantity");
            if (exactMinimum == 0 || projectedMinimum != exactMinimum ||
                !string.Equals(
                    ReadString(clump, "clear_output_projection_status"),
                    "exact_vanilla_conservative_minimum",
                    StringComparison.Ordinal))
            {
                continue;
            }
            yield return new HardwoodReserveSource(
                string.Empty,
                "farm_resource_clump",
                "Farm",
                ReadInt(clump, "tile_x"),
                ReadInt(clump, "tile_y"),
                exactMinimum,
                Math.Max(0, ReadInt(clump, "minimum_tool_upgrade_level")),
                parent,
                ReadString(clump, "clear_obstacle_executor_status"));
        }
    }

    private static System.Collections.Generic.IEnumerable<HardwoodReserveSource>
        HardwoodCurrentLocationWildTrees(SnapshotEnvelope snapshot)
    {
        var features = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
        if (!features.HasValue || features.Value.ValueKind != JsonValueKind.Array)
            yield break;
        var location = ReadStateFieldString(snapshot, "player", "location_id");
        foreach (var feature in features.Value.EnumerateArray()
                     .Where(row => row.ValueKind == JsonValueKind.Object &&
                         string.Equals(
                             ReadString(row, "runtime_type"),
                             "StardewValley.TerrainFeatures.Tree",
                             StringComparison.Ordinal)))
        {
            var minimum = HardwoodMinimumFromArray(
                feature,
                "tree_chop_guaranteed_minimum_outputs");
            if (minimum <= 0)
                continue;
            yield return new HardwoodReserveSource(
                string.Empty,
                "wild_tree",
                location,
                ReadInt(feature, "tile_x"),
                ReadInt(feature, "tile_y"),
                minimum,
                0,
                -1,
                ReadString(feature, "tree_chop_acquisition_status"));
        }
    }

    private static int HardwoodMinimumFromArray(JsonElement source, string property)
    {
        if (!source.TryGetProperty(property, out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }
        return rows.EnumerateArray()
            .Where(row => string.Equals(
                ReadStringEither(row, "qualified_item_id", "qualifiedItemId"),
                HardwoodQualifiedItemId,
                StringComparison.Ordinal))
            .Sum(row => Math.Max(
                0,
                ReadIntEither(row, "quantity_min", "quantityMin")));
    }

    private static int HardwoodMinimumFromJson(string json, string quantityProperty)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind != JsonValueKind.Array
                ? 0
                : document.RootElement.EnumerateArray()
                    .Where(row => string.Equals(
                        ReadStringEither(row, "qualified_item_id", "qualifiedItemId"),
                        HardwoodQualifiedItemId,
                        StringComparison.Ordinal))
                    .Sum(row => Math.Max(
                        0,
                        ReadIntEither(row, quantityProperty, "Quantity")));
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string ReadStringEither(JsonElement source, string first, string second) =>
        ReadString(source, first) is { Length: > 0 } value
            ? value
            : ReadString(source, second);

    private static int ReadIntEither(JsonElement source, string first, string second)
    {
        var value = ReadInt(source, first);
        return value != 0 ? value : ReadInt(source, second);
    }
}
