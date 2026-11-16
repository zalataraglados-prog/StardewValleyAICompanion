using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
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
    // Declared mail observations through the real compilers/journal/owner; no game or Teacher credit.
    private sealed class NativeMailWorkFixture
    {
        internal OwnedRouteFixture Source { get; } = new(prepareSource: AddMailbox);
        internal RuntimeFixture Runtime => Source.Runtime;
        internal OwnerFiles Files => Source.Files;
        internal CropTeacherSharedSourceSelection Selection { get; private set; } = null!;
        internal string ReceiptPath { get; private set; } = "";
        internal string AfterPath { get; private set; } = "";
        internal string RuntimePath { get; private set; } = "";
        internal ActionQueueEnvelope Queue { get; private set; } = null!;
        internal int Stage { get; private set; }
        internal bool PendingCandidateOnTypedCompletion { get; set; }

        internal void Compile(string sourcePath)
        {
            Stage++;
            Runtime.LiveOptions.SkipTraining = true;
            Runtime.LiveOptions.SaveIsolationPath = "isolated-single-till-fixture";
            var before = LifecycleRead<SnapshotEnvelope>(sourcePath);
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(before, ["mail.process_letter"], true);
            var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "daily.closed_loop");
            if (Selection is not null)
            {
                var lease = SelectedQueueDecisionLease.CreateFromPlan(LifecycleRead<SmallModelPlanEnvelope>(Selection.DecisionPlanPath),
                    JsonNode.Parse(File.ReadAllText(Selection.DecisionRankingPath))!.AsObject());
                var matches = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(
                    JsonSerializer.SerializeToNode(rows, JsonOptions)!.AsArray(), lease.CandidateAt(Selection.SelectedQueueIndex));
                rows = matches.Deserialize<PolicyEventCandidatePrediction[]>(JsonOptions)!;
            }
            var plan = new DailyPlanCompiler().Compile(rows, before.StateHash, maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
            Assert.True(plan.Steps.Length > 0, JsonSerializer.Serialize(availability, JsonOptions));
            Queue = new ActionQueueCompiler().Compile(plan, before, Runtime.Repository.Get(before));
            Assert.True(Queue.Status == "pending", JsonSerializer.Serialize(Queue, JsonOptions));
            Assert.Single(Queue.Items);
            var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
            var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
            var prefix = Path.Combine(Source.Source.RuntimePath, "mail-work-" + Stage);
            string Save(string suffix, object value) { var path = prefix + suffix; Fixture.Save(path, value); return path; }
            var planPath = Save("-plan.json", plan);
            var rankingPath = Save("-ranking.json", ranking);
            var original = Selection;
            var originalId = original is null ? selected.CandidateId : SelectedQueueDecisionLease.CreateFromPlan(
                LifecycleRead<SmallModelPlanEnvelope>(original.DecisionPlanPath),
                JsonNode.Parse(File.ReadAllText(original.DecisionRankingPath))!.AsObject()).CandidateAt(original.SelectedQueueIndex).CandidateId;
            Selection = new(original?.DecisionSnapshotPath ?? sourcePath, original?.DecisionPlanPath ?? planPath,
                original?.DecisionRankingPath ?? rankingPath, planPath, Save("-queue.json", Queue), Save("-refresh.json", new JsonObject
                {
                    ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = before.StateHash,
                    ["goal_id"] = plan.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
                    ["selected_queue_index"] = 0, ["selected_candidate_id"] = originalId, ["selected_option_id"] = "mail.process_letter",
                    ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
                }), 0, []) { DecisionReference = original?.DecisionReference };
            ReceiptPath = prefix + "-receipt.json"; AfterPath = prefix + "-after.json";
            RuntimePath = Files.Native + "-mail-" + Stage;
        }

        internal void BindDecision(CropTeacherDailyDecisionReference reference) => Selection = Selection with { DecisionReference = reference };

        internal async Task<JsonObject> Emit(SnapshotEnvelope before)
        {
            var item = Assert.Single(Queue.Items);
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
            after.GameTick += 2;
            if (item.OptionId == "executor.move_to_tile") ChangeState(after, "player", node =>
            { node["tile_x"]!["value"] = 1; node["tile_y"]!["value"] = 1; });
            else if (item.OptionId == "executor.interact")
            {
                ChangeState(after, "quests", node =>
                {
                    node["mailbox"] = Field(Array.Empty<string>(), after.GameTick);
                    node["mail_received"] = Field(new[] { "mail-work" }, after.GameTick);
                    node["mailbox_processing"] = Field(new { available = false, queue_count = 0,
                        blocked_diagnostics = new[] { "mailbox_queue_empty" } }, after.GameTick);
                });
                SetMenu(after, open: true);
            }
            else { Assert.Equal("executor.close_menu", item.OptionId); SetMenu(after, open: false); }
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            Fixture.Save(AfterPath, after); Runtime.Store.Snapshots[after.StateHash] = after;
            var sourcePath = Stage == 1 ? Selection.DecisionSnapshotPath : Path.Combine(Source.Source.RuntimePath, "mail-work-" + (Stage - 1) + "-after.json");
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", Runtime.LiveOptions,
                JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, Queue.QueueId, item.OptionId);
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(Runtime.LiveOptions.SnapshotDir, 410 + Stage, 1,
                JsonSerializer.Serialize(request, JsonOptions), "http://test/api/v1/training/execute", sourcePath);
            var facts = new[] { new SimulatedFactChange { Path = item.OptionId == "executor.move_to_tile" ? "player.tile" : "menus.active_menu",
                Before = "declared-before", After = "declared-after" } };
            var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult { RunId = request.RunId, QueueId = Queue.QueueId,
                QueueItemId = item.QueueItemId, OptionId = item.OptionId, BeforeStateHash = before.StateHash, Status = "applied",
                FeedbackAvailable = true, PrimitiveKind = item.OptionId["executor.".Length..], PrimitiveVerificationStatus = "verified",
                ChangedFacts = facts }, JsonOptions)!.AsObject();
            result["execution_request_journal_path"] = journal.JournalPath; result["execution_request_nonce"] = request.RequestNonce;
            result["execution_request_sha256"] = journal.RawRequestSha256; result["mechanical_compiled_queue_path"] = Selection.QueuePath;
            result["after_snapshot_path"] = AfterPath; result["after_state_hash"] = after.StateHash;
            result["before_game_tick"] = before.GameTick; result["after_game_tick"] = after.GameTick; result["after_snapshot_fresh"] = true;
            var resultPath = ReceiptPath + "-result.json"; Fixture.Save(resultPath, result);
            var candidate = item.NormalizedCommand.Parameters.Single(row => row.Name == "precondition" && row.Value.StartsWith("candidate_id:")).Value["candidate_id:".Length..];
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", Queue, before, after,
                request.RunId, candidate, item.OptionId["executor.".Length..]);
            var objective = QueueReplanFilter.ReadObjectiveContinuation(JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject())!;
            var completed = QueueReplanFilter.CompletesObjectiveContinuation(JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), objective, "applied");
            receipt.Success = receipt.SelectedCandidateCompleted = receipt.StepResults[0].SelectedQueueCandidateCompleted =
                completed && !PendingCandidateOnTypedCompletion;
            receipt.StepResults[0].AfterGameTick = after.GameTick; receipt.StepResults[0].AfterSnapshotPath = AfterPath;
            receipt.StepResults[0].ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions);
            var aggregate = JsonSerializer.SerializeToNode(receipt, JsonOptions)!.AsObject();
            aggregate["after_snapshot_path"] = AfterPath; aggregate["queue_progress_verified"] = true;
            aggregate["objective_continuation_completed"] = completed;
            aggregate["objective_continuation"] = completed && !PendingCandidateOnTypedCompletion ? null : objective.DeepClone();
            aggregate["completed_objective_continuations"] = completed ? new JsonArray(objective.DeepClone()) : new JsonArray();
            aggregate["step_results"]![0]!["execution_path"] = resultPath;
            aggregate["step_results"]![0]!["execution_request_nonce"] = request.RequestNonce;
            Fixture.Save(ReceiptPath, aggregate);
            return new JsonObject { ["execution_path"] = ReceiptPath, ["after_snapshot_path"] = AfterPath };
        }

        private static JsonNode Field(object value, long tick) => JsonSerializer.SerializeToNode(new
            { status = "available", value, source = new { kind = "test", path = "declared-mail-work" }, adapter = "test", read_at_tick = tick, confidence = 1d })!;

        private static void AddMailbox(Fixture source)
        {
            var before = source.Before;
            before.GameVersion = "1.6.15";
            before.State["environment"] = JsonSerializer.SerializeToElement(new
            {
                training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                training_mode = new { status = "available", value = "1" },
                save_isolation_path = new { status = "available", value = "isolated-single-till-fixture" }
            });
            ChangeState(before, "player", node =>
            {
                node["tile_x"]!["value"] = 2; node["tile_y"]!["value"] = 1;
                node["inventory_capacity"] = Field(new { empty_slots = 12 }, before.GameTick);
                node["facing_direction"] = Field(0, before.GameTick);
            });
            before.State["quests"] = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["mailbox"] = Field(new[] { "mail-work" }, before.GameTick), ["mail_received"] = Field(Array.Empty<string>(), before.GameTick),
                ["mailbox_processing"] = Field(new { available = true, queue_count = 1, queue_mail_ids_native_order = new[] { "mail-work" },
                    pending_mail_id = "mail-work", mail_data_found = true, mail_data_sha256 = "declared-mail-data", directives = Array.Empty<object>(),
                    constructor_effect_classes = Array.Empty<string>(), attachment_slot_upper_bound = 0, inventory_empty_slots = 12,
                    attachment_capacity_sufficient = true, mailbox_location_id = "Farm", mailbox_action_tile_x = 1, mailbox_action_tile_y = 0,
                    stand_tile_x = 1, stand_tile_y = 1, menu_clear = true, status = "ready", blocked_diagnostics = Array.Empty<string>() }, before.GameTick)
            });
            ChangeState(before, "locations", node => node["route_action_branch_coverage"] = Field(new { rows = new[]
                { new { tile_x = 1, tile_y = 0, branch = "Mailbox", route_training_blocked = false } } }, before.GameTick));
            ChangeState(before, "current_location", node => node["route_context"] = Field(new { }, before.GameTick));
            SetMenu(before, open: false);
        }

        private static void SetMenu(SnapshotEnvelope snapshot, bool open) => snapshot.State["menus"] = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["active_menu"] = Field(new { is_open = open, type = open ? "LetterViewerMenu" : "none" }, snapshot.GameTick),
            ["sleep_prompt_context"] = Field(new { prompt_open = false }, snapshot.GameTick),
            ["menu_specific_state"] = Field(open ? new { kind = "letter_viewer", mail_title = "mail-work", is_mail = true,
                is_from_collection = false, page = 0, page_count = 1, message_pages = new[] { "declared mail" }, scale = 1,
                can_receive_input = true, ready_to_close = true, has_interactable = false, should_show_interactable = false,
                items_left_to_grab = false, attachment_count = 0, attachments = Array.Empty<object>(), quest_id = "",
                special_order_id = "", has_quest_or_special_order = false, money_included = 0, learned_recipe = "",
                cooking_or_crafting = "", destroy = false, menu_identity_sha256 = "declared-mail-menu" } : (object)new { }, snapshot.GameTick)
        });
    }
}
