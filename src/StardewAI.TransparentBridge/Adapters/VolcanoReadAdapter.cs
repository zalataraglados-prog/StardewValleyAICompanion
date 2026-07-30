using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewAI.Contracts.State;
using System.Reflection;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class VolcanoReadAdapter : ReadAdapterBase
{
    private static readonly FieldInfo? BreakableContainerHealthField = typeof(BreakableContainer)
        .GetField("health", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

    private string? cachedCollisionSignature;
    private object? cachedCollisionContext;

    public override string Domain => "volcano";
    public override int Priority => 54;

    public override StateAdapterResult Collect(long tick)
    {
        if (!Context.IsWorldReady || Game1.currentLocation is not VolcanoDungeon volcano)
        {
            var unavailable = new[]
            {
                "volcano.current_level",
                "volcano.tiles",
                "volcano.connectors",
                "volcano.gates",
                "volcano.objects",
                "volcano.monsters",
                "volcano.debris",
                "volcano.player_resources"
            };
            return Section("volcano", unavailable.ToDictionary(
                field => field.Split('.')[1],
                field => (object)Unavailable("not_loaded_volcano_dungeon", "Game1.currentLocation is VolcanoDungeon", tick, "volcano_read_adapter")), unavailable);
        }

        var fields = new Dictionary<string, object>
        {
            ["current_level"] = Field(ReadCurrentLevel(volcano), "VolcanoDungeon live level/layout/generation fields and progression mail", tick, "volcano_read_adapter"),
            ["tiles"] = Field(ReadTiles(volcano), "loaded VolcanoDungeon map, waterTiles, cooledLavaTiles, dirtTiles, and read-only collision checks", tick, "volcano_read_adapter"),
            ["connectors"] = Field(ReadConnectors(volcano), "VolcanoDungeon.warps and loaded action tiles", tick, "volcano_read_adapter"),
            ["gates"] = Field(ReadGates(volcano), "VolcanoDungeon.dwarfGates live net fields", tick, "volcano_read_adapter"),
            ["objects"] = Field(ReadObjects(volcano), "VolcanoDungeon.objects live fields", tick, "volcano_read_adapter"),
            ["monsters"] = Field(ReadMonsters(volcano), "VolcanoDungeon.characters filtered to Monster", tick, "volcano_read_adapter"),
            ["debris"] = Field(ReadDebris(volcano), "VolcanoDungeon.debris live item and chunk fields", tick, "volcano_read_adapter"),
            ["player_resources"] = Field(ReadPlayerResources(Game1.player), "Game1.player live inventory, health, energy, and tool resources", tick, "volcano_read_adapter"),
            ["completeness"] = Field(new
            {
                status = "complete",
                source = "live_loaded_volcano_dungeon_only",
                unavailable_reasons = Array.Empty<string>(),
                read_only_methods = new[] { "GameLocation.IsTileBlockedBy", "Object.IsBreakableStone", "VolcanoDungeon.isMushroomLevel", "VolcanoDungeon.isMonsterLevel", "VolcanoDungeon.IsGeneratedLevel" },
                forbidden_calls = new[] { "VolcanoDungeon.GenerateContents", "VolcanoDungeon.GenerateLevel", "VolcanoDungeon.CoolLava", "VolcanoDungeon.performToolAction", "DwarfGate.pressEvent.Fire", "DwarfGate.openEvent.Fire", "Game1.warpFarmer" }
            }, "VolcanoReadAdapter static live reads", tick, "volcano_read_adapter")
        };

        return Section("volcano", fields, Array.Empty<string>(), "complete");
    }

    private static object ReadCurrentLevel(VolcanoDungeon volcano)
    {
        var level = volcano.level.Value;
        return new
        {
            location_id = volcano.NameOrUniqueName,
            level,
            level_kind = level switch
            {
                0 => "entrance",
                5 => "rest_shop",
                9 => "caldera_exit",
                _ => "generated"
            },
            layout_index = volcano.layoutIndex.Value,
            generation_seed = volcano.generationSeed.Value,
            is_generated_level = VolcanoDungeon.IsGeneratedLevel(volcano.NameOrUniqueName),
            is_mushroom_level = volcano.isMushroomLevel(),
            is_monster_level = volcano.isMonsterLevel(),
            lava_cooling_enabled = level != 5,
            map_width = volcano.mapWidth,
            map_height = volcano.mapHeight,
            start_position = PointValue(volcano.startPosition),
            end_position = PointValue(volcano.endPosition),
            current_time = Game1.timeOfDay,
            progression = new
            {
                entrance_bridge_unlocked = Game1.player.hasOrWillReceiveMail("Island_VolcanoBridge"),
                level_five_shortcut_out_unlocked = Game1.player.hasOrWillReceiveMail("Island_VolcanoShortcutOut"),
                entrance_shortcut_gate_unlocked = Game1.player.hasOrWillReceiveMail("volcanoShortcutUnlocked")
            },
            source = "VolcanoDungeon.level/layoutIndex/generationSeed/startPosition/endPosition and exact progression mail flags"
        };
    }

    private object ReadTiles(VolcanoDungeon volcano)
    {
        var map = volcano.map;
        var width = map?.Layers.Count > 0 ? map.Layers[0].LayerWidth : volcano.mapWidth;
        var height = map?.Layers.Count > 0 ? map.Layers[0].LayerHeight : volcano.mapHeight;
        var cooledPoints = volcano.cooledLavaTiles.Pairs
            .Where(pair => pair.Value)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => (X: (int)pair.Key.X, Y: (int)pair.Key.Y))
            .ToArray();
        var cooledSet = cooledPoints.ToHashSet();
        var lava = new List<(int X, int Y, bool Cooled)>();
        if (volcano.waterTiles is not null)
        {
            var waterWidth = volcano.waterTiles.waterTiles.GetLength(0);
            var waterHeight = volcano.waterTiles.waterTiles.GetLength(1);
            for (var y = 0; y < waterHeight; y++)
            {
                for (var x = 0; x < waterWidth; x++)
                {
                    if (volcano.waterTiles[x, y])
                    {
                        lava.Add((x, y, cooledSet.Contains((x, y))));
                    }
                }
            }
        }

        return new
        {
            player_tile = Tile(Game1.player.TilePoint.X, Game1.player.TilePoint.Y),
            map = map is null || map.Layers.Count == 0
                ? null
                : new { width, height, status = "loaded_field_only" },
            start_position = PointValue(volcano.startPosition),
            end_position = PointValue(volcano.endPosition),
            native_water_or_lava_tiles = lava.Select(tile => new { tile_x = tile.X, tile_y = tile.Y, cooled = tile.Cooled, passable_bridge = tile.Cooled }).ToArray(),
            cooled_lava_tiles = cooledPoints.Select(tile => Tile(tile.X, tile.Y)).ToArray(),
            coolable_uncooled_tiles = volcano.level.Value == 5
                ? Array.Empty<object>()
                : lava.Where(tile => !tile.Cooled).Select(tile => Tile(tile.X, tile.Y)).ToArray(),
            dirt_tiles = volcano.dirtTiles.OrderBy(point => point.Y).ThenBy(point => point.X).Select(point => Tile(point.X, point.Y)).ToArray(),
            collision_context = CollisionContext(volcano, map),
            source = "loaded map plus exact waterTiles/cooledLavaTiles/dirtTiles; level 5 excluded by VolcanoDungeon.performToolAction"
        };
    }

    private static object ReadConnectors(VolcanoDungeon volcano)
    {
        var level = volcano.level.Value;
        var warps = volcano.warps
            .OrderBy(warp => warp.Y)
            .ThenBy(warp => warp.X)
            .Select(warp =>
            {
                var targetLevel = TryVolcanoLevel(warp.TargetName);
                var kind = string.Equals(warp.TargetName, "Caldera", StringComparison.Ordinal)
                    ? "caldera"
                    : string.Equals(warp.TargetName, "IslandNorth", StringComparison.Ordinal)
                        ? "island_north"
                        : targetLevel.HasValue && targetLevel.Value > level
                            ? "forward_level"
                            : targetLevel.HasValue && targetLevel.Value < level
                                ? "backward_level"
                                : "other";
                return new
                {
                    tile_x = warp.X,
                    tile_y = warp.Y,
                    target_location = warp.TargetName,
                    target_tile_x = warp.TargetX,
                    target_tile_y = warp.TargetY,
                    connector_kind = kind,
                    target_level = targetLevel,
                    npc_only = warp.npcOnly.Value,
                    flip_farmer = warp.flipFarmer.Value
                };
            })
            .ToArray();

        return new
        {
            warps,
            forward_warps = warps.Where(warp => warp.connector_kind is "forward_level" or "caldera").ToArray(),
            backward_warps = warps.Where(warp => warp.connector_kind is "backward_level" or "island_north").ToArray(),
            leave_volcano_action_tiles = IndexedTiles(volcano.map?.GetLayer("Buildings"), 367, "LeaveVolcano"),
            volcano_shop_action_tiles = IndexedTiles(volcano.map?.GetLayer("Buildings"), 77, "VolcanoShop"),
            source = "VolcanoDungeon.warps and checkAction tile indices 367/77"
        };
    }

    private static object[] ReadGates(VolcanoDungeon volcano)
    {
        return volcano.dwarfGates
            .OrderBy(gate => gate.gateIndex.Value)
            .ThenBy(gate => gate.tilePosition.Y)
            .ThenBy(gate => gate.tilePosition.X)
            .Select(gate => new
            {
                gate_index = gate.gateIndex.Value,
                tile_x = gate.tilePosition.X,
                tile_y = gate.tilePosition.Y,
                blocking_tile_x = gate.tilePosition.X,
                blocking_tile_y = gate.tilePosition.Y + 1,
                opened = gate.opened.Value,
                local_opened = gate.localOpened,
                pressed_switch_count = gate.pressedSwitches.Value,
                required_switch_count = gate.switches.Count(),
                all_switches_pressed = gate.switches.Count() == 0 || gate.switches.Pairs.All(pair => pair.Value),
                switches = gate.switches.Pairs
                    .OrderBy(pair => pair.Key.Y)
                    .ThenBy(pair => pair.Key.X)
                    .Select(pair => new
                    {
                        tile_x = pair.Key.X,
                        tile_y = pair.Key.Y,
                        pressed = pair.Value,
                        touch_action = pair.Value ? string.Empty : "DwarfSwitch"
                    })
                    .ToArray(),
                source = "DwarfGate.tilePosition/gateIndex/opened/pressedSwitches/switches"
            })
            .ToArray();
    }

    private static object[] ReadObjects(VolcanoDungeon volcano)
    {
        return volcano.objects.Pairs
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair =>
            {
                var obj = pair.Value;
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    item_id = obj.ItemId,
                    qualified_item_id = obj.QualifiedItemId,
                    display_name = obj.DisplayName,
                    runtime_type = obj.GetType().FullName,
                    is_breakable_stone = obj.IsBreakableStone(),
                    is_breakable_container = obj is BreakableContainer,
                    is_chest = obj is Chest,
                    minutes_until_ready = obj.MinutesUntilReady,
                    health_or_hits_remaining = obj.IsBreakableStone()
                        ? obj.MinutesUntilReady
                        : obj is BreakableContainer container ? ReadBreakableContainerHealth(container) : null,
                    can_be_grabbed = obj.CanBeGrabbed,
                    tool_requirement = obj.IsBreakableStone() ? "pickaxe" : obj is BreakableContainer ? "heavy_hitter" : "unknown_or_interact",
                    source = "VolcanoDungeon.objects and Object live fields"
                };
            })
            .ToArray();
    }

    private static object[] ReadMonsters(VolcanoDungeon volcano)
    {
        return volcano.characters.OfType<Monster>()
            .OrderBy(monster => monster.TilePoint.Y)
            .ThenBy(monster => monster.TilePoint.X)
            .Select(monster =>
            {
                var box = monster.GetBoundingBox();
                var vanillaRuntimeType = monster.GetType().Assembly == typeof(Monster).Assembly;
                var meleeSupported = vanillaRuntimeType && monster is not Spiker;
                return new
                {
                    runtime_identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"),
                    runtime_type = monster.GetType().FullName,
                    name = monster.Name,
                    tile_x = monster.TilePoint.X,
                    tile_y = monster.TilePoint.Y,
                    pixel_x = monster.Position.X,
                    pixel_y = monster.Position.Y,
                    bounding_box = new { x = box.X, y = box.Y, width = box.Width, height = box.Height },
                    health = monster.Health,
                    max_health = monster.MaxHealth,
                    resilience = monster.resilience.Value,
                    miss_chance = monster.missChance.Value,
                    damage_to_farmer = monster.DamageToFarmer,
                    is_glider = monster.isGlider.Value,
                    is_invisible = monster.IsInvisible,
                    is_invincible = monster.isInvincible(),
                    invincible_countdown_ms = monster.invincibleCountdown,
                    stun_time_ms = monster.stunTime.Value,
                    melee_executor_supported = meleeSupported,
                    melee_executor_block_reason = meleeSupported
                        ? string.Empty
                        : monster is Spiker ? "spiker_permanent_melee_immunity" : "custom_monster_melee_semantics_unverified",
                    tile_manhattan_distance_to_player = Math.Abs(monster.TilePoint.X - Game1.player.TilePoint.X) + Math.Abs(monster.TilePoint.Y - Game1.player.TilePoint.Y),
                    future_ai_path_not_predicted = true,
                    source = "Monster live fields; future AI requires after-snapshot replanning"
                };
            })
            .ToArray();
    }

    private static object[] ReadDebris(VolcanoDungeon volcano)
    {
        return volcano.debris.Select((debris, index) =>
        {
            var qualifiedItemId = debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value;
            return new
            {
                debris_index = index,
                item_id = debris.itemId.Value,
                qualified_item_id = qualifiedItemId,
                is_collectible_item_debris = !string.IsNullOrWhiteSpace(qualifiedItemId),
                is_sinking = debris.isSinking.Value,
                chunks = debris.Chunks.Select((chunk, chunkIndex) => new
                {
                    chunk_index = chunkIndex,
                    tile_x = (int)((chunk.position.X + 32f) / Game1.tileSize),
                    tile_y = (int)((chunk.position.Y + 32f) / Game1.tileSize),
                    pixel_x = (int)MathF.Round(chunk.position.X),
                    pixel_y = (int)MathF.Round(chunk.position.Y)
                }).ToArray(),
                source = "VolcanoDungeon.debris and Debris.Chunks live fields"
            };
        }).ToArray();
    }

    private static object ReadPlayerResources(Farmer player)
    {
        return new
        {
            health = player.health,
            max_health = player.maxHealth,
            energy = player.Stamina,
            max_energy = player.MaxStamina,
            current_time = Game1.timeOfDay,
            selected_slot_index = player.CurrentToolIndex,
            selected_qualified_item_id = player.CurrentItem?.QualifiedItemId ?? string.Empty,
            inventory_capacity = new
            {
                max_items = player.maxItems.Value,
                empty_slots = Math.Max(0, player.maxItems.Value - player.Items.Take(player.maxItems.Value).Count(item => item is not null))
            },
            watering_can_slots = player.Items.Select((item, index) => item is WateringCan can ? new
            {
                slot_index = index,
                item_id = can.ItemId,
                qualified_item_id = can.QualifiedItemId,
                upgrade_level = can.UpgradeLevel,
                water_left = can.WaterLeft,
                water_capacity = can.waterCanMax,
                is_bottomless = can.IsBottomless,
                can_cool_lava_now = can.IsBottomless || can.WaterLeft > 0
            } : null).Where(slot => slot is not null).ToArray(),
            pickaxe_slots = player.Items.Select((item, index) => item is Pickaxe pickaxe ? new
            {
                slot_index = index,
                item_id = pickaxe.ItemId,
                qualified_item_id = pickaxe.QualifiedItemId,
                upgrade_level = pickaxe.UpgradeLevel,
                additional_power = pickaxe.additionalPower.Value,
                damage_per_hit = Math.Max(1, pickaxe.UpgradeLevel + 1)
            } : null).Where(slot => slot is not null).ToArray(),
            weapon_slots = player.Items.Select((item, index) => item is MeleeWeapon weapon ? new
            {
                slot_index = index,
                item_id = weapon.ItemId,
                qualified_item_id = weapon.QualifiedItemId,
                minimum_damage = weapon.minDamage.Value,
                maximum_damage = weapon.maxDamage.Value,
                weapon_type = weapon.type.Value,
                is_scythe = weapon.isScythe()
            } : null).Where(slot => slot is not null).ToArray(),
            heavy_hitter_slots = player.Items.Select((item, index) => item is Tool tool && tool.isHeavyHitter() ? new
            {
                slot_index = index,
                item_id = tool.ItemId,
                qualified_item_id = tool.QualifiedItemId,
                runtime_type = tool.GetType().FullName,
                upgrade_level = tool.UpgradeLevel,
                container_damage_per_hit = tool is MeleeWeapon weapon && weapon.type.Value == 2 ? 2 : 1
            } : null).Where(slot => slot is not null).ToArray(),
            source = "Game1.player live resources and inventory"
        };
    }

    private static int? ReadBreakableContainerHealth(BreakableContainer container)
    {
        var netInt = BreakableContainerHealthField?.GetValue(container);
        return netInt?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(netInt) as int?;
    }

    private object CollisionContext(VolcanoDungeon volcano, xTile.Map? map)
    {
        if (map is null || map.Layers.Count == 0)
        {
            return new { status = "unavailable", reason = "loaded_map_field_null" };
        }

        var width = map.Layers[0].LayerWidth;
        var height = map.Layers[0].LayerHeight;
        var signature = CollisionSignature(volcano, width, height);
        if (cachedCollisionContext is not null && string.Equals(signature, cachedCollisionSignature, StringComparison.Ordinal))
        {
            return cachedCollisionContext;
        }

        var rows = new string[height];
        var staticRows = new string[height];
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        for (var y = 0; y < height; y++)
        {
            var row = new char[width];
            var staticRow = new char[width];
            for (var x = 0; x < width; x++)
            {
                var mapPassable = volcano.isTilePassable(
                    new xTile.Dimensions.Location(x, y),
                    Game1.viewport);
                var isLava = volcano.waterTiles is not null &&
                    x >= 0 &&
                    y >= 0 &&
                    x < volcano.waterTiles.waterTiles.GetLength(0) &&
                    y < volcano.waterTiles.waterTiles.GetLength(1) &&
                    volcano.waterTiles[x, y];
                var cooled = volcano.cooledLavaTiles.TryGetValue(
                    new Vector2(x, y),
                    out var isCooled) &&
                    isCooled;
                var blocked = !cooled &&
                    (volcano.IsTileBlockedBy(
                        new Vector2(x, y),
                        collisionMask,
                        CollisionMask.None,
                        useFarmerTile: true) ||
                    volcano.farmers.Any(farmer =>
                        farmer != Game1.player &&
                        farmer.TilePoint.X == x &&
                        farmer.TilePoint.Y == y));
                row[x] = blocked ? '1' : '0';
                staticRow[x] = cooled ||
                    mapPassable && !isLava
                    ? '0'
                    : '1';
            }
            rows[y] = new string(row);
            staticRows[y] = new string(staticRow);
        }

        cachedCollisionSignature = signature;
        cachedCollisionContext = new
        {
            status = "available",
            width,
            height,
            encoding = "row_major_strings_1_blocked_0_passable",
            blocked_rows = rows,
            static_blocked_rows = staticRows,
            excludes_current_player = true,
            includes_loaded_map_objects_characters_and_other_farmers = true,
            static_rows_exclude_objects_characters_farmers_and_gates = true,
            static_rows_include_uncooled_lava = true,
            source = "GameLocation.IsTileBlockedBy plus GameLocation.isTilePassable and live waterTiles/cooledLavaTiles; decompiled methods are read-only"
        };
        return cachedCollisionContext;
    }

    private static string CollisionSignature(VolcanoDungeon volcano, int width, int height)
    {
        var hash = new HashCode();
        hash.Add(volcano.level.Value);
        hash.Add(volcano.layoutIndex.Value);
        hash.Add(width);
        hash.Add(height);
        hash.Add(Game1.ticks / 30);
        foreach (var pair in volcano.cooledLavaTiles.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }
        foreach (var gate in volcano.dwarfGates.OrderBy(gate => gate.gateIndex.Value))
        {
            hash.Add(gate.gateIndex.Value);
            hash.Add(gate.opened.Value);
            hash.Add(gate.pressedSwitches.Value);
        }
        foreach (var pair in volcano.objects.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.QualifiedItemId);
            hash.Add(pair.Value.MinutesUntilReady);
        }
        foreach (var monster in volcano.characters.OfType<Monster>().OrderBy(monster => monster.Name, StringComparer.Ordinal).ThenBy(monster => monster.Position.Y).ThenBy(monster => monster.Position.X))
        {
            hash.Add(monster.GetType().FullName);
            hash.Add(monster.Position);
            hash.Add(monster.Health);
        }
        return hash.ToHashCode().ToString("X8");
    }

    private static object[] IndexedTiles(xTile.Layers.Layer? layer, int tileIndex, string action)
    {
        if (layer is null)
        {
            return Array.Empty<object>();
        }

        var tiles = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (layer.Tiles[x, y]?.TileIndex == tileIndex)
                {
                    tiles.Add(new { tile_x = x, tile_y = y, tile_index = tileIndex, action });
                }
            }
        }
        return tiles.ToArray();
    }

    private static int? TryVolcanoLevel(string locationName)
    {
        return VolcanoDungeon.IsGeneratedLevel(locationName, out var level) ? level : null;
    }

    private static object? PointValue(Point? point)
    {
        return point.HasValue ? Tile(point.Value.X, point.Value.Y) : null;
    }

    private static object Tile(int x, int y)
    {
        return new { tile_x = x, tile_y = y };
    }
}
