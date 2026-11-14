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

        Assert.Contains("\"grass\" or \"weeds\" or \"twig\" or \"seed_spot\" or \"artifact_spot\" or \"tree_moss\"", fixture, StringComparison.Ordinal);
        Assert.Contains("ItemRegistry.Create<StardewValley.Object>", fixture, StringComparison.Ordinal);
        Assert.Contains("\"weeds\" => \"(O)784\"", fixture, StringComparison.Ordinal);
        Assert.Contains("fixtureKind == \"weeds\" ? \"scythe\"", fixture, StringComparison.Ordinal);
        Assert.Contains("\"weeds\" => observedObstacle.IsWeeds() && observedObstacle.QualifiedItemId == \"(O)784\"", fixture, StringComparison.Ordinal);
        Assert.Contains("\"twig\" => \"(O)294\"", fixture, StringComparison.Ordinal);
        Assert.Contains("\"stone\" => \"(O)343\"", fixture, StringComparison.Ordinal);
        Assert.Contains("!createdObstacle.IsBreakableStone()", fixture, StringComparison.Ordinal);
        Assert.Contains("\"stone\" => observedObstacle.IsBreakableStone()", fixture, StringComparison.Ordinal);
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

        Assert.Contains("var targetClearanceCompleted = targetIsTreeMoss", source, StringComparison.Ordinal);
        Assert.Contains("? TreeMossHarvestCompleted(active)", source, StringComparison.Ordinal);
        Assert.Contains(": targetIsArtifactSpot", source, StringComparison.Ordinal);
        Assert.Contains("? !location.objects.ContainsKey(target.ToVector2())", source, StringComparison.Ordinal);
        Assert.Contains(": after == \"clear\";", source, StringComparison.Ordinal);
        Assert.Contains("targetTerrainFeatureAfter == expectedTerrainFeatureAfter", source, StringComparison.Ordinal);
        Assert.Contains("ClearanceOutputDeltaMatches(", source, StringComparison.Ordinal);
        Assert.Contains("active.Lifecycle.Advance(ObserveNativeToolAction())", source, StringComparison.Ordinal);
        Assert.Contains("active.SwingCount == 1", source, StringComparison.Ordinal);
        Assert.Contains("TreeMossNativeToolTraceMatches(", source, StringComparison.Ordinal);
        Assert.Contains("before.Length == after.Length", source, StringComparison.Ordinal);
        Assert.Contains(":foraging_delta=0:moss_stat_delta=0", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyClearanceTool", source, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeBindsNativeProjectionFieldsFromFreshSnapshot()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeClearObstacleSmoke.ps1");

        Assert.Contains("ValidateSet(\"grass\", \"weeds\", \"twig\", \"seed_spot\", \"artifact_spot\", \"tree_moss\", \"tree_chop\")", smoke, StringComparison.Ordinal);
        Assert.Contains("if ($FixtureKind -eq \"weeds\" -and", smoke, StringComparison.Ordinal);
        Assert.Contains("$targetObject.qualified_item_id -ne \"(O)784\"", smoke, StringComparison.Ordinal);
        Assert.Contains("$FixtureKind -notin @(\"grass\", \"weeds\")", smoke, StringComparison.Ordinal);
        Assert.Contains("clear_obstacle_executor_status", smoke, StringComparison.Ordinal);
        Assert.Contains("clear_output_items_json", smoke, StringComparison.Ordinal);
        Assert.Contains("tool_slot_index", smoke, StringComparison.Ordinal);
        Assert.Contains("artifact_spots_dug_expected_after", smoke, StringComparison.Ordinal);
        Assert.Contains("clear_terrain_feature_expected_after", smoke, StringComparison.Ordinal);
        Assert.Contains("defense_book_mail_expected_after", smoke, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL", smoke, StringComparison.Ordinal);
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
