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
        Assert.Contains("executionRequest.WorkbenchAccessPointId = workbenchAccessPointId", source);
        Assert.Contains("executionRequest.WorkbenchContainerNodeIdsJson = workbenchContainerNodeIdsJson", source);
    }

    [Fact]
    public void StorageCraftingReusesNativeMenuWithTypedRuntimeIdentity()
    {
        var dispatch = RuntimeHarnessSources.File("ModEntry.cs");
        var executor = RuntimeHarnessSources.File(
            "ModEntry.MachineCrafting.cs");
        var whitelist = RuntimeHarnessSources.File(
            "ModEntry.Shipping.Utilities.cs");

        Assert.Contains(
            "pending.Request.OptionId == \"executor.craft_storage_item\"",
            dispatch);
        Assert.Contains(
            "ExecuteCraftMachineItem(pending.Request)",
            dispatch);
        Assert.Contains(
            "request.OptionId == \"executor.craft_storage_item\"",
            executor);
        Assert.Contains(
            "request.OptionId != \"executor.craft_storage_item\"",
            whitelist);
        Assert.Contains(
            "request.OptionId != \"executor.craft_machine_item\"",
            whitelist);
    }

    [Fact]
    public void LiveTrainingDispatchChecksLatestMaterialLedgerBeforeRuntimeInput()
    {
        var execution = RuntimeHarnessSources.RepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs");
        var readiness = RuntimeHarnessSources.RepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.DispatchReadiness.cs");

        Assert.Contains("ReadDispatchReadinessAsync(", execution);
        Assert.Contains("dispatchReadiness[\"ready\"]", execution);
        Assert.Contains("BuildQueueFromDailyPlanAsync(", execution);
        Assert.Contains("/dispatch-readiness?stateHash=", readiness);
        Assert.Contains("controller_dispatch_guard", readiness);
        Assert.Contains("dispatch_rejected", readiness);
    }

    [Fact]
    public void WorkbenchRuntimeUsesNativeLocksMenuAndClicks()
    {
        var dispatch = RuntimeHarnessSources.File("ModEntry.cs");
        var executor = RuntimeHarnessSources.File("ModEntry.WorkbenchCrafting.cs");
        var bridge = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters",
            "FarmReadAdapter.MaterialInventoryGraph.cs");

        Assert.Contains("StartWorkbenchCraft(pending)", dispatch);
        Assert.Contains("active.Location.checkAction(", executor);
        Assert.Contains("active.Workbench.mutex.IsLockHeld()", executor);
        Assert.Contains("row.Chest.GetMutex().IsLockHeld()", executor);
        Assert.Contains("page.receiveLeftClick(", executor);
        Assert.Contains("Game1.exitActiveMenu()", executor);
        Assert.Contains("ProjectNativeWorkbenchIngredients", executor);
        Assert.DoesNotContain(".consumeIngredients(", executor);
        Assert.DoesNotContain(".Items.Add(", executor);
        Assert.DoesNotContain(".Items.Remove(", executor);
        Assert.Contains("NativeContainerNodeIds = nativeContainerNodeIds", bridge);
        Assert.Contains("workbench_native_container_not_owned_or_unmapped", bridge);
    }
}
