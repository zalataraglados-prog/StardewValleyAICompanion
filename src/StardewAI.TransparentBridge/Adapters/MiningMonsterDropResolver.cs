using StardewValley;
using System.Globalization;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal static partial class MiningMonsterDropResolver
{
    public const string RandomCosmeticCatalogKey = "utility_random_cosmetic_item";

    public const string HardMineTreasureCatalogKey = "mine_hard_special_treasure_room";

    public const string NaturalTrinketCatalogKey = "natural_monster_trinkets";

    public static MiningMonsterDropProjection Resolve(MineShaft mine, Monster monster, Farmer player, int deathTileX, int deathTileY)
    {
        var selected = monster.objectsToDrop.Select(QualifyDropId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var guaranteed = new HashSet<string>(StringComparer.Ordinal);
        var conditional = new HashSet<string>(StringComparer.Ordinal);
        var guaranteedOneOf = new List<string[]>();
        var conditionalCatalogKeys = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new List<string>();
        var primaryStatus = "base_selected_drops";

        if (monster.hasSpecialItem.Value)
        {
            var special = PreviewSpecialItem(mine, deathTileX, deathTileY);
            conditional.UnionWith(PossibleSpecialItems(mine));
            if (Game1.mine is not null && Game1.mine.GetAdditionalDifficulty() > 0)
            {
                conditionalCatalogKeys.Add(HardMineTreasureCatalogKey);
            }
            if (string.IsNullOrWhiteSpace(special))
            {
                primaryStatus = "special_item_replaces_base_drops_current_tile_uses_shared_treasure_catalog";
            }
            else
            {
                conditional.Add(special);
                primaryStatus = "special_item_replaces_base_drops_current_death_tile_preview";
            }

            var projection = Build(
                selected,
                guaranteed,
                conditional,
                guaranteedOneOf,
                conditionalCatalogKeys,
                primaryStatus,
                "complete_possible_identity_set_with_shared_catalogs",
                unresolved);
            projection.CurrentDeathTilePreviewQualifiedItemId = special ?? string.Empty;
            projection.CurrentDeathTilePreviewStatus = string.IsNullOrWhiteSpace(special)
                ? "unavailable_global_rng_not_consumed"
                : "available_for_current_death_tile";
            projection.RuntimeExtraDropRuleInputs = ReadRuntimeExtraDropRuleInputs(monster, player);
            projection.RuntimeExtraDropRuleCompleteness = "not_executed_special_item_override";
            projection.DropProbabilityRules = SpecialItemProbabilityRules(special);
            projection.DropProbabilityCompleteness = string.IsNullOrWhiteSpace(special)
                ? "complete_primary_branch_event;treasure_catalog_item_selection_not_previewed"
                : "complete_current_death_tile_primary_branch";
            return projection;
        }

        var pendantOverrideEligible = mine.mineLevel > 121 &&
            player.getFriendshipHeartLevelForNPC("Krobus") >= 10 &&
            player.HouseUpgradeLevel >= 1 &&
            !player.isMarriedOrRoommates() &&
            !player.isEngaged();
        var baseBranchChance = pendantOverrideEligible ? 0.999d : 1d;
        if (pendantOverrideEligible)
        {
            conditional.UnionWith(selected);
            conditional.Add("(O)808");
            primaryStatus = "base_drops_or_rare_krobus_pendant_override";
        }
        else
        {
            guaranteed.UnionWith(selected);
        }

        AddRuntimeTypeExtraDrops(monster, guaranteed, conditional, guaranteedOneOf, unresolved);
        if (pendantOverrideEligible)
        {
            conditional.UnionWith(guaranteed);
            guaranteed.Clear();
            foreach (var group in guaranteedOneOf)
            {
                conditional.UnionWith(group);
            }
            guaranteedOneOf.Clear();
        }
        AddBaseMonsterDropPossibilities(monster, player, conditional, conditionalCatalogKeys);
        var result = Build(
            selected,
            guaranteed,
            conditional,
            guaranteedOneOf,
            conditionalCatalogKeys,
            primaryStatus,
            unresolved.Count == 0 ? "complete_possible_identity_set_for_vanilla_mineshaft_monster" : "partial_death_time_dynamic_sources",
            unresolved);
        result.RuntimeExtraDropRuleInputs = ReadRuntimeExtraDropRuleInputs(monster, player);
        result.RuntimeExtraDropRuleCompleteness = monster.GetType().Assembly == typeof(Monster).Assembly
            ? "complete_for_vanilla_runtime_type"
            : "partial_custom_runtime_type";
        result.DropProbabilityRules = ReadBaseProbabilityRules(monster, player, selected, pendantOverrideEligible, baseBranchChance);
        result.DropProbabilityCompleteness = monster.GetType().Assembly == typeof(Monster).Assembly
            ? "complete_current_snapshot_vanilla_runtime_type_and_common_event_probabilities;position_seeded_rules_require_replan_after_movement;weighted_catalog_identity_probability_in_shared_catalog"
            : "partial_common_game_location_rules_exact;custom_runtime_type_probabilities_unavailable";
        return result;
    }

    private static MiningMonsterDropProjection Build(
        string[] selected,
        HashSet<string> guaranteed,
        HashSet<string> conditional,
        List<string[]> guaranteedOneOf,
        HashSet<string> conditionalCatalogKeys,
        string primaryStatus,
        string completeness,
        List<string> unresolved)
    {
        var possible = Ordered(guaranteed.Concat(conditional).Concat(guaranteedOneOf.SelectMany(group => group)));
        return new MiningMonsterDropProjection
        {
            SelectedBaseDropQualifiedItemIds = selected,
            GuaranteedDropQualifiedItemIds = Ordered(guaranteed),
            ConditionalDropQualifiedItemIds = Ordered(conditional),
            GuaranteedOneOfQualifiedItemIdGroups = guaranteedOneOf.Select(Ordered).ToArray(),
            ConditionalDropCatalogKeys = Ordered(conditionalCatalogKeys),
            PossibleDropQualifiedItemIds = possible,
            PossibleDropItems = ProjectItems(possible),
            PrimaryDropStatus = primaryStatus,
            ItemIdentityCompleteness = completeness,
            UnresolvedDynamicRules = unresolved.Distinct(StringComparer.Ordinal).OrderBy(rule => rule, StringComparer.Ordinal).ToArray(),
            Source = "MineShaft.monsterDrop; GameLocation.monsterDrop; Monster.objectsToDrop/getExtraDropItems; MineShaft.getSpecialItemForThisMineLevel"
        };
    }

    private static MiningDropItemProjection[] ProjectItems(IEnumerable<string> qualifiedItemIds)
    {
        return qualifiedItemIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(itemId => itemId, StringComparer.Ordinal)
            .Select(itemId =>
            {
                try
                {
                    var item = ItemRegistry.Create(itemId);
                    return new MiningDropItemProjection
                    {
                        QualifiedItemId = item.QualifiedItemId,
                        ContextTags = item.GetContextTags()
                            .OrderBy(tag => tag, StringComparer.Ordinal)
                            .ToArray(),
                        ContextTagStatus = "exact_item_get_context_tags"
                    };
                }
                catch
                {
                    return new MiningDropItemProjection
                    {
                        QualifiedItemId = itemId,
                        ContextTagStatus = "unavailable_item_registry_resolution_failed"
                    };
                }
            })
            .ToArray();
    }

}
