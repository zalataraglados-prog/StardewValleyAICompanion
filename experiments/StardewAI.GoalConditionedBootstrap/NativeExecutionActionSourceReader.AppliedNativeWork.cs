using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    // Every immutable queue item is applied; only its higher-level selected objective may remain pending.
    internal static VerifiedNativeExecutionQueue ReadAppliedNativeWorkQueue(string queue, string before, string receipt,
        string after, string runId, string candidateId, bool product)
    {
        var native = ReadQueueCore(queue, before, receipt, after, runId, candidateId, NativeProductBoundary.NativeAction,
            product ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            appliedNativeWork: true);
        if (product) RequireOriginalProductWire(native);
        return native;
    }

    private static void VerifyPendingNativeWorkObjective(ActionQueueEnvelope queue, JsonElement raw)
    {
        Require(raw.TryGetProperty("queue_progress_verified", out var progress) && progress.ValueKind == JsonValueKind.True &&
                raw.TryGetProperty("objective_continuation", out var objective) && objective.ValueKind == JsonValueKind.Object &&
                objective.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(kind.GetString()) &&
                objective.TryGetProperty("option_id", out var option) && option.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(option.GetString()),
            "Applied native work lacks its original verified pending objective.");
        var optionId = raw.GetProperty("objective_continuation").GetProperty("option_id").GetString();
        var boundOptions = queue.Items.SelectMany(item => item.NormalizedCommand.Parameters)
            .Where(parameter => parameter.Name == "continuation.option_id").Select(parameter => parameter.Value).Distinct(StringComparer.Ordinal).ToArray();
        Require(boundOptions.Length == 1 && boundOptions[0] == optionId &&
                raw.TryGetProperty("objective_continuation_completed", out var complete) && complete.ValueKind == JsonValueKind.False &&
                raw.TryGetProperty("completed_objective_continuations", out var completed) && completed.ValueKind == JsonValueKind.Array && completed.GetArrayLength() == 0 &&
                (!raw.TryGetProperty("selected_queue_redecision_required", out var redecision) || redecision.ValueKind == JsonValueKind.False) &&
                (!raw.TryGetProperty("selected_queue_decision_complete", out var decision) || decision.ValueKind == JsonValueKind.False),
            "Applied native work changed or completed its pending compiled objective.");
    }
}
