using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class BushHarvestRuntimeMatrixSourceGuardTests
{
    [Fact]
    public void FixtureProfileSurvivesExecutionRequestTransport()
    {
        var request = new TrainingExecutionRequest
        {
            FixtureBushProfile = "golden_walnut"
        };

        var json = JsonSerializer.Serialize(
            request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTrip);
        Assert.Equal("golden_walnut", roundTrip.FixtureBushProfile);
    }

    [Fact]
    public void FixtureCoversEveryNativeBushBranchAndBlockedState()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ForageSourceFixture.cs");

        foreach (var profile in new[]
        {
            "berry_standard",
            "berry_botanist",
            "tea_leaf",
            "golden_walnut",
            "golden_walnut_collected",
            "berry_cooldown"
        })
        {
            Assert.Contains(profile, fixture, StringComparison.Ordinal);
        }
        Assert.Contains(
            "Game1.player.professions.Add(16)",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.player.team.collectedNutTracker.Add(nutKey)",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "bush.shakeTimer",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeSupportsBushProfilesAndUpstreamExclusion()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeForageTaskSourceSmoke.ps1");

        Assert.Contains(
            "$BushFixtureProfile",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "golden_walnut_already_collected",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "bush_shake_cooldown_active",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "harvest_status = \"excluded_upstream\"",
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
