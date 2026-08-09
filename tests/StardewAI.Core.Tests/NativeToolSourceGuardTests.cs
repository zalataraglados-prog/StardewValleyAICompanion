namespace StardewAI.Core.Tests;

public sealed class NativeToolSourceGuardTests
{
    [Fact]
    public void RuntimeFarmToolExecutorsUseNativeToolFunctions()
    {
        var source = RuntimeHarnessSources.All;

        Assert.DoesNotContain("HoeDirt.watered", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DoFunction(farm", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".DoFunction(farm", source, StringComparison.Ordinal);
        Assert.DoesNotContain("while (Game1.player.TilePoint != next", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.EndUsingTool()", source, StringComparison.Ordinal);
        Assert.Equal(2, source.Split("CleanupBlockedNativeToolLifecycle(tool);", StringSplitOptions.None).Length - 1);
        Assert.Contains("tool.Lifecycle.Phase == NativeToolActionPhase.Ready", source, StringComparison.Ordinal);
        Assert.Contains("tool.Lifecycle.Advance(ObserveNativeToolAction())", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.completelyStopAnimatingOrDoingAction();", source, StringComparison.Ordinal);
        Assert.Contains("\"water_crop\"", source, StringComparison.Ordinal);
        Assert.Contains("\"till_soil\"", source, StringComparison.Ordinal);

        var executionSource = Slice(source, "private void StartWaterCrop", "private static TrainingExecutionResult NativeToolBlocked");
        Assert.DoesNotContain("Game1.player.Position =", executionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("currentLocation = farm", executionSource, StringComparison.Ordinal);
        Assert.Contains("request.LocationId, location.NameOrUniqueName", executionSource, StringComparison.Ordinal);
        Assert.Contains("BuildAdjacentToolPath(location, target", executionSource, StringComparison.Ordinal);
        Assert.Contains("ValidateWaterCropTarget(Game1.currentLocation, tool.Target", executionSource, StringComparison.Ordinal);
        Assert.Contains("ValidateTillSoilTarget(Game1.getFarm(), tool.Target", executionSource, StringComparison.Ordinal);
        Assert.Contains("CompleteNativeTool(tool);", executionSource, StringComparison.Ordinal);
        Assert.Contains("? !tool.BeforeWatered.GetValueOrDefault() && IsCropWatered(location, tool.Target)", executionSource, StringComparison.Ordinal);
        Assert.Contains(": !tool.BeforeHadHoeDirt.GetValueOrDefault() && farm.terrainFeatures.TryGetValue", executionSource, StringComparison.Ordinal);
        Assert.Contains("Status = verified ? \"applied\" : \"blocked\"", executionSource, StringComparison.Ordinal);

        var wateringFixtureSource = Slice(source, "private TrainingExecutionResult ExecuteSetupWateringTarget", "private TrainingExecutionResult ExecuteSetupTillSoilTarget");
        Assert.Contains("Path = \"player.location_id\"", wateringFixtureSource, StringComparison.Ordinal);
        Assert.Contains("Path = \"player.tile\"", wateringFixtureSource, StringComparison.Ordinal);
        Assert.Contains("player.location_id=Farm", wateringFixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Path = \"player.location\"", wateringFixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("player.location=Farm", wateringFixtureSource, StringComparison.Ordinal);

        var tillFixtureSource = Slice(source, "private TrainingExecutionResult ExecuteSetupTillSoilTarget", "private static Point? FindTillSoilFixtureTarget");
        Assert.Contains("Path = \"player.location_id\"", tillFixtureSource, StringComparison.Ordinal);
        Assert.Contains("Path = \"player.tile\"", tillFixtureSource, StringComparison.Ordinal);
        Assert.Contains("player.location_id=Farm", tillFixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Path = \"player.location\"", tillFixtureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("player.location=Farm", tillFixtureSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GiantCropHarvestReusesNativePerFrameResourceClumpLifecycle()
    {
        var dispatch = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var fixtureSource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FarmFixtures.cs"));
        var miningSource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MiningResources.cs"));
        var giantCropExecution = Slice(
            fixtureSource,
            "private void StartHarvestGiantCrop",
            "private static GiantCrop? GiantCropAt");

        Assert.Contains("StartHarvestGiantCrop(pending);", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteHarvestGiantCrop", dispatch, StringComparison.Ordinal);
        Assert.Contains("activeResourceClump = new ActiveResourceClump", giantCropExecution, StringComparison.Ordinal);
        Assert.Contains("request.ResourceClumpTileX", giantCropExecution, StringComparison.Ordinal);
        Assert.Contains("request.StandTileX", giantCropExecution, StringComparison.Ordinal);
        Assert.Contains("request.ToolSlotIndex", giantCropExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction", giantCropExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceClumps.Remove", giantCropExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("while (GiantCropAt", giantCropExecution, StringComparison.Ordinal);
        Assert.Contains("if (active.IsGiantCrop)", miningSource, StringComparison.Ordinal);
        Assert.Contains("CompleteGiantCrop(active, nativeToolTrace);", miningSource, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker: " + startMarker);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing source marker: " + endMarker);
        return source[start..end];
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }
}
