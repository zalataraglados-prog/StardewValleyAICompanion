namespace StardewAI.Core.Tests;

public sealed class TaskAcquisitionRuntimeCalibrationSourceGuardTests
{
    [Fact]
    public void NativeCollectionTaskConstructionIsCentralized()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.CollectionTaskFixture.cs");
        var monsterFixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.QuestMonsterDropFixture.cs");

        Assert.Contains("new ResourceCollectionQuest()", fixture, StringComparison.Ordinal);
        Assert.Contains("new SpecialOrder()", fixture, StringComparison.Ordinal);
        Assert.Contains("TryInstallCollectionTaskFixture(", monsterFixture, StringComparison.Ordinal);
        Assert.DoesNotContain("new ResourceCollectionQuest()", monsterFixture, StringComparison.Ordinal);
        Assert.DoesNotContain("new SpecialOrder()", monsterFixture, StringComparison.Ordinal);
    }

    [Fact]
    public void GreenRainFixtureUsesVanillaResourceClumpAndNativeExecutor()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.GreenRainResourceClumpFixture.cs");
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeGreenRainResourceClumpSmoke.ps1");

        Assert.Contains("new ResourceClump(", fixture, StringComparison.Ordinal);
        Assert.Contains("ResourceClump.greenRainBush1Index", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventory", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "option_id = \"executor.break_current_location_resource_clump\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "expected_core_output_items_json",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "debug.setup_collection_task_fixture",
            smoke,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Invoke-RuntimeHarvestCropSmoke.ps1")]
    [InlineData("Invoke-RuntimeGiantCropSmoke.ps1")]
    [InlineData("Invoke-RuntimeGreenRainResourceClumpSmoke.ps1")]
    [InlineData("Invoke-RuntimeFishingDailyPlanSmoke.ps1")]
    [InlineData("Invoke-RuntimePickupDebrisSmoke.ps1")]
    [InlineData("Invoke-RuntimeForageTaskSourceSmoke.ps1")]
    public void SourceCalibrationScriptsPreserveNativeTaskRoles(string fileName)
    {
        var smoke = ReadRepositoryFile("scripts", fileName);

        Assert.Contains("ordinary_quest", smoke, StringComparison.Ordinal);
        Assert.Contains("special_order", smoke, StringComparison.Ordinal);
        Assert.Contains("debug.setup_collection_task_fixture", smoke, StringComparison.Ordinal);
        Assert.Contains("quest_acquisition_source_step", smoke, StringComparison.Ordinal);
        Assert.Contains("quest_acquisition_target_step", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void ForageFixtureConstructsNativeSourcesWithoutHarvestingOrCreditingTasks()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ForageSourceFixture.cs");

        Assert.Contains("new Bush(", fixture, StringComparison.Ordinal);
        Assert.Contains("new HoeDirt(", fixture, StringComparison.Ordinal);
        Assert.Contains("new Crop(", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventory", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("performToolAction", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checkAction", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".shake(", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".harvest(", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OnItemReceived", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onItemCollected", fixture, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForageSmokeBuildsExecutionFromTransparentNativeProjection()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeForageTaskSourceSmoke.ps1");

        Assert.Contains("current_location.large_terrain_features.value", smoke, StringComparison.Ordinal);
        Assert.Contains("current_location.terrain_features.value", smoke, StringComparison.Ordinal);
        Assert.Contains("current_location.debris.value", smoke, StringComparison.Ordinal);
        Assert.Contains("bush_output_qualified_item_id", smoke, StringComparison.Ordinal);
        Assert.Contains("ginger_tool_slot_index", smoke, StringComparison.Ordinal);
        Assert.Contains("expected_foraging_experience_delta", smoke, StringComparison.Ordinal);
        Assert.Contains("\"executor.harvest_bush\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"executor.harvest_ginger\"", smoke, StringComparison.Ordinal);
        Assert.Contains("option_id = \"executor.pickup_debris\"", smoke, StringComparison.Ordinal);
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
