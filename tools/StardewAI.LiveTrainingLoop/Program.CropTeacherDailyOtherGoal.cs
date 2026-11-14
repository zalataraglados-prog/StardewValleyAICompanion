using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ComposedCropDispatch> PrepareCropTeacherSelectedOtherGoalAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, SnapshotEnvelope snapshot, CropTeacherDailySelection selection,
        SelectedQueueCandidateLock candidate)
    {
        if (selection.OwnedCandidateIds.Contains(candidate.CandidateId))
            throw new InvalidOperationException("Owned crop work must stay with its original crop dispatch.");
        // Compile through the existing selected-candidate path, never through the crop executor.
        var prepared = await BuildQueueFromSelectedCandidateAsync(http, options, snapshot.StateHash, selection.GoalId,
            candidate).ConfigureAwait(false);
        if (ReadString(prepared.Evidence["availability"]?.AsObject(), "state_hash") != snapshot.StateHash ||
            ReadString(prepared.Plan, "state_hash") != snapshot.StateHash ||
            ReadString(prepared.Queue, "state_hash") != snapshot.StateHash)
            throw new InvalidDataException("Selected other-goal preparation belongs to a different snapshot.");
        var prefix = Path.Combine(options.SnapshotDir, "crop-joint-daily-" + iteration.ToString("D4") + "-other-goal");
        var planPath = prefix + "-plan.json";
        var queuePath = prefix + "-queue.json";
        var evidencePath = prefix + "-evidence.json";
        prepared.Evidence["decision_plan_path"] = selection.PlanPath;
        prepared.Evidence["decision_ranking_path"] = selection.RankingPath;
        prepared.Evidence["decision_snapshot_path"] = selection.SourceSnapshotPath;
        prepared.Evidence["native_dispatch_performed"] = false;
        prepared.Evidence["shared_source_return_required"] = true;
        prepared.Evidence["native_owner_interleaving_authorized"] = false;
        prepared.Evidence["formal_product_training_authorized"] = false;
        await WritePortfolioJournalFileAsync(planPath, prepared.Plan.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(queuePath, prepared.Queue.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(evidencePath, prepared.Evidence.ToJsonString(JsonOptions)).ConfigureAwait(false);
        return new(null, false, selection, "crop_teacher_joint_daily_selected_other_owner_required",
            new(candidate, planPath, queuePath, evidencePath, ReadString(prepared.Queue, "status"))
            {
                SourceSelection = new(selection.SourceSnapshotPath, selection.PlanPath, selection.RankingPath,
                    planPath, queuePath, evidencePath, candidate.QueueIndex,
                    selection.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray())
            });
    }
}
