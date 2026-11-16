using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

// Declared observations through the original request compiler and journal, not live gameplay.
internal static class NativeQueueWireFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal sealed record Wire(string ResultPath, string Nonce);

    internal static async Task<Wire> WriteAsync(LiveTrainingOptions options, string queuePath, string beforePath,
        string afterPath, int itemIndex, string primitive, SimulatedFactChange[] facts, string resultPath, int iteration)
    {
        T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)!;
        var before = Read<SnapshotEnvelope>(beforePath); var after = Read<SnapshotEnvelope>(afterPath);
        var queue = Read<ActionQueueEnvelope>(queuePath); var item = queue.Items[itemIndex];
        var compiler = typeof(CropTeacherManagedSeedOwner).Assembly.EntryPoint!.DeclaringType!
            .GetMethod("BuildExecutionRequest", BindingFlags.Static | BindingFlags.NonPublic)!;
        var request = (TrainingExecutionRequest)compiler.Invoke(null, [options,
            JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, queue.QueueId, item.OptionId])!;
        var journal = await RuntimeExecutionRequestJournal.WriteAsync(options.SnapshotDir, iteration, itemIndex + 1,
            JsonSerializer.Serialize(request, JsonOptions), "http://test/api/v1/training/execute", beforePath);
        var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
            BeforeStateHash = before.StateHash, Status = "applied", FeedbackAvailable = true,
            PrimitiveKind = primitive, PrimitiveVerificationStatus = "verified", ChangedFacts = facts
        }, JsonOptions)!.AsObject();
        result["execution_request_journal_path"] = journal.JournalPath; result["execution_request_nonce"] = request.RequestNonce;
        result["execution_request_sha256"] = journal.RawRequestSha256; result["mechanical_compiled_queue_path"] = queuePath;
        result["after_snapshot_path"] = afterPath; result["after_state_hash"] = after.StateHash;
        result["before_game_tick"] = before.GameTick; result["after_game_tick"] = after.GameTick; result["after_snapshot_fresh"] = true;
        await File.WriteAllTextAsync(resultPath, result.ToJsonString(JsonOptions));
        return new(resultPath, request.RequestNonce);
    }

    internal static void Bind(JsonObject aggregate, int index, string afterPath, SimulatedFactChange[] facts, Wire wire)
    {
        var step = aggregate["step_results"]![index]!;
        step["after_snapshot_path"] = afterPath;
        step["changed_facts"] = JsonSerializer.SerializeToNode(facts, JsonOptions);
        step["execution_path"] = wire.ResultPath; step["execution_request_nonce"] = wire.Nonce;
    }
}
