using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    private sealed partial class Fixture
    {
        internal string ProofPath => Path.Combine(root, "planting-proof.json");
        internal SnapshotEnvelope After { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest NativeTransition(CropTeacherSeedPlantingMarker marker,
            int consumed = 8, bool failedSuffix = false, Action<SnapshotEnvelope>? change = null,
            Action<QueueExecutionReceiptEnvelope>? receiptChange = null, bool firstSlot = false,
            Action<SnapshotEnvelope>? suffixChange = null)
        {
            var queue = Compile(marker, marker.BuildActions(Batch(), firstSlot ? null : OwnedSlotOne));
            consumed = Math.Min(consumed, queue.Items.Length);
            var snapshots = new SnapshotEnvelope[queue.Items.Length];
            var paths = Enumerable.Range(0, queue.Items.Length).Select(index => Path.Combine(root, "step-" + index + ".json")).ToArray();
            var current = Before;
            for (var index = 0; index < snapshots.Length; index++)
            {
                var next = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(current, JsonOptions), JsonOptions)!;
                next.GameTick += 5;
                if (index < consumed) Plant(next, marker, index, firstSlot ? 0 : 1);
                if (index == consumed - 1) change?.Invoke(next);
                if (index == consumed && failedSuffix) suffixChange?.Invoke(next);
                next.StateHash = SnapshotHash.ComputeStateHash(next.State);
                snapshots[index] = next;
                current = next;
            }
            var attempted = consumed + (failedSuffix ? 1 : 0);
            After = snapshots[attempted - 1];
            var afterPath = Path.Combine(root, "after.json");
            paths[attempted - 1] = afterPath;
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", queue,
                Before, After, snapshots, paths, "run.owned.planting", "candidate.owned.planting");
            receipt.StepResults = receipt.StepResults.Take(attempted).ToArray();
            receipt.ExecutedItemCount = attempted;
            receipt.FinalPendingItemCount = queue.Items.Length - attempted;
            receipt.Success = receipt.SelectedCandidateCompleted = consumed == queue.Items.Length;
            receipt.Status = failedSuffix ? "blocked" : "applied";
            foreach (var step in receipt.StepResults)
                step.SelectedQueueCandidateCompleted = receipt.Success && step.QueueItemIndex == attempted - 1;
            if (failedSuffix)
            {
                var failed = receipt.StepResults[^1];
                failed.Status = "blocked";
                failed.PrimitiveVerificationStatus = "blocked";
                failed.BlockReasons = receipt.BlockReasons = new[] { "fixture.native.planting_blocked" };
            }
            receiptChange?.Invoke(receipt);
            Save(QueuePath, queue);
            for (var index = 0; index < attempted; index++) Save(paths[index], snapshots[index]);
            var receiptPath = Path.Combine(root, "receipt.json");
            Save(receiptPath, receipt);
            return new("owned_seed_planting", 112, QueuePath, SourcePath, receiptPath, afterPath, "run.owned.planting",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, "candidate.owned.planting");
        }

        private void Plant(SnapshotEnvelope snapshot, CropTeacherSeedPlantingMarker marker, int index, int slotIndex)
        {
            var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!.AsObject();
            var actor = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
            var graph = farm["material_inventory_graph"]!["value"]!;
            var slots = graph["inventory_nodes"]![0]!["slots"]!.AsArray();
            var selected = slots.Single(row => row!["slot_index"]!.GetValue<int>() == slotIndex)!;
            var remaining = selected["stack"]!.GetValue<int>() - 1;
            if (remaining == 0) slots.Remove(selected); else selected["stack"] = remaining;
            var quantity = graph["quantity_rows"]!.AsArray().Single(row => row!["qualified_item_id"]!.GetValue<string>() == "(O)745")!;
            quantity["available_quantity"] = quantity["available_quantity"]!.GetValue<int>() - 1;
            CropTeacherSeedTransferTests.Fixture.ApplyNativeEchoes(farm, actor, snapshot.GameTick);
            var crop = marker.Ledger.CropPlantingCommitments.Single();
            var tile = marker.BoundBatch.Tiles[index];
            farm["crops"]!["value"]!.AsArray().Add(JsonSerializer.SerializeToNode(new
            {
                location_id = "Farm", tile_x = tile.TileX, tile_y = tile.TileY,
                harvest_source_seed_id = crop.SeedId, harvest_item_qualified_id = crop.HarvestItemQualifiedId,
                phase_days = new[] { crop.BaseGrowDays, 99999 }, current_phase = 0, phase_count = 2,
                day_of_current_phase = 0, dead = false, fully_grown = false, ready_for_harvest = false,
                days_until_next_harvest_if_watered = crop.BaseGrowDays, regrow_days = crop.RegrowDays, watered = false,
                unknown_native_crop_detail = "observed-not-invented-by-verifier"
            }, JsonOptions));
            snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            snapshot.State["player"] = JsonSerializer.SerializeToElement(actor, JsonOptions);
            var progress = JsonNode.Parse(snapshot.State["world_progress"].GetRawText())!;
            var stats = progress["game_state_query_unlock_state"]!["value"]!["players"]![0]!["stats"]!;
            stats["seedsSown"] = stats["seedsSown"]!.GetValue<int>() + 1;
            snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonOptions);
        }

        internal CropTeacherSeedPlantingSettlement Verify(CropTeacherSeedPlantingMarker marker,
            MoneyRevenueCropNativeLifecycleTransitionRequest transition) =>
            marker.VerifyTransition(transition, ProofPath, marker.ValidateQueue(transition.QueuePath));

        internal static JsonNode NativeField(object value, long tick) => JsonSerializer.SerializeToNode(new
        {
            status = "available", confidence = 1d, value, read_at_tick = tick,
            source = new { kind = "game_object", path = "fixture.owned.planting" }, adapter = "test"
        }, JsonOptions)!;
    }
}
