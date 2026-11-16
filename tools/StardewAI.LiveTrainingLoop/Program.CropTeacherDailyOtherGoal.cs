using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Strategy;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ComposedCropDispatch> PrepareCropTeacherSelectedOtherGoalAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, SnapshotEnvelope snapshot, CropTeacherDailySelection selection,
        SelectedQueueCandidateLock candidate, CropTeacherManagedSeedOwner? owner = null)
    {
        if (selection.OwnedCandidateIds.Contains(candidate.CandidateId))
            throw new InvalidOperationException("Owned crop work must stay with its original crop dispatch.");
        var parameters = candidate.RankedCandidate["parameters"] as JsonArray;
        if (parameters?.Any(row => row is JsonObject parameter && parameter["name"] is JsonValue name &&
            name.TryGetValue<string>(out var value) && value.Equals(CropSaleStockTransferPreparation.RequestParameter, StringComparison.OrdinalIgnoreCase)) == true)
            return await PrepareCropTeacherSelectedSaleStockAsync(http, options, iteration, snapshot, selection, candidate,
                owner ?? throw new InvalidOperationException("Selected sale stock preparation requires its current owner before mutation.")).ConfigureAwait(false);
        // Compile through the existing selected-candidate path, never through the crop executor.
        candidate = CropTeacherSharedCandidateSelection.WithAcceptedContinuation(candidate, selection.ForeignObjectiveContinuations);
        var prepared = await BuildQueueFromSelectedCandidateAsync(http, options, snapshot.StateHash, selection.GoalId,
            candidate, materializedFilter: CropTeacherSharedCandidateSelection.FilterMaterializedCandidates).ConfigureAwait(false);
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
        var released = ReleaseUnavailableCropTeacherSelection(selection, candidate, prepared.Evidence, prepared.Queue);
        await WritePortfolioJournalFileAsync(planPath, prepared.Plan.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(queuePath, prepared.Queue.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(evidencePath, prepared.Evidence.ToJsonString(JsonOptions)).ConfigureAwait(false);
        if (released is not null)
            return new(null, false, released, "crop_teacher_joint_daily_continuation_released") { ReplanWithoutDispatch = true };
        return new(null, false, selection, "crop_teacher_joint_daily_selected_other_owner_required",
            new(candidate, planPath, queuePath, evidencePath, ReadString(prepared.Queue, "status"))
            {
                SourceSelection = new(selection.SourceSnapshotPath, selection.PlanPath, selection.RankingPath,
                    planPath, queuePath, evidencePath, candidate.QueueIndex,
                    selection.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray())
                    { DecisionReference = selection.DecisionReference }
            });
    }
}
