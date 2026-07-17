using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewAI.Contracts.State;
using System.Reflection;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter : ReadAdapterBase
{
    private static readonly FieldInfo? BreakableContainerHealthField = typeof(BreakableContainer)
        .GetField("health", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
    private static readonly FieldInfo? TreasureRoomField = typeof(MineShaft)
        .GetField("netIsTreasureRoom", BindingFlags.Instance | BindingFlags.NonPublic);

    private string? cachedCollisionSignature;
    private object? cachedCollisionContext;

    public override string Domain => "mining";
    public override int Priority => 55;

    public override StateAdapterResult Collect(long tick)
    {
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine)
        {
            var unavailable = new[]
            {
                "mining.current_mine",
                "mining.tiles",
                "mining.objects",
                "mining.resource_clumps",
                "mining.monsters",
                "mining.monster_drop_catalogs",
                "mining.debris",
                "mining.floor_objectives",
                "mining.player_resources"
            };
            return Section("mining", unavailable.ToDictionary(
                field => field.Split('.')[1],
                field => (object)Unavailable("not_loaded_mineshaft", "Game1.currentLocation is MineShaft", tick, "mining_read_adapter")), unavailable);
        }

        var fields = new Dictionary<string, object>
        {
            ["current_mine"] = Field(ReadCurrentMine(mine), "MineShaft.mineLevel/getMineArea/GetAdditionalDifficulty and flags", tick, "mining_read_adapter"),
            ["tiles"] = Field(ReadTiles(mine), "loaded GameLocation.map plus side-effect-free GameLocation.IsTileBlockedBy reads", tick, "mining_read_adapter"),
            ["objects"] = Field(ReadObjects(mine, Game1.player), "MineShaft.objects, Object.IsBreakableStone/MinutesUntilReady, and BreakableContainer live fields", tick, "mining_read_adapter"),
            ["resource_clumps"] = Field(ReadResourceClumps(mine, Game1.player), "MineShaft.resourceClumps live type, footprint, health, and decompiled tool gates", tick, "mining_read_adapter"),
            ["monsters"] = Field(ReadMonsters(mine), "MineShaft.characters filtered to Monster", tick, "mining_read_adapter"),
            ["monster_drop_catalogs"] = Field(MiningMonsterDropResolver.ReadSharedCatalogs(Game1.player), "shared decompile-derived monster drop identity catalogs", tick, "mining_read_adapter"),
            ["debris"] = Field(ReadDebris(mine), "MineShaft.debris live item and chunk fields", tick, "mining_read_adapter"),
            ["floor_objectives"] = Field(ReadFloorObjectives(mine), "MineShaft live flags, ladder rule, and deterministic per-stone preview inputs", tick, "mining_read_adapter"),
            ["player_resources"] = Field(ReadPlayerResources(Game1.player), "Game1.player resources and inventory", tick, "mining_read_adapter"),
            ["completeness"] = Field(new
            {
                status = "complete",
                source = "live_loaded_mineshaft_only",
                unavailable_reasons = Array.Empty<string>(),
                read_only_methods = new[] { "GameLocation.IsTileBlockedBy", "Object.IsBreakableStone", "Utility.CreateDaySaveRandom", "Utility.CreateRandom" },
                forbidden_calls = new[] { "MineShaft.findLadder", "MineShaft.loadLevel", "MineShaft.createLadderDown", "MineShaft.checkStoneForItems", "Monster.getExtraDropItems", "MineShaft.getTreasureRoomItem", "Trinket.TrySpawnTrinket", "monster_ai_update" }
            }, "MiningReadAdapter static live reads", tick, "mining_read_adapter")
        };

        return Section("mining", fields, Array.Empty<string>(), "complete");
    }

    private static object ReadCurrentMine(MineShaft mine)
    {
        var level = mine.mineLevel;
        var area = mine.getMineArea();
        return new
        {
            location_id = mine.NameOrUniqueName,
            mine_level = level,
            mine_area = area,
            mine_kind = area == 121 ? "skull_cavern" : area == 77377 ? "quarry_mine" : "ordinary_mines",
            generated_identity = mine.NameOrUniqueName + ":" + level,
            is_loaded_current_location = Game1.currentLocation == mine,
            is_skull_cavern = area == 121,
            is_quarry_mine = area == 77377 || mine.isQuarryArea,
            is_dangerous = mine.GetAdditionalDifficulty() > 0,
            additional_difficulty = mine.GetAdditionalDifficulty(),
            is_slime_area = mine.isSlimeArea,
            is_dino_area = mine.isDinoArea,
            is_monster_area = mine.isMonsterArea,
            source = "MineShaft.mineLevel/getMineArea/GetAdditionalDifficulty/is*Area"
        };
    }

    private object ReadTiles(MineShaft mine)
    {
        var loadedMap = mine.map;
        var buildings = loadedMap?.GetLayer("Buildings");
        return new
        {
            player_tile = new { tile_x = Game1.player.TilePoint.X, tile_y = Game1.player.TilePoint.Y },
            map = loadedMap is null || loadedMap.Layers.Count == 0 ? null : new { width = loadedMap.Layers[0].LayerWidth, height = loadedMap.Layers[0].LayerHeight, status = "loaded_field_only" },
            exits = MineExitTiles(buildings, mine),
            entries = ActionTiles(buildings, "Mine"),
            elevators = ActionTiles(buildings, "MineElevator"),
            golden_scythe_altars = ActionTiles(buildings, "GoldenScythe"),
            ladders = IndexedTiles(buildings, 173, "native_mineshaft_ladder_tile"),
            shafts = ShaftTiles(buildings, mine, Game1.player),
            tile_beneath_ladder = Tile(mine.tileBeneathLadder),
            tile_beneath_elevator = Tile(mine.tileBeneathElevator),
            collision_context = CollisionContext(mine, loadedMap),
            action_tile_status = loadedMap is null ? "unavailable_loaded_map_field_null" : "complete_exact_action_tokens"
        };
    }

}
