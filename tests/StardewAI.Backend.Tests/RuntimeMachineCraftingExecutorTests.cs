namespace StardewAI.Backend.Tests;

public sealed class RuntimeMachineCraftingExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeCraftingPageAndVerifiesExactDeltas()
    {
        var dispatch = RuntimeHarnessSources.File("ModEntry.cs");
        var executor = RuntimeHarnessSources.File("ModEntry.MachineCrafting.cs");

        Assert.Contains("ExecuteCraftMachineItem(pending.Request)", dispatch);
        Assert.Contains("new CraftingPage(", executor);
        Assert.Contains("page.receiveLeftClick(pair.Key.bounds.Center.X", executor);
        Assert.Contains("page.receiveLeftClick(target.X, target.Y", executor);
        Assert.Contains("ProjectNativePersonalCraftIngredients", executor);
        Assert.Contains("exact_ingredient_and_output_multiset_verified", executor);
        Assert.Contains("native_recipe_count_increment_verified", executor);
        Assert.DoesNotContain("consumeIngredients(", executor);
        Assert.DoesNotContain("addItemToInventory", executor);
        Assert.DoesNotContain("craftingRecipes[request.RecipeName] =", executor);
    }

    [Fact]
    public void TrainingRequestCarriesReboundCraftingContract()
    {
        var source = RuntimeHarnessSources.RepositoryFile("tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs");

        Assert.Contains("executionRequest.RecipeName = recipeName", source);
        Assert.Contains("executionRequest.OutputQualifiedItemId = outputQualifiedItemId", source);
        Assert.Contains("executionRequest.IngredientRowsJson = ingredientRowsJson", source);
        Assert.Contains("executionRequest.TimesCraftedBefore = timesCraftedBefore", source);
    }
}
