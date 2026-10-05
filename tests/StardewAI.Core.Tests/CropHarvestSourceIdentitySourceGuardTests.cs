namespace StardewAI.Core.Tests;

public sealed class CropHarvestSourceIdentitySourceGuardTests
{
    [Fact]
    public void ResolverKeepsOrdinaryAndWildSeedSourcesDistinct()
    {
        var source = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CropHarvestIdentityResolver.cs");

        source = source.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "crop.netSeedIndex.Value",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop.whichForageCrop.Value",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "NativeWildSeedOutputs.TryGetValue(\n" +
            "                wildSeedSourceId,",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"exact_from_live_index_of_harvest\",\n" +
            "                false,\n" +
            "                sourceSeedId",
            source,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(
                directory.FullName,
                "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Cannot find repository root."),
            Path.Combine(segments)));
    }
}
