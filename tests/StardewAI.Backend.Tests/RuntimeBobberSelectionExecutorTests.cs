namespace StardewAI.Backend.Tests;

public sealed class RuntimeBobberSelectionExecutorTests
{
    [Fact]
    public void ProductionExecutorUsesNativeMenuInputWithoutDirectPreferenceWrites()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness",
            "ModEntry.BobberSelection.cs"));
        Assert.Contains("AdvanceNativeObjectInteractionMovement", source, StringComparison.Ordinal);
        Assert.Contains("checkAction(", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("bobberStyle.Value != request.BobberStyleId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bobberStyle.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("usingRandomizedBobber =", source, StringComparison.Ordinal);
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
