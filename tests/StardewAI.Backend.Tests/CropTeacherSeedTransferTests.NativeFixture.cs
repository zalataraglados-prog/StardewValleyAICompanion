using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    internal sealed partial class Fixture
    {
        internal string NativeProofPath => Path.Combine(root, "native-transfer-proof.json");
        internal SnapshotEnvelope After { get; private set; } = null!;

        internal MoneyRevenueCropNativeLifecycleTransitionRequest NativeTransition(CropTeacherSeedTransferMarker marker,
            Action<SnapshotEnvelope>? afterChange = null, Action<QueueExecutionReceiptEnvelope>? receiptChange = null,
            ActionQueueEnvelope? compiledQueue = null)
        {
            var queue = compiledQueue ?? Compile(marker);
            Assert.Equal("pending", queue.Status);
            After = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(Before))!;
            After.GameTick += 5;
            var farm = JsonNode.Parse(After.State["farm"].GetRawText())!.AsObject();
            var graph = farm["material_inventory_graph"]!["value"]!;
            var source = graph["inventory_nodes"]![1]!["slots"]!.AsArray();
            var seed = source.Single(row => row!["qualified_item_id"]!.GetValue<string>() == "(O)745")!.DeepClone();
            var seedRemaining = seed["stack"]!.GetValue<int>() - 10;
            if (seedRemaining > 0)
            {
                source[0]!["slot_index"] = 0;
                source[0]!["stack"] = seedRemaining;
                if (source.Count > 1) source[1]!["slot_index"] = 1;
            }
            else
            {
                source.RemoveAt(0);
                if (source.Count > 0) source[0]!["slot_index"] = 0;
            }
            var destination = graph["inventory_nodes"]![0]!["slots"]!.AsArray();
            var held = destination.SingleOrDefault(row => row!["qualified_item_id"]!.GetValue<string>() == "(O)745");
            var allocated = held is null ? 0 : Math.Min(10, 999 - held["stack"]!.GetValue<int>());
            if (held is not null) held["stack"] = held["stack"]!.GetValue<int>() + allocated;
            if (allocated < 10)
            {
                seed["slot_index"] = held is null ? 0 : 1;
                seed["stack"] = 10 - allocated;
                destination.Insert(held is null ? 0 : 1, seed);
            }
            var actor = JsonNode.Parse(After.State["player"].GetRawText())!.AsObject();
            ApplyNativeEchoes(farm, actor, After.GameTick);
            After.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            After.State["player"] = JsonSerializer.SerializeToElement(actor, JsonOptions);
            afterChange?.Invoke(After);
            After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            var queuePath = Path.Combine(root, "native-queue.json");
            var afterPath = Path.Combine(root, "native-after.json");
            var receiptPath = Path.Combine(root, "native-receipt.json");
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", queue,
                Before, After, new[] { After }, new[] { afterPath }, "run.owned.seed.transfer", "candidate.owned.seed.transfer");
            foreach (var step in receipt.StepResults)
            {
                step.PrimitiveVerificationReasons = new[] { "native_material_transfer_delta_verified" };
                step.ChangedFacts = JsonSerializer.SerializeToElement(new[] { "native_material_transfer" });
            }
            receiptChange?.Invoke(receipt);
            Save(queuePath, queue);
            Save(afterPath, After);
            Save(receiptPath, receipt);
            return new("owned_seed_transfer", Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
                queuePath, SourcePath, receiptPath, afterPath, "run.owned.seed.transfer",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, "candidate.owned.seed.transfer");
        }

        internal CropTeacherSeedTransferSettlement VerifyNative(CropTeacherSeedTransferMarker marker,
            MoneyRevenueCropNativeLifecycleTransitionRequest transition) => marker.VerifyTransition(transition, NativeProofPath,
            marker.ValidateQueue(transition.QueuePath));
    }
}
