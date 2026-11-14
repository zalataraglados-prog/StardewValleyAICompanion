using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("complete")]
    [InlineData("partial")]
    [InlineData("unverified")]
    public async Task NativeObservedRefillOriginalLoopCompletesOnlyItsFrozenRootQueue(string mode)
    {
        using var f = new NativeRefillProductFixture(repair: true, nativeObserved: true);
        var shared = f.Trace.Approach.Watering.Shared;
        var live = shared.Runtime.LiveOptions;
        live.UseProductExecutor = true;
        var options = shared.Files.Options with { ExecutorVersion = PolicyTrajectoryVersionPins.ProductExecutor,
            UseSelectedRefillResourceEffects = true, UseNativeRefillOutcomeAuthority = true };
        await using var owner = await shared.Files.Open(options);
        var operation = await PrepareOwnerSelectedRefill(f.Trace, owner);
        await f.Build(retainCompilation: true);
        Assert.Equal(new[] { "executor.move_to_tile", "executor.clear_obstacle", "executor.move_to_tile" },
            f.Trace.Approach.Queue.Items.Select(item => item.OptionId));
        live.MaxQueueItemAttempts = mode == "partial" ? 2 : 3;
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(f.ReceiptPath), JsonOptions)!;
        // Declared Product transport responses; the original loop must produce its own journals and completion markers.
        using var transport = new OriginalWateringTransport(shared.Runtime, f.Trace.Approach.Watering.Before, receipt,
            resultFactory: (request, ordinal) =>
            {
                var result = JsonNode.Parse(File.ReadAllText(f.Results[ordinal - 1]))!.AsObject();
                result["primitive_verification_reasons"] = JsonSerializer.SerializeToNode(
                    receipt.StepResults[ordinal - 1].PrimitiveVerificationReasons, JsonOptions);
                var journal = ExecutionRequestJournalReader.Read(Path.Combine(live.SnapshotDir,
                    $"execution-request-0001-item-{ordinal:D4}.json"));
                Assert.Equal(request.RequestNonce, journal.Request.RequestNonce);
                result["product_request_sha256"] = journal.Metadata.GetProperty("product_request_sha256").GetString();
                result["product_receipt_id"] = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
                if (mode == "unverified" && ordinal == 2) result["primitive_verification_status"] = "not_verified";
                return result;
            });
        using var client = new HttpClient(transport);
        Task Execute() => InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync", client, client, live, 1, owner, operation);
        if (mode == "complete") await Execute();
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(Execute);
            Assert.Contains(mode == "partial" ? "queue_execution_receipt_item_count_mismatch" :
                "queue_execution_receipt_step_not_verified:1", error.Message);
        }
        var actual = JsonNode.Parse(File.ReadAllText(shared.ReceiptPath))!;
        var steps = actual["step_results"]!.AsArray();
        Assert.Equal(mode == "complete", actual["success"]!.GetValue<bool>());
        Assert.All(steps.Take(steps.Count - 1), step => Assert.False(step!["selected_queue_candidate_completed"]!.GetValue<bool>()));
        Assert.Equal(steps.Count, transport.NativeRequests);
        Assert.False(live.UsePrecompiledQueue);
        Assert.False((bool)typeof(LiveTrainingOptions).GetProperty("UseOwnedSupportingQueueBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(live)!);
        var acceptedPath = Path.Combine(options.EvidenceDirectory, "operation-000001-accepted.json");
        if (mode != "complete")
        {
            Assert.NotNull(owner.Pending); Assert.Equal(0, shared.Runtime.Handler.Commits);
            Assert.False(File.Exists(acceptedPath));
            return;
        }
        Assert.Equal(3, steps.Count);
        Assert.True(steps[2]!["selected_queue_candidate_completed"]!.GetValue<bool>());
        Assert.Null(owner.Pending); Assert.Equal(1, shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(acceptedPath))!;
        Assert.True(accepted["native_refill_observed_outcome_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
        await owner.DisposeAsync();
        await using var reopened = await shared.Files.Open(options);
        await reopened.RestoreHeadAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        Assert.Equal(shared.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(3, transport.NativeRequests); Assert.Equal(1, shared.Runtime.Handler.Commits);
    }
}
