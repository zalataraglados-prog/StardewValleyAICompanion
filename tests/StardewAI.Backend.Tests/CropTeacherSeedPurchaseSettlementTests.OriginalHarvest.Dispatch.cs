using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedHarvestPickupRestoresItsSettledHeadWithoutRepeatingDispatch(bool settlesWhileRefreshing)
    {
        using var f = new OvernightFixture(productExecutor: true,
            prepareSource: source => HarvestFixture.Put(source.Before, "current_location", "debris", new JsonArray()));
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var operation = await f.PrepareHarvest(owner, retainSaleStock: true);
        var options = f.Shared.Runtime.LiveOptions;
        options.BridgeSnapshotUrl = "http://test/fixture/pickup-snapshot";
        options.ExecutorUrl = "http://test";
        options.SnapshotArtifactMode = ContentAddressedJsonArtifactStore.PlainMode;
        var output = f.Shared.Source.Ledger.CropPlantingCommitments.Single().HarvestItemQualifiedId;
        using var transport = new HarvestPickupTransport(f.Shared.Runtime.Handler, f.HarvestAfter, output, settlesWhileRefreshing);
        using var client = new HttpClient(transport);
        var bridge = typeof(CropTeacherManagedSeedOwner).GetMethod("ExecuteOriginalHarvestSettlingPickupAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var settle = new Func<CropTeacherHarvest.NativeBatch, Task<CropTeacherHarvest>>(batch =>
            InvokeLive<Task<CropTeacherHarvest>>("SettleCropTeacherHarvestPickupAsync", client, client, options, operation, batch));
        await (Task)bridge.Invoke(owner, [operation, client, new Func<Task<JsonObject>>(() => f.EmitHarvest(owner, leaveDrop: true)), settle])!;
        await f.RebindHarvest(owner, operation);

        Assert.Equal(settlesWhileRefreshing ? 0 : 1, transport.Dispatches);
        Assert.Equal(1, f.NativeHarvestCalls);
        Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
        var acceptedPath = owner.CurrentSnapshotPath;
        var trace = await owner.ReadProductionTraceAsync();
        var harvest = trace.Transitions[^1].NativeTransition;
        Assert.Equal(f.HarvestAfter, harvest.AfterSnapshotPath);
        Assert.Equal(acceptedPath, harvest.EffectiveAfterSnapshotPath);
        Assert.NotEqual(harvest.AfterSnapshotPath, acceptedPath);
        var manifest = JsonSerializer.Deserialize<CropTeacherHarvestPickupFiles[]>(
            File.ReadAllText(harvest.NativeHarvestSettlement!.SettlementPath), JsonOptions)!;
        var settlement = Assert.Single(manifest);
        Assert.Equal(settlesWhileRefreshing, settlement.ObservedWithoutDispatch);
        if (settlesWhileRefreshing)
        {
            Assert.Empty(settlement.QueuePath);
            Assert.Empty(settlement.ReceiptPath);
            Assert.Equal(harvest.AfterSnapshotPath, settlement.BeforeSnapshotPath);
        }
        trace.ValidateInputsUnchanged();
        var ledger = ReadLedger(owner.CurrentLedgerPath);
        Assert.Equal(2, ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            CropSaleStockReservationContract.TryRead(row, out _)).Sum(row => row.Quantity));

        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Null(reopened.Pending);
        Assert.Equal(acceptedPath, reopened.CurrentSnapshotPath);
        var restored = await reopened.ReadProductionTraceAsync();
        Assert.Equal(JsonSerializer.Serialize(harvest, JsonOptions), JsonSerializer.Serialize(restored.Transitions[^1].NativeTransition, JsonOptions));
        Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(settlesWhileRefreshing ? 0 : 1, transport.Dispatches);
    }

    private sealed class HarvestPickupTransport(HttpMessageHandler backendHandler, string harvestAfter, string itemId,
        bool settlesWhileRefreshing) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker backend = new(backendHandler, disposeHandler: false);
        private SnapshotEnvelope? current;
        internal int Dispatches { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/fixture/pickup-snapshot")
            {
                if (current is null)
                {
                    current = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(harvestAfter), JsonOptions)!;
                    if (settlesWhileRefreshing) Collect(current);
                }
                return Reply(current);
            }
            if (path != "/api/v1/product/execute") return await backend.SendAsync(request, token);

            var bound = (await request.Content!.ReadFromJsonAsync<TrainingExecutionRequest>(JsonOptions, token))!;
            Assert.Equal("executor.pickup_debris", bound.OptionId);
            Assert.Equal(current!.StateHash, bound.BeforeStateHash);
            Assert.Equal(itemId, bound.QualifiedItemId);
            Assert.Equal(0, bound.DebrisIndex);
            Dispatches++;
            var beforeHash = current.StateHash;
            var beforeTick = current.GameTick;
            Collect(current);
            return Reply(new JsonObject
            {
                ["schema_version"] = "training_execution_result.v1", ["status"] = "applied", ["source"] = "product_executor",
                ["run_id"] = bound.RunId, ["queue_id"] = bound.QueueId, ["queue_item_id"] = bound.QueueItemId,
                ["option_id"] = bound.OptionId, ["before_state_hash"] = beforeHash, ["primitive_kind"] = "pickup_debris",
                ["primitive_verification_status"] = "verified", ["primitive_verification_reasons"] = new JsonArray("fixture_pickup"),
                ["block_reasons"] = new JsonArray(), ["actual_ticks"] = 1,
                ["product_before_state_hash"] = beforeHash, ["product_before_game_tick"] = beforeTick,
                ["product_after_state_hash"] = current.StateHash, ["product_after_game_tick"] = current.GameTick,
                ["changed_facts"] = new JsonArray(new JsonObject { ["path"] = "current_location.debris.count", ["before"] = "1", ["after"] = "0" })
            });
        }

        private void Collect(SnapshotEnvelope snapshot)
        {
            HarvestFixture.AddHeldOutput(snapshot, itemId, 1);
            HarvestFixture.Put(snapshot, "current_location", "debris", new JsonArray());
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            snapshot.GameTick++;
        }

        private static HttpResponseMessage Reply<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: JsonOptions) };
        protected override void Dispose(bool disposing) { if (disposing) backend.Dispose(); base.Dispose(disposing); }
    }
}
