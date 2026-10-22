using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter
{
    private const int SecretWoodsEntryLogIndex = 602;
    private const int SecretWoodsEntryLogTileX = 1;
    private const int SecretWoodsEntryLogTileY = 6;
    private const int SecretWoodsRequiredAxeUpgradeLevel = 2;

    private static object ReadSecretWoodsAccess()
    {
        var farmTypeKey = Game1.GetFarmTypeKey();
        var forest = Game1.getLocationFromName("Forest");
        var woods = Game1.getLocationFromName("Woods");
        var entryWarps = forest?.warps
            .Where(warp =>
                string.Equals(warp.TargetName, "Woods", StringComparison.Ordinal) &&
                warp.X == -1 &&
                warp.Y is 6 or 7)
            .OrderBy(warp => warp.Y)
            .ToArray() ?? Array.Empty<Warp>();
        var matchingClumps = forest?.resourceClumps
            .Where(clump =>
                (int)clump.Tile.X == SecretWoodsEntryLogTileX &&
                (int)clump.Tile.Y == SecretWoodsEntryLogTileY)
            .ToArray() ?? Array.Empty<ResourceClump>();
        var entryLog = matchingClumps.Length == 1
            ? matchingClumps[0]
            : null;
        var exactEntryLog = entryLog is not null &&
            entryLog.GetType() == typeof(ResourceClump) &&
            entryLog.parentSheetIndex.Value == SecretWoodsEntryLogIndex &&
            entryLog.width.Value == 2 &&
            entryLog.height.Value == 2;
        var axe = Game1.player.Items
            .Select((item, index) => new { Tool = item as Axe, SlotIndex = index })
            .Where(row => row.Tool is not null)
            .OrderByDescending(row =>
                NativeToolPowerProjection.EffectiveUpgradeLevel(row.Tool!))
            .ThenBy(row => row.SlotIndex)
            .FirstOrDefault();
        var effectiveAxeLevel = axe is null
            ? (int?)null
            : NativeToolPowerProjection.EffectiveUpgradeLevel(axe.Tool!);
        var damagePerHit = axe is null
            ? (float?)null
            : NativeToolPowerProjection.ResourceClumpDamage(axe.Tool!);
        var expectedHits = entryLog is null || !damagePerHit.HasValue
            ? (int?)null
            : Math.Max(
                1,
                (int)Math.Ceiling(
                    Math.Max(0f, entryLog.health.Value) /
                    damagePerHit.Value));

        var status = string.Equals(farmTypeKey, "Forest", StringComparison.Ordinal)
            ? "forest_farm_alternative_available"
            : forest is null || woods is null
                ? "blocked_required_location_not_loaded"
                : entryWarps.Length != 2
                    ? "blocked_secret_woods_connector_identity_drifted"
                    : matchingClumps.Length == 0
                        ? "secret_woods_open"
                        : matchingClumps.Length != 1 || !exactEntryLog
                            ? "blocked_secret_woods_entry_log_identity_drifted"
                            : axe is null || effectiveAxeLevel < SecretWoodsRequiredAxeUpgradeLevel
                                ? "requires_steel_axe"
                                : "ready_to_clear_entry_log";

        return new
        {
            schema_version = "secret_woods_access.v1",
            projection_status = "complete_live_native_contract",
            status,
            active_farm_type_key = farmTypeKey,
            forest_farm_alternative_available = string.Equals(
                farmTypeKey,
                "Forest",
                StringComparison.Ordinal),
            forest_location_loaded = forest is not null,
            woods_location_loaded = woods is not null,
            connector_source_location = "Forest",
            connector_target_location = "Woods",
            connector_tiles = entryWarps.Select(warp => new
            {
                tile_x = warp.X,
                tile_y = warp.Y,
                target_x = warp.TargetX,
                target_y = warp.TargetY
            }).ToArray(),
            entry_log_present = entryLog is not null,
            entry_log_identity_exact = exactEntryLog,
            entry_log_tile_x = SecretWoodsEntryLogTileX,
            entry_log_tile_y = SecretWoodsEntryLogTileY,
            entry_log_parent_sheet_index = entryLog?.parentSheetIndex.Value,
            entry_log_width = entryLog?.width.Value,
            entry_log_height = entryLog?.height.Value,
            entry_log_health = entryLog?.health.Value,
            required_tool_kind = "axe",
            minimum_tool_upgrade_level = SecretWoodsRequiredAxeUpgradeLevel,
            selected_tool_slot_index = axe?.SlotIndex,
            selected_tool_qualified_item_id = axe?.Tool?.QualifiedItemId ?? string.Empty,
            selected_tool_upgrade_level = axe?.Tool?.UpgradeLevel,
            selected_tool_effective_upgrade_level = effectiveAxeLevel,
            damage_per_hit = damagePerHit,
            expected_hits_to_clear = expectedHits,
            native_contract = "Forest_ctor_ResourceClump_602_at_1_6_then_Axe_DoFunction_to_GameLocation_performToolAction_then_ResourceClump_destroy_then_Forest_to_Woods_warp",
            fresh_snapshot_after_each_primitive = true
        };
    }
}
