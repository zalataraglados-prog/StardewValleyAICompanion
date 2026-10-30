namespace StardewAI.Core.Tests;

public sealed class GingerIslandArrivalEvidenceSourceGuardTests
{
    [Fact]
    public void IslandReachedRequiresArrivalEvidenceRatherThanDepartureCounter()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.GingerIsland.cs"));
        var start = source.IndexOf("var islandReached =", StringComparison.Ordinal);
        Assert.True(start >= 0, "islandReached assignment not found");
        var end = source.IndexOf(';', start);
        Assert.True(end > start, "islandReached assignment is incomplete");
        var assignment = source[start..(end + 1)];

        Assert.Contains(
            "player.hasOrWillReceiveMail(\"Visited_Island\")",
            assignment,
            StringComparison.Ordinal);
        Assert.Contains(
            "currentLocation.StartsWith(\"Island\"",
            assignment,
            StringComparison.Ordinal);
        Assert.DoesNotContain("boatRidesToIsland", assignment, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file: " + Path.Combine(parts));
    }
}
