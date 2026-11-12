using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherRuntimeInstallationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoreReturnsActualReadbackAndRejectsReusedOutputWithoutAnotherMutation(bool loseReplies)
    {
        using var files = new Files();
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var decisionPath = Prepare(fixture, files);
        var offline = MoneyRevenueCropResourceReservationBuilder.Build(decisionPath).ReservationResult.Ledger!;
        var installation = CropTeacherRuntimeInstallation.Open(decisionPath);
        using var handler = new RepositoryHandler(files.Repository, fixture.SourceSnapshot)
        {
            LoseCropReply = loseReplies, LoseResourceReply = loseReplies
        };
        using var http = new HttpClient(handler);

        var evidence = await InstallCore(http, installation, files.Output);

        var persisted = new FileStrategyCommitmentRepository(files.Repository).Get(fixture.SourceSnapshot);
        Assert.Equal(4, evidence.Ledger.Revision);
        Assert.NotEqual(offline.UpdatedAt, evidence.Ledger.UpdatedAt);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(persisted, JsonOptions),
            JsonSerializer.SerializeToNode(evidence.Ledger, JsonOptions)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(evidence.Path)),
            JsonSerializer.SerializeToNode(evidence.Ledger, JsonOptions)));
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(files.Output, "installation-summary.json")))!;
        Assert.Equal(evidence.Path, summary["installed_ledger_path"]!.GetValue<string>());
        Assert.True(summary["runtime_ledger_installed"]!.GetValue<bool>());
        Assert.False(summary["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Equal(1, handler.CropPosts);
        Assert.Equal(1, handler.ResourcePosts);
        Assert.Equal(loseReplies ? 1 : 0, handler.Lookups);
        Assert.Equal(loseReplies ? 3 : 2, handler.Reads);

        await Assert.ThrowsAsync<IOException>(() => InstallCore(http, installation, files.Output));
        Assert.Equal(1, handler.CropPosts);
        Assert.Equal(1, handler.ResourcePosts);
        Assert.Equal(loseReplies ? 1 : 0, handler.Lookups);
        Assert.Equal(loseReplies ? 3 : 2, handler.Reads);
    }

    private static Task<StrategyLedgerEvidence> InstallCore(HttpClient http,
        CropTeacherRuntimeInstallation installation, string output) =>
        (Task<StrategyLedgerEvidence>)typeof(LiveTrainingOptions).Assembly.GetType("Program")!
            .GetMethod("InstallCropReservationsCoreAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { http, installation, "http://test", output })!;
}
