using Microsoft.Xna.Framework;
using System.Text.Json;
using StardewValley;
using StardewValley.Constants;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string TreasureTotemNativeContract =
        "Object.performUseAction((O)TreasureTotem)->outdoors_guard->Object.treasureTotem->TreasureTotemsUsed++->rounded_distance_3_ring->placement_occupancy_front_bush_diggable_or_winter_grass_gate->objects.Add((O)590)";

    private static object ReadTreasureTotemContext(Farmer? player)
    {
        if (player?.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        var center = player.Tile;
        var candidateRows = ReadTreasureTotemRing(location, center);
        var spawnTiles = candidateRows
            .Where(row => row.spawn_expected)
            .Select(row => new { row.tile_x, row.tile_y })
            .ToArray();
        var spawnTilesJson = JsonSerializer.Serialize(spawnTiles);
        var existingSpotCount = location.objects.Pairs.Count(pair => pair.Value.QualifiedItemId == "(O)590");
        var totemsUsedBefore = Game1.netWorldState.Value.TreasureTotemsUsed;
        var rows = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                string.Equals(entry.item.QualifiedItemId, "(O)TreasureTotem", StringComparison.Ordinal))
            .Select(entry => new
            {
                inventory_slot_index = entry.slot,
                item_id = entry.item!.ItemId,
                qualified_item_id = entry.item.QualifiedItemId,
                display_name = entry.item.DisplayName,
                inventory_runtime_type = entry.item.GetType().FullName,
                stack_before = entry.item.Stack,
                stack_after = Math.Max(0, entry.item.Stack - 1),
                temporarily_invisible = ((StardewValley.Object)entry.item).isTemporarilyInvisible
            })
            .ToArray();
        var visibleItem = rows.Any(row => !row.temporarily_invisible && row.stack_before > 0);
        var nativeBaseGate = player.canMove && visibleItem && !Game1.eventUp && !Game1.isFestival() &&
            !Game1.fadeToBlack && !player.swimming.Value && !player.bathingClothes.Value &&
            !player.onBridge.Value && Game1.activeClickableMenu is null;
        var gateStatus = rows.Length == 0 ? "blocked_no_inventory_treasure_totem" :
            !nativeBaseGate ? "blocked_base_object_use_gate" :
            !location.IsOutdoors ? "blocked_location_not_outdoors" :
            spawnTiles.Length == 0 ? "blocked_no_spawnable_ring_tiles" :
            "ready";
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "treasure_totem.v1",
            location = location.NameOrUniqueName,
            center_x = (int)center.X,
            center_y = (int)center.Y,
            location.IsOutdoors,
            existingSpotCount,
            totemsUsedBefore,
            nativeBaseGate,
            candidateRows,
            rows
        }));

        return new
        {
            schema_version = "treasure_totem.v1",
            projection_status = "complete_current_native_treasure_totem_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            native_use_gate_status = gateStatus,
            location_is_outdoors = location.IsOutdoors,
            native_base_use_gate = new
            {
                can_move = player.canMove,
                visible_treasure_totem_available = visibleItem,
                event_up = Game1.eventUp,
                festival = Game1.isFestival(),
                fade_to_black = Game1.fadeToBlack,
                swimming = player.swimming.Value,
                bathing_clothes = player.bathingClothes.Value,
                on_bridge = player.onBridge.Value,
                active_menu_clear = Game1.activeClickableMenu is null,
                passed = nativeBaseGate
            },
            center_tile = new { tile_x = (int)center.X, tile_y = (int)center.Y },
            spawn_projection = new
            {
                ring_candidate_count = candidateRows.Length,
                expected_spawn_count = spawnTiles.Length,
                expected_spawn_tiles_json = spawnTilesJson,
                expected_spawn_tiles = spawnTiles,
                existing_artifact_spot_count_before = existingSpotCount,
                existing_artifact_spot_count_after = existingSpotCount + spawnTiles.Length,
                treasure_totems_used_before = totemsUsedBefore,
                treasure_totems_used_after = totemsUsedBefore + 1,
                candidate_rows = candidateRows
            },
            ring_contract = new
            {
                scan_radius = 4,
                rounded_radius = 3,
                artifact_spot_qualified_item_id = "(O)590",
                initial_sound = "treasure_totem",
                forest_exclusion_operand = "Object.Name",
                forest_exclusion_active_for_base_item = false,
                visual_sprite_randomness = "Game1.random_visual_only_not_spawn_membership"
            },
            native_contract = TreasureTotemNativeContract,
            rows
        };
    }

    private static TreasureTotemRingTileProjection[] ReadTreasureTotemRing(GameLocation location, Vector2 center)
    {
        const int scanRadius = 4;
        const int roundedRadius = 3;
        var rows = new List<TreasureTotemRingTileProjection>(16);
        for (var x = (int)center.X - scanRadius; x < center.X + scanRadius; x++)
        for (var y = (int)center.Y - scanRadius; y < center.Y + scanRadius; y++)
        {
            var roundedDistance = (int)Math.Round(Utility.distance(x, center.X, y, center.Y));
            if (roundedDistance != roundedRadius)
                continue;
            var tile = new Vector2(x, y);
            var canItemBePlaced = location.CanItemBePlacedHere(tile);
            var tileOccupied = location.IsTileOccupiedBy(tile);
            var alwaysFront = location.hasTileAt(x, y, "AlwaysFront");
            var front = location.hasTileAt(x, y, "Front");
            var behindBush = location.isBehindBush(tile);
            var diggable = location.doesTileHaveProperty(x, y, "Diggable", "Back") is not null;
            var winterGrass = location.GetSeason() == Season.Winter &&
                string.Equals(location.doesTileHaveProperty(x, y, "Type", "Back"), "Grass", StringComparison.Ordinal);
            var nativeForestExclusion = string.Equals("Treasure Totem", "Forest", StringComparison.Ordinal) &&
                x >= 93 && y <= 22;
            var spawnExpected = location.IsOutdoors && canItemBePlaced && !tileOccupied &&
                !alwaysFront && !front && !behindBush && (diggable || winterGrass) && !nativeForestExclusion;
            rows.Add(new TreasureTotemRingTileProjection(
                x, y, roundedDistance, canItemBePlaced, tileOccupied, alwaysFront, front,
                behindBush, diggable, winterGrass, nativeForestExclusion, spawnExpected));
        }
        return rows.ToArray();
    }

    private sealed record TreasureTotemRingTileProjection(
        int tile_x,
        int tile_y,
        int rounded_distance,
        bool can_item_be_placed_here,
        bool tile_occupied,
        bool has_always_front_tile,
        bool has_front_tile,
        bool behind_bush,
        bool diggable_back_property,
        bool winter_grass_back_property,
        bool native_forest_exclusion,
        bool spawn_expected);
}
