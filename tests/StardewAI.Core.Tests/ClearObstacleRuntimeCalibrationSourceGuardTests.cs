namespace StardewAI.Core.Tests;

public sealed class ClearObstacleRuntimeCalibrationSourceGuardTests
{
    [Fact]
    public void FixtureOnlyConstructsNativeObstacleState()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ClearObstacleFixture.cs");
        var fixture = Slice(
            source,
            "private TrainingExecutionResult ExecuteSetupClearObstacle(",
            "private static void EnsureClearObstacleFixtureTool(");

        Assert.Contains("\"grass\" or \"twig\" or \"seed_spot\" or \"artifact_spot\"", fixture, StringComparison.Ordinal);
        Assert.Contains("ItemRegistry.Create<StardewValley.Object>", fixture, StringComparison.Ordinal);
        Assert.Contains("\"twig\" => \"(O)294\"", fixture, StringComparison.Ordinal);
        Assert.Contains("\"seed_spot\" => \"(O)SeedSpot\"", fixture, StringComparison.Ordinal);
        Assert.Contains("_ => \"(O)590\"", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyClearanceTool", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gainExperience", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debris.Add", fixture, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArtifactSpotVerifierAcceptsProjectedTerrainWithoutWeakeningOtherObstacles()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.ObstacleClearance.cs");

        Assert.Contains("var targetClearanceCompleted = targetIsArtifactSpot", source, StringComparison.Ordinal);
        Assert.Contains("? !location.objects.ContainsKey(target.ToVector2())", source, StringComparison.Ordinal);
        Assert.Contains(": after == \"clear\";", source, StringComparison.Ordinal);
        Assert.Contains("targetTerrainFeatureAfter == expectedTerrainFeatureAfter", source, StringComparison.Ordinal);
        Assert.Contains("ClearanceOutputDeltaMatches(", source, StringComparison.Ordinal);
        Assert.Contains("active.Lifecycle.Advance(ObserveNativeToolAction())", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyClearanceTool", source, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeBindsNativeProjectionFieldsFromFreshSnapshot()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeClearObstacleSmoke.ps1");

        Assert.Contains("ValidateSet(\"grass\", \"twig\", \"seed_spot\", \"artifact_spot\")", smoke, StringComparison.Ordinal);
        Assert.Contains("clear_obstacle_executor_status", smoke, StringComparison.Ordinal);
        Assert.Contains("clear_output_items_json", smoke, StringComparison.Ordinal);
        Assert.Contains("tool_slot_index", smoke, StringComparison.Ordinal);
        Assert.Contains("artifact_spots_dug_expected_after", smoke, StringComparison.Ordinal);
        Assert.Contains("clear_terrain_feature_expected_after", smoke, StringComparison.Ordinal);
        Assert.Contains("defense_book_mail_expected_after", smoke, StringComparison.Ordinal);
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source[startIndex..endIndex];
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ??
                throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments)));
    }
}
