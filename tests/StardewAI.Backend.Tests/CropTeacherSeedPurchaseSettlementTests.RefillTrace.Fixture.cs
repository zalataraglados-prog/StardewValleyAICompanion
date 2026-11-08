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

        internal RefillTraceFixture(bool repair)
        {
            Approach = new(repair, prepareSource: source =>
            {
                source.Before.GameVersion = "1.6.15";
                source.Before.State["environment"] = JsonSerializer.SerializeToElement(new
                {
                    training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                    training_mode = new { status = "available", value = "1" },
                    save_isolation_path = new { status = "available", value = "isolated-refill-trace-fixture" }
                });
                source.Before.StateHash = SnapshotHash.ComputeStateHash(source.Before.State);
            });
        }

        internal async Task Build()
        {
            await Approach.Compile();
            var scope = Approach.Open();
            var options = Approach.Watering.Shared.Runtime.LiveOptions;
            options.ObserveNativeOutputs = true; options.SkipTraining = true;
            options.SaveIsolationPath = "isolated-refill-trace-fixture";
            Assert.Equal("run.seed-acquisition.fixture", options.RunId);
            var before = Approach.Watering.Before;
            var beforePath = scope.SourcePath;
            var rows = new List<JsonObject>();
            var session = Guid.NewGuid().ToString("N");
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
                after.State["trace_fixture"] = JsonSerializer.SerializeToElement(new { status = "available", value = index + 1 });
                after.StateHash = SnapshotHash.ComputeStateHash(after.State);
                var afterPath = Path.Combine(options.SnapshotDir, "refill-frame-" + index + ".json");
                Fixture.Save(afterPath, after); AfterFrames.Add(afterPath);
                var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", options,
                    JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, scope.QueueId, item.OptionId);
                var journal = await RuntimeExecutionRequestJournal.WriteAsync(options.SnapshotDir, 200, index + 1,
                    JsonSerializer.Serialize(request, JsonOptions), "http://test/api/v1/training/execute", beforePath);
                Journals.Add(journal.JournalPath);
                var facts = new[] { new SimulatedFactChange { Path = "trace_fixture", Before = index.ToString(), After = (index + 1).ToString() } };
                var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
                {
                    RunId = options.RunId, QueueId = scope.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                    BeforeStateHash = before.StateHash, Status = "applied", FeedbackAvailable = true,
                    PrimitiveKind = item.OptionId["executor.".Length..], PrimitiveVerificationStatus = "verified", ChangedFacts = facts,
                    ActionOriginObservation = new()
                    {
                        ScopeId = request.ActionOriginObservationScopeId!, CaptureSessionId = session, RunId = options.RunId,
                        QueueId = scope.QueueId, QueueItemId = item.QueueItemId, RequestNonce = request.RequestNonce,
                        BeforeStateHash = before.StateHash, LocationId = "Farm", BeforeGameTick = before.GameTick,
                        AfterGameTick = after.GameTick, Status = "observed"
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
                    CompiledCommandStateHash = Approach.Queue.StateHash, AfterStateHash = after.StateHash, StateHashChanged = true,
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
        public void Dispose() => Approach.Dispose();
    }
}
