using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("live")]
    [InlineData("native_ack_lost")]
    [InlineData("marker_reply_lost")]
    public async Task RefillControlUsesOriginalOwnedInvocationAndRestoresZeroCreditAcceptedHead(string mode)
    {
        using var f = new OwnedWateringFixture(water: 0, atRefillApproach: true);
        var loseWrite = true;
        await using var owner = await f.Shared.Files.Open(writer: async (path, json) =>
        {
            if (mode == "marker_reply_lost" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Refill marker committed before reply retention."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var operation = await f.Prepare(owner);
        var calls = 0;
        async Task<JsonObject> Native()
        {
            calls++;
            var result = await f.EmitNative(applied: 0, failedSuffix: true, refillOnlySuffix: true);
            if (mode == "native_ack_lost") throw new IOException("Refill native files retained before acknowledgement.");
            return result;
        }
        if (mode == "native_ack_lost") await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, Native));
        else
        {
            await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, Native);
            if (mode == "marker_reply_lost") await Assert.ThrowsAsync<IOException>(() => f.Rebind(owner, operation));
            else await f.Rebind(owner, operation);
        }
        Assert.Equal(1, calls);
        if (mode == "live") Assert.Null(owner.Pending);
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        if (mode == "native_ack_lost") await f.Rebind(reopened, reopened.Pending!);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        else await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_refill_control_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_watering_prefix_verified"]!.GetValue<bool>());
        Assert.False(accepted["complete_batch"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.Empty(accepted["applied_tiles"]!.AsArray());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.NotNull(accepted["maintenance_native_claim_sha256"]);
        var next = await InvokeLive<Task<StardewAI.LiveTrainingLoop.CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 2, f.Basis.Step, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, reopened, f.Shared.Files.Native + "-after-refill");
        Assert.Equal(2, next.Sequence);
        var nextQueue = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-batch-0002-compiled-queue.json")))!;
        Assert.Equal(f.Basis.After.StateHash, nextQueue["state_hash"]!.GetValue<string>());
        Assert.Equal(2, nextQueue["items"]!.AsArray().Count);
    }
}
