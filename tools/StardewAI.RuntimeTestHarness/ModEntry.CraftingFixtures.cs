using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string CrabPotQualifiedItemId = "(O)710";
    private const string RainTotemQualifiedItemId = "(O)681";

    private TrainingExecutionResult
        ExecuteSetupCrabPotCraftingTarget(
            TrainingExecutionRequest request)
    {
        return ExecuteSetupExactCraftingTarget(
            request,
            defaultRecipeName: "Crab Pot",
            primitiveKind: "debug_setup_crab_pot_crafting_target",
            requestedEffect:
                "player.crab_pot_crafting.recipe_ready=true",
            fixturePrefix: "crab_pot_crafting_fixture",
            learnedRecipeReason:
                "learned_crab_pot_recipe_preserved",
            validateOutput: output =>
            {
                var accepted =
                    output is StardewValley.Object &&
                    string.Equals(
                        output.QualifiedItemId,
                        CrabPotQualifiedItemId,
                        StringComparison.Ordinal);
                return accepted
                    ? (true, "native_crab_pot_output=true")
                    : (false, "requires_native_crab_pot_output");
            });
    }

    private TrainingExecutionResult
        ExecuteSetupRainTotemCraftingTarget(
            TrainingExecutionRequest request)
    {
        return ExecuteSetupExactCraftingTarget(
            request,
            defaultRecipeName: "Rain Totem",
            primitiveKind: "debug_setup_rain_totem_crafting_target",
            requestedEffect:
                "player.rain_totem_crafting.recipe_ready=true",
            fixturePrefix: "rain_totem_crafting_fixture",
            learnedRecipeReason:
                "learned_rain_totem_recipe_preserved",
            validateOutput: output =>
            {
                var accepted =
                    output is StardewValley.Object &&
                    string.Equals(
                        output.QualifiedItemId,
                        RainTotemQualifiedItemId,
                        StringComparison.Ordinal);
                return accepted
                    ? (true, "native_rain_totem_output=true")
                    : (false, "requires_native_rain_totem_output");
            });
    }

    private TrainingExecutionResult ExecuteSetupExactCraftingTarget(
        TrainingExecutionRequest request,
        string defaultRecipeName,
        string primitiveKind,
        string requestedEffect,
        string fixturePrefix,
        string learnedRecipeReason,
        Func<Item, (bool Accepted, string Detail)> validateOutput)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var recipeName = string.IsNullOrWhiteSpace(request.RecipeName)
            ? defaultRecipeName
            : request.RecipeName;
        if (!Game1.player.craftingRecipes.ContainsKey(recipeName) ||
            !CraftingRecipe.craftingRecipes.ContainsKey(recipeName))
        {
            return BlockedWithPrimitive(
                request,
                primitiveKind,
                requestedEffect,
                "recipe=" + recipeName,
                fixturePrefix + "_requires_learned_recipe");
        }

        CraftingRecipe recipe;
        Item output;
        try
        {
            recipe = new CraftingRecipe(
                recipeName,
                isCookingRecipe: false);
            output = recipe.createItem();
        }
        catch (Exception ex)
        {
            return BlockedWithPrimitive(
                request,
                primitiveKind,
                requestedEffect,
                "recipe=" + recipeName,
                fixturePrefix + "_recipe_creation_failed:" +
                ex.GetType().Name);
        }

        var outputValidation = validateOutput(output);
        if (!outputValidation.Accepted)
        {
            return BlockedWithPrimitive(
                request,
                primitiveKind,
                requestedEffect,
                "output=" + output.QualifiedItemId,
                fixturePrefix + "_" + outputValidation.Detail);
        }

        var ingredientSlots = new List<string>();
        foreach (var ingredient in recipe.recipeList)
        {
            if (!int.TryParse(ingredient.Key, out var itemId) ||
                itemId < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    primitiveKind,
                    requestedEffect,
                    "requirement=" + ingredient.Key,
                    fixturePrefix +
                    "_requires_exact_item_ingredients");
            }

            var qualifiedId = ItemRegistry.ManuallyQualifyItemId(
                itemId.ToString(),
                "(O)");
            var slot = EnsureInventoryItem(
                qualifiedId,
                ingredient.Value);
            if (slot < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    primitiveKind,
                    requestedEffect,
                    "requirement=" + qualifiedId,
                    fixturePrefix + "_inventory_full");
            }
            ingredientSlots.Add(
                qualifiedId + "@" + slot + ":" + ingredient.Value);
        }

        var ingredientsReady =
            recipe.doesFarmerHaveIngredientsInInventory();
        var outputAccepted =
            Game1.player.couldInventoryAcceptThisItem(output);
        var verified = ingredientsReady && outputAccepted;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    learnedRecipeReason,
                    "exact_recipe_ingredients_available",
                    "native_output_inventory_acceptance=true",
                    "recipe_name=" + recipeName,
                    "output_qualified_item_id=" +
                        output.QualifiedItemId,
                    outputValidation.Detail,
                    "ingredient_slots=" +
                        string.Join(",", ingredientSlots)
                }
                : new[]
                {
                    "native_recipe_ingredient_or_output_gate_failed"
                },
            RequestedEffect = requestedEffect,
            ObservedEffect =
                "recipe_name=" + recipeName +
                ";output_qualified_item_id=" +
                output.QualifiedItemId +
                ";ingredients_ready=" +
                ingredientsReady.ToString().ToLowerInvariant() +
                ";output_accepted=" +
                outputAccepted.ToString().ToLowerInvariant(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    fixturePrefix + "_verification_failed"
                }
        };
    }
}
