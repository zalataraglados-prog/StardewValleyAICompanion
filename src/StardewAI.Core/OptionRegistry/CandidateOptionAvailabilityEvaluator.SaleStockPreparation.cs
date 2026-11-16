using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private CompilerProbeResult ProbeSaleStockDailyPlan(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, EventCandidate[] candidates)
    {
        var availability = new OptionAvailabilityEnvelope { StateHash = snapshot.StateHash,
            Options = new[] { new OptionAvailability { OptionId = "inventory.transfer_item", Available = true, ReadEligible = true,
                EventCandidates = candidates } } };
        var goal = ledger.PreparedMaterialTransfer!.GoalId;
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, goal);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash, goalId: goal, maxCandidates: 1);
        var queue = compiler.Compile(plan, snapshot, ledger);
        var reasons = candidates.SelectMany(row => row.BlockReasons)
            .Concat(queue.CompilerDiagnostics).Concat(queue.Items.SelectMany(CompilerProbeBlockingReasons)).Distinct(StringComparer.Ordinal).ToArray();
        if (queue.Items.Length == 0) reasons = reasons.Append("compiler_probe_did_not_return_queue_item").ToArray();
        return new CompilerProbeResult { BindingStatus = "bound", CompileStatus = queue.Status == "pending" && reasons.Length == 0 ? "ready" : "blocked",
            BlockingReasons = reasons };
    }

    private OptionAvailability EvaluateSaleStockPreparationPreview(SnapshotEnvelope snapshot,
        OptionAvailabilityCandidate candidate, StrategyCommitmentLedger? ledger)
    {
        var actual = JsonSerializer.Deserialize<OptionAvailabilityCandidate>(JsonSerializer.Serialize(candidate))!;
        actual.AllowCropSaleStockPreparationPreview = false;
        if (candidate.OptionId != "inventory.transfer_item" || ledger is null || ledger.PreparedMaterialTransfer is not null ||
            candidate.Parameters.Any(row => CropSaleStockMaterialTransferBinding.IsControlParameter(row.Name) ||
                CropSaleStockMaterialTransferBinding.IsForeignControlParameter(row.Name) || row.Name == PreparedMaterialTransferContract.BindingParameter) ||
            candidate.Parameters.Any(row => row.Name == MaterialTransferIntentBinder.PhaseParameter))
            return EvaluateOne(snapshot, actual, ledger);
        var preview = CropSaleStockTransferPreparation.Create(snapshot, ledger, candidate.Parameters, out var reasons);
        if (preview is null)
        {
            var blocked = EvaluateOne(snapshot, actual, ledger);
            blocked.AvailabilityNotes = blocked.AvailabilityNotes.Concat(reasons).Distinct(StringComparer.Ordinal).ToArray();
            return blocked;
        }
        actual.Parameters = CropSaleStockMaterialTransferBinding.ToParameters(preview.PreviewLedger);
        var result = EvaluateOne(snapshot, actual, preview.PreviewLedger);
        var marker = new SmallModelActionParameter
        { Name = CropSaleStockTransferPreparation.RequestParameter, Value = JsonSerializer.Serialize(preview.Request) };
        result.BindingStatus = "preparation_preview";
        result.CompileStatus = "requires_original_preparation_commit";
        result.ExecutionAuthorization = "denied";
        if (result.Available) result.Status = "preparation_preview_available";
        result.Parameters = result.Parameters.Append(marker).ToArray();
        foreach (var row in result.EventCandidates)
        {
            row.Parameters = row.Parameters.Append(marker).ToArray();
            row.AvailabilityClass = "crop_sale_stock_transfer_preparation_preview.v1";
            if (!result.Available) { row.Available = false; row.AllowedNow = false; row.AllowedToday = false; }
        }
        result.AvailabilityNotes = result.AvailabilityNotes.Append("original_preparation_commit_required_before_dispatch").ToArray();
        return result;
    }
}
