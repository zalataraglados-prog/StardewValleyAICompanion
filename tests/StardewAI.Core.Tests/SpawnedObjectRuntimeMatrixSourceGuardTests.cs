using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class SpawnedObjectRuntimeMatrixSourceGuardTests
{
    [Fact]
    public void FixtureAndExactOutputFieldsSurviveRequestTransport()
    {
        var request = new TrainingExecutionRequest
        {
            FixtureSpawnedObjectProfile = "gatherer_duplicate",
            ExpectedOutputQuality = 4,
            ExpectedForagingExperienceDelta = 14,
            ExpectedFarmingExperienceDelta = 3
        };

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            JsonSerializer.Serialize(request, options),
            options);

        Assert.NotNull(roundTrip);
        Assert.Equal("gatherer_duplicate", roundTrip.FixtureSpawnedObjectProfile);
        Assert.Equal(4, roundTrip.ExpectedOutputQuality);
        Assert.Equal(14, roundTrip.ExpectedForagingExperienceDelta);
        Assert.Equal(3, roundTrip.ExpectedFarmingExperienceDelta);
    }

    [Fact]
    public void FixtureAndSmokeCoverTheFiveNativePickupProfiles()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.SpawnedObjectFixture.cs");
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeSpawnedObjectSmoke.ps1");

        foreach (var profile in new[]
        {
            "ordinary",
            "botanist",
            "gatherer_duplicate",
            "special_724519",
            "farm_interior"
        })
        {
            Assert.Contains(profile, fixture, StringComparison.Ordinal);
            Assert.Contains(profile, smoke, StringComparison.Ordinal);
        }
        Assert.Contains("Utility.CreateDaySaveRandom", fixture);
        Assert.Contains("GetHarvestSpawnedObjectQuality", fixture);
        Assert.Contains("new AnimalHouse(\"Maps\\\\Coop\"", fixture);
        Assert.Contains("$env:SMAPI_MODS_PATH = $smokeModsPath", smoke);
        Assert.Contains("-WindowStyle Hidden", smoke);
    }

    [Fact]
    public void RuntimeRequiresTransportedProjectionAndUsesNativeCheckAction()
    {
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Foraging.cs");
        var plan = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "Training",
            "DailyPlanCompiler.Foraging.cs");
        var loop = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs");

        Assert.Contains("active.Location.checkAction(", runtime);
        Assert.Contains("request.ExpectedOutputQuality != expectedQuality", runtime);
        Assert.Contains("request.ExpectedFarmingExperienceDelta != expectedFarmingExperience", runtime);
        Assert.Contains("Parameter(\"expected_farming_experience_delta\"", plan);
        Assert.Contains("executionRequest.ExpectedFarmingExperienceDelta", loop);
    }

    [Fact]
    public void UnprojectedLewisBasementSideEffectFailsClosedAtReadAndRuntime()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.SpawnedObjects.cs");
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Foraging.cs");

        Assert.Contains("blocked_unprojected_lewis_basement_789_side_effect", bridge);
        Assert.Contains("collect_spawned_object_unprojected_lewis_basement_789_side_effect", runtime);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(
            Path.Combine(
                directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."),
                Path.Combine(segments)));
    }
}
