namespace StardewAI.Backend.Tests;

public sealed class RuntimeIncubatorExecutorTests
{
    [Fact]
    public void HatchExecutorUsesNativeNamingMenuOnly()
    {
        var source = RuntimeHarnessSources.File(
            "ModEntry.Incubators.cs");

        Assert.Contains(
            "menu.textBox.Text = request.TargetName",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "menu.receiveLeftClick(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "house.addNewHatchedAnimal(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "readyIncubator.heldObject.Value = null",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "house.adoptAnimal(",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentBridgeExposesNamingAndNativeSelection()
    {
        var menuSource = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MenuReadAdapter.cs");
        var incubatorSource = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.IncubatorState.cs");

        Assert.Contains(
            "NamingMenu namingMenu",
            menuSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "done_callback_present",
            menuSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_ready_selected",
            incubatorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "AnimalHouse.objects.Values_first_ready_incubator_then_break",
            incubatorSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IncubatorValueUsesNativePurchasePriceWithoutScaling()
    {
        var source = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.IncubatorPrediction.cs");

        Assert.Contains(
            "? animalData.PurchasePrice",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "animalData.PurchasePrice *",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IncubatorCatalogEnumeratesNativeEggDataWithoutExamples()
    {
        var source = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.IncubatorCatalog.cs");

        Assert.Contains(
            "Game1.farmAnimalData",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            ".EggItemIds",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MachineDataUtility.TryGetMachineOutputRule",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FarmAnimal.TryGetAnimalDataFromEgg",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "StardewValley.Object.OutputIncubator",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "available_complete_native_data_and_rule_probe",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"(O)176\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"(O)289\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NaturalCycleUsesCopiedSaveAndProductionSleep()
    {
        var source = RuntimeHarnessSources.RepositoryFile(
            "scripts",
            "Invoke-RuntimeIncubatorNaturalCycleSmoke.ps1");

        Assert.Contains(
            "Copy-Item -LiteralPath $sourceSlotPath",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:STARDEWAI_TEST_SAVES = $runSavesPath",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"executor.load_machine_input\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"executor.sleep\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"executor.name_hatched_animal\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"debug.advance_time_to\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NaturalCycleFixturesWarpWithoutAdvancingIncubatorState()
    {
        var source = RuntimeHarnessSources.File(
            "ModEntry.Incubators.cs");
        var start = source.IndexOf(
            "ExecutePrepareIncubatorSleep(",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "ExecuteEnterReadyIncubatorHouse(",
            start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source[start..end];

        Assert.Contains(
            "Game1.warpFarmer(",
            method,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "incubator.MinutesUntilReady = 0",
            method,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "incubator.MinutesUntilReady -=",
            method,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "readyForHarvest.Value =",
            method,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "passTimeForObjects",
            method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IncubatorBirthDialogueExceptionIsNarrowlyGated()
    {
        var dialogueSource = RuntimeHarnessSources.File(
            "ModEntry.Dialogue.cs");
        var incubatorSource = RuntimeHarnessSources.File(
            "ModEntry.Incubators.cs");

        Assert.Contains(
            "\"incubator_birth_message\"",
            dialogueSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsNativeIncubatorBirthMessageOpen()",
            dialogueSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "house.currentEvent is not null",
            incubatorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine.MinutesUntilReady <= 0",
            incubatorSource,
            StringComparison.Ordinal);
    }
}
