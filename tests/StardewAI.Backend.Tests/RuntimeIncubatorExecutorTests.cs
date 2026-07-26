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
}
