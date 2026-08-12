namespace StardewAI.Core.Tests;

public sealed class GenericMovementClearanceBoundarySourceGuardTests
{
    [Fact]
    public void GenericMovementNeverPlansThroughAnObstacleThatRequiresASeparatePrimitive()
    {
        var movement = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));
        var clearance = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.ObstacleClearance.cs"));

        Assert.Contains("allowRemovableObstacles: false", movement, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", clearance, StringComparison.Ordinal);
        Assert.Contains(
            "Route repair is compiled as an explicit clear_obstacle primitive.",
            clearance,
            StringComparison.Ordinal);
        Assert.Contains("return false;", clearance, StringComparison.Ordinal);
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
