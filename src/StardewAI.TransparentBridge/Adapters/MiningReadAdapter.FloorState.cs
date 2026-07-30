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
    private static object[] ReadDebris(MineShaft mine)
    {
        return mine.debris.Select((debris, index) =>
        {
            var qualifiedItemId = debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value;
            var collectible = !string.IsNullOrWhiteSpace(qualifiedItemId);
            return new
            {
                debris_index = index,
                debris_type = debris.debrisType.Value.ToString(),
                chunk_type = debris.chunkType.Value,
                item_id = debris.itemId.Value,
                qualified_item_id = qualifiedItemId,
                item_quality = debris.itemQuality,
                chunk_count = debris.Chunks.Count,
                is_sinking = debris.isSinking.Value,
                is_essential_item = debris.isEssentialItem(),
                is_collectible_item_debris = collectible,
                collection_identity_status = collectible ? "qualified_item_identity_available" : "non_item_visual_or_numeric_debris",
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
            golden_scythe_applicable = mine.mineLevel == 77377,
            golden_scythe_claimed = Game1.player.mailReceived.Contains("gotGoldenScythe"),
            golden_scythe_reward_qualified_item_id = "(W)53",
            golden_scythe_action_token = "GoldenScythe",
            skull_key_applicable = mine.getMineArea() != MineShaft.desertArea && mine.mineLevel == MineShaft.bottomOfMineLevel,
            skull_key_acquired = Game1.player.hasSkullKey,
            skull_key_reward_chests = ReadSkullKeyRewardChests(mine),
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

    private static object[] ReadSkullKeyRewardChests(MineShaft mine)
    {
        return mine.overlayObjects
            .Where(pair => pair.Value is Chest chest &&
                chest.Items.OfType<SpecialItem>().Any(item => item.which.Value == 4))
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair =>
            {
                var chest = (Chest)pair.Value;
                return (object)new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    runtime_type = chest.GetType().FullName,
                    item_count = chest.Items.Count,
                    contains_skull_key = true,
                    skull_key_special_item_which = 4,
                    interaction_kind = "overlay_object",
                    expected_action_type = "SkullKeyChest",
                    source = "MineShaft.overlayObjects Chest.Items SpecialItem.which"
                };
            })
            .ToArray();
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
            attack = player.Attack,
            luck_level = player.LuckLevel,
            added_speed = player.addedSpeed,
            cardinal_movement = ReadCardinalMovement(player),
            professions = player.professions.OrderBy(id => id).ToArray(),
            current_time = Game1.timeOfDay,
            deepest_mine_level = player.deepestMineLevel,
            selected_slot_index = player.CurrentToolIndex,
            selected_item_id = player.CurrentItem?.ItemId ?? string.Empty,
            selected_qualified_item_id = player.CurrentItem?.QualifiedItemId ?? string.Empty,
            selected_item_runtime_type = player.CurrentItem?.GetType().FullName ?? string.Empty,
            inventory_capacity = new { max_items = player.maxItems.Value, empty_slots = Math.Max(0, player.maxItems.Value - player.Items.Take(player.maxItems.Value).Count(item => item is not null)) },
            golden_scythe_in_inventory = player.Items.Any(item => item?.QualifiedItemId == "(W)53"),
            golden_scythe_inventory_count = player.Items.Where(item => item?.QualifiedItemId == "(W)53").Sum(item => item?.Stack ?? 0),
            pickaxe_slots = player.Items.Select((item, index) => item is Pickaxe pickaxe ? PickaxeSlot(index, pickaxe) : null).Where(item => item is not null).ToArray(),
            weapon_slots = player.Items.Select((item, index) => item is MeleeWeapon weapon ? WeaponSlot(index, weapon) : null).Where(item => item is not null).ToArray(),
            slingshot_slots = player.Items.Select((item, index) => item is Slingshot slingshot ? SlingshotSlot(index, slingshot) : null).Where(item => item is not null).ToArray(),
            combat_damage_modifiers = new
            {
                statue_of_blessings_5_active = player.hasBuff("statue_of_blessings_5"),
                player_enchantments = player.enchantments.Select(enchantment => new
                {
                    runtime_type = enchantment.GetType().FullName,
                    level = enchantment.Level
                }).ToArray(),
                equipped_trinkets = player.trinketItems.Select((trinket, index) => trinket is null ? null : new
                {
                    slot_index = index,
                    item_id = trinket.ItemId,
                    qualified_item_id = trinket.QualifiedItemId,
                    runtime_type = trinket.GetType().FullName
                }).Where(trinket => trinket is not null).ToArray(),
                formula_status = "raw_live_inputs_for_decompiled_melee_damage_and_on_hit_effects"
            },
            bomb_counts = CountItems(player, new[] { "(O)286", "(O)287", "(O)288" }),
            bomb_slots = player.Items.Select((item, index) => item is StardewValley.Object bomb && BombRadius(bomb.QualifiedItemId) > 0
                ? BombSlot(index, bomb, player)
                : null).Where(item => item is not null).ToArray(),
            staircase_count = CountItem(player, "(BC)71"),
            staircase_slots = player.Items.Select((item, index) =>
                    item?.QualifiedItemId == "(BC)71"
                        ? Slot(index, item)
                        : null)
                .Where(item => item is not null)
                .ToArray(),
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

    private static object ReadCardinalMovement(Farmer player)
    {
        var immobilized = player.hasBuff("19");
        var effectiveSpeed = player.Speed + player.addedSpeed + player.temporarySpeedBuff;
        var pixelsPerMillisecond = effectiveSpeed * 0.066d;
        return new
        {
            base_speed = player.Speed,
            added_speed = player.addedSpeed,
            temporary_speed_buff = player.temporarySpeedBuff,
            immobilized,
            effective_speed = effectiveSpeed,
            pixels_per_millisecond = immobilized || pixelsPerMillisecond <= 0d ? (double?)null : pixelsPerMillisecond,
            tile_duration_ms = immobilized || pixelsPerMillisecond <= 0d ? (double?)null : Game1.tileSize / pixelsPerMillisecond,
            status = immobilized ? "blocked_by_buff_19" : "exact_mine_cardinal_input_without_collision_delay",
            source = "Farmer.getMovementSpeed cardinal non-event non-mounted branch"
        };
    }

}
