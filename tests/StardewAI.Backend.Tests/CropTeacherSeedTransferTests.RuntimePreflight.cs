using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("late")]
    [InlineData("clock_mismatch")]
    [InlineData("clock_unknown")]
    [InlineData("future_map")]
    public async Task TransferSourceBudgetOrGeometryRejectsUpstreamWithoutMutation(string fault)
    {
        using var f = new Fixture(nativeEvidence: true, beforeChange: snapshot =>
        {
            if (fault == "clock_mismatch") snapshot.InGameTime.Value = 610;
            if (fault == "clock_unknown") snapshot.InGameTime.Status = "unknown";
            if (fault == "late")
            {
                var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!.AsObject();
                foreach (var name in new[] { "time", "time_of_day" })
                    if (time[name] is JsonObject field) field["value"] = 1900;
                snapshot.State["time"] = JsonSerializer.SerializeToElement(time, JsonOptions);
                snapshot.InGameTime.Value = 1900;
            }
            if (fault == "future_map")
            {
                var location = JsonNode.Parse(snapshot.State["locations"].GetRawText())!.AsObject();
                location["collision_grid"]!["read_at_tick"] = snapshot.GameTick + 1;
                snapshot.State["locations"] = JsonSerializer.SerializeToElement(location, JsonOptions);
            }
        });
        using var runtime = new RuntimeFixture(f);
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Prepare());
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(0, runtime.Handler.LedgerReads);
        Assert.False(Directory.Exists(f.RuntimePath));
    }

    [Fact]
    public async Task TransferConcurrentLedgerChangeRejectsBeforePreparationCommit()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        var scope = f.Open();
        var concurrent = runtime.Repository.CommitReservationPortfolio(f.Before, new ReservationPortfolioCommitRequest
        {
            StateHash = f.Before.StateHash, ExpectedLedgerRevision = f.Ledger.Revision,
            PortfolioId = "concurrent.marker", SourceDecisionId = "other-root", GoalId = "earn_money"
        });
        Assert.True(concurrent.Accepted);
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Prepare(scope));
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.LedgerReads);
        Assert.Equal("crop_seed_support_preparation_incomplete", runtime.Summary("preparation-summary.json")["status"]!.GetValue<string>());
    }
}
