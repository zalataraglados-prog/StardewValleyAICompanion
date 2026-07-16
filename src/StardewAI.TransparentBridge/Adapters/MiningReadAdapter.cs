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
            var dropProjection = breakableStone ? MiningStoneDropResolver.Resolve(mine, obj, player) : null;
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
                guaranteed_drop_qualified_item_ids = dropProjection?.GuaranteedDropQualifiedItemIds ?? Array.Empty<string>(),
                conditional_drop_qualified_item_ids = dropProjection?.ConditionalDropQualifiedItemIds ?? Array.Empty<string>(),
                guaranteed_one_of_qualified_item_id_groups = dropProjection?.GuaranteedOneOfQualifiedItemIdGroups ?? Array.Empty<string[]>(),
                possible_drop_qualified_item_ids = dropProjection?.PossibleDropQualifiedItemIds ?? Array.Empty<string>(),
                drop_rule_branch = dropProjection?.RuleBranch ?? "not_breakable_stone",
                drop_item_identity_completeness = dropProjection?.ItemIdentityCompleteness ?? "not_applicable",
                drop_probability_status = dropProjection?.ProbabilityStatus ?? "not_applicable",
                drop_rule_conditions = dropProjection?.AppliedRuleConditions ?? Array.Empty<string>(),
                ladder_preview = breakableStone ? ReadLadderPreview(mine, pair.Key, player) : null,
                source = "Object.IsBreakableStone/MinutesUntilReady; Pickaxe.DoFunction; BreakableContainer.health read-only reflection; " +
                    (dropProjection?.Source ?? "no_stone_drop_projection")
            };
        }).ToArray();
    }

    private static object[] ReadMonsters(MineShaft mine)
    {
        return mine.characters.OfType<Monster>().Select(monster =>
        {
            var box = monster.GetBoundingBox();
            var drops = MiningMonsterDropResolver.Resolve(mine, monster, Game1.player, box.Center.X / Game1.tileSize, box.Center.Y / Game1.tileSize);
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
                melee_damage_semantics = ReadMeleeDamageSemantics(monster, Game1.player),
                melee_attack_projections = ReadMeleeAttackProjections(monster, Game1.player),
                slingshot_attack_projections = ReadSlingshotAttackProjections(mine, monster, Game1.player),
                bomb_damage_semantics = ReadBombDamageSemantics(monster),
                is_hard_mode_monster = monster.isHardModeMonster.Value,
                movement_speed = monster.Speed,
                tile_manhattan_distance_to_player = Math.Abs(monster.TilePoint.X - Game1.player.TilePoint.X) + Math.Abs(monster.TilePoint.Y - Game1.player.TilePoint.Y),
                center_distance_pixels = Vector2.Distance(box.Center.ToVector2(), Game1.player.GetBoundingBox().Center.ToVector2()),
                selected_drop_item_ids = monster.objectsToDrop.ToArray(),
                selected_drop_qualified_item_ids = drops.SelectedBaseDropQualifiedItemIds,
                guaranteed_drop_qualified_item_ids = drops.GuaranteedDropQualifiedItemIds,
                conditional_drop_qualified_item_ids = drops.ConditionalDropQualifiedItemIds,
                guaranteed_one_of_qualified_item_id_groups = drops.GuaranteedOneOfQualifiedItemIdGroups,
                conditional_drop_catalog_keys = drops.ConditionalDropCatalogKeys,
                possible_drop_qualified_item_ids = drops.PossibleDropQualifiedItemIds,
                current_death_tile_preview_qualified_item_id = drops.CurrentDeathTilePreviewQualifiedItemId,
                current_death_tile_preview_status = drops.CurrentDeathTilePreviewStatus,
                runtime_extra_drop_rule_inputs = drops.RuntimeExtraDropRuleInputs,
                runtime_extra_drop_rule_completeness = drops.RuntimeExtraDropRuleCompleteness,
                drop_probability_rules = drops.DropProbabilityRules,
                drop_probability_completeness = drops.DropProbabilityCompleteness,
                primary_drop_status = drops.PrimaryDropStatus,
                drop_item_identity_completeness = drops.ItemIdentityCompleteness,
                unresolved_dynamic_drop_rules = drops.UnresolvedDynamicRules,
                has_special_item = monster.hasSpecialItem.Value,
                contact_damage_readable = true,
                behavior_observation = new
                {
                    runtime_type = monster.GetType().FullName,
                    dynamic_replan_required = true,
                    future_ai_path_not_predicted = true
                },
                source = "Monster live fields; " + drops.Source + "; future AI is handled by after-snapshot replanning, not guessed"
            };
        }).ToArray();
    }

    private static object[] ReadResourceClumps(MineShaft mine, Farmer player)
    {
        return mine.resourceClumps
            .OrderBy(clump => clump.Tile.Y)
            .ThenBy(clump => clump.Tile.X)
            .Select(clump =>
            {
                var index = clump.parentSheetIndex.Value;
                var requirement = ResourceClumpRequirement(index);
                var supported = !string.IsNullOrWhiteSpace(requirement.ToolKind);
                Tool? tool = requirement.ToolKind == "axe"
                    ? player.Items.OfType<Axe>().OrderByDescending(candidate => candidate.UpgradeLevel).FirstOrDefault()
                    : requirement.ToolKind == "pickaxe"
                        ? player.Items.OfType<Pickaxe>().OrderByDescending(candidate => candidate.UpgradeLevel).FirstOrDefault()
                        : null;
                var gateSatisfied = supported && tool is not null && tool.UpgradeLevel >= requirement.MinimumUpgradeLevel;
                var damagePerHit = gateSatisfied ? Math.Max(1f, (tool!.UpgradeLevel + 1) * 0.75f) : (float?)null;
                var health = clump.health.Value;
                return new
                {
                    tile_x = (int)clump.Tile.X,
                    tile_y = (int)clump.Tile.Y,
                    width = clump.width.Value,
                    height = clump.height.Value,
                    parent_sheet_index = index,
                    runtime_type = clump.GetType().FullName,
                    health,
                    required_tool = requirement.ToolKind,
                    minimum_upgrade_level = requirement.MinimumUpgradeLevel,
                    selected_tool_slot_index = tool is null ? (int?)null : player.Items.IndexOf(tool),
                    selected_tool_qualified_item_id = tool?.QualifiedItemId ?? string.Empty,
                    selected_tool_upgrade_level = tool?.UpgradeLevel,
                    native_executor_supported = supported,
                    tool_gate_satisfied = gateSatisfied,
                    damage_per_hit = damagePerHit,
                    expected_hits_remaining = damagePerHit.HasValue ? (int)Math.Ceiling(health / damagePerHit.Value) : (int?)null,
                    executor_status = !supported
                        ? "blocked_unsupported_resource_clump_parent_sheet_index"
                        : gateSatisfied
                            ? "native_executor_available"
                            : "blocked_missing_required_tool_or_upgrade",
                    source = "GameLocation.resourceClumps; ResourceClump.performToolAction exact parentSheetIndex tool gate and health formula"
                };
            })
            .ToArray();
    }

    private static (string ToolKind, int MinimumUpgradeLevel) ResourceClumpRequirement(int parentSheetIndex)
    {
        return parentSheetIndex switch
        {
            ResourceClump.stumpIndex => ("axe", 1),
            ResourceClump.hollowLogIndex => ("axe", 2),
            ResourceClump.quarryBoulderIndex or ResourceClump.meteoriteIndex => ("pickaxe", 3),
            ResourceClump.boulderIndex => ("pickaxe", 2),
            ResourceClump.mineRock1Index or ResourceClump.mineRock2Index or ResourceClump.mineRock3Index or ResourceClump.mineRock4Index => ("pickaxe", 0),
            _ => (string.Empty, 0)
        };
    }

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

    private static object[] IndexedTiles(xTile.Layers.Layer? layer, int tileIndex, string reason)
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
                if (layer.Tiles[x, y]?.TileIndex == tileIndex)
                {
                    tiles.Add(new { tile_x = x, tile_y = y, tile_index = tileIndex, present = true, usable = new { status = "derived", reason } });
                }
            }
        }

        return tiles.ToArray();
    }

    private static object[] MineExitTiles(xTile.Layers.Layer? layer, MineShaft mine)
    {
        if (layer is null)
        {
            return Array.Empty<object>();
        }

        var destination = mine.mineLevel == 77377
            ? new { location_id = "Mine", tile_x = 67, tile_y = 10 }
            : mine.mineLevel > 120
                ? new { location_id = "SkullCave", tile_x = 3, tile_y = 4 }
                : new { location_id = "Mine", tile_x = 23, tile_y = 8 };
        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (layer.Tiles[x, y]?.TileIndex == 115)
                {
                    tiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        tile_index = 115,
                        present = true,
                        expected_destination = destination,
                        native_question_key = "ExitMine",
                        native_response_key = "ExitMine_Leave",
                        usable = new { status = "derived", reason = "native_mineshaft_exit_tile" }
                    });
                }
            }
        }

        return tiles.ToArray();
    }

    private static object[] ShaftTiles(xTile.Layers.Layer? layer, MineShaft mine, Farmer player)
    {
        if (layer is null || mine.getMineArea() != MineShaft.desertArea || mine.mineLevel <= MineShaft.bottomOfMineLevel)
        {
            return Array.Empty<object>();
        }

        var levels = ShaftFallLevels(mine.mineLevel, Game1.uniqueIDForThisGame, Game1.Date.TotalDays);
        var damage = levels * 3;
        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (layer.Tiles[x, y]?.TileIndex == 174)
                {
                    tiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        tile_index = 174,
                        present = true,
                        expected_level_delta = levels,
                        expected_mine_level_after = mine.mineLevel + levels,
                        expected_health_cost = damage,
                        expected_health_after = Math.Max(1, player.health - damage),
                        preview_source = "MineShaft.enterMineShaft deterministic local random",
                        usable = new { status = "derived", reason = "native_mineshaft_shaft_tile" }
                    });
                }
            }
        }

        return tiles.ToArray();
    }

    public static int ShaftFallLevels(int mineLevel, ulong uniqueGameId, int totalDays)
    {
        var random = Utility.CreateRandom(mineLevel, uniqueGameId, totalDays);
        var levels = random.Next(3, 9);
        if (random.NextDouble() < 0.1)
        {
            levels = levels * 2 - 1;
        }
        if (mineLevel < 220 && mineLevel + levels > 220)
        {
            levels = 220 - mineLevel;
        }
        return levels;
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

    private sealed class ExplosiveAmmoAreaProjection
    {
        public ExplosiveAmmoAreaProjection(
            bool safe,
            string safetyStatus,
            int targetMotionMarginTiles,
            int usefulObjectHits,
            int monsterHits,
            int additionalMonsterHits,
            int protectedObjectHits,
            int protectedTerrainFeatureHits,
            int otherFarmerHits)
        {
            Safe = safe;
            SafetyStatus = safetyStatus;
            TargetMotionMarginTiles = targetMotionMarginTiles;
            UsefulObjectHits = usefulObjectHits;
            MonsterHits = monsterHits;
            AdditionalMonsterHits = additionalMonsterHits;
            ProtectedObjectHits = protectedObjectHits;
            ProtectedTerrainFeatureHits = protectedTerrainFeatureHits;
            OtherFarmerHits = otherFarmerHits;
        }

        public bool Safe { get; }
        public string SafetyStatus { get; }
        public int TargetMotionMarginTiles { get; }
        public int UsefulObjectHits { get; }
        public int MonsterHits { get; }
        public int AdditionalMonsterHits { get; }
        public int ProtectedObjectHits { get; }
        public int ProtectedTerrainFeatureHits { get; }
        public int OtherFarmerHits { get; }
        public bool HasAdditionalValue => UsefulObjectHits > 0 || AdditionalMonsterHits > 0;
    }
}
