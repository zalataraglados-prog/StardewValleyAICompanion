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
        var projector = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmSitePreparationProjector.cs"));
        var farmAdapter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.cs"));

        Assert.Contains("\"farm_layout\"", modEntry, StringComparison.Ordinal);
        Assert.Contains("(long)maxX - minX + 1 > 32", modEntry, StringComparison.Ordinal);
        Assert.Contains("(long)maxY - minY + 1 > 32", modEntry, StringComparison.Ordinal);
        Assert.Contains("sitePreparationBounds.MinX", modEntry, StringComparison.Ordinal);
        Assert.Contains("CollectFarmSitePreparation", currentLocation, StringComparison.Ordinal);
        Assert.Contains("FarmSitePreparationProjector.ReadContext", currentLocation,
            StringComparison.Ordinal);
        Assert.Contains("FarmSitePreparationProjector.ReadContext", farmAdapter,
            StringComparison.Ordinal);
        Assert.Contains("Where(clump =>", projector, StringComparison.Ordinal);
        Assert.Contains("if (x == bounds.MaxX)", projector, StringComparison.Ordinal);
        Assert.Contains("if (y == bounds.MaxY)", projector, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "x <= bounds.MaxX",
            projector,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "y <= bounds.MaxY",
            projector,
            StringComparison.Ordinal);
        Assert.DoesNotContain("makeHoeDirt(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", projector, StringComparison.Ordinal);

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
