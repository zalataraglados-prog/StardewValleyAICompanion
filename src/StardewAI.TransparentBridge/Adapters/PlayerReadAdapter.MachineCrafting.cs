using StardewValley;
using StardewValley.Objects;
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
