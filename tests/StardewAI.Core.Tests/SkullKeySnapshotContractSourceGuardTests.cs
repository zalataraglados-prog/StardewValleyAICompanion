namespace StardewAI.Core.Tests;

public sealed class SkullKeySnapshotContractSourceGuardTests
{
    [Fact]
    public void BridgeAndCompilerUseTheSameSpecialItemField()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MiningReadAdapter.FloorState.cs");
        var compiler = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "Execution",
            "ActionQueueCompiler.Validation.Interaction.cs");

        Assert.Contains("special_item_which = 4", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "skull_key_special_item_which",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReadInt(chest, \"special_item_which\") == 4",
            compiler,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file.",
            Path.Combine(segments));
    }
}
