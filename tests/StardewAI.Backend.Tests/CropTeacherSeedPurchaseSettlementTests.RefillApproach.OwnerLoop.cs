using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("live")]
    [InlineData("native_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    [InlineData("corrupt_approach_flag")]
    public async Task OwnedRefillApproachUsesOriginalWireLoopAndReadOnlyRecoveryWithoutCropProgress(string mode)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: false);
        var shared = f.Approach.Watering.Shared;
        var loseWrite = true;
        await using var owner = await shared.Files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            var expected = mode switch
            {
                "native_ack_lost" => "maintenance-native-result.json",
                "marker_reply_lost" => "marker-reply.json",
                "acceptance_ack_lost" => "operation-000001-accepted.json",
                _ => string.Empty
            };
            if (loseWrite && Path.GetFileName(path) == expected)
            { loseWrite = false; throw new IOException("Declared acknowledgement lost after durable write."); }
        });
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            shared.Runtime.Client, shared.Runtime.LiveOptions, 1, f.Approach.Watering.Step,
            shared.Inputs.Plan, shared.Inputs.Request, owner, shared.Files.Native);
        // Only declared transport fixtures are prepared here, not claimed native game effects.
        await f.Build(retainCompilation: true);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(f.ReceiptPath), JsonOptions)!;
        using var transport = new OriginalWateringTransport(shared.Runtime, f.Approach.Watering.Before, receipt, fullExecutionResult: true);
        using var client = new HttpClient(transport);
        Task Execute() => InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync", client, client,
            shared.Runtime.LiveOptions, 1, owner, operation);
        if (mode is "live" or "corrupt_approach_flag") await Execute();
        else await Assert.ThrowsAsync<IOException>(Execute);
        Assert.Equal(1, transport.NativeRequests);
        Assert.InRange(transport.FullSnapshotReads, 1, 4);
        await owner.DisposeAsync();
        if (mode == "corrupt_approach_flag")
        {
            var path = Path.Combine(shared.Files.OwnerRoot, "operation-000001-accepted.json");
            var corrupt = JsonNode.Parse(File.ReadAllText(path))!;
            corrupt["native_refill_approach_verified"] = false;
            Fixture.Save(path, corrupt);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => shared.Files.Open());
            Assert.Contains("watering prefix acceptance changed", error.Message);
            Assert.Equal(1, transport.NativeRequests);
            return;
        }
        await using var reopened = await shared.Files.Open();
        if (mode == "native_ack_lost")
            await reopened.RunWateringMaintenanceAsync(reopened.Pending!, shared.Runtime.Client,
                (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync",
                    shared.Runtime.Client, shared.Runtime.LiveOptions, diagnostic, request), shared.Files.Post);
        else if (mode == "marker_reply_lost")
            await reopened.RecoverPendingAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        else await reopened.RestoreHeadAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        Assert.Equal(1, transport.NativeRequests);
        Assert.Equal(1, shared.Runtime.Handler.Commits);
        Assert.Null(reopened.Pending);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(shared.AfterPath, reopened.CurrentSnapshotPath);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_refill_approach_verified"]!.GetValue<bool>());
        Assert.Null(accepted["native_refill_control_verified"]);
        Assert.False(accepted["native_watering_prefix_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.False(accepted["complete_batch"]!.GetValue<bool>());
        Assert.Empty(accepted["applied_tiles"]!.AsArray());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
        var next = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            shared.Runtime.Client, shared.Runtime.LiveOptions, 2, f.Approach.Watering.Step,
            shared.Inputs.Plan, shared.Inputs.Request, reopened, shared.Files.Native + "-next");
        Assert.Equal(2, next.Sequence);
        var queue = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-batch-0002-compiled-queue.json")))!;
        Assert.All(queue["items"]!.AsArray(), row => Assert.Equal("executor.water_crop", row!["option_id"]!.GetValue<string>()));
    }
}
