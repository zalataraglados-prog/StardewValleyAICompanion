using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeSettleOwnerExecutesOriginalWaitWithoutGenericFallbackOrFalseHomeProgress()
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        await using var owner = await f.Files.Open();
        var operation = (await PrepareManagedSettle(f, owner))!;
        InvokeLive<object>("RequireManagedCropDispatch", owner, operation);
        var outcome = BuildManagedSettleResult(f, owner);
        using var transport = new OriginalRouteTransport(f, outcome);
        using var client = new HttpClient(transport);
        var result = await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync", client, client,
            f.Runtime.LiveOptions, 1, owner, operation, ManagedSettleQueue(f),
            (Func<Task<JsonObject>>)(() => throw new InvalidOperationException("No generic dispatch for an owned control.")));
        Assert.Equal(f.AfterPath, result["after_snapshot_path"]!.GetValue<string>());
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Null(owner.Pending); Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.False(accepted["route_target_reached"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.Null(await PrepareManagedSettle(f, owner, 2));
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("native_ack_lost")]
    [InlineData("native_result_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    public async Task NativeSettleOwnerRecoversTheOriginalSingleWaitAndMarker(string mode)
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        var loseWrite = true;
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            if (mode == "marker_reply_lost" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Marker committed before reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (loseWrite && (mode == "native_result_ack_lost" && Path.GetFileName(path) == "route-native-result.json" ||
                mode == "acceptance_ack_lost" && Path.GetFileName(path) == "operation-000001-accepted.json"))
            { loseWrite = false; throw new IOException("Durable record acknowledgement lost."); }
        });
        var operation = (await PrepareManagedSettle(f, owner))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PrepareManagedSettle(f, owner, 2));
        var calls = 0;
        Task<JsonObject> Native()
        {
            calls++;
            var (after, receipt) = BuildManagedSettleResult(f, owner);
            Fixture.Save(f.AfterPath, after); Fixture.Save(f.ReceiptPath, receipt);
            f.Runtime.Store.Snapshots[after.StateHash] = after;
            if (mode == "native_ack_lost") throw new IOException("Native output durable before acknowledgement.");
            return Task.FromResult(new JsonObject { ["execution_path"] = f.ReceiptPath, ["after_snapshot_path"] = f.AfterPath });
        }
        if (mode is "native_ack_lost" or "native_result_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, Native));
        else
        {
            await owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, Native);
            await Assert.ThrowsAsync<IOException>(() => f.Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else await f.Rebind(reopened, reopened.Pending!);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, calls); Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Runtime.Handler.Lookups);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
    }

    private static string ManagedSettleQueue(OwnedRouteFixture f) =>
        Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "native-control-settle-compiled-queue-0001.json");

    [Fact]
    public async Task NativeSettleOwnerDoesNotOverwriteAnExistingQueueOrAdmitItAsNewWork()
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        await using var owner = await f.Files.Open();
        File.WriteAllText(ManagedSettleQueue(f), "retained original queue");
        await Assert.ThrowsAsync<IOException>(() => PrepareManagedSettle(f, owner));
        Assert.Equal("retained original queue", File.ReadAllText(ManagedSettleQueue(f)));
        Assert.Null(owner.Pending); Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.Empty(Directory.EnumerateFiles(f.Files.OwnerRoot, "operation-*-intent.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSettleOwnerRetainsAnUnknownNativeClaimWithoutRepeatingTheWait(bool claimAckLost)
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (claimAckLost && Path.GetFileName(path) == "route-native-claim.json")
                throw new IOException("Claim durable before dispatch acknowledgement.");
        });
        var operation = (await PrepareManagedSettle(f, owner))!;
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, () =>
        { calls++; throw new IOException("Unknown native wait outcome."); }));
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalRouteAsync(reopened.Pending!, f.Runtime.Client, () =>
        { calls++; throw new InvalidOperationException("Never repeat an unknown wait."); }));
        await Assert.ThrowsAnyAsync<IOException>(() => f.Rebind(reopened, reopened.Pending!));
        Assert.Equal(claimAckLost ? 0 : 1, calls); Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.NotNull(reopened.Pending);
    }

    private static async Task<CropTeacherManagedSeedOperation?> PrepareManagedSettle(OwnedRouteFixture f,
        CropTeacherManagedSeedOwner owner, int iteration = 1)
    {
        var task = InvokeLive<Task>("PrepareCropTeacherOwnedNativeControlSettleAsync", f.Runtime.LiveOptions, iteration, owner);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return result is null ? null : (CropTeacherManagedSeedOperation)result.GetType().GetProperty("Operation")!.GetValue(result)!;
    }

    private static (SnapshotEnvelope After, QueueExecutionReceiptEnvelope Receipt) BuildManagedSettleResult(
        OwnedRouteFixture f, CropTeacherManagedSeedOwner owner)
    {
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        ChangeState(after, "player", node =>
        {
            node["movement_timing_context"]!["value"]!["player_can_move_now"] = true;
            node["movement_timing_context"]!["value"]!["route_timing_ready_now"] = true;
        });
        after.GameTick += NativeControlSettleRecovery.WaitTicks;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(ManagedSettleQueue(f)), JsonOptions)!;
        var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before, after,
            f.Files.Options.RunId, NativeControlSettleRecovery.CandidateId, "wait_ticks");
        receipt.StepResults[0].AfterGameTick = after.GameTick;
        return (after, receipt);
    }

}
