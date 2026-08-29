using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadTerrainFeatureDetails(Vector2 tile, TerrainFeature feature)
    {
        if (feature is FruitTree fruitTree)
        {
            return ReadFruitTreeDetails(tile, fruitTree);
        }

        if (feature is Flooring flooring)
        {
            return ReadFlooringDetails(tile, flooring);
        }

        if (feature is Grass grass)
        {
            return new
            {
                tile_x = (int)tile.X,
                tile_y = (int)tile.Y,
                type = grass.GetType().FullName,
                grass_type = grass.grassType.Value,
                number_of_weeds = grass.numberOfWeeds.Value,
                is_passable = true,
                source = "Grass live net fields"
            };
        }

        if (feature is HoeDirt dirt)
        {
            return ReadHoeDirtDetails(tile, dirt);
        }

        if (feature is not Tree tree)
        {
            return new
            {
                tile_x = (int)tile.X,
                tile_y = (int)tile.Y,
                type = feature.GetType().FullName
            };
        }

        var bestAxe = Game1.player.Items.OfType<Axe>()
            .OrderByDescending(axe => axe.UpgradeLevel)
            .FirstOrDefault();
        var mossExperience = tree.hasMoss.Value ? ProjectMossStack() : 0;
        var foragingExperience = tree.growthStage.Value >= 5
            ? (tree.stump.Value ? 2 : 16) + mossExperience
            : 0;
        var expectedHits = bestAxe is null || tree.tapped.Value
            ? (int?)null
            : ExpectedTreeAxeHits(tree, bestAxe.UpgradeLevel);

        var treeProduct = ProjectWildTreeProduct(tree);

        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            type = feature.GetType().FullName,
            runtime_type = feature.GetType().FullName,
            is_tree = true,
            tree_type = tree.treeType.Value,
            growth_stage = tree.growthStage.Value,
            health = tree.health.Value,
            stump = tree.stump.Value,
            tapped = tree.tapped.Value,
            fertilized = tree.fertilized.Value,
            has_moss = tree.hasMoss.Value,
            stop_growing_moss = tree.stopGrowingMoss.Value,
            has_seed = tree.hasSeed.Value,
            was_shaken_today = tree.wasShakenToday.Value,
            max_shake = tree.maxShake,
            is_temporary_green_rain_tree = tree.isTemporaryGreenRainTree.Value,
            tree_product_harvest_status = treeProduct.Status,
            tree_product_data_contract_status = treeProduct.DataContractStatus,
            tree_product_branch = treeProduct.Branch,
            tree_product_seed_item_id = treeProduct.SeedItemId,
            tree_product_primary_qualified_item_id = treeProduct.PrimaryQualifiedItemId,
            tree_product_primary_quality = treeProduct.PrimaryQuality,
            tree_product_primary_quantity = treeProduct.PrimaryQuantity,
            tree_product_guaranteed_outputs = treeProduct.GuaranteedOutputs,
            tree_product_optional_output_domain = treeProduct.OptionalOutputDomain,
            tree_product_output_distribution_status = treeProduct.OutputDistributionStatus,
            tree_product_expected_has_seed_after = false,
            tree_product_expected_was_shaken_today_after = true,
            tree_product_expected_foraging_experience_delta = 0,
            tree_product_safe_slot_index = treeProduct.SafeSlotIndex,
            tree_product_restore_slot_index = treeProduct.RestoreSlotIndex,
            tree_product_projection_status = treeProduct.ProjectionStatus,
            tree_product_native_contract = WildTreeProductNativeContract,
            tree_treatment_required_qualified_item_id = "(O)419",
            tree_treatment_native_allowed = tree.GetType() == typeof(Tree) && !tree.stopGrowingMoss.Value,
            tree_treatment_executor_status = tree.GetType() != typeof(Tree)
                ? "blocked_custom_tree_runtime_type"
                : tree.stopGrowingMoss.Value
                    ? "blocked_moss_growth_already_stopped"
                    : "ready_if_vinegar_inventory_source_available",
            tree_treatment_effect = "has_moss=false;stop_growing_moss=true",
            moss_stack_on_next_axe_hit = mossExperience,
            best_axe_upgrade_level = bestAxe?.UpgradeLevel,
            expected_axe_hits_to_clear = expectedHits,
            tree_clear_executor_status = tree.GetType() != typeof(Tree)
                ? "blocked_custom_tree_runtime_type"
                : tree.tapped.Value
                    ? "blocked_tapped_tree"
                    : bestAxe is null
                        ? "blocked_axe_missing"
                        : "ready",
            harvest_experience_skill_id = "foraging",
            harvest_experience_skill_index = Farmer.foragingSkill,
            harvest_experience_on_success_min = foragingExperience,
            harvest_experience_on_success_max = foragingExperience,
            harvest_experience_condition = tree.growthStage.Value >= 5
                ? tree.stump.Value
                    ? "native_player_axe_destroys_mature_tree_stump_including_current_moss"
                    : "native_player_axe_fells_mature_tree_and_destroys_resulting_stump_including_current_moss"
                : "native_young_tree_clear_has_no_skill_experience",
            harvest_experience_projection_status = tree.GetType() == typeof(Tree)
                ? "exact_from_decompiled_native_tree_branches"
                : "unavailable_custom_tree_runtime_type",
            source = "Tree live net fields; Data/WildTrees locked base 1.6.15; Tree.performUseAction/shake; Tree.performToolAction/performTreeFall/CreateMossItem; Object.placementAction (O)419"
        };
    }

    private static object ReadHoeDirtDetails(Vector2 tile, HoeDirt dirt)
    {
        var crop = dirt.crop;
        var isGinger = crop is not null &&
            crop.forageCrop.Value &&
            string.Equals(crop.whichForageCrop.Value, Crop.forageCrop_gingerID, StringComparison.Ordinal);
        var hoeEntry = Game1.player.Items
            .Select((item, index) => new { Item = item, Index = index })
            .FirstOrDefault(entry => entry.Item is Hoe);
        var hoe = hoeEntry?.Item as Hoe;
        var exactVanillaRuntimeTypes = dirt.GetType() == typeof(HoeDirt) &&
            crop?.GetType() == typeof(Crop) &&
            hoe?.GetType() == typeof(Hoe);
        var energyCost = hoe?.isEfficient.Value == true
            ? 0f
            : Math.Max(0f, 2f - Game1.player.FarmingLevel * 0.1f);
        var gingerOutputContextTags = isGinger
            ? ItemRegistry.Create("(O)829")
                .GetContextTags()
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        var harvestStatus = !isGinger
            ? "not_ginger"
            : hoe is null
                ? "blocked_hoe_missing"
                : !exactVanillaRuntimeTypes
                    ? "blocked_custom_ginger_runtime_type"
                    : Game1.player.Stamina < energyCost
                        ? "blocked_insufficient_energy"
                        : "ready";

        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            type = dirt.GetType().FullName,
            hoe_dirt_state = dirt.state.Value,
            has_crop = crop is not null,
            crop_runtime_type = crop?.GetType().FullName,
            crop_is_forage = crop?.forageCrop.Value ?? false,
            forage_crop_id = crop?.whichForageCrop.Value ?? string.Empty,
            is_ginger = isGinger,
            ginger_harvest_status = harvestStatus,
            ginger_required_tool_kind = "Hoe",
            ginger_tool_slot_index = hoeEntry?.Index,
            ginger_tool_qualified_item_id = hoe?.QualifiedItemId,
            ginger_tool_upgrade_level = hoe?.UpgradeLevel,
            ginger_tool_is_efficient = hoe?.isEfficient.Value,
            ginger_exact_vanilla_runtime_types = exactVanillaRuntimeTypes,
            ginger_energy_cost = energyCost,
            ginger_output_qualified_item_id = isGinger ? "(O)829" : string.Empty,
            ginger_output_context_tags = gingerOutputContextTags,
            ginger_output_quality = isGinger ? 0 : (int?)null,
            ginger_output_quantity_min = isGinger ? 1 : 0,
            ginger_output_quantity_max = isGinger ? 1 : 0,
            ginger_output_delivery = isGinger ? "native_world_debris_then_automatic_pickup_possible" : "not_applicable",
            ginger_foraging_experience_on_success_min = isGinger ? 7 : 0,
            ginger_foraging_experience_on_success_max = isGinger ? 7 : 0,
            ginger_crop_expected_after = isGinger ? "none" : "unchanged",
            ginger_hoe_dirt_expected_after = isGinger ? "present" : "unchanged",
            ginger_hoe_dirt_state_expected_after = isGinger ? (Game1.currentLocation.IsRainingHere() ? 1 : 0) : dirt.state.Value,
            ginger_projection_status = isGinger
                ? exactVanillaRuntimeTypes ? "exact_from_native_crop_hit_with_hoe" : "unavailable_custom_runtime_type"
                : "not_applicable",
            source = "HoeDirt live net fields; Crop.hitWithHoe; HoeDirt.performToolAction; Hoe.DoFunction"
        };
    }

    private static int ExpectedTreeAxeHits(Tree tree, int axeUpgradeLevel)
    {
        if (tree.growthStage.Value < 3)
        {
            return 1;
        }

        var matureDamage = axeUpgradeLevel switch
        {
            0 => 1f,
            1 => 1.25f,
            2 => 1.67f,
            3 => 2.5f,
            4 => 5f,
            _ => axeUpgradeLevel + 1f
        };
        var youngTreeDamage = axeUpgradeLevel switch
        {
            0 => 2f,
            1 => 2.5f,
            2 => 3.34f,
            3 => 5f,
            4 => 10f,
            _ => 10f + (axeUpgradeLevel - 4)
        };
        var damage = tree.growthStage.Value >= 5 ? matureDamage : youngTreeDamage;
        var currentPhaseHits = Math.Max(1, (int)Math.Ceiling(Math.Max(0f, tree.health.Value) / damage));
        if (tree.growthStage.Value < 5 || tree.stump.Value)
        {
            return currentPhaseHits;
        }

        return currentPhaseHits + Math.Max(1, (int)Math.Ceiling(5f / matureDamage));
    }

    private static int ProjectMossStack()
    {
        var random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.Get("mossHarvested") * 50);
        return random.Next(1, 3);
    }
}
