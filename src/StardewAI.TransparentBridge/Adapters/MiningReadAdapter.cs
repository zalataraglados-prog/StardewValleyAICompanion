using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewAI.Contracts.State;
using System.Reflection;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class MiningReadAdapter : ReadAdapterBase
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
                "mining.monsters",
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
            ["monsters"] = Field(ReadMonsters(mine), "MineShaft.characters filtered to Monster", tick, "mining_read_adapter"),
            ["debris"] = Field(ReadDebris(mine), "MineShaft.debris live item and chunk fields", tick, "mining_read_adapter"),
            ["floor_objectives"] = Field(ReadFloorObjectives(mine), "MineShaft live flags, ladder rule, and deterministic per-stone preview inputs", tick, "mining_read_adapter"),
            ["player_resources"] = Field(ReadPlayerResources(Game1.player), "Game1.player resources and inventory", tick, "mining_read_adapter"),
            ["completeness"] = Field(new
            {
                status = "complete",
                source = "live_loaded_mineshaft_only",
                unavailable_reasons = Array.Empty<string>(),
                read_only_methods = new[] { "GameLocation.IsTileBlockedBy", "Object.IsBreakableStone", "Utility.CreateDaySaveRandom" },
                forbidden_calls = new[] { "MineShaft.findLadder", "MineShaft.loadLevel", "MineShaft.createLadderDown", "MineShaft.checkStoneForItems", "monster_ai_update" }
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
            exits = ActionTiles(buildings, "Exit"),
            entries = ActionTiles(buildings, "Mine"),
            elevators = ActionTiles(buildings, "MineElevator"),
            ladders = ActionTiles(buildings, "Ladder").Concat(ActionTiles(buildings, "MineLadder")).ToArray(),
            shafts = ActionTiles(buildings, "Shaft").Concat(ActionTiles(buildings, "MineShaft")).ToArray(),
            tile_beneath_ladder = Tile(mine.tileBeneathLadder),
            tile_beneath_elevator = Tile(mine.tileBeneathElevator),
            collision_context = CollisionContext(mine, loadedMap),
            action_tile_status = loadedMap is null ? "unavailable_loaded_map_field_null" : "complete_exact_action_tokens"
        };
    }

    private static object[] ReadObjects(MineShaft mine, Farmer player)
    {
        var bestPickaxe = player.Items.OfType<Pickaxe>()
            .OrderByDescending(pickaxe => PickaxeDamagePerHit(pickaxe.UpgradeLevel, pickaxe.additionalPower.Value))
            .FirstOrDefault();
        var pickaxeDamage = bestPickaxe is null
            ? 0
            : PickaxeDamagePerHit(bestPickaxe.UpgradeLevel, bestPickaxe.additionalPower.Value);

        return mine.objects.Pairs.Select(pair =>
        {
            var obj = pair.Value;
            var qualifiedId = obj.QualifiedItemId;
            var breakableStone = obj.IsBreakableStone();
            var container = obj is BreakableContainer;
            var remainingHealth = breakableStone
                ? obj.MinutesUntilReady
                : container ? ReadBreakableContainerHealth((BreakableContainer)obj) : null;
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
                is_breakable_stone = breakableStone,
                is_ore_or_resource_node = breakableStone,
                mining_node_kind = breakableStone ? "breakable_stone_or_resource_node" : "none",
                is_container = container,
                is_placed_staircase = qualifiedId == "(O)71",
                tool_requirement = breakableStone ? "pickaxe" : container ? "heavy_hitter" : qualifiedId == "(O)71" ? "none_staircase" : "unknown_or_interact",
                health_or_hits_remaining = remainingHealth,
                best_pickaxe_damage_per_hit = breakableStone ? pickaxeDamage : 0,
                best_pickaxe_hits_remaining = breakableStone && remainingHealth.HasValue && pickaxeDamage > 0
                    ? RemainingHits(remainingHealth.Value, pickaxeDamage)
                    : (int?)null,
                ladder_preview = breakableStone ? ReadLadderPreview(mine, pair.Key, player) : null,
                source = "Object.IsBreakableStone/MinutesUntilReady; Pickaxe.DoFunction; BreakableContainer.health read-only reflection"
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
                damage_to_farmer = monster.DamageToFarmer,
                resilience = monster.resilience.Value,
                miss_chance = monster.missChance.Value,
                is_monster = monster.IsMonster,
                slipperiness = monster.Slipperiness,
                is_glider = monster.isGlider.Value,
                ignore_damage_line_of_sight = monster.ignoreDamageLOS.Value,
                is_invisible = monster.IsInvisible,
                is_invincible = monster.isInvincible(),
                invincible_countdown_ms = monster.invincibleCountdown,
                stun_time_ms = monster.stunTime.Value,
                is_hard_mode_monster = monster.isHardModeMonster.Value,
                movement_speed = monster.Speed,
                tile_manhattan_distance_to_player = Math.Abs(monster.TilePoint.X - Game1.player.TilePoint.X) + Math.Abs(monster.TilePoint.Y - Game1.player.TilePoint.Y),
                center_distance_pixels = Vector2.Distance(box.Center.ToVector2(), Game1.player.GetBoundingBox().Center.ToVector2()),
                selected_drop_item_ids = monster.objectsToDrop.ToArray(),
                selected_drop_qualified_item_ids = monster.objectsToDrop.Select(itemId => ItemRegistry.QualifyItemId(itemId) ?? "(O)" + itemId).ToArray(),
                has_special_item = monster.hasSpecialItem.Value,
                contact_damage_readable = true,
                behavior_observation = new
                {
                    runtime_type = monster.GetType().FullName,
                    dynamic_replan_required = true,
                    future_ai_path_not_predicted = true
                },
                source = "Monster live fields; future AI is handled by after-snapshot replanning, not guessed"
            };
        }).ToArray();
    }

    private static object[] ReadDebris(MineShaft mine)
    {
        return mine.debris.Select((debris, index) => new
        {
            debris_index = index,
            debris_type = debris.debrisType.Value.ToString(),
            chunk_type = debris.chunkType.Value,
            item_id = debris.itemId.Value,
            qualified_item_id = debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value,
            item_quality = debris.itemQuality,
            chunk_count = debris.Chunks.Count,
            is_sinking = debris.isSinking.Value,
            is_essential_item = debris.isEssentialItem(),
            chunks = debris.Chunks.Select((chunk, chunkIndex) => new
            {
                chunk_index = chunkIndex,
                pixel_x = (int)MathF.Round(chunk.position.X),
                pixel_y = (int)MathF.Round(chunk.position.Y),
                tile_x = (int)((chunk.position.X + 32f) / Game1.tileSize),
                tile_y = (int)((chunk.position.Y + 32f) / Game1.tileSize),
                x_velocity = chunk.xVelocity.Value,
                y_velocity = chunk.yVelocity.Value,
                bounces = chunk.bounces,
                alpha = chunk.alpha
            }).ToArray(),
            source = "MineShaft.debris and Debris.Chunks live fields"
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
            ladder_creation_rule = new
            {
                should_create_ladder_on_level = mine.shouldCreateLadderOnThisLevel(),
                source = "MineShaft.shouldCreateLadderOnThisLevel/checkStoneForItems/monsterDrop",
                stone_rule = "decrement_stones_then_seeded_roll_or_zero_stones",
                monster_rule = mine.mustKillAllMonstersToAdvance() ? "kill_all_monsters" : "possible_monster_drop_ladder"
            },
            treasure_room = ReadPrivateNetBool(mine, TreasureRoomField),
            slime_area = mine.isSlimeArea,
            dino_area = mine.isDinoArea,
            quarry_area = mine.isQuarryArea,
            water_or_bridge_constraints = new { status = "derived", source = "mining.tiles.collision_context", blocked_tiles_include_non_passable_map_geometry = true },
            ladder_probability_preview = new
            {
                status = "derived",
                next_stone_chance = LadderChanceAfterBreak(
                    mine.stonesLeftOnThisLevel,
                    Game1.player.LuckLevel,
                    Game1.player.DailyLuck,
                    mine.EnemyCount,
                    Game1.player.hasBuff("dwarfStatue_1")),
                exact_seeded_rolls_recorded_per_breakable_stone = true,
                source = "MineShaft.checkStoneForItems"
            },
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
            selected_slot_index = player.CurrentToolIndex,
            selected_item_id = player.CurrentItem?.ItemId ?? string.Empty,
            selected_qualified_item_id = player.CurrentItem?.QualifiedItemId ?? string.Empty,
            selected_item_runtime_type = player.CurrentItem?.GetType().FullName ?? string.Empty,
            inventory_capacity = new { max_items = player.maxItems.Value, empty_slots = Math.Max(0, player.maxItems.Value - player.Items.Take(player.maxItems.Value).Count(item => item is not null)) },
            pickaxe_slots = ToolSlots<Pickaxe>(player),
            weapon_slots = player.Items.Select((item, index) => item is MeleeWeapon weapon ? WeaponSlot(index, weapon) : null).Where(item => item is not null).ToArray(),
            bomb_counts = CountItems(player, new[] { "(O)286", "(O)287", "(O)288" }),
            staircase_count = CountItem(player, "(O)71"),
            food_slots = player.Items.Select((item, index) => item is StardewValley.Object obj && obj.Edibility > 0 ? FoodSlot(index, obj) : null).Where(item => item is not null).ToArray(),
            buffs = new
            {
                status = "available",
                mining_level = player.buffs.MiningLevel,
                combat_level = player.buffs.CombatLevel,
                speed = player.buffs.Speed,
                defense = player.buffs.Defense,
                attack_multiplier = player.buffs.AttackMultiplier,
                knockback_multiplier = player.buffs.KnockbackMultiplier,
                weapon_speed_multiplier = player.buffs.WeaponSpeedMultiplier,
                critical_chance_multiplier = player.buffs.CriticalChanceMultiplier,
                critical_power_multiplier = player.buffs.CriticalPowerMultiplier,
                weapon_precision_multiplier = player.buffs.WeaponPrecisionMultiplier
            },
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
                    tiles.Add(new { tile_x = x, tile_y = y, action, present = true, usable = new { status = "derived", reason = "exact_action_token_on_loaded_map" } });
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

    public static int PickaxeDamagePerHit(int upgradeLevel, int additionalPower)
    {
        return Math.Max(1, upgradeLevel + 1) + Math.Max(0, additionalPower);
    }

    public static int RemainingHits(int remainingHealth, int damagePerHit)
    {
        return damagePerHit <= 0 || remainingHealth <= 0
            ? 0
            : (int)Math.Ceiling(remainingHealth / (double)damagePerHit);
    }

    public static double LadderChanceAfterBreak(int stonesBeforeBreak, int luckLevel, double dailyLuck, int enemyCount, bool dwarfStatueBuff)
    {
        var stonesAfterBreak = Math.Max(0, stonesBeforeBreak - 1);
        var chance = 0.02 + 1.0 / Math.Max(1, stonesAfterBreak) + luckLevel / 100.0 + dailyLuck / 5.0;
        if (enemyCount == 0)
        {
            chance += 0.04;
        }
        if (dwarfStatueBuff)
        {
            chance *= 1.25;
        }
        return chance;
    }

    private static object?[] ToolSlots<TTool>(Farmer player) where TTool : Tool
    {
        return player.Items.Select((item, index) => item is TTool ? Slot(index, item) : null).Where(item => item is not null).ToArray();
    }

    private static object Slot(int index, Item item)
    {
        return new { slot_index = index, item_id = item.ItemId, qualified_item_id = item.QualifiedItemId, display_name = item.DisplayName, stack = item.Stack, runtime_type = item.GetType().FullName };
    }

    private static object WeaponSlot(int index, MeleeWeapon weapon)
    {
        var type = weapon.type.Value;
        return new
        {
            slot_index = index,
            item_id = weapon.ItemId,
            qualified_item_id = weapon.QualifiedItemId,
            display_name = weapon.DisplayName,
            runtime_type = weapon.GetType().FullName,
            weapon_type = type,
            weapon_type_name = type == MeleeWeapon.dagger ? "dagger" : type == MeleeWeapon.club ? "club" : type == MeleeWeapon.defenseSword ? "sword" : "stabbing_sword",
            is_scythe = weapon.isScythe(),
            min_damage = weapon.minDamage.Value,
            max_damage = weapon.maxDamage.Value,
            speed = weapon.speed.Value,
            precision = weapon.addedPrecision.Value,
            defense = weapon.addedDefense.Value,
            area_of_effect = weapon.addedAreaOfEffect.Value,
            knockback = weapon.knockback.Value,
            critical_chance = weapon.critChance.Value,
            critical_multiplier = weapon.critMultiplier.Value,
            is_on_special = weapon.isOnSpecial,
            special_cooldown_remaining_ms = type == MeleeWeapon.dagger
                ? MeleeWeapon.daggerCooldown
                : type == MeleeWeapon.club ? MeleeWeapon.clubCooldown : MeleeWeapon.defenseCooldown,
            enchantments = weapon.enchantments.Select(enchantment => new { runtime_type = enchantment.GetType().FullName, level = enchantment.Level }).ToArray(),
            source = "live MeleeWeapon net fields and active enchantments"
        };
    }

    private static object FoodSlot(int index, StardewValley.Object food)
    {
        return new
        {
            slot_index = index,
            item_id = food.ItemId,
            qualified_item_id = food.QualifiedItemId,
            display_name = food.DisplayName,
            stack = food.Stack,
            quality = food.Quality,
            edibility = food.Edibility,
            health_recovery = food.healthRecoveredOnConsumption(),
            energy_recovery = food.staminaRecoveredOnConsumption(),
            sell_price = food.sellToStorePrice(),
            runtime_type = food.GetType().FullName,
            source = "Object live fields and healthRecoveredOnConsumption/staminaRecoveredOnConsumption"
        };
    }

    private static object[] CountItems(Farmer player, string[] qualifiedIds)
    {
        return qualifiedIds.Select(id => new { qualified_item_id = id, count = CountItem(player, id) }).ToArray();
    }

    private static int CountItem(Farmer player, string qualifiedId)
    {
        return player.Items.Where(item => item?.QualifiedItemId == qualifiedId).Sum(item => item?.Stack ?? 0);
    }

    private object CollisionContext(MineShaft mine, xTile.Map? loadedMap)
    {
        if (loadedMap is null || loadedMap.Layers.Count == 0)
        {
            return new { status = "unavailable", reason = "loaded_map_field_null" };
        }

        var width = loadedMap.Layers[0].LayerWidth;
        var height = loadedMap.Layers[0].LayerHeight;
        var signature = CollisionSignature(mine, width, height);
        if (cachedCollisionContext is not null && string.Equals(signature, cachedCollisionSignature, StringComparison.Ordinal))
        {
            return cachedCollisionContext;
        }

        var rows = new string[height];
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        for (var y = 0; y < height; y++)
        {
            var row = new char[width];
            for (var x = 0; x < width; x++)
            {
                var blocked = mine.IsTileBlockedBy(new Vector2(x, y), collisionMask, CollisionMask.None, useFarmerTile: true) ||
                    mine.farmers.Any(farmer => farmer != Game1.player && FarmerBlocksTile(farmer, x, y));
                row[x] = blocked ? '1' : '0';
            }
            rows[y] = new string(row);
        }

        cachedCollisionSignature = signature;
        cachedCollisionContext = new
        {
            status = "available",
            width,
            height,
            encoding = "row_major_strings_1_blocked_0_passable",
            blocked_rows = rows,
            excludes_current_player = true,
            includes_map_objects_characters_terrain_and_other_farmers = true,
            source = "GameLocation.IsTileBlockedBy; decompiled method is read-only"
        };
        return cachedCollisionContext;
    }

    private static string CollisionSignature(MineShaft mine, int width, int height)
    {
        var hash = new HashCode();
        hash.Add(mine.mineLevel);
        hash.Add(mine.loadedMapNumber);
        hash.Add(width);
        hash.Add(height);
        hash.Add(mine.ladderHasSpawned);
        hash.Add(Game1.ticks / 30);
        foreach (var pair in mine.objects.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.QualifiedItemId);
            hash.Add(pair.Value.MinutesUntilReady);
        }
        foreach (var character in mine.characters.OrderBy(character => character.Name, StringComparer.Ordinal).ThenBy(character => character.Position.Y).ThenBy(character => character.Position.X))
        {
            hash.Add(character.GetType().FullName);
            hash.Add(character.Position);
            hash.Add(character.GetBoundingBox());
        }
        foreach (var pair in mine.terrainFeatures.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.GetType().FullName);
            hash.Add(pair.Value.getBoundingBox());
            hash.Add(pair.Value.isPassable());
            hash.Add(pair.Value.isTemporarilyInvisible);
        }
        foreach (var clump in mine.resourceClumps.OrderBy(clump => clump.Tile.Y).ThenBy(clump => clump.Tile.X))
        {
            hash.Add(clump.GetType().FullName);
            hash.Add(clump.Tile);
            hash.Add(clump.getBoundingBox());
            hash.Add(clump.health.Value);
        }
        foreach (var feature in mine.largeTerrainFeatures.OrderBy(feature => feature.Tile.Y).ThenBy(feature => feature.Tile.X))
        {
            hash.Add(feature.GetType().FullName);
            hash.Add(feature.Tile);
            hash.Add(feature.getBoundingBox());
            hash.Add(feature.isPassable());
            hash.Add(feature.isTemporarilyInvisible);
        }
        foreach (var furniture in mine.furniture.OrderBy(furniture => furniture.GetBoundingBox().Y).ThenBy(furniture => furniture.GetBoundingBox().X))
        {
            hash.Add(furniture.GetType().FullName);
            hash.Add(furniture.GetBoundingBox());
            hash.Add(furniture.isPassable());
        }
        foreach (var pair in mine.animals.Pairs.OrderBy(pair => pair.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.GetType().FullName);
            hash.Add(pair.Value.GetBoundingBox());
            hash.Add(pair.Value.farmerPassesThrough);
        }
        foreach (var farmer in mine.farmers.Where(farmer => farmer != Game1.player).OrderBy(farmer => farmer.UniqueMultiplayerID))
        {
            hash.Add(farmer.UniqueMultiplayerID);
            hash.Add(farmer.GetBoundingBox());
        }
        return hash.ToHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool FarmerBlocksTile(Farmer farmer, int tileX, int tileY)
    {
        return farmer.GetBoundingBox().Intersects(new Rectangle(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize, Game1.tileSize));
    }

    private static object ReadLadderPreview(MineShaft mine, Vector2 tile, Farmer player)
    {
        var stonesAfterBreak = Math.Max(0, mine.stonesLeftOnThisLevel - 1);
        var chance = LadderChanceAfterBreak(mine.stonesLeftOnThisLevel, player.LuckLevel, player.DailyLuck, mine.EnemyCount, player.hasBuff("dwarfStatue_1"));
        var random = Utility.CreateDaySaveRandom((int)tile.X * 1000, (int)tile.Y, mine.mineLevel);
        _ = random.NextDouble();
        var roll = random.NextDouble();
        var eligible = !mine.ladderHasSpawned && !mine.mustKillAllMonstersToAdvance() && mine.shouldCreateLadderOnThisLevel();
        return new
        {
            eligible,
            stones_after_break = stonesAfterBreak,
            chance,
            seeded_roll = roll,
            guaranteed_by_last_stone = stonesAfterBreak == 0,
            creates_ladder = eligible && (stonesAfterBreak == 0 || roll < chance),
            source = "MineShaft.checkStoneForItems exact seed and comparison"
        };
    }

    private static int? ReadBreakableContainerHealth(BreakableContainer container)
    {
        return BreakableContainerHealthField?.GetValue(container) is NetInt health ? health.Value : null;
    }

    private static bool? ReadPrivateNetBool(object target, FieldInfo? field)
    {
        return field?.GetValue(target) is NetBool value ? value.Value : null;
    }
}
