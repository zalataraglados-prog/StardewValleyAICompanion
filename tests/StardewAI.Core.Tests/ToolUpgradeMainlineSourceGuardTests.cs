using System.Text.RegularExpressions;

namespace StardewAI.Core.Tests;

public sealed class ToolUpgradeMainlineSourceGuardTests
{
    [Fact]
    public void TransparentProjectionUsesLiveNativeToolDataContract()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.ToolUpgrade.cs"));

        Assert.Contains("Game1.toolData", source, StringComparison.Ordinal);
        Assert.Contains("ConventionalUpgradeFrom", source, StringComparison.Ordinal);
        Assert.Contains("ShopBuilder.GetToolUpgradeData", source, StringComparison.Ordinal);
        Assert.Contains("player.toolBeingUpgraded.Value", source, StringComparison.Ordinal);
        Assert.Contains("player.daysLeftForToolUpgrade.Value", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeUsesNativeBlacksmithMenuWithoutDirectStateWrites()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ToolUpgrade.cs"));

        Assert.Contains("location.checkAction", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("answerDialogue", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("menu.ShopId != \"ClintUpgrade\"", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"Game1\.player\.Money\s*=(?!=)"), source);
        Assert.DoesNotMatch(new Regex(@"toolBeingUpgraded\.Value\s*=(?!=)"), source);
        Assert.DoesNotMatch(new Regex(@"daysLeftForToolUpgrade\.Value\s*=(?!=)"), source);
        Assert.DoesNotContain("removeItemFromInventory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsumeTradeItem", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("StardewAI repository root not found.");
    }
}
