namespace StardewAI.Backend.Tests;

public sealed class RuntimeJukeboxSelectionExecutorTests
{
    [Fact]
    public void ProductionExecutorUsesOnlyNativeJukeboxMenuInput()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness",
            "ModEntry.JukeboxSelection.cs"));
        Assert.Contains("AdvanceNativeObjectInteractionMovement", source, StringComparison.Ordinal);
        Assert.Contains("checkAction(", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMusicTrackName()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.changeMusicTrack(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("miniJukeboxTrack", source, StringComparison.Ordinal);
        Assert.DoesNotContain("songsHeard.Add", source, StringComparison.Ordinal);
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
