using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingNativeControlSettleIntegrationTests
{
    [Fact]
    public async Task ExactV93SelectionBuildsOneWaitAndCannotBecomePolicyOrProgress()
    {
        // The optional read-only input permits replay of the full frozen snapshot
        // locally; CI uses the same exact relevant projection already pinned in Core.
        var evidencePath = Environment.GetEnvironmentVariable("STARDEWAI_TEST_V93_SNAPSHOT_PATH");
        var json = string.IsNullOrWhiteSpace(evidencePath)
            ? JsonSerializer.Serialize(NativeControlSettleRecoveryTests.V93Snapshot())
            : File.ReadAllText(evidencePath);
        using var fixture = new Fixture(json);
        Assert.Equal(NativeControlSettleRecoveryTests.V93Snapshot().StateHash, fixture.StateHash);

        var result = Assert.IsType<JsonObject>(await fixture.Build());
        Assert.True(result["attempted"]!.GetValue<bool>());
        Assert.Equal("ready_native_control_settle", result["status"]!.GetValue<string>());
        Assert.Equal(NativeControlSettleRecovery.CandidateId, result["selectedCandidateId"]!.GetValue<string>());
        var queue = result["queue"]!.AsObject();
        Assert.Equal("pending", queue["status"]!.GetValue<string>());
        Assert.Equal(fixture.StateHash, queue["state_hash"]!.GetValue<string>());
        var item = Assert.IsType<JsonObject>(Assert.Single(queue["items"]!.AsArray()));
        Assert.Equal("pending", item["status"]!.GetValue<string>());
        Assert.Equal("executor.wait_ticks", item["option_id"]!.GetValue<string>());
        Assert.Contains(item["normalized_command"]!["parameters"]!.AsArray(), parameter =>
            parameter!["name"]!.GetValue<string>() == "wait_ticks" &&
            parameter["value"]!.GetValue<string>() == "30");
        Assert.True(QueueReplanFilter.RequiresFreshSnapshotReplan(item));

        var preference = JsonNode.Parse(File.ReadAllText(fixture.PathFor("preference")))!;
        Assert.False(preference["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.False(preference["formal_training_authorized"]!.GetValue<bool>());
        var ranking = JsonSerializer.Deserialize<AvailabilityAwarePolicyPredictionEnvelope>(
            File.ReadAllText(fixture.PathFor("ranking")))!;
        var candidate = Assert.Single(ranking.RankedEventCandidates);
        Assert.Equal("deterministic_control.native_control_settle.v1", candidate.PolicyModelSource);
        Assert.Null(candidate.ModelScore);

        var execution = new JsonObject
        {
            ["status"] = "applied",
            ["primitive_kind"] = "wait_ticks",
            ["primitive_verification_status"] = "verified",
            ["after_snapshot_fresh"] = true,
            ["selected_queue_candidate_completed"] = true,
            ["effective_ranking_path"] = fixture.PathFor("ranking"),
            ["effective_decision_source_state_hash"] = fixture.StateHash,
            ["effective_before_state_hash"] = fixture.StateHash,
            ["effective_before_snapshot_path"] = fixture.SnapshotPath,
            ["effective_queue_item"] = item.DeepClone(),
            ["after_state_hash"] = fixture.StateHash,
            ["state_hash_changed"] = false,
            ["changed_facts"] = new JsonArray(
                new JsonObject { ["path"] = "executor.wait_ticks", ["before"] = "0", ["after"] = "30" },
                new JsonObject { ["path"] = "time.time", ["before"] = "600", ["after"] = "600" })
        };
        var append = Assert.IsType<PolicyTrajectoryAppendBatchResult>(ProgramMethod(
            "AppendPolicyDecisionTrajectories").Invoke(null, new object[]
            {
                fixture.Options, 1, execution,
                new TrainingDatasetAppendResult { RowId = "row.wait", EpisodeId = "episode.wait" }
            }));
        Assert.Equal(0, append.AppendedCount);
        Assert.Equal(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason, append.FirstSkipReason);
        Assert.False(File.Exists(fixture.Options.PolicyTrajectoryDatasetPath));
        var backoff = new NoProgressBackoffPolicy(0, 0);
        Assert.True(backoff.ObserveExecution(queue, execution).NoProgress);
        Assert.True(NoProgressBackoffPolicy.ReachedLimit(backoff.ObserveExecution(queue, execution), 2));

        var stale = QueueReplanFilter.DecideAfterExecution("applied", true, true, false, false, true,
            QueueReplanFilter.RequiresFreshSnapshotReplan(item));
        Assert.True(stale.ShouldStop);
        Assert.False(stale.ShouldReplan);
        Assert.Equal("stale_after_snapshot", stale.Reason);
        var fresh = QueueReplanFilter.DecideAfterExecution("applied", true, true, false, true, true,
            QueueReplanFilter.RequiresFreshSnapshotReplan(item));
        Assert.True(fresh.ShouldReplan);
        Assert.Equal("continuable_execution_requires_fresh_snapshot_replan", fresh.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettledOrIncompatibleStateDoesNotSelectSettleQueue(bool incompatible)
    {
        var snapshot = NativeControlSettleRecoveryTests.V93Snapshot(root =>
        {
            var movement = root["state"]!["player"]!["movement_timing_context"]!["value"]!;
            if (incompatible)
                movement["runtime_calibration_compatible"] = false;
            else
            {
                movement["route_timing_ready_now"] = true;
                movement["dialogue_up"] = false;
                movement["player_freeze_pause"] = 0;
                movement["native_control_gate_open"] = true;
            }
        });
        using var fixture = new Fixture(JsonSerializer.Serialize(snapshot));
        Assert.Null(await fixture.Build());
        Assert.False(File.Exists(fixture.PathFor("queue")));
        Assert.False(File.Exists(fixture.PathFor("preference")));
    }

    [Fact]
    public async Task StaleCallerStateHashCannotProduceDispatchableSettleQueue()
    {
        using var fixture = new Fixture(JsonSerializer.Serialize(NativeControlSettleRecoveryTests.V93Snapshot()));
        var result = Assert.IsType<JsonObject>(await fixture.Build("stale-caller-hash"));
        Assert.True(result["attempted"]!.GetValue<bool>());
        Assert.Equal("blocked_native_control_settle_not_dispatchable", result["status"]!.GetValue<string>());
        Assert.Null(result["queue"]);
        Assert.False(File.Exists(fixture.PathFor("queue")));
    }

    private static MethodInfo ProgramMethod(string name) =>
        typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "stardewai-settle-" + Guid.NewGuid().ToString("N"));
        private readonly string snapshotJson;
        public LiveTrainingOptions Options { get; }
        public string SnapshotPath => PathFor("snapshot");
        public string StateHash { get; }
        public string PathFor(string kind) => Path.Combine(root, kind + ".json");

        public Fixture(string snapshotJson)
        {
            this.snapshotJson = snapshotJson;
            StateHash = JsonNode.Parse(snapshotJson)!["state_hash"]!.GetValue<string>();
            Options = new LiveTrainingOptions
            {
                Root = root, RunId = "run.v93-settle", UseDailyPlan = true,
                RequireGoldenWalnutSourceScheduleReady = true, SkipTraining = true,
                TargetExecutionMode = "training_singleplayer"
            };
            Directory.CreateDirectory(root);
            File.WriteAllText(SnapshotPath, snapshotJson);
        }

        public async Task<JsonObject?> Build(string? stateHash = null)
        {
            var task = Assert.IsAssignableFrom<Task>(ProgramMethod("TryBuildNativeControlSettleQueueAsync")
                .Invoke(null, new object[]
                {
                    Options, snapshotJson, stateHash ?? StateHash, SnapshotPath,
                    PathFor("preference"), PathFor("plan"), PathFor("ranking"), PathFor("queue")
                }));
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task);
            return result is null ? null : JsonSerializer.SerializeToNode(result, result.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        }

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
