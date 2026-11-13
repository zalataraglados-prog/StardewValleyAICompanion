using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Synthetic original protocol, not game evidence. Private tool state is deliberately not predicted.
    private sealed class SiteClearanceNativeFixture
    {
        internal SiteTillFiles Files { get; }
        internal CropTeacherSiteClearanceCompilation Preparation { get; }
        internal SnapshotEnvelope After { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        internal string ResultPath => Path.Combine(Files.F.Source.RuntimePath, "clearance-native-frame-result.json");
        internal string JournalPath { get; private set; } = "";
        internal string AfterPath => Path.Combine(Files.F.Source.RuntimePath, "clearance-native-after.json");
        internal string ReceiptPath => Path.Combine(Files.F.Source.RuntimePath, "clearance-native-aggregate.json");
        private readonly bool product;

        internal SiteClearanceNativeFixture(bool product = true, string frontier = "clear")
        {
            this.product = product;
            Files = new SiteTillFiles(frontier: frontier, prepareSource: source =>
            {
                source.Before.GameVersion = "1.6.15";
                source.Before.State["environment"] = JsonSerializer.SerializeToElement(new
                {
                    training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                    training_mode = new { status = "available", value = "1" },
                    save_isolation_path = new { status = "available", value = "isolated-clearance-fixture" }
                });
                ChangeState(source.Before, "farm", farm => farm["objects"] = JsonSerializer.SerializeToNode(new
                {
                    status = "available", value = new[]
                    { new { tile_x = frontier == "access" ? 1 : 0, tile_y = 0, qualified_item_id = "(O)784" },
                      new { tile_x = 1, tile_y = 1, qualified_item_id = "(O)784" } }
                }));
            });
            Preparation = Files.OpenClear();
        }

        internal CropTeacherOriginalClearanceCompilation OwnerCompilation => new(Id, Files.Observation,
            Files.Request, Files.Plan, Files.Narrow, Files.Full, Files.Queue, ReceiptPath, AfterPath, Files.F.Files.Native);

        internal async Task Build()
        {
            var before = Files.F.Source.Before;
            After = ReadSiteSnapshot(Files.F.Source.SourcePath); After.GameTick += 2;
            ChangeState(After, "farm", farm => farm["objects"]!["value"] = new JsonArray());
            ChangeState(After, "current_location", location => location["objects"]!["value"] = new JsonArray());
            // Normal native metadata/collateral changes are observations, not a second simulation contract.
            ChangeState(After, "player", player => player["tool_internal_state"] =
                JsonSerializer.SerializeToNode(new { status = "available", value = new { swingTicker = 8, hasBeenInInventory = true } }));
            After.StateHash = SnapshotHash.ComputeStateHash(After.State); Fixture.Save(AfterPath, After);
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(Files.Queue), JsonOptions)!;
            var item = Assert.Single(queue.Items);
            var options = Files.F.Runtime.LiveOptions; options.SkipTraining = true;
            options.SaveIsolationPath = "isolated-clearance-fixture";
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", options,
                JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, queue.QueueId, item.OptionId);
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(options.SnapshotDir, 301, 1,
                JsonSerializer.Serialize(request, JsonOptions), product ? "http://test/api/v1/product/execute" :
                    "http://test/api/v1/training/execute", Preparation.SourcePath);
            JournalPath = journal.JournalPath;
            var facts = new[] { new SimulatedFactChange { Path = "farm.objects", Before = "2", After = "0" } };
            var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
            {
                RunId = request.RunId, QueueId = queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                BeforeStateHash = before.StateHash, Status = "applied", FeedbackAvailable = true,
                PrimitiveKind = "clear_obstacle", PrimitiveVerificationStatus = "verified", ChangedFacts = facts,
                ToolQualifiedItemId = "(W)47", ToolUseCount = 1, TargetLocation = "Farm",
                TargetTileX = Preparation.Target.TileX, TargetTileY = Preparation.Target.TileY,
                ProductAfterStateHash = After.StateHash, ProductAfterGameTick = After.GameTick
            }, JsonOptions)!.AsObject();
            result["execution_request_journal_path"] = JournalPath; result["execution_request_nonce"] = request.RequestNonce;
            result["execution_request_sha256"] = journal.RawRequestSha256; result["mechanical_compiled_queue_path"] = Files.Queue;
            result["after_snapshot_path"] = AfterPath; result["after_state_hash"] = After.StateHash;
            result["before_game_tick"] = before.GameTick; result["after_game_tick"] = After.GameTick; result["after_snapshot_fresh"] = true;
            if (product)
            {
                result["source"] = "product_executor"; result["product_executor_schema_version"] = "stardewai.product_executor.v1";
                result["product_authorization_status"] = "authorized";
                result["product_receipt_id"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
                result["product_request_sha256"] = journal.ProductRequestSha256; result["product_request_before_state_hash"] = before.StateHash;
                result["product_before_state_hash"] = before.StateHash; result["product_before_game_tick"] = before.GameTick;
                result["product_request_state_hash_drift"] = false; result["product_dispatch_guard"] = "native_action_preconditions";
                result["product_replan_required"] = false; result["product_idempotent_replay"] = false;
            }
            Fixture.Save(ResultPath, result);
            var aggregate = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue,
                before, After, request.RunId, Preparation.CandidateId, "clear_obstacle");
            aggregate.StepResults[0].ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions);
            aggregate.StepResults[0].AfterGameTick = After.GameTick;
            aggregate.StepResults[0].AfterSnapshotPath = AfterPath;
            var raw = JsonSerializer.SerializeToNode(aggregate, JsonOptions)!.AsObject();
            raw["step_results"]![0]!["execution_path"] = ResultPath;
            raw["step_results"]![0]!["execution_request_nonce"] = request.RequestNonce;
            raw["after_snapshot_path"] = AfterPath; Fixture.Save(ReceiptPath, raw);
            Transition = new("crop_actions", Preparation.TotalDay, Files.Queue, Preparation.SourcePath,
                ReceiptPath, AfterPath, request.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, Preparation.CandidateId);
        }
    }
}
