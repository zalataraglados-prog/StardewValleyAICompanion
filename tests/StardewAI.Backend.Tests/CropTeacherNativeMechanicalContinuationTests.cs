using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherNativeMechanicalContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshQueueUsesItsOwnItemsAndCountWithoutWeakeningImmutableValidation(bool expanded)
    {
        var f = new Fixture(expanded);
        var binding = f.Read()!;
        var queues = (ActionQueueEnvelope[])binding.GetType().GetProperty("Queues")!.GetValue(binding)!;
        Assert.Equal(f.Receipt.StepResults.Length, queues.Length);
        Assert.Same(f.Root, queues[0]);
        Assert.All(queues.Skip(1), queue => Assert.Equal("fresh", queue.QueueId));
        Assert.Empty(f.Validate(queues));
        Assert.NotEmpty(f.Validate(null));
    }

    [Theory]
    [InlineData("previous_queue")]
    [InlineData("trigger_item")]
    [InlineData("policy")]
    [InlineData("lock")]
    [InlineData("kind")]
    [InlineData("stale_source")]
    [InlineData("foreign_candidate")]
    [InlineData("foreign_goal")]
    [InlineData("response_queue")]
    [InlineData("response_plan")]
    [InlineData("missing_evidence")]
    [InlineData("wrong_origin")]
    public void FreshQueueRejectsBrokenLineageOrSemanticallyMismatchedCompileArtifacts(string fault)
    {
        var f = new Fixture(false);
        var previous = f.ReadFile(f.FramePaths[0]);
        switch (fault)
        {
            case "previous_queue": previous["queue_replan_previous_queue_id"] = "foreign"; break;
            case "trigger_item": previous["queue_replan_trigger_queue_item_id"] = "foreign"; break;
            case "policy": previous["policy_model_invoked"] = true; break;
            case "lock": previous["selected_queue_lock_preserved"] = false; break;
            case "kind": previous["queue_replan_kind"] = "policy_redecision"; break;
            case "missing_evidence": previous["queue_replan_evidence_path"] = Path.Combine(f.Directory, "never-created.json"); break;
            case "wrong_origin": previous["mechanical_compiled_queue_path"] = f.FreshPath; break;
            case "stale_source": f.Change(f.EvidencePath, node => node["state_hash"] = f.Root.StateHash); break;
            case "foreign_candidate": f.Change(f.EvidencePath, node => node["selected_candidate_id"] = "foreign"); break;
            case "foreign_goal": f.Change(f.PlanPath, node => node["goal_id"] = "foreign"); break;
            case "response_queue": f.Change(f.ResponsePath, node => node["action_queue"]!["queue_id"] = "foreign"); break;
            case "response_plan": f.Change(f.ResponsePath, node => node["plan"]!["state_hash"] = "foreign"); break;
        }
        f.Save(f.FramePaths[0], previous);
        var error = Assert.Throws<TargetInvocationException>(() => f.Read());
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void ImmutableModeDoesNotAcquireNewRefreshArtifactDependencies()
    {
        var f = new Fixture(false);
        foreach (var step in f.Receipt.StepResults)
        {
            step.QueueId = f.Root.QueueId;
            step.CompiledCommandStateHash = f.Root.StateHash;
        }
        Assert.Null(f.Read(new JsonObject()));
    }

    [Fact]
    public void EmptyImmutableInputKeepsItsExistingRejectionDiagnostics()
    {
        var f = new Fixture(false);
        f.Root.Items = [];
        f.Receipt.StepResults = [];
        f.Receipt.PlannedItemCount = f.Receipt.ExecutedItemCount = 0;
        var reasons = f.Validate(null);
        Assert.Contains("execution_receipt_compiled_queue_invalid", reasons);
        Assert.DoesNotContain("queue_execution_receipt_item_count_mismatch", reasons);
    }

    // Metadata/validator fixtures only. Exact native request/frame wiring is
    // covered by the existing wire tests and retained actual114 offline replay.
    private sealed class Fixture
    {
        internal string Directory { get; } = Path.Combine(Path.GetTempPath(), "stardewai-mechanical", Guid.NewGuid().ToString("N"));
        internal ActionQueueEnvelope Root { get; }
        internal QueueExecutionReceiptEnvelope Receipt { get; }
        internal string[] FramePaths { get; }
        internal string FreshPath => Path.Combine(Directory, "fresh.json");
        internal string PlanPath => Path.Combine(Directory, "plan.json");
        internal string ResponsePath => Path.Combine(Directory, "response.json");
        internal string EvidencePath => Path.Combine(Directory, "evidence.json");
        private string RootPath => Path.Combine(Directory, "root.json");
        private readonly ActionQueueEnvelope fresh;
        private readonly JsonObject raw;
        private static readonly Type Reader = typeof(CropTeacherSharedSourceReturn).Assembly.GetType(
            "StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!;

        internal Fixture(bool expanded)
        {
            System.IO.Directory.CreateDirectory(Directory);
            Root = Queue("root", new string('a', 64), "root-move", "old-terminal");
            fresh = Queue("fresh", new string('b', 64), expanded ? new[] { "new-route", "new-terminal" } : new[] { "new-terminal" });
            Save(RootPath, Root); Save(FreshPath, fresh);
            var plan = new SmallModelPlanEnvelope { StateHash = fresh.StateHash, GoalId = "goal", ExecutionMode = fresh.ExecutionMode };
            Save(PlanPath, plan);
            var rootPlanPath = Path.Combine(Directory, "root-plan.json");
            Save(rootPlanPath, new SmallModelPlanEnvelope { StateHash = Root.StateHash, GoalId = "goal", ExecutionMode = Root.ExecutionMode });
            Save(ResponsePath, new { schema_version = "daily_plan_compile_response.v1", plan, action_queue = fresh });
            var objective = new JsonObject { ["kind"] = "economy_shipping", ["option_id"] = "economy.ship_items" };
            Save(EvidencePath, new JsonObject
            {
                ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = fresh.StateHash,
                ["goal_id"] = "goal", ["selected_candidate_id"] = "original", ["selected_queue_index"] = 0,
                ["selected_candidate_count"] = 1, ["selected_option_id"] = "economy.ship_items",
                ["policy_model_invoked"] = false, ["selection_locked"] = true, ["objective"] = objective.DeepClone(),
                ["availability"] = new JsonObject { ["state_hash"] = fresh.StateHash }
            });
            var count = expanded ? 3 : 2;
            FramePaths = Enumerable.Range(0, count).Select(index => Path.Combine(Directory, "frame-" + index + ".json")).ToArray();
            var steps = new List<QueueExecutionStepReceipt>();
            var source = Root.StateHash;
            for (var index = 0; index < count; index++)
            {
                var queue = index == 0 ? Root : fresh;
                var item = queue.Items[index == 0 ? 0 : index - 1];
                var after = new string((char)('b' + index), 64);
                steps.Add(new QueueExecutionStepReceipt
                {
                    QueueItemIndex = index, QueueItemCount = index == 0 ? 2 : count, OriginalPlannedItemCount = 2,
                    QueueId = queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                    SourceStateHash = source, CompiledCommandStateHash = queue.StateHash, AfterStateHash = after,
                    StateHashChanged = true, BeforeGameTick = index, AfterGameTick = index + 1, AfterSnapshotFresh = true,
                    Status = "applied", PrimitiveKind = "move_to_tile", PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = ["declared"], SelectedQueueCandidateCompleted = index == count - 1,
                    ChangedFacts = JsonSerializer.SerializeToElement(new[] { new { path = "player.tile_x", before = index, after = index + 1 } }),
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(item)
                });
                source = after;
                var frame = new JsonObject
                {
                    ["schema_version"] = "training_execution_result.v1", ["mechanical_compiled_queue_path"] = index == 0 ? RootPath : FreshPath,
                    ["effective_compiled_queue_path"] = RootPath, ["effective_model_plan_path"] = rootPlanPath,
                    ["effective_selected_candidate_id"] = "original", ["effective_selected_queue_index"] = 0, ["policy_model_invoked_for_step"] = false
                };
                if (index == 0)
                {
                    frame["queue_replan_applied"] = true; frame["queue_replan_kind"] = "selected_queue_mechanical_continuation";
                    frame["policy_model_invoked"] = false; frame["selected_queue_lock_preserved"] = true;
                    frame["queue_replan_trigger_status"] = "applied"; frame["queue_replan_source_state_hash"] = fresh.StateHash;
                    frame["queue_replan_previous_queue_id"] = Root.QueueId; frame["queue_replan_trigger_queue_item_id"] = item.QueueItemId;
                    frame["queue_replan_queue_id"] = fresh.QueueId; frame["queue_replan_queue_path"] = FreshPath;
                    frame["queue_replan_plan_path"] = PlanPath; frame["queue_replan_response_path"] = ResponsePath; frame["queue_replan_evidence_path"] = EvidencePath;
                }
                Save(FramePaths[index], frame);
            }
            Receipt = new QueueExecutionReceiptEnvelope
            {
                RunId = "run", QueueId = Root.QueueId, SourceStateHash = Root.StateHash, AfterStateHash = source,
                BeforeGameTick = 0, AfterGameTick = count, AfterSnapshotFresh = true, QueueExecutionMode = "sequential_queue_items",
                Status = "applied", Success = true, SelectedCandidateCompleted = true, SelectedCandidateId = "original",
                PlannedItemCount = 2, ExecutedItemCount = count, MaxQueueItemAttempts = 12, StepResults = steps.ToArray()
            };
            raw = new JsonObject
            {
                ["step_results"] = new JsonArray(FramePaths.Select(path => (JsonNode)new JsonObject { ["execution_path"] = path }).ToArray()),
                ["selected_candidate_completed"] = true, ["objective_continuation_completed"] = true,
                ["objective_continuation"] = null, ["completed_objective_continuations"] = new JsonArray(objective)
            };
        }

        internal object? Read(JsonObject? overrideRaw = null) => Reader.GetMethod("ReadMechanicalContinuation", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [RootPath, Root, Receipt, JsonSerializer.SerializeToElement(overrideRaw ?? raw), "original"]);

        internal string[] Validate(ActionQueueEnvelope[]? queues)
        {
            var validator = Reader.Assembly.GetType("StardewAI.GoalConditionedBootstrap.QueueExecutionReceiptValidator")!;
            SnapshotEnvelope Snapshot(string state, long tick) => new()
            {
                StateHash = state, GameTick = tick, GameVersion = "1.6.15",
                SaveId = new() { Status = FieldStatus.Available, Value = "save" }, PlayerId = new() { Status = FieldStatus.Available, Value = "actor" }
            };
            return (string[])validator.GetMethod("ValidateAppliedNativeWork", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
                [Root, Snapshot(Root.StateHash, 0), Receipt, Snapshot(Receipt.AfterStateHash, Receipt.AfterGameTick), "run",
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, "original", Root.StateHash, queues])!;
        }

        private static ActionQueueEnvelope Queue(string id, string state, params string[] items) => new()
        {
            QueueId = id, StateHash = state, GoalId = "goal", Items = items.Select(item => new ActionQueueItem
            {
                QueueItemId = item, SourceActionId = id, OptionId = "executor.move_to_tile",
                NormalizedCommand = new NormalizedCommand { StateHash = state, OptionId = "executor.move_to_tile", Steps = [new() { StepId = item, StepType = "move_to_tile" }] }
            }).ToArray()
        };
        internal JsonObject ReadFile(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        internal void Change(string path, Action<JsonObject> change) { var node = ReadFile(path); change(node); Save(path, node); }
        internal void Save<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value));
    }
}
