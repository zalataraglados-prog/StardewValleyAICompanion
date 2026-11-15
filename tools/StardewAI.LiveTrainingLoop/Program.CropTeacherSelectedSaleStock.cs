using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ComposedCropDispatch> PrepareCropTeacherSelectedSaleStockAsync(HttpClient http, LiveTrainingOptions options,
        int iteration, SnapshotEnvelope snapshot, CropTeacherDailySelection selection, SelectedQueueCandidateLock candidate,
        CropTeacherManagedSeedOwner owner)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        if (snapshot.StateHash != JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!.StateHash)
            throw new InvalidDataException("Selected sale stock preparation lost the current owner source.");
        var decision = new CropTeacherSharedTransferDecision(selection.SourceSnapshotPath, selection.PlanPath, selection.RankingPath,
            candidate.QueueIndex, selection.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(), selection.DecisionReference);
        var operation = await owner.AdmitSelectedSaleStockPreparationAsync(decision,
            Path.Combine(options.SnapshotDir, $"execution-{iteration:D4}.json"),
            Path.Combine(options.SnapshotDir, $"after-snapshot-{iteration:D4}.json"),
            Path.Combine(owner.EvidenceDirectory, $"shared-source-{iteration:D4}")).ConfigureAwait(false);
        var prepared = await owner.PrepareSharedTransferAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (ledger, output) => CompileCropTeacherSelectedSaleStockAsync(http, options, operation, ledger, output)).ConfigureAwait(false);
        return new(null, false, selection, "crop_teacher_joint_daily_selected_other_owner_required",
            new(candidate, prepared.ModelPlanPath, prepared.QueuePath, prepared.EvidencePath, "pending")
            { SourceSelection = prepared, Operation = operation });
    }

    private static async Task<CropTeacherSharedSourceSelection> CompileCropTeacherSelectedSaleStockAsync(HttpClient http,
        LiveTrainingOptions options, CropTeacherManagedSeedOperation operation, StrategyCommitmentLedger ledger, string output)
    {
        var decision = operation.Record["shared_transfer_decision"]!.Deserialize<CropTeacherSharedTransferDecision>(JsonOptions)!;
        var original = JsonSerializer.Deserialize<StardewAI.Contracts.Execution.SmallModelPlanEnvelope>(
            File.ReadAllText(decision.PlanPath), JsonOptions)!;
        var ranking = JsonNode.Parse(File.ReadAllText(decision.RankingPath))!.AsObject();
        var candidate = SelectedQueueDecisionLease.CreateFromPlan(original, ranking).CandidateAt(decision.SelectedQueueIndex);
        var rebound = (JsonObject)candidate.RankedCandidate.DeepClone();
        rebound["parameters"] = JsonSerializer.SerializeToNode(CropSaleStockMaterialTransferBinding.ToParameters(ledger), JsonOptions);
        var prepared = await BuildQueueFromSelectedCandidateAsync(http, options, ledger.SourceStateHash,
            ledger.PreparedMaterialTransfer!.GoalId, candidate with { RankedCandidate = rebound }).ConfigureAwait(false);
        var planPath = Path.Combine(output, "selected-plan.json");
        var queuePath = Path.Combine(output, "selected-queue.json");
        var evidencePath = Path.Combine(output, "selected-refresh.json");
        prepared.Evidence["decision_plan_path"] = decision.PlanPath;
        prepared.Evidence["decision_ranking_path"] = decision.RankingPath;
        prepared.Evidence["decision_snapshot_path"] = decision.SnapshotPath;
        prepared.Evidence["native_dispatch_performed"] = false;
        prepared.Evidence["shared_source_return_required"] = true;
        prepared.Evidence["formal_product_training_authorized"] = false;
        await WritePortfolioJournalFileAsync(planPath, prepared.Plan.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(queuePath, prepared.Queue.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(evidencePath, prepared.Evidence.ToJsonString(JsonOptions)).ConfigureAwait(false);
        return decision.WithOutputs(planPath, queuePath, evidencePath);
    }
}
