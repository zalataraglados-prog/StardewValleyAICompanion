namespace StardewAI.Backend.Tests;

public sealed class RuntimePlayerCustomizationExecutorTests
{
    [Fact]
    public void ProductionExecutorUsesOnlyNativeCustomizationInputs()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness",
            "ModEntry.PlayerCustomization.cs"));
        Assert.Contains("AdvanceNativeObjectInteractionMovement", source, StringComparison.Ordinal);
        Assert.Contains("checkAction(", source, StringComparison.Ordinal);
        Assert.Contains("answerDialogue", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("nativeEvent.receiveMouseClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.changeGender(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.changeSkinColor(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.changeHairStyle(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.changeAccessory(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReceiveMakeOver(", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Game1\.player\.Money\s*(?:\+=|-=|=(?!=))", source);
        Assert.DoesNotContain("activeDialogueEvents[\"DesertMakeover\"] =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentProjectionUsesTheFestivalReplacementLocation()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "StardewAI.TransparentBridge",
            "Adapters", "PlayerReadAdapter.Customization.cs"));
        Assert.Contains("Game1.getLocationFromName(\"DesertFestival\") as DesertFestival", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.getLocationFromName(\"Desert\") as DesertFestival", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
