using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;
using StardewAI.Contracts.State;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const int MaxProjectedMachineCraftCount = 999;

    private static object ReadMachineCraftingContext(Farmer? player)
    {
        if (player is null || CraftingRecipe.craftingRecipes is null)
        {
            return new
            {
                projection_status = "unavailable_world_or_crafting_catalog",
                machine_recipe_count = 0,
                unclassified_known_recipe_count = 0,
                unclassified_known_recipe_names = Array.Empty<string>(),
                unclassified_known_recipes = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        if (SnapshotProfileContext.Current is not ("machine" or "training_machine" or "full"))
        {
            return new
            {
                projection_status = "blocked_requires_machine_training_machine_or_full_profile",
                known_recipe_count = player.craftingRecipes.Count(),
                machine_recipe_count = 0,
                unclassified_known_recipe_count = 0,
                unclassified_known_recipe_names = Array.Empty<string>(),
                unclassified_known_recipes = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        var rows = new List<object>();
        var unclassified = new List<UnclassifiedRecipe>();
        foreach (var recipeName in player.craftingRecipes.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!CraftingRecipe.craftingRecipes.TryGetValue(recipeName, out var rawRecipe))
            {
                unclassified.Add(new UnclassifiedRecipe(recipeName, "known_recipe_missing_crafting_data_entry"));
                continue;
            }

            CraftingRecipe recipe;
            try
            {
                recipe = new CraftingRecipe(recipeName, isCookingRecipe: false);
            }
            catch (Exception ex)
            {
                unclassified.Add(new UnclassifiedRecipe(recipeName, "crafting_recipe_parse_failed:" + ex.GetType().Name));
                continue;
            }

            var outputFailures = new List<string>();
            var outputs = ReadMachineRecipeOutputs(recipe, outputFailures).ToArray();
            if (outputFailures.Count > 0)
            {
                unclassified.Add(new UnclassifiedRecipe(
                    recipeName,
                    "recipe_output_item_creation_failed:" + string.Join(",", outputFailures.OrderBy(id => id, StringComparer.Ordinal))));
                continue;
            }
            if (outputs.Length == 0)
            {
                continue;
            }

            var remaining = player.Items.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
            var firstPlan = TryConsumeOneCraft(recipe, player.Items, remaining, out var materialRows)
                ? materialRows
                : ReadUnavailableMaterialRows(recipe, player.Items);
            var craftable = ProjectCraftableCount(recipe, player.Items);
            var output = outputs[0];
            var workbenchSources = ReadWorkbenchCraftingSources(recipe, player, output.Item);
            var acceptsOutputAfterConsumption = craftable.Count > 0 &&
                InventoryAcceptsAfterConsumption(player.Items, remaining, output.Item);
            var potentialInputs = ReadPotentialMachineInputs(output, player);
            var ownedOutputSlots = player.Items
                .Select((item, index) => new { item, index })
                .Where(entry => entry.item is not null && string.Equals(entry.item.QualifiedItemId, output.QualifiedItemId, StringComparison.Ordinal))
                .Select(entry => new { slot_index = entry.index, stack = entry.item!.Stack })
                .ToArray();
            rows.Add(new
            {
                recipe_name = recipe.name,
                raw_recipe_data = rawRecipe,
                times_crafted = recipe.timesCrafted,
                output_selection_status = outputs.Length == 1 && recipe.itemToProduce.Count == 1
                    ? "exact_single_machine_output"
                    : "blocked_multiple_or_mixed_outputs",
                output_item_id = output.ItemId,
                output_qualified_item_id = output.QualifiedItemId,
                output_display_name = output.DisplayName,
                output_runtime_type = output.RuntimeType,
                output_context_tags = output.Item.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                output_big_craftable = output.BigCraftable,
                output_machine_data_status = output.MachineDataStatus,
                output_machine_data = FarmReadAdapter.ReadCompleteMachineDataSummary((output.Item as StardewValley.Object)?.GetMachineData()),
                output_count_per_craft = recipe.numberProducedPerCraft,
                output_is_cask = output.IsCask,
                placement_location_rule = output.IsCask
                    ? "Cellar_or_location_map_property_CanCaskHere"
                    : "Object.canBePlacedHere_and_GameLocation.CanItemBePlacedHere",
                known_recipe = true,
                owned_output_inventory_slots = ownedOutputSlots,
                owned_output_inventory_count = ownedOutputSlots.Sum(entry => entry.stack),
                potential_input_probe_status = potentialInputs.Status,
                potential_loadable_inputs = potentialInputs.Rows,
                potential_loadable_input_count = potentialInputs.Rows.Length,
                potential_input_probe_failures = potentialInputs.Failures,
                potential_input_probe_failure_count = potentialInputs.Failures.Length,
                ingredients_player_inventory_only = true,
                ingredient_rows = firstPlan,
                workbench_crafting_sources = workbenchSources,
                workbench_crafting_source_count = workbenchSources.Length,
                has_ingredients_for_one = craftable.Count > 0,
                craftable_count_from_player_inventory = craftable.Count,
                craftable_count_status = craftable.Capped
                    ? "bounded_at_999_requires_larger_projection"
                    : "exact_native_match_and_reverse_slot_consumption",
                output_inventory_acceptance_for_one = player.couldInventoryAcceptThisItem(output.Item),
                output_inventory_acceptance_after_material_consumption = acceptsOutputAfterConsumption,
                output_inventory_acceptance_status = craftable.Count == 0
                    ? "not_applicable_materials_unavailable"
                    : acceptsOutputAfterConsumption
                        ? "exact_acceptance_after_projected_native_consumption"
                        : "blocked_output_cannot_fit_after_projected_native_consumption",
                craft_candidate_status = outputs.Length == 1 && recipe.itemToProduce.Count == 1 && craftable.Count > 0
                    ? acceptsOutputAfterConsumption
                        ? "ready_for_native_personal_crafting_menu"
                        : "blocked_output_cannot_fit_after_material_consumption"
                    : outputs.Length != 1 || recipe.itemToProduce.Count != 1
                        ? "blocked_non_deterministic_output"
                        : "blocked_insufficient_player_inventory_materials",
                native_contract = "CraftingPage.receiveLeftClick_then_clickCraftingRecipe_then_CraftingRecipe.consumeIngredients_reverse_inventory_order"
            });
        }

        return new
        {
            projection_status = unclassified.Count == 0
                ? "complete_known_machine_recipe_projection"
                : "partial_unclassified_known_recipe_catalog_entries",
            machine_recipe_count = rows.Count,
            unclassified_known_recipe_count = unclassified.Count,
            unclassified_known_recipe_names = unclassified.Select(row => row.RecipeName).ToArray(),
            unclassified_known_recipes = unclassified.Select(row => new { recipe_name = row.RecipeName, reason = row.Reason }).ToArray(),
            max_projected_craft_count = MaxProjectedMachineCraftCount,
            scan_scope = "all_learned_recipes_from_game_start_independent_of_house_level",
            rows = rows.ToArray()
        };
    }

    private static IEnumerable<MachineRecipeOutput> ReadMachineRecipeOutputs(
        CraftingRecipe recipe,
        ICollection<string> outputFailures)
    {
        foreach (var itemId in recipe.itemToProduce)
        {
            var qualifiedId = ItemRegistry.ManuallyQualifyItemId(itemId, recipe.bigCraftable ? "(BC)" : "(O)");
            Item item;
            try
            {
                item = ItemRegistry.Create(qualifiedId, Math.Max(1, recipe.numberProducedPerCraft));
            }
            catch
            {
                outputFailures.Add(itemId);
                continue;
            }
            if (item is not StardewValley.Object obj || !obj.bigCraftable.Value || obj.GetMachineData() is null)
            {
                continue;
            }
            yield return new MachineRecipeOutput(
                item,
                item.ItemId,
                item.QualifiedItemId,
                item.DisplayName,
                item.GetType().FullName ?? item.GetType().Name,
                obj.bigCraftable.Value,
                "available_Object.GetMachineData",
                item is Cask);
        }
    }

    private static CraftCountProjection ProjectCraftableCount(CraftingRecipe recipe, IList<Item?> inventory)
    {
        var remaining = inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        var count = 0;
        while (count < MaxProjectedMachineCraftCount)
        {
            var attempt = (int[])remaining.Clone();
            if (!TryConsumeOneCraft(recipe, inventory, attempt, out _))
            {
                return new CraftCountProjection(count, false);
            }
            remaining = attempt;
            count++;
        }

        var overflowAttempt = (int[])remaining.Clone();
        return new CraftCountProjection(count, TryConsumeOneCraft(recipe, inventory, overflowAttempt, out _));
    }

    private static bool TryConsumeOneCraft(
        CraftingRecipe recipe,
        IList<Item?> inventory,
        int[] remainingStacks,
        out object[] materialRows)
    {
        var rows = new List<object>();
        var allSatisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var requiredRemaining = ingredient.Value;
            var consumed = new List<object>();
            var availableBefore = MatchingStackTotal(inventory, remainingStacks, ingredient.Key);
            for (var slot = inventory.Count - 1; slot >= 0 && requiredRemaining > 0; slot--)
            {
                var item = inventory[slot];
                if (remainingStacks[slot] <= 0 || !CraftingRecipe.ItemMatchesForCrafting(item, ingredient.Key))
                {
                    continue;
                }
                var amount = Math.Min(requiredRemaining, remainingStacks[slot]);
                remainingStacks[slot] -= amount;
                requiredRemaining -= amount;
                consumed.Add(new
                {
                    slot_index = slot,
                    qualified_item_id = item!.QualifiedItemId,
                    amount
                });
            }
            rows.Add(new
            {
                requirement_id_or_category = ingredient.Key,
                required_count = ingredient.Value,
                available_count_before_this_ingredient = availableBefore,
                satisfied = requiredRemaining == 0,
                reverse_slot_consumption_plan = consumed.ToArray()
            });
            if (requiredRemaining > 0)
            {
                allSatisfied = false;
            }
        }
        materialRows = rows.ToArray();
        return allSatisfied;
    }

    private static object[] ReadUnavailableMaterialRows(CraftingRecipe recipe, IList<Item?> inventory)
    {
        var stacks = inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        TryConsumeOneCraft(recipe, inventory, stacks, out var rows);
        return rows;
    }

    private static int MatchingStackTotal(IList<Item?> inventory, int[] remainingStacks, string requirement)
    {
        var total = 0;
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            if (remainingStacks[slot] > 0 && CraftingRecipe.ItemMatchesForCrafting(inventory[slot], requirement))
            {
                total += remainingStacks[slot];
            }
        }
        return total;
    }

    private static bool InventoryAcceptsAfterConsumption(IList<Item?> inventory, int[] remainingStacks, Item output)
    {
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            var item = inventory[slot];
            if (item is null || remainingStacks[slot] <= 0)
            {
                return true;
            }
            if (item.canStackWith(output) &&
                item.maximumStackSize() - remainingStacks[slot] >= output.Stack)
            {
                return true;
            }
        }
        return false;
    }

    private static object[] ReadWorkbenchCraftingSources(
        CraftingRecipe recipe,
        Farmer player,
        Item output)
    {
        var farm = Game1.getFarm();
        if (farm is null)
        {
            return Array.Empty<object>();
        }

        var graph = FarmReadAdapter.ReadMaterialInventoryGraph(farm, player);
        var locations = MachineLocationTopology.ReadPersistentLocations(farm, player)
            .GroupBy(
                row => row.Location.NameOrUniqueName,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Location,
                StringComparer.Ordinal);
        var rows = new List<object>();
        foreach (var link in graph.WorkbenchLinks)
        {
            if (!locations.TryGetValue(link.LocationId, out var location) ||
                !location.objects.TryGetValue(new Microsoft.Xna.Framework.Vector2(link.TileX, link.TileY), out var value) ||
                value is not Workbench)
            {
                rows.Add(new
                {
                    workbench_access_point_id = link.WorkbenchAccessPointId,
                    location_id = link.LocationId,
                    tile_x = link.TileX,
                    tile_y = link.TileY,
                    projection_status = "blocked_workbench_identity_drifted",
                    blocking_reasons = new[] { "workbench_identity_drifted" },
                    native_container_node_ids = link.NativeContainerNodeIds,
                    ingredient_rows = Array.Empty<object>(),
                    craftable_count = 0,
                    craftable_count_status = "unavailable",
                    output_inventory_acceptance_after_material_consumption = false,
                    craft_candidate_status = "blocked_workbench_identity_drifted"
                });
                continue;
            }

            var nativeChests = FarmReadAdapter.WorkbenchChestOffsets
                .Select(offset => new Microsoft.Xna.Framework.Vector2(link.TileX, link.TileY) + offset)
                .Where(tile => location.objects.TryGetValue(tile, out var adjacent) &&
                    adjacent is Chest chest &&
                    chest.SpecialChestType is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest)
                .Select(tile => (Chest)location.objects[tile])
                .ToArray();
            var inventories = nativeChests
                .Select(chest => (IInventory)chest.Items)
                .ToArray();
            var topologyMatches =
                link.ProjectionStatus == "exact_native_container_order" &&
                inventories.Length == link.NativeContainerNodeIds.Length;
            var playerRemaining = player.Items.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
            var containerRemaining = inventories
                .Select(inventory => inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray())
                .ToArray();
            object[]? ingredientRows = null;
            var satisfied = topologyMatches &&
                TryConsumeOneWorkbenchCraft(
                    recipe,
                    player.Items,
                    inventories,
                    link.NativeContainerNodeIds,
                    playerRemaining,
                    containerRemaining,
                    out ingredientRows);
            ingredientRows ??= Array.Empty<object>();
            var craftable = topologyMatches
                ? ProjectWorkbenchCraftableCount(
                    recipe,
                    player.Items,
                    inventories,
                    link.NativeContainerNodeIds)
                : new CraftCountProjection(0, false);
            var acceptsOutput = satisfied &&
                InventoryAcceptsAfterConsumption(player.Items, playerRemaining, output);
            var reasons = link.BlockingReasons
                .Concat(topologyMatches
                    ? Array.Empty<string>()
                    : new[] { "workbench_native_container_topology_drifted" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            rows.Add(new
            {
                workbench_access_point_id = link.WorkbenchAccessPointId,
                location_id = link.LocationId,
                tile_x = link.TileX,
                tile_y = link.TileY,
                projection_status = topologyMatches
                    ? "exact_native_player_then_container_reverse_slot_consumption"
                    : "blocked_workbench_topology_or_ownership",
                blocking_reasons = reasons,
                native_container_node_ids = link.NativeContainerNodeIds,
                ingredient_rows = ingredientRows,
                has_ingredients_for_one = satisfied,
                craftable_count = craftable.Count,
                craftable_count_status = craftable.Capped
                    ? "bounded_at_999_requires_larger_projection"
                    : "exact_native_player_then_container_reverse_slot_consumption",
                output_inventory_acceptance_after_material_consumption = acceptsOutput,
                craft_candidate_status = topologyMatches && satisfied
                    ? acceptsOutput
                        ? "ready_for_native_workbench_crafting_menu"
                        : "blocked_output_cannot_fit_after_material_consumption"
                    : reasons.Length > 0
                        ? "blocked_workbench_topology_or_ownership"
                        : "blocked_insufficient_workbench_materials",
                native_contract = "Workbench.checkForAction->MultipleMutexRequest->CraftingPage(materialContainers)->CraftingRecipe.consumeIngredients"
            });
        }
        return rows.ToArray();
    }

    private static CraftCountProjection ProjectWorkbenchCraftableCount(
        CraftingRecipe recipe,
        IList<Item?> playerInventory,
        IReadOnlyList<IInventory> containers,
        IReadOnlyList<string> containerNodeIds)
    {
        var playerRemaining = playerInventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        var containerRemaining = containers
            .Select(inventory => inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray())
            .ToArray();
        var count = 0;
        while (count < MaxProjectedMachineCraftCount)
        {
            var nextPlayer = (int[])playerRemaining.Clone();
            var nextContainers = containerRemaining.Select(row => (int[])row.Clone()).ToArray();
            if (!TryConsumeOneWorkbenchCraft(
                    recipe,
                    playerInventory,
                    containers,
                    containerNodeIds,
                    nextPlayer,
                    nextContainers,
                    out _))
            {
                return new CraftCountProjection(count, false);
            }
            playerRemaining = nextPlayer;
            containerRemaining = nextContainers;
            count++;
        }

        var overflowPlayer = (int[])playerRemaining.Clone();
        var overflowContainers = containerRemaining.Select(row => (int[])row.Clone()).ToArray();
        return new CraftCountProjection(
            count,
            TryConsumeOneWorkbenchCraft(
                recipe,
                playerInventory,
                containers,
                containerNodeIds,
                overflowPlayer,
                overflowContainers,
                out _));
    }

    private static bool TryConsumeOneWorkbenchCraft(
        CraftingRecipe recipe,
        IList<Item?> playerInventory,
        IReadOnlyList<IInventory> containers,
        IReadOnlyList<string> containerNodeIds,
        int[] playerRemaining,
        int[][] containerRemaining,
        out object[]? materialRows)
    {
        var rows = new List<object>();
        var allSatisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var required = ingredient.Value;
            var available = MatchingStackTotal(playerInventory, playerRemaining, ingredient.Key);
            for (var index = 0; index < containers.Count; index++)
            {
                available += MatchingStackTotal(containers[index], containerRemaining[index], ingredient.Key);
            }

            var consumed = new List<object>();
            ConsumeWorkbenchIngredient(
                playerInventory,
                playerRemaining,
                ingredient.Key,
                ref required,
                "player:" + Game1.player.UniqueMultiplayerID,
                consumed);
            for (var index = 0; index < containers.Count && required > 0; index++)
            {
                ConsumeWorkbenchIngredient(
                    containers[index],
                    containerRemaining[index],
                    ingredient.Key,
                    ref required,
                    containerNodeIds[index],
                    consumed);
            }
            rows.Add(new
            {
                requirement_id_or_category = ingredient.Key,
                required_count = ingredient.Value,
                available_count_before_this_ingredient = available,
                satisfied = required == 0,
                native_consumption_plan = consumed.ToArray()
            });
            allSatisfied &= required == 0;
        }
        materialRows = rows.ToArray();
        return allSatisfied;
    }

    private static void ConsumeWorkbenchIngredient(
        IList<Item?> inventory,
        int[] remaining,
        string requirement,
        ref int required,
        string sourceNodeId,
        ICollection<object> consumed)
    {
        for (var slot = inventory.Count - 1; slot >= 0 && required > 0; slot--)
        {
            var item = inventory[slot];
            if (item is null ||
                remaining[slot] <= 0 ||
                !CraftingRecipe.ItemMatchesForCrafting(item, requirement))
            {
                continue;
            }
            var amount = Math.Min(required, remaining[slot]);
            remaining[slot] -= amount;
            required -= amount;
            consumed.Add(new
            {
                source_node_id = sourceNodeId,
                slot_index = slot,
                qualified_item_id = item.QualifiedItemId,
                amount
            });
        }
    }

    private static PotentialMachineInputProjection ReadPotentialMachineInputs(MachineRecipeOutput output, Farmer player)
    {
        if (SnapshotProfileContext.Current is not ("machine" or "training_machine" or "full"))
        {
            return new PotentialMachineInputProjection(
                "blocked_requires_machine_training_machine_or_full_profile",
                Array.Empty<object>(),
                Array.Empty<object>());
        }
        if (Game1.getFarm() is not { } farm)
        {
            return new PotentialMachineInputProjection("unavailable_farm_topology", Array.Empty<object>(), Array.Empty<object>());
        }

        var contexts = MachineLocationTopology.ReadPersistentLocations(farm, player)
            .Where(location => location.IsPlayerControlled && !Utility.isPlacementForbiddenHere(location.Location))
            .ToArray();
        if (contexts.Length == 0)
        {
            return new PotentialMachineInputProjection("unavailable_player_controlled_machine_context", Array.Empty<object>(), Array.Empty<object>());
        }
        var rows = new List<object>();
        var failures = new List<object>();
        for (var slot = 0; slot < player.Items.Count; slot++)
        {
            var input = player.Items[slot];
            if (input is not StardewValley.Object)
            {
                continue;
            }

            var acceptingLocations = new List<string>();
            var acceptingContexts = new List<object>();
            foreach (var context in contexts)
            {
                try
                {
                    var probe = ItemRegistry.Create<StardewValley.Object>(output.QualifiedItemId);
                    probe.Location = context.Location;
                    probe.TileLocation = Microsoft.Xna.Framework.Vector2.Zero;
                    if (probe is Cask cask && !cask.IsValidCaskLocation())
                    {
                        continue;
                    }
                    if (probe.performObjectDropInAction(input, probe: true, player))
                    {
                        acceptingLocations.Add(context.Location.NameOrUniqueName);
                        acceptingContexts.Add(new
                        {
                            location_id = context.Location.NameOrUniqueName,
                            predicted_output = FarmReadAdapter.ReadPredictedMachineOutput(probe, input)
                        });
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new
                    {
                        slot_index = slot,
                        qualified_item_id = input.QualifiedItemId,
                        location_id = context.Location.NameOrUniqueName,
                        exception_type = ex.GetType().Name,
                        reason = "detached_machine_native_input_probe_exception"
                    });
                }
            }
            if (acceptingLocations.Count > 0)
            {
                rows.Add(new
                {
                    slot_index = slot,
                    item_id = input.ItemId,
                    qualified_item_id = input.QualifiedItemId,
                    stack = input.Stack,
                    quality = input.Quality,
                    accepting_location_ids = acceptingLocations.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    accepting_location_count = acceptingLocations.Count,
                    accepting_contexts = acceptingContexts.ToArray(),
                    probe_source = "detached_machine.performObjectDropInAction(probe:true)_across_native_player_controlled_location_topology"
                });
            }
        }
        return new PotentialMachineInputProjection(
            failures.Count == 0
                ? "complete_native_probe_across_player_controlled_persistent_location_topology"
                : "partial_native_probe_exceptions_fail_closed",
            rows.ToArray(),
            failures.ToArray());
    }

    private sealed record MachineRecipeOutput(
        Item Item,
        string ItemId,
        string QualifiedItemId,
        string DisplayName,
        string RuntimeType,
        bool BigCraftable,
        string MachineDataStatus,
        bool IsCask);

    private sealed record CraftCountProjection(int Count, bool Capped);

    private sealed record PotentialMachineInputProjection(string Status, object[] Rows, object[] Failures);

    private sealed record UnclassifiedRecipe(string RecipeName, string Reason);
}
