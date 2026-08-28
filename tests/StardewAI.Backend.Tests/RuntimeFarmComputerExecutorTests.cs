using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFarmComputerExecutorTests
{
    [Fact]
    public void TransparentProjectionUsesEveryNativeReportSource()
    {
        var source = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.FarmComputer.cs");

        Assert.Contains("rootLocation.getTotalCrops()", source);
        Assert.Contains("rootLocation.getTotalOpenHoeDirt()", source);
        Assert.Contains("rootLocation.getTotalCropsReadyForHarvest()", source);
        Assert.Contains("rootLocation.getTotalUnwateredCrops()", source);
        Assert.Contains("rootLocation.getTotalGreenhouseCropsReadyForHarvest()", source);
        Assert.Contains("rootLocation.getTotalForageItems()", source);
        Assert.Contains("rootLocation.getNumberOfMachinesReadyForHarvest()", source);
        Assert.Contains("farm?.doesFarmCaveNeedHarvesting()", source);
        Assert.Contains("rootLocation.piecesOfHay.Value", source);
        Assert.Contains("rootLocation.GetHayCapacity()", source);
    }

    [Fact]
    public void RuntimeUsesOneNativeLocationActionAndVerifiesDelayedDialogue()
    {
        var source = RuntimeHarnessSources.File("ModEntry.FarmComputer.cs");

        Assert.Contains("StartFarmComputerReport(pending);", RuntimeHarnessSources.All);
        Assert.Contains("AdvanceNativeObjectInteractionMovement(", source);
        Assert.Contains("active, \"farm_computer\"", source);
        Assert.Equal(1, Count(source, "active.Location.checkAction("));
        Assert.Contains("DialogueBox", source);
        Assert.Contains("ReportSha256", source);
        Assert.DoesNotContain("Game1.multipleDialogues(", source);
        Assert.DoesNotContain("activeClickableMenu =", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("farming.read_farm_computer_report"));
        Assert.Contains("new StardewObject(target.Value.ToVector2(), \"239\")",
            RuntimeHarnessSources.File("ModEntry.FarmComputerFixture.cs"));
        Assert.Contains("row.HasValue && row.Value.Value is not null",
            RuntimeHarnessSources.File("ModEntry.FarmComputerFixture.cs"));
        Assert.Contains("\"debug.setup_farm_computer\"",
            RuntimeHarnessSources.File("ModEntry.SupportedOptions.cs"));
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
