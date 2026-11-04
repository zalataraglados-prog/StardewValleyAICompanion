using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public async Task CustodyControlUsesActualLiveBackendAfterNormalMovementAndOriginalHttpSettlement()
    {
        var root = NewPortfolioReplyRoot();
        var attempt = PrepareCustodyControl(new CustodyTransferFixture(new FileStrategyCommitmentRepository(root)));
        var path = Assert.Single(Directory.GetFiles(root));
        var bytes = File.ReadAllBytes(path);
        var moved = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(attempt.Before))!;
        moved.GameTick++;
        moved.State["time"] = JsonSerializer.SerializeToElement(new { time_of_day = new { value = 620, status = "available" } });
        moved.StateHash = SnapshotHash.ComputeStateHash(moved.State);
        var actual = TeacherPreferenceQueueLoader.RebindQueueItemToState(JsonSerializer.SerializeToNode(attempt.Transfer)!.AsObject(), moved.StateHash);
        var rebound = actual.Deserialize<ActionQueueItem>()!;
        var after = attempt.After;
        after.State["time"] = moved.State["time"];
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var receipt = attempt.Receipt();
        receipt.SourceStateHash = moved.StateHash;
        receipt.EffectiveQueueItem = JsonSerializer.SerializeToElement(rebound);
        receipt.AfterStateHash = after.StateHash;

        await using var app = PortfolioReplyApplication(root);
        using var client = app.CreateClient();
        var store = app.Services.GetRequiredService<StateStore>();
        store.StoreSnapshot(attempt.Before);
        store.StoreSnapshot(moved);
        var ready = await ReadActualLiveTransferDispatch(client, actual, moved.StateHash);
        Assert.True(ready?["ready"]?.GetValue<bool>(), ready?.ToJsonString());
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(attempt.Before.StateHash, attempt.Fixture.Ledger.PreparedMaterialTransfer!.SourceStateHash);
        var built = MuseumOutputCustodyTransferSettlementRequestBuilder.Build(attempt.Compilation.Queue!, rebound, receipt,
            moved, after, attempt.Fixture.Ledger);
        Assert.Empty(built.Errors);
        store.StoreSnapshot(after);
        Assert.False(store.Snapshots.ContainsKey(attempt.Before.StateHash));
        using var response = await client.PostAsJsonAsync(PortfolioEndpoint + "settle-supporting-transition", built.Request);
        response.EnsureSuccessStatusCode();
        var settled = await response.Content.ReadFromJsonAsync<ReservationPortfolioSupportingTransitionSettlementResult>();
        Assert.True(settled!.Accepted, string.Join(",", settled.Errors));
        Assert.Null(settled.Ledger!.PreparedMaterialTransfer);
        Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(
            settled.Ledger.MaterialReservations.Single(row => row.ReservationId == attempt.Fixture.CustodyId), settled.Ledger, out _));
        var stale = await ReadActualLiveTransferDispatch(client, actual, moved.StateHash);
        Assert.False(stale?["ready"]?.GetValue<bool>());
    }
}
