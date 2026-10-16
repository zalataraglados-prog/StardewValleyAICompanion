namespace StardewAI.Core.Tests;

public sealed class FarmSitePreparationSourceGuardTests
{
    [Fact]
    public void FarmLayoutProfileIsBoundedCachedAndPurposeLimited()
    {
        var root = FindRepositoryRoot();
        var modEntry = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));
        var currentLocation = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.SitePreparation.cs"));
        var farmAdapter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.cs"));

        Assert.Contains("\"farm_layout\"", modEntry, StringComparison.Ordinal);
        Assert.Contains("maxX - minX + 1 > 32", modEntry, StringComparison.Ordinal);
        Assert.Contains("maxY - minY + 1 > 32", modEntry, StringComparison.Ordinal);
        Assert.Contains("sitePreparationBounds.MinX", modEntry, StringComparison.Ordinal);
        Assert.Contains("CollectFarmSitePreparation", currentLocation, StringComparison.Ordinal);
        Assert.Contains("Where(pair => Inside(bounds, pair.Key))", currentLocation, StringComparison.Ordinal);
        Assert.DoesNotContain("makeHoeDirt(", currentLocation, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", currentLocation, StringComparison.Ordinal);

        var boundedBranch = farmAdapter.IndexOf(
            "SnapshotProfileContext.IncludesFarmSitePreparation",
            StringComparison.Ordinal);
        var broadInventoryRead = farmAdapter.IndexOf(
            "ReadCachedMaterialInventoryGraph(",
            StringComparison.Ordinal);
        Assert.True(boundedBranch >= 0 && boundedBranch < broadInventoryRead);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ??
            throw new InvalidOperationException("Cannot find repository root.");
    }
}
