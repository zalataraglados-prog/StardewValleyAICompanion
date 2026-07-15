using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Tools;
using StardewAI.Contracts.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class MiningReadAdapter : ReadAdapterBase
{
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
                "mining.monsters",
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
            ["tiles"] = Partial(ReadTiles(mine), "GameLocation.map loaded field, MineShaft tileBeneathLadder/tileBeneathElevator", tick, "map_collision_passability_unavailable"),
            ["objects"] = Partial(ReadObjects(mine), "MineShaft.objects live dictionary; classification intentionally unavailable", tick, "object_classification_incomplete"),
            ["monsters"] = Field(ReadMonsters(mine), "MineShaft.characters filtered to Monster", tick, "mining_read_adapter"),
            ["floor_objectives"] = Partial(ReadFloorObjectives(mine), "MineShaft flags and mustKillAllMonstersToAdvance", tick, "floor_constraints_incomplete"),
            ["player_resources"] = Field(ReadPlayerResources(Game1.player), "Game1.player resources and inventory", tick, "mining_read_adapter"),
            ["completeness"] = Field(new
            {
                status = "incomplete",
                source = "live_loaded_mineshaft_only",
                unavailable_reasons = new[] { "map_collision_passability_unavailable", "object_classification_incomplete", "floor_constraints_incomplete" },
                forbidden_calls = new[] { "MineShaft.findLadder", "MineShaft.loadLevel", "MineShaft.createLadderDown", "MineShaft.checkStoneForItems", "monster_ai_update" }
            }, "MiningReadAdapter static live reads", tick, "mining_read_adapter")
        };

        return Section("mining", fields, new[]
        {
            "mining.tiles.value.collision_context",
            "mining.objects.value[*].is_ore_or_resource_node",
            "mining.objects.value[*].is_container",
            "mining.objects.value[*].health_or_hits_remaining",
            "mining.floor_objectives.value.ladder_creation_rule",
            "mining.floor_objectives.value.water_or_bridge_constraints",
            "mining.floor_objectives.value.ladder_probability_preview"
        }, "partial");
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

    private static object ReadTiles(MineShaft mine)
    {
        var loadedMap = mine.map;
        var buildings = loadedMap?.GetLayer("Buildings");
        return new
        {
            player_tile = new { tile_x = Game1.player.TilePoint.X, tile_y = Game1.player.TilePoint.Y },
            map = loadedMap is null || loadedMap.Layers.Count == 0 ? null : new { width = loadedMap.Layers[0].LayerWidth, height = loadedMap.Layers[0].LayerHeight, status = "loaded_field_only" },
            exits = ActionTiles(buildings, "Exit"),
            entries = ActionTiles(buildings, "Mine"),
            elevators = ActionTiles(buildings, "MineElevator"),
            ladders = ActionTiles(buildings, "Ladder").Concat(ActionTiles(buildings, "MineLadder")).ToArray(),
            shafts = ActionTiles(buildings, "Shaft").Concat(ActionTiles(buildings, "MineShaft")).ToArray(),
            tile_beneath_ladder = Tile(mine.tileBeneathLadder),
            tile_beneath_elevator = Tile(mine.tileBeneathElevator),
            collision_context = new { source = "GameLocation.isTilePassable/isCollidingPosition not invoked", status = "unavailable", reason = "no side-effect-free complete passability projection in this slice" },
            action_tile_status = loadedMap is null ? "unavailable_loaded_map_field_null" : "partial_exact_action_tokens_only"
        };
    }

    private static object[] ReadObjects(MineShaft mine)
    {
        return mine.objects.Pairs.Select(pair =>
        {
            var obj = pair.Value;
            var qualifiedId = obj.QualifiedItemId;
            return new
            {
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                item_id = obj.ItemId,
                qualified_item_id = qualifiedId,
                display_name = obj.DisplayName,
                name = obj.Name,
                runtime_type = obj.GetType().FullName,
                big_craftable = obj.bigCraftable.Value,
                category = obj.Category,
                fragility = obj.Fragility,
                can_be_grabbed = obj.CanBeGrabbed,
                minutes_until_ready = obj.MinutesUntilReady,
                is_breakable_stone = obj.IsBreakableStone(),
                is_ore_or_resource_node = new { status = "unavailable", reason = "no complete decompile-backed mine resource ID table in this slice" },
                is_container = new { status = "unavailable", reason = "crate/barrel/container classification not complete from live object fields alone" },
                is_placed_staircase = qualifiedId == "(O)71",
                tool_requirement = obj.IsBreakableStone() ? "pickaxe" : qualifiedId == "(O)71" ? "none_staircase" : "unknown_or_interact",
                health_or_hits_remaining = new { status = "unavailable", reason = "MinutesUntilReady is not stone health/hits remaining" },
                source = "StardewValley.Object live fields; no break/drop methods called"
            };
        }).ToArray();
    }

    private static object[] ReadMonsters(MineShaft mine)
    {
        return mine.characters.OfType<Monster>().Select(monster =>
        {
            var box = monster.GetBoundingBox();
            return new
            {
                runtime_type = monster.GetType().FullName,
                name = monster.Name,
                tile_x = monster.TilePoint.X,
                tile_y = monster.TilePoint.Y,
                pixel_x = monster.Position.X,
                pixel_y = monster.Position.Y,
                bounding_box = new { x = box.X, y = box.Y, width = box.Width, height = box.Height },
                health = monster.Health,
                max_health = monster.MaxHealth,
                damage_to_farmer = monster.DamageToFarmer,
                is_monster = monster.IsMonster,
                slipperiness = monster.Slipperiness,
                contact_damage_readable = true,
                ranged_or_special_behavior = new { status = "unavailable", reason = "no complete decompile-backed monster behavior table in this slice" },
                source = "Monster live fields; no MovePosition/AI/drop methods called"
            };
        }).ToArray();
    }

    private static object ReadFloorObjectives(MineShaft mine)
    {
        return new
        {
            must_kill_all_monsters_to_advance = mine.mustKillAllMonstersToAdvance(),
            enemy_count = mine.EnemyCount,
            stones_left_on_level = mine.stonesLeftOnThisLevel,
            ladder_has_spawned = mine.ladderHasSpawned,
            ladder_creation_rule = new { status = "unavailable", reason = "future ladder creation is executor/game progression, not an observed tile" },
            treasure_room = ReadBoolProperty(mine, "isTreasureRoom"),
            slime_area = mine.isSlimeArea,
            dino_area = mine.isDinoArea,
            quarry_area = mine.isQuarryArea,
            water_or_bridge_constraints = new { status = "unavailable", reason = "no non-mutating vanilla aggregate property; inspect map tiles only" },
            ladder_probability_preview = new { status = "unavailable", reason = "would require RNG/drop progression or future stone break state" },
            source = "MineShaft flags and simple methods only"
        };
    }

    private static object ReadPlayerResources(Farmer player)
    {
        return new
        {
            health = player.health,
            max_health = player.maxHealth,
            energy = player.Stamina,
            max_energy = player.MaxStamina,
            mining_level = player.MiningLevel,
            combat_level = player.CombatLevel,
            current_time = Game1.timeOfDay,
            deepest_mine_level = player.deepestMineLevel,
            inventory_capacity = new { max_items = player.maxItems.Value, empty_slots = Math.Max(0, player.maxItems.Value - player.Items.Take(player.maxItems.Value).Count(item => item is not null)) },
            pickaxe_slots = ToolSlots<Pickaxe>(player),
            weapon_slots = player.Items.Select((item, index) => item is MeleeWeapon ? Slot(index, item) : null).Where(item => item is not null).ToArray(),
            bomb_counts = CountItems(player, new[] { "(O)286", "(O)287", "(O)288" }),
            staircase_count = CountItem(player, "(O)71"),
            food_slots = player.Items.Select((item, index) => item is StardewValley.Object obj && obj.Edibility > 0 ? Slot(index, item) : null).Where(item => item is not null).ToArray(),
            buffs = new { status = "available", mining_level = player.buffs.MiningLevel, combat_level = player.buffs.CombatLevel, speed = player.buffs.Speed, defense = player.buffs.Defense },
            source = "Game1.player live resource fields"
        };
    }

    private static object[] ActionTiles(xTile.Layers.Layer? layer, string actionToken)
    {
        if (layer is null)
        {
            return Array.Empty<object>();
        }

        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                var tile = layer.Tiles[x, y];
                var action = tile?.Properties.TryGetValue("Action", out var property) == true
                    ? property.ToString()
                    : tile?.TileIndexProperties.TryGetValue("Action", out property) == true ? property.ToString() : null;
                if (ActionTokenEquals(action, actionToken))
                {
                    tiles.Add(new { tile_x = x, tile_y = y, action, present = true, usable = new { status = "unavailable", reason = "vanilla interaction path not proven by appearance alone" } });
                }
            }
        }

        return tiles.ToArray();
    }

    public static bool ActionTokenEquals(string? action, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var token = action.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.Equals(token, expectedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static object Tile(Vector2 tile) => new { tile_x = (int)tile.X, tile_y = (int)tile.Y };

    private static bool? ReadBoolProperty(object target, string name)
    {
        var property = target.GetType().GetProperty(name);
        return property?.PropertyType == typeof(bool) ? (bool?)property.GetValue(target) : null;
    }

    private static object?[] ToolSlots<TTool>(Farmer player) where TTool : Tool
    {
        return player.Items.Select((item, index) => item is TTool ? Slot(index, item) : null).Where(item => item is not null).ToArray();
    }

    private static object Slot(int index, Item item)
    {
        return new { slot_index = index, item_id = item.ItemId, qualified_item_id = item.QualifiedItemId, display_name = item.DisplayName, stack = item.Stack, runtime_type = item.GetType().FullName };
    }

    private static object[] CountItems(Farmer player, string[] qualifiedIds)
    {
        return qualifiedIds.Select(id => new { qualified_item_id = id, count = CountItem(player, id) }).ToArray();
    }

    private static int CountItem(Farmer player, string qualifiedId)
    {
        return player.Items.Where(item => item?.QualifiedItemId == qualifiedId).Sum(item => item?.Stack ?? 0);
    }

    private static FieldEnvelope<object> Partial(object value, string source, long readAtTick, string reason)
    {
        return new FieldEnvelope<object>
        {
            Value = value,
            Status = FieldStatus.Available,
            Source = new SourceRef { Kind = "game_object_partial", Path = source },
            Adapter = "mining_read_adapter",
            ReadAtTick = readAtTick,
            Confidence = 1.0,
            Reason = reason
        };
    }
}
