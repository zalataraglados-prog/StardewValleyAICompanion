namespace StardewAI.Core.Tests;

public sealed class MiningStoneRouteSourceGuardTests
{
    [Fact]
    public void RadioactiveNodeSourceRequiresExactNodeBranchAndSelectedTile()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MiningReadAdapter.ObjectRouteSources.cs");
        var core = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "MiningAuthoritativeRouteSourceBinding.Stone.cs");

        Assert.Contains("obj.ItemId != \"95\"", bridge,
            StringComparison.Ordinal);
        Assert.Contains("game_location_break_stone_direct_node", bridge,
            StringComparison.Ordinal);
        Assert.Contains("GuaranteedDropQualifiedItemIds", bridge,
            StringComparison.Ordinal);
        Assert.Contains("native_radioactive_ore_node", bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", bridge,
            StringComparison.Ordinal);

        Assert.Contains("floorStep.TargetTileX", core,
            StringComparison.Ordinal);
        Assert.Contains("floorStep.TargetTileY", core,
            StringComparison.Ordinal);
        Assert.Contains("floorStep.TargetQualifiedItemId", core,
            StringComparison.Ordinal);
        Assert.Contains("matches.Length != 1", core,
            StringComparison.Ordinal);
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
            "Repository file not found: " + Path.Combine(segments));
    }
}
