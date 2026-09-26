namespace StardewAI.Core.Tests;

public sealed class GeodeOutputSourceGuardTests
{
    [Fact]
    public void GeodeSourcesFollowNativeDropRowsAndBoundedDefaultBranch()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.GeodeProcessing.cs");
        var selection = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteDispatchCompilationBuilder.Selection.cs");

        Assert.Contains("data.GeodeDrops", bridge, StringComparison.Ordinal);
        Assert.Contains("sourcedDrop.RowIndex", bridge, StringComparison.Ordinal);
        Assert.Contains("native_geode_drop", bridge, StringComparison.Ordinal);
        Assert.Contains(
            "native_geode_default_drop",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "projection.Primary?.QualifiedItemId == \"(O)82\"",
            bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", bridge, StringComparison.Ordinal);
        Assert.Contains(
            "candidate.geode_expected_output_qid",
            selection,
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
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
