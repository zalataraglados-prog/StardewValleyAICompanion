using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class QueueExecutionReceiptClassifier
{
    public static QueueExecutionReceiptClassification Classify(
        IReadOnlyList<JsonObject> steps)
    {
        var completionMarkerExact = steps.Count > 0 &&
            steps[^1]["selected_queue_candidate_completed"]?
                .GetValue<bool>() == true &&
            steps.Take(steps.Count - 1).All(step =>
                step["selected_queue_candidate_completed"]?
                    .GetValue<bool>() != true);
        var allSnapshotsFresh = steps.Count > 0 &&
            steps.All(step =>
                step["after_snapshot_fresh"]?.GetValue<bool>() == true);
        var executionProgressVerified = allSnapshotsFresh &&
            steps.All(QueueReplanFilter.IsTrainingVerifiedExecution);

        return new QueueExecutionReceiptClassification(
            executionProgressVerified ? "applied" : "blocked",
            executionProgressVerified,
            completionMarkerExact,
            executionProgressVerified && completionMarkerExact,
            allSnapshotsFresh);
    }
}

public sealed record QueueExecutionReceiptClassification(
    string Status,
    bool ExecutionProgressVerified,
    bool SelectedCandidateCompleted,
    bool Success,
    bool AllSnapshotsFresh);
