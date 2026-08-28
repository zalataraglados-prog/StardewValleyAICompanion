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
using System.Text.Json;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter : ReadAdapterBase
{
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
                is_placed_staircase = qualifiedId == "(BC)71",
                tool_requirement = breakableStone ? "pickaxe" : container ? "heavy_hitter" : qualifiedId == "(BC)71" ? "none_staircase" : "unknown_or_interact",
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
                mining_experience_skill_index = breakableStone ? Farmer.miningSkill : (int?)null,
                mining_experience_on_break_min = dropProjection?.MiningExperienceMinimum,
                mining_experience_on_break_max = dropProjection?.MiningExperienceMaximum,
                mining_experience_condition = dropProjection?.MiningExperienceCondition ?? "not_applicable",
                mining_experience_projection_status = dropProjection?.MiningExperienceProjectionStatus ?? "not_applicable",
                luck_experience_skill_index = breakableStone ? Farmer.luckSkill : (int?)null,
                luck_experience_on_break_min = dropProjection?.LuckExperienceMinimum,
                luck_experience_on_break_max = dropProjection?.LuckExperienceMaximum,
                luck_experience_condition = dropProjection?.LuckExperienceCondition ?? "not_applicable",
                luck_experience_projection_status = dropProjection?.LuckExperienceProjectionStatus ?? "not_applicable",
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
                combat_experience_on_defeat = mine.IsFarm
                    ? Math.Max(1, monster.ExperienceGained / 3)
                    : monster.ExperienceGained,
                combat_experience_skill_index = Farmer.combatSkill,
                combat_experience_condition = "native_monster_death_attributed_to_player",
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
                possible_drop_items = drops.PossibleDropItems,
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
                source = "Monster live fields including GameLocation.monsterDrop combat XP semantics; " + drops.Source + "; future AI is handled by after-snapshot replanning, not guessed"
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
                var exactVanillaType = clump.GetType() == typeof(ResourceClump);
                var supported = exactVanillaType &&
                    IsMiningResourceClumpParentIndex(index) &&
                    !string.IsNullOrWhiteSpace(requirement.ToolKind);
                Tool? tool = requirement.ToolKind == "axe"
                    ? player.Items.OfType<Axe>()
                        .OrderByDescending(candidate =>
                            NativeToolPowerProjection.EffectiveUpgradeLevel(
                                candidate))
                        .FirstOrDefault()
                    : requirement.ToolKind == "pickaxe"
                        ? player.Items.OfType<Pickaxe>()
                            .OrderByDescending(candidate =>
                                NativeToolPowerProjection
                                    .EffectiveUpgradeLevel(candidate))
                            .FirstOrDefault()
                        : null;
                var additionalPower = tool is null
                    ? (int?)null
                    : NativeToolPowerProjection.AdditionalPower(tool);
                var effectiveUpgradeLevel = tool is null
                    ? (int?)null
                    : NativeToolPowerProjection.EffectiveUpgradeLevel(tool);
                var gateSatisfied = supported &&
                    tool is not null &&
                    effectiveUpgradeLevel >= requirement.MinimumUpgradeLevel;
                var damagePerHit = gateSatisfied
                    ? NativeToolPowerProjection.ResourceClumpDamage(tool!)
                    : (float?)null;
                var health = clump.health.Value;
                var coreOutputs = supported
                    ? ProjectMiningResourceClumpCoreOutputs(clump)
                    : Array.Empty<ClearanceOutputItemProjection>();
                var possibleSecretNoteId =
                    mine.HasUnlockedAreaSecretNotes(player)
                        ? "(O)79"
                        : string.Empty;
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
                    selected_tool_additional_power = additionalPower,
                    selected_tool_effective_upgrade_level =
                        effectiveUpgradeLevel,
                    native_executor_supported = supported,
                    tool_gate_satisfied = gateSatisfied,
                    damage_per_hit = damagePerHit,
                    expected_hits_remaining = damagePerHit.HasValue ? (int)Math.Ceiling(health / damagePerHit.Value) : (int?)null,
                    expected_core_output_items = coreOutputs,
                    expected_core_output_items_json =
                        JsonSerializer.Serialize(coreOutputs),
                    core_output_projection_status = supported
                        ? "exact_vanilla_destroy_core_outputs"
                        : "unavailable_unsupported_runtime_type_or_parent_sheet_index",
                    possible_secret_note_qualified_item_id =
                        possibleSecretNoteId,
                    secret_note_outer_roll_probability =
                        possibleSecretNoteId.Length > 0 ? 0.05 : 0,
                    secret_note_projection_status =
                        possibleSecretNoteId.Length > 0
                            ? "bounded_global_rng_optional_identity_not_realized"
                            : "exact_secret_notes_not_unlocked",
                    executor_status = !supported
                        ? exactVanillaType
                            ? "blocked_unsupported_resource_clump_parent_sheet_index"
                            : "blocked_custom_resource_clump_runtime_type"
                        : gateSatisfied
                            ? "native_executor_available"
                            : "blocked_missing_required_tool_or_upgrade",
                    source = "GameLocation.resourceClumps; ResourceClump.performToolAction exact parentSheetIndex tool gate and health formula"
                };
            })
            .ToArray();
    }

    private static ClearanceOutputItemProjection[]
        ProjectMiningResourceClumpCoreOutputs(ResourceClump clump)
    {
        return clump.parentSheetIndex.Value switch
        {
            ResourceClump.boulderIndex => new[]
            {
                ClearanceOutputItemProjection.FromStandard("(O)390", 15)
            },
            ResourceClump.quarryBoulderIndex or
            ResourceClump.mineRock1Index or
            ResourceClump.mineRock2Index or
            ResourceClump.mineRock3Index or
            ResourceClump.mineRock4Index => new[]
            {
                ClearanceOutputItemProjection.FromStandard("(O)390", 10)
            },
            ResourceClump.meteoriteIndex =>
                ProjectMeteoriteCoreOutputs(clump),
            _ => Array.Empty<ClearanceOutputItemProjection>()
        };
    }

    private static ClearanceOutputItemProjection[]
        ProjectMeteoriteCoreOutputs(ResourceClump clump)
    {
        var outputs = new List<ClearanceOutputItemProjection>
        {
            ClearanceOutputItemProjection.FromStandard("(O)386", 10),
            ClearanceOutputItemProjection.FromStandard("(O)390", 8),
            ClearanceOutputItemProjection.FromStandard("(O)749", 2)
        };
        var random = Utility.CreateRandom(
            Game1.uniqueIDForThisGame,
            clump.Tile.X,
            clump.Tile.Y * 983728f);
        if (random.NextDouble() < 0.25)
        {
            outputs.Add(
                ClearanceOutputItemProjection.FromStandard("(O)74"));
        }
        return outputs.ToArray();
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

    private static bool IsMiningResourceClumpParentIndex(int index)
    {
        return index is
            ResourceClump.quarryBoulderIndex or
            ResourceClump.meteoriteIndex or
            ResourceClump.boulderIndex or
            ResourceClump.mineRock1Index or
            ResourceClump.mineRock2Index or
            ResourceClump.mineRock3Index or
            ResourceClump.mineRock4Index;
    }

}
