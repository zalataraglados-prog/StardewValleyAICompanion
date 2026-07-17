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
    private static object?[] ToolSlots<TTool>(Farmer player) where TTool : Tool
    {
        return player.Items.Select((item, index) => item is TTool ? Slot(index, item) : null).Where(item => item is not null).ToArray();
    }

    private static object Slot(int index, Item item)
    {
        return new { slot_index = index, item_id = item.ItemId, qualified_item_id = item.QualifiedItemId, display_name = item.DisplayName, stack = item.Stack, runtime_type = item.GetType().FullName };
    }

    private static object PickaxeSlot(int index, Pickaxe pickaxe)
    {
        return new
        {
            slot_index = index,
            item_id = pickaxe.ItemId,
            qualified_item_id = pickaxe.QualifiedItemId,
            display_name = pickaxe.DisplayName,
            stack = pickaxe.Stack,
            runtime_type = pickaxe.GetType().FullName,
            upgrade_level = pickaxe.UpgradeLevel,
            additional_power = pickaxe.additionalPower.Value,
            stone_damage_per_hit = PickaxeDamagePerHit(pickaxe.UpgradeLevel, pickaxe.additionalPower.Value),
            source = "live Pickaxe.UpgradeLevel/additionalPower fields"
        };
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

    private static object SlingshotSlot(int index, Slingshot slingshot)
    {
        var ammo = slingshot.attachments.Count > 0 ? slingshot.attachments[0] : null;
        return new
        {
            slot_index = index,
            item_id = slingshot.ItemId,
            qualified_item_id = slingshot.QualifiedItemId,
            display_name = slingshot.DisplayName,
            runtime_type = slingshot.GetType().FullName,
            multiplier = SlingshotMultiplier(slingshot.ItemId),
            loaded = ammo is not null,
            ammo_item_id = ammo?.ItemId ?? string.Empty,
            ammo_qualified_item_id = ammo?.QualifiedItemId ?? string.Empty,
            ammo_display_name = ammo?.DisplayName ?? string.Empty,
            ammo_stack = ammo?.Stack ?? 0,
            ammo_base_damage = ammo is null ? 0 : SlingshotAmmoDamage(ammo.QualifiedItemId),
            explosive_ammo_radius = ammo?.QualifiedItemId == "(O)441" ? 2 : 0,
            full_charge_ms = 300,
            projectile_speed_pixels_per_tick_min = 19d * (1d + Game1.player.buffs.WeaponSpeedMultiplier),
            projectile_speed_pixels_per_tick_max = 20d * (1d + Game1.player.buffs.WeaponSpeedMultiplier),
            requires_clear_projectile_path = true,
            source = "live Slingshot.ItemId/attachments; Slingshot.GetRequiredChargeTime/GetAmmoDamage/PerformFire"
        };
    }

    private static object BombSlot(int index, StardewValley.Object bomb, Farmer player)
    {
        var radius = BombRadius(bomb.QualifiedItemId);
        var basePlayerDamage = radius * 3;
        var immune = player.hasBuff("dwarfStatue_3");
        var bookReduction = player.stats.Get("Book_Bombs") != 0;
        return new
        {
            slot_index = index,
            item_id = bomb.ItemId,
            qualified_item_id = bomb.QualifiedItemId,
            display_name = bomb.DisplayName,
            runtime_type = bomb.GetType().FullName,
            stack = bomb.Stack,
            radius_tiles = radius,
            fuse_ms = 2400,
            monster_damage_min = radius * 6,
            monster_damage_max = radius * 8,
            player_damage_before_mitigation = basePlayerDamage,
            player_damage_after_book = immune ? 0 : bookReduction ? (int)(basePlayerDamage * 0.75f) : basePlayerDamage,
            player_damage_immune = immune,
            player_damage_square_side_tiles = radius * 2 + 1,
            object_destruction_shape = "exact_getCircleOutlineGrid_fill",
            source = "Object.placementAction; TemporaryAnimatedSprite bomb constructor; GameLocation.explode/performDamagePlayers"
        };
    }

    private static int SlingshotAmmoDamage(string qualifiedItemId)
    {
        return qualifiedItemId switch
        {
            "(O)388" => 2,
            "(O)390" => 5,
            "(O)378" => 10,
            "(O)380" => 20,
            "(O)384" => 30,
            "(O)382" => 15,
            "(O)386" => 50,
            "(O)441" => 20,
            _ => 1
        };
    }

    private static float SlingshotMultiplier(string itemId)
    {
        return itemId switch
        {
            "33" => 2f,
            "34" => 4f,
            _ => 1f
        };
    }

    private static int BombRadius(string qualifiedItemId)
    {
        return qualifiedItemId switch
        {
            "(O)286" => 3,
            "(O)287" => 5,
            "(O)288" => 7,
            _ => 0
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

}
