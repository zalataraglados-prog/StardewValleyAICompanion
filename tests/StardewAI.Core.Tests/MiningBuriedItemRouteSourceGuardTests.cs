namespace StardewAI.Core.Tests;

public sealed class MiningBuriedItemRouteSourceGuardTests
{
    [Fact]
    public void BuriedItemSourceUsesExactSelectedTileWithoutRngRead()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MiningReadAdapter.Collision.cs");
        var binding = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "MiningAuthoritativeRouteSourceBinding.BuriedItem.cs");

        Assert.Contains(
            "MineShaft.checkForBuriedItem",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "0.001575d",
            bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.random.",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "~(CollisionMask.Characters | CollisionMask.Farmers)",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "mine.IsTileOccupiedBy(",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedTileIsEligible",
            binding,
            StringComparison.Ordinal);
        Assert.Contains(
            "floorStep.TargetTileX",
            binding,
            StringComparison.Ordinal);
        Assert.Contains(
            "floorStep.TargetTileY",
            binding,
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
