using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    internal static VerifiedNativeExecutionQueue ReadQueue(string queuePath, string beforePath, string receiptPath,
        string afterPath, string runId, string candidateId)
    {
        queuePath = RequiredFile(queuePath);
        beforePath = RequiredFile(beforePath);
        receiptPath = RequiredFile(receiptPath);
        afterPath = RequiredFile(afterPath);
        var queue = ReadTyped<ActionQueueEnvelope>(queuePath, "action_queue.v1");
        var before = ReadTyped<SnapshotEnvelope>(beforePath, "snapshot.v1");
        var after = ReadTyped<SnapshotEnvelope>(afterPath, "snapshot.v1");
        ValidateSnapshot(before);
        ValidateSnapshot(after);
        var receipt = ReadTyped<QueueExecutionReceiptEnvelope>(receiptPath, "queue_execution_receipt.v1");
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(receiptPath, "Native sequential receipt");
        var reasons = QueueExecutionReceiptValidator.Validate(queue, before, receipt, after, runId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, candidateId, queue.StateHash,
            requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0, "Native sequential queue proof rejected: " + string.Join(",", reasons));
        Require(SamePath(afterPath, RequiredFile(Text(raw, "after_snapshot_path"))) &&
            receipt.StepResults.All(step => !step.TeacherPreferenceStateRebound),
            "Native sequential source requires the original immutable precompiled queue and recorded final capture.");
        var rows = raw.GetProperty("step_results").EnumerateArray().ToArray();
        var actions = new List<NativeExecutionActionFrame>();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Length; index++)
        {
            var step = receipt.StepResults[index];
            var resultPath = RequiredFile(Text(rows[index], "execution_path"));
            var frame = ReadFrame(queuePath, resultPath, RequiredFile(step.AfterSnapshotPath), queue.StateHash);
            Require(frame.Result.RunId == runId && frame.Item.QueueItemId == queue.Items[index].QueueItemId &&
                frame.Before.StateHash == step.SourceStateHash && frame.After.StateHash == step.AfterStateHash &&
                frame.Before.GameTick == step.BeforeGameTick && frame.After.GameTick == step.AfterGameTick &&
                frame.Result.PrimitiveKind == step.PrimitiveKind &&
                Text(rows[index], "execution_request_nonce") == frame.Request.RequestNonce &&
                nonces.Add(frame.Request.RequestNonce) &&
                EqualJson(JsonSerializer.SerializeToElement(frame.Result.ChangedFacts, JsonDefaults.Options), step.ChangedFacts),
                "Native sequential action does not bind the exact aggregate step, nonce and observed facts.");
            foreach (var snapshot in new[] { frame.Before, frame.After })
                Require(Value(snapshot, "environment", "training_run_id").GetString() == runId &&
                    Value(snapshot, "environment", "save_isolation_path").GetString() == frame.Request.SaveIsolationPath &&
                    Value(snapshot, "environment", "training_mode").GetString() == "1",
                    "Native sequential snapshot environment differs from the recorded isolated request.");
            var previous = index == 0 ? beforePath : actions[index - 1].AfterSnapshotPath;
            Require(SameFrame(previous, frame.BeforeSnapshotPath), "Native sequential action frame is not the preceding original capture.");
            foreach (var hash in frame.SourceSha256) hashes["action_" + index + "." + hash.Key] = hash.Value;
            actions.Add(frame);
        }
        Require(SameFrame(actions[^1].AfterSnapshotPath, afterPath), "Native sequential final frame differs from the aggregate capture.");
        foreach (var file in new[] { ("queue", queuePath), ("before", beforePath), ("after", afterPath), ("aggregate", receiptPath) })
            hashes[file.Item1] = CurrentTeacherFrontierSupport.HashFile(file.Item2);
        return new(queue, before, after, receipt, actions.ToArray(), hashes);
    }

    private static bool SameFrame(string left, string right) =>
        JsonNode.DeepEquals(JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(left)),
            JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(right)));
}

internal sealed record VerifiedNativeExecutionQueue(ActionQueueEnvelope Queue, SnapshotEnvelope Before,
    SnapshotEnvelope After, QueueExecutionReceiptEnvelope Receipt, NativeExecutionActionFrame[] Actions,
    IReadOnlyDictionary<string, string> SourceSha256);
