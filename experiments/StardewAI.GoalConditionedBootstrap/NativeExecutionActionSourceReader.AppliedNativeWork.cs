using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    // Native effects are carried forward separately from the owner's selected-objective retirement.
    internal static VerifiedNativeExecutionQueue ReadAppliedNativeWorkQueue(string queue, string before, string receipt,
        string after, string runId, string candidateId, bool product)
    {
        var native = ReadQueueCore(queue, before, receipt, after, runId, candidateId, NativeProductBoundary.NativeAction,
            product ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            appliedNativeWork: true);
        if (product) RequireOriginalProductWire(native);
        else Require(native.Actions.All(frame => Uri.TryCreate(frame.Endpoint, UriKind.Absolute, out var endpoint) &&
                endpoint.AbsolutePath == "/api/v1/training/execute"),
            "Runtime native work requires the original Runtime wire for every action.");
        return native;
    }

    private static void VerifyNativeWorkObjectiveContinuation(ActionQueueEnvelope queue, JsonElement raw,
        bool allowCompletedDecision = false)
    {
        var objective = NativeObjectiveContinuationJournal.ReadRecordedObjective(raw);
        raw.TryGetProperty("objective_continuation_completed", out var complete);
        raw.TryGetProperty("completed_objective_continuations", out var completed);
        Require(raw.TryGetProperty("queue_progress_verified", out var progress) && progress.ValueKind == JsonValueKind.True &&
                objective.ValueKind == JsonValueKind.Object &&
                objective.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(kind.GetString()) &&
                objective.TryGetProperty("option_id", out var option) && option.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(option.GetString()),
            "Applied native work lacks its original verified typed objective.");
        var optionId = objective.GetProperty("option_id").GetString();
        var boundOptions = queue.Items.SelectMany(item => item.NormalizedCommand.Parameters)
            .Where(parameter => parameter.Name == "continuation.option_id").Select(parameter => parameter.Value).Distinct(StringComparer.Ordinal).ToArray();
        Require(boundOptions.Length == 1 && boundOptions[0] == optionId &&
                (complete.ValueKind is JsonValueKind.False or JsonValueKind.True) &&
                completed.ValueKind == JsonValueKind.Array &&
                completed.GetArrayLength() == (complete.ValueKind == JsonValueKind.True ? 1 : 0) &&
                (!raw.TryGetProperty("selected_queue_redecision_required", out var redecision) || redecision.ValueKind == JsonValueKind.False) &&
                (allowCompletedDecision || !raw.TryGetProperty("selected_queue_decision_complete", out var decision) || decision.ValueKind == JsonValueKind.False),
            "Applied native work changed its compiled objective or its completion journal.");
        if (complete.ValueKind == JsonValueKind.False) return;
        Require(JsonNode.DeepEquals(JsonNode.Parse(objective.GetRawText()), JsonNode.Parse(completed[0].GetRawText())),
            "Applied native work completed a different typed objective.");
        // This proves the native execution journal, not selected-objective retirement. The owner
        // reuses the executor's canonical continuation reader/completion rule before sealing that result.
    }
}
