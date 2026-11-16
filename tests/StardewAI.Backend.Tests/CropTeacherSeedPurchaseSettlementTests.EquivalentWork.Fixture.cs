using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Declared equivalent outcome, real candidate/compiler/wire/owner; not a native game sample.
    private sealed class EquivalentWorkFixture(bool product) : IDisposable
    {
        internal OwnedRouteFixture Source { get; } = new(prepareSource: AddPrairieKing);
        internal RuntimeFixture Runtime => Source.Runtime;
        internal OwnerFiles Files => Source.Files;
        internal CropTeacherSharedSourceSelection Selection { get; private set; } = null!;
        internal ActionQueueEnvelope Queue { get; private set; } = null!;
        internal string ReceiptPath => Path.Combine(Source.Source.RuntimePath, "equivalent-receipt.json");
        internal string ResultPath => Path.Combine(Source.Source.RuntimePath, "equivalent-result.json");
        internal string AfterPath => Path.Combine(Source.Source.RuntimePath, "equivalent-after.json");
        internal string RuntimePath => Files.Native + "-equivalent";
        internal CropTeacherManagedSeedOwnerOptions Options => Files.Options with
        { ExecutorVersion = product ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor };

        internal void Compile(string sourcePath)
        {
            Runtime.LiveOptions.SkipTraining = true;
            Runtime.LiveOptions.SaveIsolationPath = "isolated-equivalent-fixture";
            Runtime.LiveOptions.UseProductExecutor = product;
            var before = LifecycleRead<SnapshotEnvelope>(sourcePath);
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(before, ["minigame.play_prairie_king"], true);
            var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "daily.closed_loop");
            var plan = new DailyPlanCompiler().Compile(rows, before.StateHash, maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
            Queue = new ActionQueueCompiler().Compile(plan, before, Runtime.Repository.Get(before));
            Assert.True(Queue.Status == "pending", JsonSerializer.Serialize(Queue, JsonOptions));
            Assert.Equal("executor.play_prairie_king", Assert.Single(Queue.Items).OptionId);
            string Save(string name, object value)
            { var path = Path.Combine(Source.Source.RuntimePath, name); Fixture.Save(path, value); return path; }
            var planPath = Save("equivalent-plan.json", plan);
            var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
            var chosen = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
            Selection = new(sourcePath, planPath, Save("equivalent-ranking.json", ranking), planPath,
                Save("equivalent-queue.json", Queue), Save("equivalent-refresh.json", new JsonObject
                {
                    ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = before.StateHash,
                    ["goal_id"] = plan.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
                    ["selected_queue_index"] = 0, ["selected_candidate_id"] = chosen.CandidateId,
                    ["selected_option_id"] = "minigame.play_prairie_king", ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
                }), 0, []);
        }

        internal async Task<JsonObject> Emit(SnapshotEnvelope before, string fault)
        {
            var item = Assert.Single(Queue.Items);
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
            after.GameTick += 1800;
            ChangeState(after, "player", player =>
            {
                var prairie = player["prairie_king"]!["value"]!;
                prairie["completed_before"] = 1; prairie["completed_without_dying_before"] = 1;
                prairie["gate_status"] = "complete_prairie_king_without_dying";
            });
            Fixture.Save(AfterPath, after); Runtime.Store.Snapshots[after.StateHash] = after;
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", Runtime.LiveOptions,
                JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, Queue.QueueId, item.OptionId);
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(Runtime.LiveOptions.SnapshotDir, 450, 1,
                JsonSerializer.Serialize(request, JsonOptions), product ? "http://test/api/v1/product/execute" :
                    "http://test/api/v1/training/execute", Selection.DecisionSnapshotPath);
            var facts = new[] { new SimulatedFactChange { Path = "player.stats.completedPrairieKing", Before = "0", After = "1" } };
            var reasons = new[] { "ai_actor_timed_equivalent_elapsed", "not_native_perfect_proxy_play" };
            var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
            {
                RunId = request.RunId, QueueId = Queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                BeforeStateHash = before.StateHash, Status = "applied", FeedbackAvailable = true,
                PrimitiveKind = "play_prairie_king", PrimitiveVerificationStatus = "simulated_equivalent",
                PrimitiveVerificationReasons = reasons, ChangedFacts = facts
            }, JsonOptions)!.AsObject();
            result["execution_request_journal_path"] = journal.JournalPath; result["execution_request_nonce"] = request.RequestNonce;
            result["execution_request_sha256"] = journal.RawRequestSha256; result["mechanical_compiled_queue_path"] = Selection.QueuePath;
            result["after_snapshot_path"] = AfterPath; result["after_state_hash"] = after.StateHash;
            result["before_game_tick"] = before.GameTick; result["after_game_tick"] = after.GameTick; result["after_snapshot_fresh"] = true;
            if (product)
            {
                result["source"] = "product_executor"; result["product_executor_schema_version"] = "stardewai.product_executor.v1";
                result["product_authorization_status"] = "authorized";
                result["product_receipt_id"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
                result["product_request_sha256"] = journal.ProductRequestSha256; result["product_request_before_state_hash"] = before.StateHash;
                result["product_before_state_hash"] = before.StateHash; result["product_before_game_tick"] = before.GameTick;
                result["product_after_state_hash"] = after.StateHash; result["product_after_game_tick"] = after.GameTick;
                result["product_request_state_hash_drift"] = false; result["product_dispatch_guard"] = "native_action_preconditions";
                result["product_replan_required"] = fault != "replan_flag"; result["product_idempotent_replay"] = false;
            }
            if (fault == "result_status") result["primitive_verification_status"] = "verified";
            Fixture.Save(ResultPath, result);
            var candidate = item.NormalizedCommand.Parameters.Single(row => row.Name == "precondition" && row.Value.StartsWith("candidate_id:")).Value["candidate_id:".Length..];
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", Queue, before, after,
                request.RunId, candidate, "play_prairie_king");
            var step = Assert.Single(receipt.StepResults);
            step.PrimitiveVerificationStatus = fault == "aggregate_status" ? "verified" : "simulated_equivalent";
            step.PrimitiveVerificationReasons = reasons; step.AfterGameTick = after.GameTick; step.AfterSnapshotPath = AfterPath;
            step.ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions);
            var aggregate = JsonSerializer.SerializeToNode(receipt, JsonOptions)!.AsObject();
            aggregate["executor_version"] = Options.ExecutorVersion;
            aggregate["after_snapshot_path"] = AfterPath;
            aggregate["step_results"]![0]!["execution_path"] = ResultPath;
            aggregate["step_results"]![0]!["execution_request_nonce"] = request.RequestNonce;
            Fixture.Save(ReceiptPath, aggregate);
            return new JsonObject { ["execution_path"] = ReceiptPath, ["after_snapshot_path"] = AfterPath };
        }

        private static void AddPrairieKing(Fixture source)
        {
            var before = source.Before; before.GameVersion = "1.6.15";
            JsonNode Field(object value) => JsonSerializer.SerializeToNode(new { status = "available", value, read_at_tick = before.GameTick })!;
            before.State["environment"] = JsonSerializer.SerializeToElement(new
            {
                training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                training_mode = new { status = "available", value = "1" },
                save_isolation_path = new { status = "available", value = "isolated-equivalent-fixture" }
            });
            ChangeState(before, "player", player =>
            {
                player["location_id"]!["value"] = "Saloon"; player["tile_x"]!["value"] = 1; player["tile_y"]!["value"] = 1;
                player["prairie_king"] = Field(new
                {
                    schema_version = "prairie_king.v1", projection_status = "complete_locked_base_1.6.15",
                    projection_fingerprint = new string('a', 64), gate_status = "ready", invocation_policy = "autonomous_timed_equivalent",
                    native_proxy_policy = "post_core_training_player_command_only", location_id = "Saloon", completed_before = 0,
                    completed_without_dying_before = 0, completion_goal = "complete_without_dying", dialogue_key = "none", dialogue_response_key = "none",
                    equivalent_duration_ticks = 108000, equivalent_acceleration = 60, native_completion_trigger = "AbigailGame.usePowerup(-3)",
                    interaction_tiles = new[] { new { tile_x = 1, tile_y = 0, action_raw = "Arcade_Prairie", action_token = "Arcade_Prairie" } },
                    equivalent_contract = "Saloon_Arcade_Prairie_checkAction_optional_CowboyGame_NewGame_then_timed_equivalent_then_AbigailGame_usePowerup_minus3_native_phase1_settlement"
                });
            });
            ChangeState(before, "locations", locations => locations["collision_grid"] = Field(new
                { location_id = "Saloon", width = 40, height = 32, notable_tiles = Array.Empty<object>() }));
            ChangeState(before, "locations", locations => locations["route_connectors"] = Field(new
                { location_id = "Saloon", connectors = Array.Empty<object>() }));
            ChangeState(before, "current_location", location => location["map"] = Field(new
                { id = "Saloon", width = 40, height = 32 }));
            ChangeState(before, "farm", farm => farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["location_id"] = "Saloon");
            before.State["menus"] = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["active_menu"] = Field(new { is_open = false, type = "none" }),
                ["sleep_prompt_context"] = Field(new { prompt_open = false }),
                ["menu_specific_state"] = Field(new { })
            });
        }

        public void Dispose() => Source.Dispose();
    }
}
