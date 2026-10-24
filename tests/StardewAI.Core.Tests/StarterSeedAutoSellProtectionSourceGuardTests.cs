namespace StardewAI.Core.Tests;

public sealed class StarterSeedAutoSellProtectionSourceGuardTests
{
    [Fact]
    public void InventoryAndSeedProjectionSharePlantableSeedIdentity()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.cs"));

        Assert.Contains(
            "var protectionReasons = SellProtectionReasons(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!IsPlantableSeed(item, location))",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "item.Category == StardewValley.Object.SeedsCategory",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.cropData.ContainsKey(resolvedSeedId)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "plantable_seed_requires_explicit_surplus_authorization",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(relative).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file: " +
            string.Join(Path.DirectorySeparatorChar, relative));
    }
}
