using StardewAI.Contracts.Capabilities;

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
        Assert.Contains("unit_sale_price = unitSalePrice", executor);
        Assert.Contains(
            "total_sale_value = (long)unitSalePrice * amount",
            executor);
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
            "RuntimeTestHarnessDispatchCatalog.IsSupported",
            whitelist);
        Assert.True(
            RuntimeTestHarnessDispatchCatalog.IsSupported(
                "executor.craft_storage_item"));
        Assert.True(
            RuntimeTestHarnessDispatchCatalog.IsSupported(
                "executor.craft_machine_item"));
    }

    [Fact]
    public void MachineInputCapabilityUsesNativeEffectiveRuleInference()
    {
        var bridge = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.Machines.cs");

        Assert.Contains("MachineDataHasEffectiveInput", bridge);
        Assert.Contains(
            "MachineOutputTrigger.ItemPlacedInMachine",
            bridge);
        Assert.Contains("MachineDataHasEffectiveOutput", bridge);
        Assert.Contains(
            "effective_capability_native_contract",
            bridge);
        Assert.Contains("has_input_forced", bridge);
        Assert.Contains("has_output_forced", bridge);
    }

    [Fact]
    public void MachineRemovalUsesNativeRecoverableDebrisChain()
    {
        var dispatch = RuntimeHarnessSources.File("ModEntry.cs");
        var runtime = RuntimeHarnessSources.File(
            "ModEntry.MachinePlacement.cs");
        var bridge = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.Machines.cs");

        Assert.Contains(
            "ExecuteRemoveMachine(pending.Request)",
            dispatch);
        Assert.Contains("pickaxe.DoFunction(", runtime);
        Assert.Contains(
            "MachineRemovalRuntimeBlockReasons(",
            runtime);
        Assert.Contains(
            "exact_machine_debris_created",
            runtime);
        Assert.DoesNotContain(
            "location.objects.Remove(targetVector",
            runtime);
        Assert.Contains(
            "removal_projection_fingerprint",
            bridge);
        Assert.Contains(
            "machine_removal_runtime_tool_override_not_verified",
            bridge);
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
        Assert.Contains("\"executor.remove_machine\"", readiness);
        Assert.Contains("controller_dispatch_guard", readiness);
        Assert.Contains("dispatch_rejected", readiness);

        var movement = RuntimeHarnessSources.File(
            "ModEntry.MovementSleep.cs");
        Assert.Contains("IsFarmerCenteredOnTile", movement);
        Assert.Contains("\"target_tile_centered\"", movement);
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
        Assert.Contains("page.exitThisMenuNoSound()", executor);
        Assert.Contains("ProjectNativeWorkbenchIngredients", executor);
        Assert.DoesNotContain(".ReleaseLock(", executor);
        Assert.DoesNotContain(".consumeIngredients(", executor);
        Assert.DoesNotContain(".Items.Add(", executor);
        Assert.DoesNotContain(".Items.Remove(", executor);
        Assert.Contains("NativeContainerNodeIds = nativeContainerNodeIds", bridge);
        Assert.Contains("workbench_native_container_not_owned_or_unmapped", bridge);
    }

    [Fact]
    public void WorkbenchLifecycleSmokeBindsTransparentContainerSource()
    {
        var script = RuntimeHarnessSources.RepositoryFile(
            "scripts",
            "Invoke-RuntimeMachineLifecycleSmoke.ps1");
        var fixture = RuntimeHarnessSources.File(
            "ModEntry.WorkbenchMachineLifecycleFixture.cs");

        Assert.Contains("[switch] $UseWorkbench", script);
        Assert.Contains(
            "ready_for_native_workbench_crafting_menu",
            script);
        Assert.Contains(
            "workbench_container_node_ids_json",
            script);
        Assert.Contains(
            "native_workbench_crafting_menu",
            script);
        Assert.Contains(
            "runtime-machine-lifecycle-smoke.move-to-placement",
            script);
        Assert.Contains("new Workbench(workbenchTile)", fixture);
        Assert.Contains(
            "exact_recipe_ingredients_available_in_native_workbench_chest",
            RuntimeHarnessSources.File(
                "ModEntry.MachineCrafting.cs"));
    }
}
