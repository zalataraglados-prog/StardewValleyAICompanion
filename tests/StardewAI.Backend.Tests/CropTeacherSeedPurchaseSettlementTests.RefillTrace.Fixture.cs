using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class RefillTraceFixture : IDisposable
    {
        internal RefillApproachFixture Approach { get; }
        internal string ReceiptPath => Path.Combine(Approach.Watering.Shared.Runtime.LiveOptions.SnapshotDir, "refill-native-aggregate.json");
        internal string AfterPath => Path.Combine(Approach.Watering.Shared.Runtime.LiveOptions.SnapshotDir, "refill-native-final.json");
        internal List<string> Results { get; } = [];
        internal List<string> Journals { get; } = [];
        internal List<string> AfterFrames { get; } = [];
        private readonly bool completeSnapshotInventory;
        private readonly bool resourceNeutral;
        private readonly bool observeNativeOutputs;
        private readonly bool observePickupInventory;

        internal RefillTraceFixture(bool repair, bool completeSnapshotInventory = false, bool resourceNeutral = false,
            bool observeNativeOutputs = true, bool observePickupInventory = false)
        {
            this.completeSnapshotInventory = completeSnapshotInventory;
            this.resourceNeutral = resourceNeutral;
            this.observeNativeOutputs = observeNativeOutputs;
            this.observePickupInventory = observePickupInventory;
            if (observePickupInventory && (!completeSnapshotInventory || !observeNativeOutputs))
                throw new ArgumentException("Pickup inventory fixture requires its original quantity observations.");
            Approach = new(repair, prepareSource: source =>
            {
                source.Before.GameVersion = "1.6.15";
                if (completeSnapshotInventory)
                {
                    ChangeState(source.Before, "player", node => node["inventory"] = JsonSerializer.SerializeToNode(new
                    {
                        status = "available",
                        // Declared synthetic complete raw slots; seed count preserves the original compiler fixture.
                        value = new object[]
                        {
                            new { slot_index = 0, item_id = "745", qualified_item_id = "(O)745", stack = 40, quality = 0, is_empty = false },
                            new { slot_index = 1, item_id = "388", qualified_item_id = "(O)388", stack = 5, quality = 0, is_empty = false },
                            new { slot_index = 2, qualified_item_id = (string?)null, stack = (int?)null, quality = (int?)null, is_empty = true }
                        }
                    }));
                    ChangeState(source.Before, "farm", node =>
                        node["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray().Add(
                            JsonSerializer.SerializeToNode(new
                            {
                                slot_index = 1, item_id = "388", qualified_item_id = "(O)388", stack = 5,
                                quality = 0, maximum_stack_size = 999, runtime_type = "StardewValley.Object"
                            })));
                }
                if (observePickupInventory)
                    ChangeState(source.Before, "player", node => PrepareRefillPickupRawSlots(node.AsObject()));
                source.Before.State["environment"] = JsonSerializer.SerializeToElement(new
                {
                    training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                    training_mode = new { status = "available", value = "1" },
                    save_isolation_path = new { status = "available", value = "isolated-refill-trace-fixture" }
                });
                source.Before.StateHash = SnapshotHash.ComputeStateHash(source.Before.State);
            });
        }

        internal async Task Build(Action<SnapshotEnvelope, int>? changeFrame = null, bool? emitObservations = null,
            bool retainCompilation = false)
        {
            if (retainCompilation) Approach.LoadOriginalCompilation();
            else await Approach.Compile();
            var scope = Approach.Open();
            var options = Approach.Watering.Shared.Runtime.LiveOptions;
            options.ObserveNativeOutputs = observeNativeOutputs; options.SkipTraining = true;
            if (observePickupInventory)
            {
                options.ObserveNativeInventoryPayload = options.ObserveNativeClearanceToolInputs = options.ObserveNativeOutputSnapshotAnchor = true;
                options.ExecutionSnapshotProfile = "full";
            }
            options.SaveIsolationPath = "isolated-refill-trace-fixture";
            Assert.Equal("run.seed-acquisition.fixture", options.RunId);
            var before = Approach.Watering.Before;
            var beforePath = scope.SourcePath;
            var rows = new List<JsonObject>();
            var session = Guid.NewGuid().ToString("N");
            var inventoryReferences = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            for (var index = 0; index < Approach.Queue.Items.Length; index++)
            {
                var item = Approach.Queue.Items[index];
                var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
                after.GameTick += 2;
                ChangeState(after, "player", node =>
                {
                    node["tile_x"]!["value"] = scope.Target.TileX;
                    node["tile_y"]!["value"] = scope.Target.TileY;
                });
                if (!resourceNeutral)
                    after.State["trace_fixture"] = JsonSerializer.SerializeToElement(new
                    {
                        status = "available", value = index + 1, source = "declared_fixture",
                        adapter = "refill_trace_fixture", read_at_tick = after.GameTick, confidence = 1.0
                    });
                // Mutate before hashing/writing the frame, result and receipt so guard tests
                // exercise physical snapshot differences rather than stale evidence hashes.
                changeFrame?.Invoke(after, index);
                after.StateHash = SnapshotHash.ComputeStateHash(after.State);
                var afterPath = Path.Combine(options.SnapshotDir, "refill-frame-" + index + ".json");
                Fixture.Save(afterPath, after); AfterFrames.Add(afterPath);
                var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", options,
                    JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, scope.QueueId, item.OptionId);
                var journal = await RuntimeExecutionRequestJournal.WriteAsync(options.SnapshotDir, 200, index + 1,
                    JsonSerializer.Serialize(request, JsonOptions), "http://test/api/v1/training/execute", beforePath);
                Journals.Add(journal.JournalPath);
                var facts = new[] { new SimulatedFactChange { Path = resourceNeutral ? "player.tile" : "trace_fixture",
                    Before = resourceNeutral ? $"{before.State["player"].GetProperty("tile_x").GetProperty("value").GetInt32()},{before.State["player"].GetProperty("tile_y").GetProperty("value").GetInt32()}" : index.ToString(),
                    After = resourceNeutral ? $"{after.State["player"].GetProperty("tile_x").GetProperty("value").GetInt32()},{after.State["player"].GetProperty("tile_y").GetProperty("value").GetInt32()}" : (index + 1).ToString() } };
                var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
                {
                    RunId = options.RunId, QueueId = scope.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                    BeforeStateHash = before.StateHash, Status = "applied", FeedbackAvailable = true,
                    PrimitiveKind = item.OptionId["executor.".Length..], PrimitiveVerificationStatus = "verified", ChangedFacts = facts,
                    ActionOriginObservation = !(emitObservations ?? observeNativeOutputs) ? null : new()
                    {
                        ScopeId = request.ActionOriginObservationScopeId ?? "unrequested.fixture.scope", CaptureSessionId = session, RunId = options.RunId,
                        QueueId = scope.QueueId, QueueItemId = item.QueueItemId, RequestNonce = request.RequestNonce,
                        BeforeStateHash = before.StateHash, LocationId = "Farm", BeforeGameTick = before.GameTick,
                        AfterGameTick = after.GameTick, Status = "observed",
                        InventoryBefore = completeSnapshotInventory ? DeclaredInventoryCounts() : [],
                        InventoryAfter = completeSnapshotInventory ? DeclaredInventoryCounts() : [],
                        ClearanceToolCalls = observePickupInventory ? [] : null,
                        InventoryPayloadBefore = observePickupInventory ? RefillPickupPayload(before, inventoryReferences) : null,
                        InventoryPayloadAfter = observePickupInventory ? RefillPickupPayload(after, inventoryReferences) : null
                    }
                }, JsonOptions)!.AsObject();
                result["execution_request_journal_path"] = journal.JournalPath;
                result["execution_request_nonce"] = request.RequestNonce; result["execution_request_sha256"] = journal.RawRequestSha256;
                result["mechanical_compiled_queue_path"] = scope.QueuePath; result["after_snapshot_path"] = afterPath;
                result["after_state_hash"] = after.StateHash; result["before_game_tick"] = before.GameTick;
                result["after_game_tick"] = after.GameTick; result["after_snapshot_fresh"] = true;
                var resultPath = Path.Combine(options.SnapshotDir, "refill-result-" + index + ".json");
                Fixture.Save(resultPath, result); Results.Add(resultPath);
                var step = JsonSerializer.SerializeToNode(new QueueExecutionStepReceipt
                {
                    QueueItemIndex = index, QueueItemCount = Approach.Queue.Items.Length, OriginalPlannedItemCount = Approach.Queue.Items.Length,
                    QueueId = scope.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId, SourceStateHash = before.StateHash,
                    CompiledCommandStateHash = Approach.Queue.StateHash, AfterStateHash = after.StateHash, StateHashChanged = before.StateHash != after.StateHash,
                    BeforeGameTick = before.GameTick, AfterGameTick = after.GameTick, AfterSnapshotPath = afterPath, AfterSnapshotFresh = true,
                    Status = "applied", PrimitiveKind = item.OptionId["executor.".Length..], PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = ["declared_trace_fixture"], ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions),
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(item, JsonOptions), SelectedQueueCandidateCompleted = index == Approach.Queue.Items.Length - 1
                }, JsonOptions)!.AsObject();
                step["execution_path"] = resultPath; step["execution_request_nonce"] = request.RequestNonce;
                rows.Add(step); before = after; beforePath = afterPath;
            }
            Fixture.Save(AfterPath, before);
            var aggregate = JsonSerializer.SerializeToNode(new QueueExecutionReceiptEnvelope
            {
                RunId = options.RunId, QueueId = scope.QueueId, SourceStateHash = Approach.Queue.StateHash, AfterStateHash = before.StateHash,
                BeforeGameTick = Approach.Watering.Before.GameTick, AfterGameTick = before.GameTick, AfterSnapshotFresh = true,
                QueueExecutionMode = "sequential_queue_items", Status = "applied", Success = true,
                PlannedItemCount = rows.Count, ExecutedItemCount = rows.Count, MaxQueueItemAttempts = rows.Count,
                SelectedCandidateId = scope.CandidateId, SelectedCandidateCompleted = true
            }, JsonOptions)!.AsObject();
            aggregate["step_results"] = new JsonArray(rows.Select(row => (JsonNode)row).ToArray());
            aggregate["after_snapshot_path"] = AfterPath; Fixture.Save(ReceiptPath, aggregate);
        }

        internal CropTeacherRefillApproachNativeTrace Open() => CropTeacherRefillApproachNativeTrace.Open(
            Approach.Open(), ReceiptPath, AfterPath, Approach.Watering.Shared.Runtime.LiveOptions.RunId);
        internal CropTeacherRefillApproachNativeTrace OpenWithSnapshotInventoryContinuity() =>
            CropTeacherRefillApproachNativeTrace.OpenWithSnapshotInventoryContinuity(
                Approach.Open(), ReceiptPath, AfterPath, Approach.Watering.Shared.Runtime.LiveOptions.RunId);
        internal CropTeacherRefillApproachNativeTrace OpenWithResourceEvidence() =>
            CropTeacherRefillApproachNativeTrace.OpenWithResourceEvidence(
                Approach.Open(), ReceiptPath, AfterPath, Approach.Watering.Shared.Runtime.LiveOptions.RunId);

        private static ActionOriginInventoryCount[] DeclaredInventoryCounts() =>
        [
            new() { QualifiedItemId = "(O)745", Quality = 0, Quantity = 40 },
            new() { QualifiedItemId = "(O)388", Quality = 0, Quantity = 5 }
        ];
        public void Dispose() => Approach.Dispose();
    }
}
