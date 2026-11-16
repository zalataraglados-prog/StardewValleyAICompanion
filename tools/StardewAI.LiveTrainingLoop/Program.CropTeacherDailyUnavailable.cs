using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static CropTeacherDailySelection? ReleaseUnavailableCropTeacherSelection(CropTeacherDailySelection selection,
        SelectedQueueCandidateLock candidate, JsonObject evidence, JsonObject queue)
    {
        if (selection.OwnedCandidateIds.Contains(candidate.CandidateId) || ReadString(queue, "status") == "pending" ||
            ExecutableQueueItems(queue).Length != 0) return null;
        // Adapt only the refresh metadata view to the old loop's release contract.
        // Actual availability, compiled queue, original choice and completion are untouched.
        var ranking = new JsonObject { ["objective_continuation_filter"] = evidence["selected_candidate_filter"]?.DeepClone() };
        if (!QueueReplanFilter.ShouldReleaseUnavailableContinuation(candidate.ObjectiveContinuation, ranking)) return null;
        var suppressed = selection.SuppressedForeignContinuations.Select(row => row.DeepClone().AsObject()).ToList();
        if (!QueueReplanFilter.AddSuppressedContinuation(suppressed, candidate.ObjectiveContinuation)) return null;
        evidence["joint_candidate_disposition"] = "unavailable_until_day_change";
        evidence["selected_candidate_completed"] = false;
        evidence["shared_source_return_required"] = false;
        evidence["native_dispatch_performed"] = false;
        return selection with { SuppressedForeignContinuations = suppressed };
    }
}
