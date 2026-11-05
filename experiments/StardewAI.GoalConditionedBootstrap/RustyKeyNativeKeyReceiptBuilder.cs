using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyNativeKeyReceiptBuilder
{
    public static RustyKeyNativeKeyReceipt Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<RustyKeyNativeKeyReceiptRequest>(requestPath, "rusty_key_native_key_receipt_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.RunId), "run_id_missing");
        var beforePath = RequiredFile(request.BeforeSnapshotPath);
        var afterPath = RequiredFile(request.AfterSnapshotPath);
        var queuePath = RequiredFile(request.ActionQueuePath);
        var resultPath = RequiredFile(request.ExecutionReceiptPath);
        var before = ReadTyped<SnapshotEnvelope>(beforePath, "snapshot.v1");
        var after = ReadTyped<SnapshotEnvelope>(afterPath, "snapshot.v1");
        ValidateSnapshots(before, after);
        var day = TotalDay(before);
        Require(day is >= 0 and < 224 && TotalDay(after) == day, "stage_one_day_invalid");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(before, new[] { "story.advance_event" }, true);
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var binding = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = before.StateHash, DirectionId = "obtain_rusty_key", RankedCandidates = candidates
        }, before);
        Require(binding.BindingStatus == "ready" && binding.BoundCandidates is { Length: 1 },
            "original_direction_attribution_rejected:" + string.Join(",", binding.BlockReasons));
        var candidate = binding.BoundCandidates[0];
        var queue = ReadTyped<ActionQueueEnvelope>(queuePath, "action_queue.v1");
        var scopedGoalId = GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == "obtain_rusty_key").EffectiveGoalId;
        Require(queue.GoalId == scopedGoalId || queue.GoalId == GoalMethodTeacherCoverageGoalIds.Authoritative ||
            queue.GoalId == GoalMethodTeacherCoverageGoalIds.AcquisitionCorpus, "queue_goal_not_attributable");
        var goalId = queue.GoalId;
        var rebuilt = new ActionQueueCompiler().Compile(new DailyPlanCompiler().Compile(
            binding.BoundCandidates, before.StateHash, goalId, maxCandidates: 1), before);
        Require(rebuilt.Items is { Length: 1 } && rebuilt.Items[0].BlockingReasons is { Length: 0 } &&
            rebuilt.CompilerDiagnostics is { Length: 0 }, "original_compiler_rejected");

        var result = ReadTyped<TrainingExecutionResult>(resultPath, "training_execution_result.v1");
        ValidateQueue(queue, rebuilt, candidate.CandidateId, before.StateHash, goalId);
        ValidateResult(result, queue, request.RunId, before.StateHash);
        ValidateOutcome(before, after);

        return new RustyKeyNativeKeyReceipt(requestPath, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["before_snapshot"] = CurrentTeacherFrontierSupport.HashFile(beforePath),
            ["after_snapshot"] = CurrentTeacherFrontierSupport.HashFile(afterPath),
            ["action_queue"] = CurrentTeacherFrontierSupport.HashFile(queuePath),
            ["execution_receipt"] = CurrentTeacherFrontierSupport.HashFile(resultPath)
        }, request.RunId, queue.QueueId, queue.Items[0].QueueItemId, candidate.CandidateId,
            before.StateHash, after.StateHash, Identity(before.SaveId), Identity(before.PlayerId),
            before.GameVersion, before.BridgeVersion, day, before.GameTick, after.GameTick);
    }

    public static RustyKeyNativeKeyReceipt Verify(string receiptPath)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(RequiredFile(receiptPath), "Rusty Key receipt");
        Require(raw.ValueKind == JsonValueKind.Object &&
            raw.TryGetProperty("request_path", out var request) && request.ValueKind == JsonValueKind.String,
            "persisted_request_path_missing");
        var rebuilt = Build(raw.GetProperty("request_path").GetString()!);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "persisted_receipt_source_or_claim_drift");
        return rebuilt;
    }

    private static void ValidateQueue(ActionQueueEnvelope queue, ActionQueueEnvelope rebuilt,
        string candidateId, string stateHash, string goalId)
    {
        Require(!string.IsNullOrWhiteSpace(queue.QueueId) && queue.GoalId == goalId && queue.StateHash == stateHash &&
            queue.Status == "pending" && queue.ExecutionMode == ExecutionTargetProfiles.TrainingSingleplayer &&
            queue.Items is { Length: 1 } && queue.Items[0] is not null && queue.CompilerDiagnostics is { Length: 0 } &&
            queue.CandidateAudit is not null && queue.CandidateAudit.All(row => row is not null) &&
            queue.CandidateAudit.Count(row => row.CandidateId == candidateId && row.Decision == "accepted") == 1 &&
            Same(queue.Actor, rebuilt.Actor), "queue_identity_or_admission_invalid");
        var item = queue.Items[0];
        var expected = rebuilt.Items[0];
        Require(!string.IsNullOrWhiteSpace(item.QueueItemId) && item.OptionId == expected.OptionId &&
            item.Status == "pending" && item.BlockingReasons is { Length: 0 } && item.MissingStateFactors is { Length: 0 } &&
            item.PreconditionResults is not null && item.PreconditionResults.All(row => row is not null && row.Status == "passed") &&
            item.TrainingRole == expected.TrainingRole && item.PermissionRequired == expected.PermissionRequired &&
            item.BehaviorCategory == expected.BehaviorCategory && item.CompilerResponsibility == expected.CompilerResponsibility &&
            item.NormalizedCommand is not null, "queue_item_invalid");
        var command = item.NormalizedCommand ?? throw new InvalidDataException("Rusty Key native terminal: command_missing");
        var expectedCommand = expected.NormalizedCommand;
        Require(command.OptionId == "executor.advance_story_event" && command.StateHash == stateHash && command.GoalId == expectedCommand.GoalId &&
            command.CommandType == expectedCommand.CommandType && command.ExecutionMode == expectedCommand.ExecutionMode &&
            command.TrainingRole == expectedCommand.TrainingRole && command.BehaviorCategory == expectedCommand.BehaviorCategory &&
            command.CompilerResponsibility == expectedCommand.CompilerResponsibility && Same(command.Actor, expectedCommand.Actor) &&
            command.StrategyPlan is { Length: 0 } && command.SocialPlan is null && command.QuestPlan is null,
            "command_envelope_invalid");
        // Generated IDs and optional budget annotations are not event semantics.
        Require(command.Steps is not null && command.Steps.All(step => step is not null) &&
            Same(SemanticParameters(command), SemanticParameters(expectedCommand)) &&
            Same(Steps(command), Steps(expectedCommand)) && command.Steps.All(step => !string.IsNullOrWhiteSpace(step.StepId)),
            "command_drifted_from_original_compiler");
    }

    private static object[] Steps(NormalizedCommand command) => (command.Steps ?? Array.Empty<CompiledActionStep>())
        .Select(step => (object)new { step.StepType, step.Target, step.ExpectedEffect, step.EstimatedTicks }).ToArray();

    private static KeyValuePair<string, string>[] SemanticParameters(NormalizedCommand command)
    {
        Require(command.Parameters is not null && command.Parameters.All(row => row is not null &&
            !string.IsNullOrWhiteSpace(row.Name) && row.Value is not null), "command_parameters_malformed");
        return command.Parameters!
        .Where(row => row.Name.StartsWith("story_event_", StringComparison.Ordinal) ||
            row.Name is "grandpa_direction_id" or "grandpa_source_state_hash")
        .GroupBy(row => row.Name, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group =>
        {
            var values = group.Select(row => row.Value).Distinct(StringComparer.Ordinal).ToArray();
            Require(values.Length == 1 && !string.IsNullOrWhiteSpace(values[0]), "event_parameter_missing_or_conflicting");
            return new KeyValuePair<string, string>(group.Key, values[0]);
        }).ToArray();
    }

    private static void ValidateResult(TrainingExecutionResult result, ActionQueueEnvelope queue, string runId, string stateHash)
    {
        Require(result.RunId == runId && result.QueueId == queue.QueueId && result.QueueItemId == queue.Items[0].QueueItemId &&
            result.BeforeStateHash == stateHash && result.OptionId == "executor.advance_story_event" &&
            result.Status == "applied" && result.FeedbackAvailable &&
            result.PrimitiveKind == "advance_story_event" && result.PrimitiveVerificationStatus == "verified" &&
            result.BlockReasons is { Length: 0 }, "native_result_identity_or_verification_invalid");
        var reasons = result.PrimitiveVerificationReasons ?? Array.Empty<string>();
        Require(reasons.ToHashSet(StringComparer.Ordinal).SetEquals(new[]
            { "native_event_input_only", "boundary=event_completed", "skipEvent_not_used" }), "native_event_completion_not_verified");
        var facts = result.ChangedFacts ?? Array.Empty<SimulatedFactChange>();
        Require(facts.All(row => row is not null), "native_event_facts_malformed");
        Require(ExactChange("player.story_event.event_id", "66", "") &&
            ExactChange("player.story_event.event_seen", "false", "true"),
            "native_event_facts_missing");

        bool ExactChange(string path, string before, string after)
        {
            var rows = facts.Where(row => row.Path == path).ToArray();
            return rows.Length == 1 && rows[0].Before == before && rows[0].After == after;
        }
    }

    private static bool Same<T>(T first, T second) =>
        JsonSerializer.Serialize(first, JsonDefaults.Options) == JsonSerializer.Serialize(second, JsonDefaults.Options);
}
