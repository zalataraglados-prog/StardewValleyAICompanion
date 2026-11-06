using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Fact]
    public void SequentialNativeSourceReusesWireProofButDoesNotRelaxTheOldSingleActionFrame()
    {
        var f = SequentialNativeFixture();
        var result = ReadSequential(f);
        Assert.Equal(2, JsonSerializer.SerializeToElement(result, JsonOptions).GetProperty("actions").GetArrayLength());
        var type = typeof(FriendshipNativeThresholdReceiptBuilder).Assembly.GetType(
            "StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!;
        var legacy = type.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.NotNull(legacy.Invoke(null, new object[] { f.Queue, f.Results[0], f.Frames[1] }));
        var failure = Assert.Throws<TargetInvocationException>(() =>
            legacy.Invoke(null, new object[] { f.Queue, f.Results[1], f.Frames[2] }));
        Assert.Contains("does not bind this action frame", Assert.IsType<InvalidDataException>(failure.InnerException).Message);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("order")]
    [InlineData("nonce")]
    [InlineData("wire")]
    [InlineData("native_status")]
    [InlineData("facts")]
    [InlineData("queue_copy")]
    [InlineData("after_copy")]
    [InlineData("environment")]
    [InlineData("root_metadata")]
    public void SequentialNativeSourceRejectsBrokenChainOrUnboundOriginalWireSources(string mode)
    {
        var f = SequentialNativeFixture(mode == "environment");
        switch (mode)
        {
            case "gap": EditNativeKeyJson(f.Receipt, node => node["step_results"]![1]!["effective_before_state_hash"] = "foreign"); break;
            case "order": EditNativeKeyJson(f.Receipt, node =>
                node["step_results"] = new JsonArray(node["step_results"]![1]!.DeepClone(), node["step_results"]![0]!.DeepClone())); break;
            case "nonce": EditNativeKeyJson(f.Results[1], node => node["execution_request_nonce"] = "foreign"); break;
            case "wire":
                f.Requests[1].WaitTicks = 2;
                WriteJoinJournal(f.Journals[1], f.Requests[1], f.Frames[1]);
                EditNativeKeyJson(f.Results[1], node => node["execution_request_sha256"] =
                    JsonNode.Parse(File.ReadAllText(f.Journals[1]))!["raw_request_sha256"]!.DeepClone()); break;
            case "native_status": EditNativeKeyJson(f.Results[1], node => node["primitive_verification_status"] = "unverified"); break;
            case "facts": EditNativeKeyJson(f.Results[1], node => node["changed_facts"]![0]!["after"] = "999"); break;
            case "queue_copy":
                var copy = Path.Combine(f.Root, "copied-queue.json"); File.Copy(f.Queue, copy); f = f with { Queue = copy }; break;
            case "after_copy":
                var after = Path.Combine(f.Root, "copied-after.json"); File.Copy(f.Frames[2], after);
                f = f with { Frames = new[] { f.Frames[0], f.Frames[1], after } }; break;
            case "root_metadata":
                var extra = Path.Combine(f.Root, "extra-before.json"); File.Copy(f.Frames[0], extra);
                EditNativeKeyJson(extra, node => node["unknown_capture_context"] = "foreign");
                EditNativeKeyJson(f.Journals[0], node => node["before_snapshot_path"] = extra); break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => ReadSequential(f));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static object ReadSequential(SequentialNativeFiles f) =>
        typeof(FriendshipNativeThresholdReceiptBuilder).Assembly.GetType(
            "StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!
        .GetMethod("ReadQueue", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, new object[] { f.Queue, f.Frames[0], f.Receipt, f.Frames[2], "native-run", "candidate" })!;

    private sealed record SequentialNativeFiles(string Root, string Queue, string Receipt, string[] Frames,
        string[] Results, string[] Journals, TrainingExecutionRequest[] Requests);

    private static SequentialNativeFiles SequentialNativeFixture(bool foreignEnvironment = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-SequentialNative", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var isolation = Path.Combine(root, "isolated-saves");
        var snapshots = Enumerable.Range(0, 3).Select(index =>
        {
            static JsonElement Field(string value) => JsonSerializer.SerializeToElement(new { status = "available", value });
            var snapshot = new SnapshotEnvelope
            {
                GameVersion = "1.6.15", BridgeVersion = "0.1.0", GameTick = index + 1,
                SaveId = new() { Status = "available", Value = "save" }, PlayerId = new() { Status = "available", Value = "player" },
                State = new()
                {
                    ["fixture"] = JsonSerializer.SerializeToElement(new { counter = index }),
                    ["environment"] = JsonSerializer.SerializeToElement(new
                    {
                        training_run_id = Field(foreignEnvironment ? "foreign" : "native-run"),
                        training_mode = Field("1"), save_isolation_path = Field(isolation)
                    })
                }
            };
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            return snapshot;
        }).ToArray();
        var frames = Enumerable.Range(0, 3).Select(index => Path.Combine(root, "frame-" + index + ".json")).ToArray();
        for (var index = 0; index < frames.Length; index++) WriteNativeKeyJson(frames[index], snapshots[index]);
        var queuePath = WriteJoinQueue(root, "wait_ticks", frames[0]);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(queuePath), JsonOptions)!;
        var template = JsonSerializer.Serialize(queue.Items[0], JsonOptions);
        queue.Items = Enumerable.Range(0, 2).Select(index =>
        {
            var item = JsonSerializer.Deserialize<ActionQueueItem>(template, JsonOptions)!;
            item.QueueItemId = "item-" + index;
            item.NormalizedCommand.Parameters = new[] { new SmallModelActionParameter { Name = "wait_ticks", Value = "1" } };
            return item;
        }).ToArray();
        WriteNativeKeyJson(queuePath, queue);
        var results = new string[2]; var journals = new string[2]; var requests = new TrainingExecutionRequest[2];
        var receipt = new QueueExecutionReceiptEnvelope
        {
            RunId = "native-run", QueueId = queue.QueueId, SourceStateHash = snapshots[0].StateHash,
            AfterStateHash = snapshots[2].StateHash, BeforeGameTick = 1, AfterGameTick = 3, AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items", Status = "applied", Success = true,
            PlannedItemCount = 2, ExecutedItemCount = 2, MaxQueueItemAttempts = 2,
            SelectedCandidateId = "candidate", SelectedCandidateCompleted = true, StepResults = new QueueExecutionStepReceipt[2]
        };
        for (var index = 0; index < 2; index++)
        {
            requests[index] = new() { RunId = "native-run", QueueId = queue.QueueId, QueueItemId = queue.Items[index].QueueItemId,
                OptionId = "executor.wait_ticks", BeforeStateHash = snapshots[index].StateHash, SaveIsolationPath = isolation,
                RequestNonce = Guid.NewGuid().ToString("N"), WaitTicks = 1 };
            journals[index] = Path.Combine(root, "journal-" + index + ".json");
            WriteJoinJournal(journals[index], requests[index], frames[index]);
            results[index] = Path.Combine(root, "native-" + index + ".json");
            var facts = new[] { new SimulatedFactChange { Path = "fixture.counter", Before = index.ToString(), After = (index + 1).ToString() } };
            var native = JsonSerializer.SerializeToNode(new TrainingExecutionResult
            {
                RunId = "native-run", QueueId = queue.QueueId, QueueItemId = queue.Items[index].QueueItemId,
                OptionId = "executor.wait_ticks", BeforeStateHash = snapshots[index].StateHash, Status = "applied",
                FeedbackAvailable = true, PrimitiveKind = "wait_ticks", PrimitiveVerificationStatus = "verified", ChangedFacts = facts
            }, JsonOptions)!;
            native["execution_request_journal_path"] = journals[index]; native["execution_request_nonce"] = requests[index].RequestNonce;
            native["execution_request_sha256"] = JsonNode.Parse(File.ReadAllText(journals[index]))!["raw_request_sha256"]!.DeepClone();
            native["mechanical_compiled_queue_path"] = queuePath; native["after_snapshot_path"] = frames[index + 1];
            native["before_game_tick"] = snapshots[index].GameTick; native["after_game_tick"] = snapshots[index + 1].GameTick;
            native["after_state_hash"] = snapshots[index + 1].StateHash; native["after_snapshot_fresh"] = true;
            WriteNativeKeyJson(results[index], native);
            receipt.StepResults[index] = new()
            {
                QueueItemIndex = index, QueueItemCount = 2, OriginalPlannedItemCount = 2, QueueId = queue.QueueId,
                QueueItemId = queue.Items[index].QueueItemId, OptionId = "executor.wait_ticks",
                SourceStateHash = snapshots[index].StateHash, CompiledCommandStateHash = snapshots[0].StateHash,
                AfterStateHash = snapshots[index + 1].StateHash, StateHashChanged = true,
                BeforeGameTick = snapshots[index].GameTick, AfterGameTick = snapshots[index + 1].GameTick,
                AfterSnapshotPath = frames[index + 1], AfterSnapshotFresh = true, Status = "applied", PrimitiveKind = "wait_ticks",
                PrimitiveVerificationStatus = "verified", PrimitiveVerificationReasons = new[] { "wait_completed" },
                ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions),
                EffectiveQueueItem = JsonSerializer.SerializeToElement(queue.Items[index], JsonOptions), SelectedQueueCandidateCompleted = index == 1
            };
        }
        var aggregate = JsonSerializer.SerializeToNode(receipt, JsonOptions)!;
        aggregate["after_snapshot_path"] = frames[2];
        for (var index = 0; index < 2; index++)
        {
            aggregate["step_results"]![index]!["execution_path"] = results[index];
            aggregate["step_results"]![index]!["execution_request_nonce"] = requests[index].RequestNonce;
        }
        var receiptPath = Path.Combine(root, "aggregate.json"); WriteNativeKeyJson(receiptPath, aggregate);
        return new(root, queuePath, receiptPath, frames, results, journals, requests);
    }
}
