using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class FruitTreeHarvestRuntimeMatrixSourceGuardTests
{
    [Fact]
    public void FixtureProfileSurvivesExecutionRequestTransport()
    {
        var request = new TrainingExecutionRequest { FixtureFruitTreeProfile = "lightning_coal" };
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTrip);
        Assert.Equal("lightning_coal", roundTrip.FixtureFruitTreeProfile);
    }

    [Fact]
    public void FixtureAndSmokeCoverNativeOutputsAndUpstreamExclusions()
    {
        var root = FindRepositoryRoot();
        var fixture = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.ForageSourceFixture.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeForageTaskSourceSmoke.ps1"));

        foreach (var profile in new[] { "single_normal", "triple_gold", "lightning_coal", "empty", "active_shake" })
        {
            Assert.Contains(profile, fixture, StringComparison.Ordinal);
            Assert.Contains(profile, smoke, StringComparison.Ordinal);
        }
        Assert.Contains("executor.harvest_fruit_tree", smoke, StringComparison.Ordinal);
        Assert.Contains("expected_output_items_json", smoke, StringComparison.Ordinal);
        Assert.Contains("fruit_tree_has_no_fruit", smoke, StringComparison.Ordinal);
        Assert.Contains("fruit_tree_shake_in_progress", smoke, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
