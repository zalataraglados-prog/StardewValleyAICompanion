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
    private static object ReadMeleeDamageSemantics(Monster monster, Farmer player)
    {
        var vanillaType = monster.GetType().Assembly == typeof(Monster).Assembly;
        var hasBugKiller = HasWeaponEnchantment(player, "BugKillerEnchantment");
        var hasCrusader = HasWeaponEnchantment(player, "CrusaderEnchantment");
        var currentHitStatus = "exact_base_resilience_and_precision_adjusted_miss_formula";
        var gateReason = string.Empty;
        var requiredWeaponEnchantment = string.Empty;
        var canDamageNow = !monster.IsInvisible && !monster.isInvincible();
        bool? canDefeatWithAvailableMelee = vanillaType ? true : null;
        int? preResilienceDamageOverride = null;

        switch (monster)
        {
            case Spiker:
                currentHitStatus = "permanent_melee_immunity";
                gateReason = "Spiker.takeDamage_always_returns_minus_one";
                canDamageNow = false;
                canDefeatWithAvailableMelee = false;
                break;
            case Bat bat when bat.Age == 789:
                currentHitStatus = "permanent_current_variant_melee_immunity";
                gateReason = "Bat.Age_789";
                canDamageNow = false;
                canDefeatWithAvailableMelee = false;
                break;
            case Bug bug when bug.isArmoredBug.Value:
                requiredWeaponEnchantment = "BugKillerEnchantment";
                currentHitStatus = hasBugKiller ? currentHitStatus : "requires_bug_killer_enchantment";
                gateReason = "armored_bug";
                canDamageNow &= hasBugKiller;
                canDefeatWithAvailableMelee = hasBugKiller;
                break;
            case Mummy mummy:
                requiredWeaponEnchantment = "CrusaderEnchantment";
                canDefeatWithAvailableMelee = hasCrusader;
                gateReason = hasCrusader ? "crusader_prevents_revive_after_lethal_hit" : "mummy_revives_without_crusader_or_bomb";
                if (mummy.reviveTimer.Value > 0)
                {
                    currentHitStatus = "temporary_melee_immunity_while_reviving";
                    canDamageNow = false;
                }
                break;
            case GreenSlime slime when slime.stackedSlimes.Value > 0:
                currentHitStatus = "exact_stacked_slime_forces_pre_resilience_damage_one";
                gateReason = "stacked_slimes_positive";
                preResilienceDamageOverride = 1;
                break;
            case Grub grub when grub.pupating.Value:
                currentHitStatus = "temporary_melee_immunity_while_pupating";
                gateReason = "Grub.pupating";
                canDamageNow = false;
                break;
            case LavaLurk lurk when lurk.currentState.Value == LavaLurk.State.Submerged:
                currentHitStatus = "temporary_melee_immunity_while_submerged";
                gateReason = "LavaLurk.currentState_submerged";
                canDamageNow = false;
                break;
            case RockCrab crab when crab.Sprite.currentFrame % 4 == 0 && !crab.shellGone.Value:
                currentHitStatus = "temporary_shell_blocks_melee_damage";
                gateReason = crab.isStickBug.Value ? "stick_bug_disguise_closed" : "rock_crab_shell_closed";
                canDamageNow = false;
                break;
        }

        if (!vanillaType)
        {
            currentHitStatus = "unknown_custom_monster_takeDamage_semantics";
            gateReason = "runtime_type_not_from_vanilla_monster_assembly";
        }
        return new
        {
            current_hit_status = currentHitStatus,
            current_hit_can_damage = canDamageNow,
            can_defeat_with_available_melee_weapon = canDefeatWithAvailableMelee,
            pre_resilience_damage_override = preResilienceDamageOverride,
            gate_reason = gateReason,
            required_weapon_enchantment_runtime_type = requiredWeaponEnchantment,
            bug_killer_weapon_available = hasBugKiller,
            crusader_weapon_available = hasCrusader,
            formula_inputs = new
            {
                health = monster.Health,
                resilience = monster.resilience.Value,
                miss_chance = monster.missChance.Value,
                invisible = monster.IsInvisible,
                invincible = monster.isInvincible()
            },
            source = "GameLocation.damageMonster; Monster.takeDamage; vanilla runtime-type takeDamage overrides"
        };
    }

    private static bool HasWeaponEnchantment(Farmer player, string runtimeTypeName)
    {
        return player.Items.OfType<MeleeWeapon>().Any(weapon =>
            weapon.enchantments.Any(enchantment => string.Equals(enchantment.GetType().Name, runtimeTypeName, StringComparison.Ordinal)));
    }

    private static object[] ReadMeleeAttackProjections(Monster monster, Farmer player)
    {
        var hasIndependentDamageSource = player.trinketItems
            .Where(trinket => trinket is not null)
            .Select(trinket => DataLoader.Trinkets(Game1.content).GetValueOrDefault(trinket!.ItemId)?.TrinketEffectClass ?? string.Empty)
            .Any(type => type.EndsWith("MagicQuiverTrinketEffect", StringComparison.Ordinal) ||
                type.EndsWith("CompanionTrinketEffect", StringComparison.Ordinal));
        return player.Items.Select((item, slotIndex) => item is MeleeWeapon weapon && !weapon.isScythe()
                ? ReadMeleeAttackProjection(monster, player, weapon, slotIndex, hasIndependentDamageSource)
                : null)
            .Where(projection => projection is not null)
            .ToArray()!;
    }

    private static object[] ReadSlingshotAttackProjections(MineShaft mine, Monster monster, Farmer player)
    {
        return player.Items.Select((item, slotIndex) => item is Slingshot slingshot
                ? ReadSlingshotAttackProjection(mine, monster, player, slingshot, slotIndex)
                : null)
            .Where(projection => projection is not null)
            .ToArray()!;
    }

    private static object ReadBombDamageSemantics(Monster monster)
    {
        var vanillaType = monster.GetType().Assembly == typeof(Monster).Assembly;
        var currentHitCanDamage = !monster.IsInvisible && !monster.isInvincible();
        var canDefeatFromCurrentState = vanillaType;
        var specialEffect = "normal_bomb_damage";
        switch (monster)
        {
            case Spiker:
                currentHitCanDamage = false;
                canDefeatFromCurrentState = false;
                specialEffect = "permanent_immunity";
                break;
            case Bat bat when bat.Age == 789:
                currentHitCanDamage = false;
                canDefeatFromCurrentState = false;
                specialEffect = "permanent_current_variant_immunity";
                break;
            case Bug bug when bug.isArmoredBug.Value:
                currentHitCanDamage = false;
                canDefeatFromCurrentState = false;
                specialEffect = "armored_bug_explicit_bomb_immunity";
                break;
            case Mummy mummy when mummy.reviveTimer.Value > 0:
                currentHitCanDamage = true;
                canDefeatFromCurrentState = true;
                specialEffect = "bomb_finalizes_reviving_mummy";
                break;
            case Mummy:
                canDefeatFromCurrentState = false;
                specialEffect = "standing_mummy_must_be_knocked_down_then_bombed";
                break;
            case RockCrab crab when !crab.isStickBug.Value && !crab.shellGone.Value:
                specialEffect = "bomb_removes_shell_before_normal_damage";
                break;
            case RockCrab crab when crab.isStickBug.Value && !crab.shellGone.Value:
                currentHitCanDamage = false;
                canDefeatFromCurrentState = false;
                specialEffect = "stick_bug_shell_not_removed_by_bomb";
                break;
            case Grub grub when grub.pupating.Value:
                currentHitCanDamage = false;
                canDefeatFromCurrentState = false;
                specialEffect = "temporary_immunity_while_pupating";
                break;
            case LavaLurk lurk when lurk.currentState.Value == LavaLurk.State.Submerged:
                currentHitCanDamage = false;
                canDefeatFromCurrentState = false;
                specialEffect = "temporary_immunity_while_submerged";
                break;
        }
        if (!vanillaType)
        {
            currentHitCanDamage = false;
            canDefeatFromCurrentState = false;
            specialEffect = "unknown_custom_monster_takeDamage_semantics";
        }
        canDefeatFromCurrentState &= currentHitCanDamage;
        return new
        {
            current_hit_can_damage = currentHitCanDamage,
            can_defeat_from_current_state = canDefeatFromCurrentState,
            special_effect = specialEffect,
            monster_damage_formula = "uniform_radius_times_6_through_radius_times_8_then_receiver_takeDamage",
            source = "GameLocation.explode/damageMonster; vanilla runtime-type takeDamage overrides"
        };
    }

    private static object ReadSlingshotAttackProjection(MineShaft mine, Monster monster, Farmer player, Slingshot slingshot, int slotIndex)
    {
        var ammo = slingshot.attachments.Count > 0 ? slingshot.attachments[0] : null;
        var explosiveArea = ammo?.QualifiedItemId == "(O)441"
            ? ReadExplosiveAmmoAreaProjection(mine, monster, player)
            : null;
        var vanillaType = monster.GetType().Assembly == typeof(Monster).Assembly;
        var exactGlobalDamage = player.enchantments.Count == 0;
        var currentHitCanDamage = ammo is not null && !monster.IsInvisible && !monster.isInvincible() && monster switch
        {
            Spiker => false,
            Bat bat when bat.Age == 789 => false,
            Bug bug when bug.isArmoredBug.Value => false,
            Mummy mummy when mummy.reviveTimer.Value > 0 => false,
            Grub grub when grub.pupating.Value => false,
            LavaLurk lurk when lurk.currentState.Value == LavaLurk.State.Submerged => false,
            RockCrab crab when crab.Sprite.currentFrame % 4 == 0 && !crab.shellGone.Value => false,
            _ => true
        };
        var canDefeat = ammo is not null && monster switch
        {
            Spiker => false,
            Bat bat when bat.Age == 789 => false,
            Bug bug when bug.isArmoredBug.Value => false,
            Mummy => false,
            _ => true
        };
        var distribution = vanillaType && exactGlobalDamage && canDefeat
            ? BuildSlingshotDamageDistribution(monster, player, slingshot, ammo!)
            : null;
        double? expectedShots = distribution is null ? null : ExpectedAttacksToDefeat(monster.Health, distribution.Entries);
        if (expectedShots.HasValue && !double.IsFinite(expectedShots.Value))
        {
            expectedShots = null;
        }
        const double chargeMilliseconds = 300d;
        return new
        {
            slot_index = slotIndex,
            qualified_item_id = slingshot.QualifiedItemId,
            slingshot_multiplier = SlingshotMultiplier(slingshot.ItemId),
            ammo_qualified_item_id = ammo?.QualifiedItemId ?? string.Empty,
            ammo_stack = ammo?.Stack ?? 0,
            ammo_base_damage = ammo is null ? 0 : SlingshotAmmoDamage(ammo.QualifiedItemId),
            explosive_ammo_radius = ammo?.QualifiedItemId == "(O)441" ? 2 : 0,
            explosive_area_safe = explosiveArea?.Safe,
            explosive_area_safety_status = explosiveArea?.SafetyStatus ?? "not_applicable_non_explosive_ammo",
            explosive_area_target_motion_margin_tiles = explosiveArea?.TargetMotionMarginTiles ?? 0,
            explosive_area_useful_object_hits = explosiveArea?.UsefulObjectHits ?? 0,
            explosive_area_monster_hits = explosiveArea?.MonsterHits ?? 0,
            explosive_area_additional_monster_hits = explosiveArea?.AdditionalMonsterHits ?? 0,
            explosive_area_protected_object_hits = explosiveArea?.ProtectedObjectHits ?? 0,
            explosive_area_protected_terrain_feature_hits = explosiveArea?.ProtectedTerrainFeatureHits ?? 0,
            explosive_area_other_farmer_hits = explosiveArea?.OtherFarmerHits ?? 0,
            explosive_area_has_additional_value = explosiveArea?.HasAdditionalValue ?? false,
            current_hit_can_damage = currentHitCanDamage,
            can_defeat_with_this_weapon = canDefeat,
            requires_clear_projectile_path = true,
            full_charge_ms = chargeMilliseconds,
            expected_damage_per_shot = distribution?.ExpectedDamagePerAttack,
            expected_shots_to_defeat = expectedShots,
            expected_active_damage_duration_ms = expectedShots * chargeMilliseconds,
            direct_damage_distribution = distribution?.Entries.Select(pair => (object)new { damage = pair.Key, probability = pair.Value }).ToArray() ?? Array.Empty<object>(),
            direct_damage_status = distribution is null ? "unavailable" : "exact_decompiled_discrete_distribution",
            total_damage_completeness = distribution is not null
                ? explosiveArea is null
                    ? "complete_direct_projectile_damage"
                    : "complete_direct_projectile_damage_with_exact_area_safety_and_utility_but_uncomposed_area_damage"
                : ammo is null ? "blocked_unloaded_slingshot" : !vanillaType ? "unknown_custom_monster_takeDamage_semantics" : "unknown_custom_player_enchantment_damage_semantics",
            duration_status = distribution is not null && currentHitCanDamage
                ? "exact_charge_phase_excluding_projectile_travel_and_reposition"
                : currentHitCanDamage ? "unavailable_incomplete_total_damage" : "unavailable_current_temporary_or_permanent_gate",
            source = "Slingshot.GetRequiredChargeTime/GetAmmoDamage/PerformFire; BasicProjectile.behaviorOnCollisionWithMonster/explode; GameLocation.damageMonster"
        };
    }

    private static ExplosiveAmmoAreaProjection ReadExplosiveAmmoAreaProjection(MineShaft mine, Monster target, Farmer player)
    {
        const int radius = 2;
        const int motionMargin = 1;
        var targetTile = target.TilePoint;
        var currentMask = BombAffectedTiles(targetTile, radius).ToHashSet();
        var usefulObjectHits = mine.objects.Pairs.Count(pair =>
            currentMask.Contains(new Point((int)pair.Key.X, (int)pair.Key.Y)) &&
            (pair.Value.IsBreakableStone() || pair.Value is BreakableContainer));
        var currentDamageRectangle = BlastDamageRectangle(targetTile, radius);
        var monsterHits = mine.characters.OfType<Monster>().Count(monster =>
            currentDamageRectangle.Intersects(monster.GetBoundingBox()));
        var protectedObjectHits = 0;
        var protectedTerrainFeatureHits = 0;
        var otherFarmerHits = 0;
        var playerSafe = true;
        for (var offsetX = -motionMargin; offsetX <= motionMargin; offsetX++)
        {
            for (var offsetY = -motionMargin; offsetY <= motionMargin; offsetY++)
            {
                var possibleCenter = new Point(targetTile.X + offsetX, targetTile.Y + offsetY);
                var damageRectangle = BlastDamageRectangle(possibleCenter, radius);
                if (damageRectangle.Intersects(player.GetBoundingBox()))
                {
                    playerSafe = false;
                }
                otherFarmerHits = Math.Max(otherFarmerHits, mine.farmers.Count(farmer =>
                    farmer != player && damageRectangle.Intersects(farmer.GetBoundingBox())));
                var mask = BombAffectedTiles(possibleCenter, radius).ToHashSet();
                protectedObjectHits = Math.Max(protectedObjectHits, mine.objects.Pairs.Count(pair =>
                    mask.Contains(new Point((int)pair.Key.X, (int)pair.Key.Y)) &&
                    !pair.Value.IsBreakableStone() &&
                    pair.Value is not BreakableContainer));
                protectedTerrainFeatureHits = Math.Max(protectedTerrainFeatureHits, mine.terrainFeatures.Pairs.Count(pair =>
                    mask.Contains(new Point((int)pair.Key.X, (int)pair.Key.Y))));
            }
        }
        var additionalMonsterHits = Math.Max(0, monsterHits - 1);
        return new ExplosiveAmmoAreaProjection(
            playerSafe && protectedObjectHits == 0 && protectedTerrainFeatureHits == 0 && otherFarmerHits == 0,
            playerSafe
                ? protectedObjectHits > 0
                    ? "blocked_protected_object_in_target_motion_envelope"
                    : protectedTerrainFeatureHits > 0
                        ? "blocked_terrain_feature_in_target_motion_envelope"
                        : otherFarmerHits > 0
                            ? "blocked_other_farmer_in_target_motion_envelope"
                            : "safe_across_current_target_plus_one_tile_motion_envelope"
                : "blocked_player_inside_target_motion_envelope",
            motionMargin,
            usefulObjectHits,
            monsterHits,
            additionalMonsterHits,
            protectedObjectHits,
            protectedTerrainFeatureHits,
            otherFarmerHits);
    }

    private static Rectangle BlastDamageRectangle(Point center, int radius)
    {
        return new Rectangle(
            (center.X - radius) * Game1.tileSize,
            (center.Y - radius) * Game1.tileSize,
            (radius * 2 + 1) * Game1.tileSize,
            (radius * 2 + 1) * Game1.tileSize);
    }

    private static IEnumerable<Point> BombAffectedTiles(Point center, int radius)
    {
        var outline = Game1.getCircleOutlineGrid(radius);
        var fill = 0;
        for (var x = 0; x < radius * 2 + 1; x++)
        {
            for (var y = 0; y < radius * 2 + 1; y++)
            {
                var include = false;
                if (x == 0 || y == 0 || x == radius * 2 || y == radius * 2)
                {
                    fill = outline[x, y] ? 1 : 0;
                }
                else if (outline[x, y])
                {
                    fill += y <= radius ? 1 : -1;
                    include = fill <= 0;
                }
                if (fill >= 1)
                {
                    include = true;
                }
                if (include)
                {
                    yield return new Point(center.X + x - radius, center.Y + y - radius);
                }
            }
        }
    }

    private static MeleeDamageDistribution BuildSlingshotDamageDistribution(
        Monster monster,
        Farmer player,
        Slingshot slingshot,
        StardewValley.Object ammo)
    {
        var ammoDamage = SlingshotAmmoDamage(ammo.QualifiedItemId);
        var multiplier = SlingshotMultiplier(slingshot.ItemId);
        var randomMinimum = -(ammoDamage / 2);
        var randomMaximumExclusive = ammoDamage + 2;
        var outcomeCount = Math.Max(1, randomMaximumExclusive - randomMinimum);
        var entries = new SortedDictionary<int, double>();
        for (var random = randomMinimum; random < randomMaximumExclusive; random++)
        {
            var projectileDamage = (int)(multiplier * (ammoDamage + random) * (1f + player.buffs.AttackMultiplier));
            for (var collisionBonus = 0; collisionBonus <= 1; collisionBonus++)
            {
                var damage = Math.Max(1, projectileDamage + collisionBonus + player.Attack * 3);
                if (player.professions.Contains(24))
                {
                    damage = (int)Math.Ceiling(damage * 1.1f);
                }
                if (player.professions.Contains(26))
                {
                    damage = (int)Math.Ceiling(damage * 1.15f);
                }
                if (monster is GreenSlime slime && slime.stackedSlimes.Value > 0)
                {
                    damage = 1;
                }
                damage = Math.Max(1, damage - monster.resilience.Value);
                AddProbability(entries, damage, 0.5d / outcomeCount);
            }
        }
        return new MeleeDamageDistribution(entries);
    }

    private static object ReadMeleeAttackProjection(
        Monster monster,
        Farmer player,
        MeleeWeapon weapon,
        int slotIndex,
        bool hasIndependentDamageSource)
    {
        var enchantmentNames = weapon.enchantments.Select(enchantment => enchantment.GetType().Name).ToArray();
        var unknownEnchantment = weapon.enchantments.Any(enchantment =>
            enchantment.GetType().Assembly != typeof(MeleeWeapon).Assembly);
        var magicProjectile = enchantmentNames.Contains("MagicEnchantment", StringComparer.Ordinal);
        var dataProjectile = weapon.GetData()?.Projectiles is { Count: > 0 };
        var exactDirectDamage = monster.GetType().Assembly == typeof(Monster).Assembly &&
            weapon.GetType().Assembly == typeof(MeleeWeapon).Assembly &&
            !unknownEnchantment;
        var currentHitCanDamage = !monster.IsInvisible && !monster.isInvincible() && monster switch
        {
            Spiker => false,
            Bat bat when bat.Age == 789 => false,
            Bug bug when bug.isArmoredBug.Value => enchantmentNames.Contains("BugKillerEnchantment", StringComparer.Ordinal),
            Mummy mummy when mummy.reviveTimer.Value > 0 => false,
            Grub grub when grub.pupating.Value => false,
            LavaLurk lurk when lurk.currentState.Value == LavaLurk.State.Submerged => false,
            RockCrab crab when crab.Sprite.currentFrame % 4 == 0 && !crab.shellGone.Value => false,
            _ => true
        };
        var canDefeat = monster switch
        {
            Spiker => false,
            Bat bat when bat.Age == 789 => false,
            Bug bug when bug.isArmoredBug.Value => enchantmentNames.Contains("BugKillerEnchantment", StringComparer.Ordinal),
            Mummy => enchantmentNames.Contains("CrusaderEnchantment", StringComparer.Ordinal),
            _ => true
        };
        var terminalEffect = monster is Mummy && !canDefeat
            ? "knockdown_requires_bomb_finish"
            : canDefeat ? "defeat" : "unavailable";
        var distribution = exactDirectDamage && currentHitCanDamage && (canDefeat || terminalEffect == "knockdown_requires_bomb_finish")
            ? BuildMeleeDamageDistribution(monster, player, weapon, enchantmentNames)
            : null;
        double? expectedAttacks = distribution is null ? null : ExpectedAttacksToDefeat(monster.Health, distribution.Entries);
        if (expectedAttacks.HasValue && !double.IsFinite(expectedAttacks.Value))
        {
            expectedAttacks = null;
        }
        var attackInterval = MeleeAttackIntervalMilliseconds(player, weapon);
        var totalDamageCompleteness = hasIndependentDamageSource || magicProjectile || dataProjectile
            ? "partial_independent_or_projectile_damage_not_composed"
            : exactDirectDamage ? "complete_direct_melee_damage_only" : "unknown_custom_damage_semantics";
        return new
        {
            slot_index = slotIndex,
            qualified_item_id = weapon.QualifiedItemId,
            weapon_type = weapon.type.Value,
            enchantment_runtime_types = enchantmentNames,
            current_hit_can_damage = currentHitCanDamage,
            can_defeat_with_this_weapon = canDefeat,
            terminal_effect = terminalEffect,
            hit_chance = distribution?.HitChance,
            expected_damage_per_attack = distribution?.ExpectedDamagePerAttack,
            expected_attacks_to_defeat = expectedAttacks,
            attack_interval_ms = attackInterval,
            expected_active_damage_duration_ms = expectedAttacks * attackInterval,
            direct_damage_distribution = distribution?.Entries.Select(pair => (object)new { damage = pair.Key, probability = pair.Value }).ToArray() ?? Array.Empty<object>(),
            direct_damage_status = distribution is null ? "unavailable" : "exact_decompiled_discrete_distribution",
            total_damage_completeness = totalDamageCompleteness,
            duration_status = distribution is not null && currentHitCanDamage && totalDamageCompleteness.StartsWith("complete", StringComparison.Ordinal)
                ? terminalEffect == "knockdown_requires_bomb_finish"
                    ? "exact_active_melee_phase_to_mummy_knockdown_excluding_movement"
                    : "exact_active_melee_phase_excluding_movement"
                : currentHitCanDamage ? "unavailable_incomplete_total_damage" : "unavailable_current_temporary_or_permanent_gate",
            source = "MeleeWeapon.setFarmerAnimating/DoDamage; GameLocation.damageMonster; Monster.takeDamage"
        };
    }

    private static MeleeDamageDistribution BuildMeleeDamageDistribution(
        Monster monster,
        Farmer player,
        MeleeWeapon weapon,
        string[] enchantmentNames)
    {
        var scaledMin = (int)((float)weapon.minDamage.Value * (1f + player.buffs.AttackMultiplier));
        var scaledMax = (int)((float)weapon.maxDamage.Value * (1f + player.buffs.AttackMultiplier));
        var baseOutcomeCount = Math.Max(1, scaledMax - scaledMin + 1);
        var criticalChance = weapon.critChance.Value;
        if (weapon.type.Value == MeleeWeapon.dagger)
        {
            criticalChance = (criticalChance + 0.005f) * 1.12f;
        }
        criticalChance *= 1f + player.buffs.CriticalChanceMultiplier;
        if (player.hasBuff("statue_of_blessings_5"))
        {
            criticalChance += 0.1f;
        }
        if (player.professions.Contains(25))
        {
            criticalChance += criticalChance * 0.5f;
        }
        criticalChance += player.LuckLevel * (criticalChance / 40f);
        var critProbability = Math.Clamp((double)criticalChance, 0d, 1d);
        var precision = (int)((float)weapon.addedPrecision.Value * (1f + player.buffs.WeaponPrecisionMultiplier)) / 10d;
        var missProbability = Math.Clamp(monster.missChance.Value - monster.missChance.Value * precision, 0d, 1d);
        var entries = new SortedDictionary<int, double>();
        AddProbability(entries, 0, missProbability);
        for (var baseDamage = scaledMin; baseDamage <= scaledMax; baseDamage++)
        {
            var baseProbability = (1d - missProbability) / baseOutcomeCount;
            AddMeleeDamageOutcome(entries, monster, player, weapon, enchantmentNames, baseDamage, false, baseProbability * (1d - critProbability));
            AddMeleeDamageOutcome(entries, monster, player, weapon, enchantmentNames, baseDamage, true, baseProbability * critProbability);
        }
        return new MeleeDamageDistribution(entries);
    }

    private static void AddMeleeDamageOutcome(
        SortedDictionary<int, double> entries,
        Monster monster,
        Farmer player,
        MeleeWeapon weapon,
        string[] enchantmentNames,
        int baseDamage,
        bool critical,
        double probability)
    {
        if (probability <= 0d)
        {
            return;
        }
        var damage = critical
            ? (int)((float)baseDamage * weapon.critMultiplier.Value * (1f + player.buffs.CriticalPowerMultiplier))
            : baseDamage;
        damage = Math.Max(1, damage + player.Attack * 3);
        if (player.professions.Contains(24))
        {
            damage = (int)Math.Ceiling((float)damage * 1.1f);
        }
        if (player.professions.Contains(26))
        {
            damage = (int)Math.Ceiling((float)damage * 1.15f);
        }
        if (critical && player.professions.Contains(29))
        {
            damage = (int)((float)damage * 2f);
        }
        foreach (var enchantment in enchantmentNames)
        {
            if (enchantment == "BugKillerEnchantment" && monster is Grub or Fly or Bug or Leaper or RockCrab)
            {
                damage = (int)((float)damage * 2f);
            }
            else if (enchantment == "CrusaderEnchantment" && monster is Ghost or Skeleton or Mummy or ShadowBrute or ShadowShaman or ShadowGirl or ShadowGuy or Shooter)
            {
                damage = (int)((float)damage * 1.5f);
            }
            else if (enchantment == "SlimeSlayerEnchantment" && monster is GreenSlime)
            {
                damage = (int)((float)damage * 1.33f + 1f);
            }
        }
        if (monster is GreenSlime slime && slime.stackedSlimes.Value > 0)
        {
            damage = 1;
        }
        damage = Math.Max(1, damage - monster.resilience.Value);
        AddProbability(entries, damage, probability);
    }

    public static double ExpectedAttacksToDefeat(int health, IReadOnlyDictionary<int, double> damageDistribution)
    {
        if (health <= 0)
        {
            return 0d;
        }
        var probabilityMass = damageDistribution.Values.Sum();
        if (damageDistribution.Any(pair => pair.Key < 0 || pair.Value < 0d) || Math.Abs(probabilityMass - 1d) > 0.000000001d)
        {
            throw new ArgumentException("Damage distribution must contain non-negative outcomes with probability mass one.", nameof(damageDistribution));
        }
        var missProbability = damageDistribution.GetValueOrDefault(0);
        if (missProbability >= 1d)
        {
            return double.PositiveInfinity;
        }
        var expected = new double[health + 1];
        for (var remaining = 1; remaining <= health; remaining++)
        {
            var continuation = damageDistribution
                .Where(pair => pair.Key > 0)
                .Sum(pair => pair.Value * expected[Math.Max(0, remaining - pair.Key)]);
            expected[remaining] = (1d + continuation) / (1d - missProbability);
        }
        return expected[health];
    }

    private static double MeleeAttackIntervalMilliseconds(Farmer player, MeleeWeapon weapon)
    {
        var swipeSpeed = ((400f - weapon.speed.Value * 40f) - player.addedSpeed * 40f) * (1f - player.buffs.WeaponSpeedMultiplier);
        return weapon.type.Value switch
        {
            MeleeWeapon.dagger => swipeSpeed * 0.5d,
            MeleeWeapon.club => swipeSpeed * 2.08d,
            _ => swipeSpeed * 0.975d
        };
    }

    private static void AddProbability(SortedDictionary<int, double> entries, int damage, double probability)
    {
        entries[damage] = entries.GetValueOrDefault(damage) + probability;
    }

    private sealed class MeleeDamageDistribution
    {
        public MeleeDamageDistribution(SortedDictionary<int, double> entries)
        {
            Entries = entries;
        }

        public SortedDictionary<int, double> Entries { get; }

        public double HitChance => 1d - Entries.GetValueOrDefault(0);

        public double ExpectedDamagePerAttack => Entries.Sum(pair => pair.Key * pair.Value);
    }

}
