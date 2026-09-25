namespace StardewAI.Core.Tests;

public sealed class WildTreeChopOutputSourceGuardTests
{
    [Fact]
    public void ChopSourceUsesLockedNativeRowsWithoutConsumingRng()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.WildTreeChop.cs");
        var candidate = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "CandidateOptionAvailabilityEvaluator.WildTreeChop.cs");
        var selection = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteDispatchCompilationBuilder.Selection.cs");

        Assert.Contains("data.ChopItems", bridge, StringComparison.Ordinal);
        Assert.Contains("row.IsValidForGrowthStage", bridge, StringComparison.Ordinal);
        Assert.Contains("native_wild_tree_chop_drop", bridge, StringComparison.Ordinal);
        Assert.Contains("source.RowIndex", bridge, StringComparison.Ordinal);
        Assert.Contains(
            ".GroupBy(source => source.QualifiedItemId",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            ".OrderBy(source => source.RowIndex)",
            bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", bridge, StringComparison.Ordinal);
        Assert.Contains(
            "tree_chop_authoritative_route_sources",
            candidate,
            StringComparison.Ordinal);
        Assert.Contains(
            "CandidateDeclaresUniqueAuthoritativeRouteItem",
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
