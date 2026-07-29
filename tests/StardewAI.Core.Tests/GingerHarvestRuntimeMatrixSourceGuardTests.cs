using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class GingerHarvestRuntimeMatrixSourceGuardTests
{
    [Fact]
    public void FixtureProfileSurvivesExecutionRequestTransport()
    {
        var request = new TrainingExecutionRequest
        {
            FixtureGingerProfile = "rain_efficient",
            DebugFillInventory = true
        };

        var json = JsonSerializer.Serialize(
            request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTrip);
        Assert.Equal("rain_efficient", roundTrip.FixtureGingerProfile);
        Assert.True(roundTrip.DebugFillInventory);
    }

    [Fact]
    public void FixtureCoversWeatherEfficiencyInventoryAndEnergyProfiles()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ForageSourceFixture.cs");

        foreach (var profile in new[]
        {
            "dry_standard",
            "rain_efficient",
            "dry_insufficient_energy"
        })
        {
            Assert.Contains(profile, fixture, StringComparison.Ordinal);
        }
        Assert.Contains(
            "location.GetWeather()",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "hoe.isEfficient.Value",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "FillGingerFixtureInventory(hoe)",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "couldInventoryAcceptThisItem",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeSupportsPureMechanicalAndUpstreamBlockedRuns()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeForageTaskSourceSmoke.ps1");

        Assert.Contains(
            "[ValidateSet(\"none\", \"ordinary_quest\", \"special_order\")]",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$ExpectedSourceStatus -ne \"ready\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "harvest_status = \"excluded_upstream\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$TaskFamily -ne \"none\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:SMAPI_MODS_PATH = $smokeModsPath",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(
                Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(
            Path.Combine(
                directory?.FullName ??
                    throw new InvalidOperationException(
                        "Cannot find repository root."),
                Path.Combine(segments)));
    }
}
