namespace StardewAI.Core.Tests;

public sealed class WildTreeTapperOutputSourceGuardTests
{
    [Fact]
    public void TapperSourceUsesLiveSameTileTreeAndAuthoritativeRowNumbering()
    {
        var source = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.MachineOutputSources.cs");

        Assert.Contains("machine.IsTapper()", source);
        Assert.Contains("location.terrainFeatures.TryGetValue", source);
        Assert.Contains("tree.GetType() != typeof(Tree)", source);
        Assert.Contains("tree.tapped.Value", source);
        Assert.Contains("data.TapItems[rowIndex]", source);
        Assert.Contains("native_wild_tree_tapper_output", source);
        Assert.Contains(
            "sourcePrefix + \":random:\" + randomIndex",
            source);
        Assert.DoesNotContain("Game1.random", source);
        Assert.DoesNotContain("TryGetTapperOutput(", source);
    }

    [Fact]
    public void MachineOutputFixtureRefreshesTransparentMachineCache()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MachinesAndPickup.cs");

        var machinePlacement = source.IndexOf(
            "farm.objects[tile] = machine;",
            StringComparison.Ordinal);
        var cacheRefresh = source.IndexOf(
            "RefreshTransparentMachineProbeCache();",
            machinePlacement,
            StringComparison.Ordinal);
        var fixtureVerification = source.IndexOf(
            "var verified = MachineAt(farm, target)",
            machinePlacement,
            StringComparison.Ordinal);

        Assert.True(machinePlacement >= 0);
        Assert.True(cacheRefresh > machinePlacement);
        Assert.True(fixtureVerification > cacheRefresh);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
