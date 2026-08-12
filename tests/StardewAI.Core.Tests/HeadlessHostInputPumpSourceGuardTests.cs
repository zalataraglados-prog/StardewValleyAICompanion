namespace StardewAI.Core.Tests;

public sealed class HeadlessHostInputPumpSourceGuardTests
{
    [Fact]
    public void HeadlessHostKeepsNativeControlInputActiveWithoutSynthesizingActions()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.HeadlessInput.cs"));

        Assert.Contains("HeadlessMovementInputSimulator", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SUPPRESS_LOCAL_RENDER", source, StringComparison.Ordinal);
        Assert.Contains("GameInputSimulatorField.SetValue(null, headlessInputPump)", source, StringComparison.Ordinal);
        Assert.Contains("moveLeftHeld = direction == 3", source, StringComparison.Ordinal);
        Assert.Contains("moveLeftReleased = previousDirection == 3", source, StringComparison.Ordinal);
        Assert.DoesNotContain("actionButtonPressed =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", source, StringComparison.Ordinal);
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
