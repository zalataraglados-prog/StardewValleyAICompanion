namespace StardewAI.Core.Tests;

public sealed class DebrisQualifiedIdentitySourceGuardTests
{
    [Theory]
    [InlineData("CurrentLocationReadAdapter.Debris.cs")]
    [InlineData("FarmReadAdapter.Entities.cs")]
    public void DebrisWithoutMaterializedItemStillPublishesQualifiedIdentity(
        string fileName)
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            fileName));

        Assert.Contains(
            "ItemRegistry.QualifyItemId(debris.itemId.Value)",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ??
                throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments));
    }
}
