using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.GoalConditionedBootstrap.RustyKeyNativeKeyReceiptBuilder;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyTeacherNativeJoinBuilder
{
    private static readonly string[] AllowedOptions = { "executor.donate_museum_item", "executor.traverse_connector",
        "executor.wait_ticks", "executor.sleep", "executor.advance_story_event" };
    private static readonly OptionRegistry Registry = new();
    private static readonly HashSet<string> RequestFieldNames = typeof(TrainingExecutionRequest)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
        .Where(name => name is not null).Select(name => name!).ToHashSet(StringComparer.Ordinal);

    private static NativeAction ReadAction(RustyKeyNativeActionSource source, RustyKeyNativeKeyReceipt key,
        IDictionary<string, string> hashes, int index)
    {
        var resultPath = RequiredFile(source.ExecutionReceiptPath);
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(resultPath, "Native action");
        var result = ReadTyped<TrainingExecutionResult>(resultPath, "training_execution_result.v1");
        Require(AllowedOptions.Contains(result.OptionId, StringComparer.Ordinal), "Rusty Key native option is outside the continuation scope.");
        var spec = Registry.GetRequired(result.OptionId);
        var journalPath = RequiredFile(Text(raw, "execution_request_journal_path"));
        var journal = ExecutionRequestJournalReader.Read(journalPath);
        var native = journal.Request;
        var requestRaw = JsonSerializer.Deserialize<JsonElement>(journal.RequestJson);
        var beforePath = RequiredFile(Text(journal.Metadata, "before_snapshot_path"));
        var before = ReadTyped<SnapshotEnvelope>(beforePath, "snapshot.v1");
        var afterPath = RequiredFile(source.AfterSnapshotPath);
        Require(SamePath(afterPath, RequiredFile(Text(raw, "after_snapshot_path"))),
            "Rusty Key supplied after snapshot is not the recorded action capture.");
        var after = ReadTyped<SnapshotEnvelope>(afterPath, "snapshot.v1");
        ValidateSnapshot(before);
        ValidateSnapshot(after);
        ValidateRecordedBoundary(raw, before, after);
        Require(before.GameVersion == key.GameVersion && after.GameVersion == key.GameVersion &&
            before.BridgeVersion == key.BridgeVersion && after.BridgeVersion == key.BridgeVersion &&
            Identity(before.SaveId) == key.SaveId && Identity(after.SaveId) == key.SaveId &&
            Identity(before.PlayerId) == key.PlayerId && Identity(after.PlayerId) == key.PlayerId &&
            before.GameTick >= 0 && after.GameTick > before.GameTick && TotalDay(before) is >= 0 and < 224 &&
            TotalDay(after) is >= 0 and < 224, "Rusty Key native frame identity/version/date drifted.");
        var queuePath = RequiredFile(source.ActionQueuePath);
        var recordedQueuePath = raw.TryGetProperty("mechanical_compiled_queue_path", out var mechanical) &&
            mechanical.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(mechanical.GetString())
                ? mechanical.GetString()! : Text(raw, "effective_compiled_queue_path");
        Require(SamePath(queuePath, RequiredFile(recordedQueuePath)),
            "Rusty Key supplied queue is not the recorded mechanical action queue.");
        var queue = ReadTyped<ActionQueueEnvelope>(queuePath, "action_queue.v1");
        var actor = queue.Actor ?? throw new InvalidDataException("Rusty Key native actor is missing.");
        Require(queue.Items is not null && queue.Items.All(item => item is not null), "Rusty Key queue has malformed items.");
        var items = queue.Items!.Where(item => item.QueueItemId == result.QueueItemId).ToArray();
        Require(items.Length == 1 && queue.Status == "pending" && queue.StateHash == before.StateHash &&
            queue.ExecutionMode == ExecutionTargetProfiles.TrainingSingleplayer && !string.IsNullOrWhiteSpace(queue.QueueId) &&
            EqualJson(actor, ExecutionTargetProfiles.CreateActor(queue.ExecutionMode)),
            "Rusty Key native queue does not bind this action frame.");
        var item = items[0];
        var command = item.NormalizedCommand;
        Require(command is not null && item.Status == "pending" && item.OptionId == result.OptionId &&
            item.BlockingReasons is { Length: 0 } && item.MissingStateFactors is { Length: 0 } &&
            item.PreconditionResults is not null && item.PreconditionResults.All(row => row is not null && row.Status == "passed") &&
            item.PermissionRequired == "executor" && item.BehaviorCategory == spec.BehaviorCategory &&
            item.CompilerResponsibility == spec.CompilerResponsibility && item.TrainingRole == spec.TrainingRole &&
            queue.CompilerDiagnostics is { Length: 0 } && command.StateHash == before.StateHash &&
            command.OptionId == result.OptionId && command.ExecutionMode == queue.ExecutionMode &&
            command.BehaviorCategory == item.BehaviorCategory && command.CompilerResponsibility == item.CompilerResponsibility &&
            command.TrainingRole == item.TrainingRole &&
            command.Parameters is not null && command.Parameters.All(row => row is not null && !string.IsNullOrWhiteSpace(row.Name)) &&
            command.Steps is { Length: > 0 } && command.Steps.All(step => step is not null && !string.IsNullOrWhiteSpace(step.StepId)) &&
            command.StrategyPlan is { Length: 0 } && command.SocialPlan is null && command.QuestPlan is null &&
            EqualJson(command.Actor, actor), "Rusty Key native command admission drifted.");
        Require(result.RunId == key.RunId && native.RunId == key.RunId &&
            result.QueueId == queue.QueueId && native.QueueId == queue.QueueId && native.QueueItemId == item.QueueItemId &&
            native.OptionId == result.OptionId && AllowedOptions.Contains(result.OptionId, StringComparer.Ordinal) &&
            native.BeforeStateHash == before.StateHash && result.BeforeStateHash == before.StateHash &&
            Text(requestRaw, "schema_version") == "training_execution_request.v1" &&
            Text(requestRaw, "execution_mode") == queue.ExecutionMode && Text(requestRaw, "actor") == actor.ActorId &&
            Text(requestRaw, "save_isolation_path") == native.SaveIsolationPath && !string.IsNullOrWhiteSpace(native.SaveIsolationPath) &&
            result.Status == "applied" && result.FeedbackAvailable && result.PrimitiveVerificationStatus == "verified" &&
            result.PrimitiveKind == result.OptionId["executor.".Length..] && result.BlockReasons is { Length: 0 } &&
            Text(raw, "execution_request_sha256") == Text(journal.Metadata, "raw_request_sha256") &&
            Text(raw, "execution_request_nonce") == native.RequestNonce,
            "Rusty Key native request/result identity or verification drifted.");
        ValidateWireParameters(requestRaw, command!);
        if (result.OptionId == "executor.traverse_connector")
            Require(Facts(result).Overlaps(new[] { "connector_location_changed_as_expected", "connector_native_arrival_tile_adjusted" }),
                "Rusty Key native connector has no verified arrival fact.");
        if (result.OptionId == "executor.wait_ticks")
            Require(Facts(result).Overlaps(new[] { "bounded_wait_window_elapsed", "wait_until_time_reached" }),
                "Rusty Key native wait has no bounded native completion fact.");
        var endpoint = Text(journal.Metadata, "executor_url");
        Require(Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
            uri.AbsolutePath == "/api/v1/training/execute", "Rusty Key source is not a native training executor request.");
        var prefix = "action_" + index + ".";
        foreach (var file in new[] { ("queue", queuePath), ("result", resultPath), ("before", beforePath),
            ("after", afterPath), ("journal", journalPath), ("request_gzip", journalPath + ".gz") })
            hashes[prefix + file.Item1] = CurrentTeacherFrontierSupport.HashFile(file.Item2);
        return new(queue, item, result, native, before, after, endpoint);
    }

    private static string Text(JsonElement row, string name) => CurrentTeacherFrontierSupport.RequiredString(row, name);
    private static bool SamePath(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void ValidateWireParameters(JsonElement request, NormalizedCommand command)
    {
        // Compare the declared wire fields, not a second queue-to-request compiler.
        var bindings = command.Parameters.Select(row => new
            { Name = TrainingExecutionParameterBindings.WireFieldForQueueParameter(row.Name), row.Value });
        foreach (var group in bindings.Where(row => RequestFieldNames.Contains(row.Name)).GroupBy(row => row.Name))
        {
            var values = group.Select(row => row.Value).Distinct(StringComparer.Ordinal).ToArray();
            Require(values.Length == 1 && request.TryGetProperty(group.Key, out var field) && Equivalent(field, values[0]),
                "Rusty Key native request differs from its compiled wire parameter: " + group.Key);
        }
    }

    private static bool Equivalent(JsonElement actual, string expected) => actual.ValueKind switch
    {
        JsonValueKind.String => actual.GetString() == expected,
        JsonValueKind.Number => actual.GetRawText() == expected,
        JsonValueKind.True or JsonValueKind.False => bool.TryParse(expected, out var boolean) && actual.GetBoolean() == boolean,
        _ => false
    };
}
