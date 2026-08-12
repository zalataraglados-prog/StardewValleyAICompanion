namespace StardewAI.Core.Tests;

public sealed class DedicatedHostClockRecoverySourceGuardTests
{
    [Fact]
    public void DedicatedHostOnlyClearsStaleNetworkPauseAfterNativeClockGatesPass()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.DedicatedHostRuntime.cs"));

        Assert.Contains("if (!IsVanillaAiHostMode()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.isFestival()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.CurrentEvent?.isWedding == true", source, StringComparison.Ordinal);
        Assert.Contains("Game1.farmEvent is not null", source, StringComparison.Ordinal);
        Assert.Contains("Game1.activeClickableMenu is not null", source, StringComparison.Ordinal);
        Assert.Contains("player.requestingTimePause.Value = false", source, StringComparison.Ordinal);
        Assert.Contains("Game1.netWorldState.Value.IsTimePaused = false", source, StringComparison.Ordinal);
        Assert.DoesNotContain("!player.CanMove && !player.UsingTool && !player.forceTimePass", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.timeOfDay =", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
