using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMaterialTransferPreparationBindings(FileBackedSupportingTransitionProof built,
        SnapshotEnvelope before, StrategyCommitmentLedger baseLedger)
    {
        var committed = CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(built.Proof.CommittedLedgerPath, "Prepared transfer test ledger");
        var commitResult = CurrentTeacherFrontierSupport.Read<ReservationPortfolioCommitResult>(built.Proof.CommitResultPath, "Prepared transfer test commit");
        foreach (var mutation in new[] { "omit", "intent", "route" })
        {
            var request = JsonSerializer.Deserialize<AcquisitionRouteSupportingTransitionRequest>(JsonSerializer.Serialize(built.Request))!;
            if (mutation == "omit") request.AtomicCommitRequest!.MaterialTransferPreparation = null;
            if (mutation == "intent") request.AtomicCommitRequest!.MaterialTransferPreparation!.Intent.Quantity++;
            if (mutation == "route") request.AtomicCommitRequest!.MaterialTransferPreparation!.RouteSourceDecisionId = "other-route:stone";
            var receipt = AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(request, baseLedger, committed, before, commitResult);
            Require(!receipt.SupportReservationCommitVerified, "Canonical commit accepted a drifted transfer preparation: " + mutation);
        }
        foreach (var mutation in new[] { "omit", "wrong", "duplicate" })
        {
            var plan = JsonSerializer.Deserialize<SmallModelPlanEnvelope>(JsonSerializer.Serialize(built.Compilation.CompiledPlan))!;
            foreach (var step in plan.Steps)
            {
                var retained = step.Parameters.Where(row => row.Name != PreparedMaterialTransferContract.BindingParameter).ToArray();
                step.Parameters = mutation switch
                {
                    "omit" => retained,
                    "wrong" => retained.Append(new SmallModelActionParameter { Name = PreparedMaterialTransferContract.BindingParameter, Value = new string('b', 64) }).ToArray(),
                    _ => step.Parameters.Concat(step.Parameters.Where(row => row.Name == PreparedMaterialTransferContract.BindingParameter)).ToArray()
                };
            }
            var queue = new ActionQueueCompiler().Compile(plan, before, committed);
            Require(queue.Status == "blocked" && queue.Items.Any(item => item.OptionId == "executor.transfer_material" && item.Status == "blocked"),
                "Original compiler accepted a missing, wrong or duplicate preparation: " + mutation);
        }
        var transition = built.TransitionReceipt;
        var commit = CurrentTeacherFrontierSupport.Read<AcquisitionRouteSupportingTransitionCommitReceipt>(built.Proof.SupportCommitReceiptPath, "Prepared transfer test receipt");
        var settlementRequest = CurrentTeacherFrontierSupport.Read<ReservationPortfolioSupportingTransitionSettlementRequest>(built.Proof.SettlementRequestPath, "Prepared transfer test settlement request");
        var settlementResult = CurrentTeacherFrontierSupport.Read<ReservationPortfolioSupportingTransitionSettlementResult>(built.Proof.SettlementResultPath, "Prepared transfer test settlement result");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(built.AfterSnapshotPath, "Prepared transfer test after");
        settlementResult.MaterialTransferReservations = null;
        var invalid = AcquisitionRouteSupportingTransitionSettlementBuilder.BuildReceiptCore(built.Request, commit, built.Compilation,
            transition, after, committed, settlementRequest, settlementResult, settlementResult.Ledger!, settlementRequest.SupportingTransitionReceiptSha256);
        Require(!invalid.ReservationLifecycleVerified && invalid.BlockingReasons.Contains("support_settlement_transfer_preparation_not_consumed", StringComparer.Ordinal),
            "Canonical settlement accepted an omitted complete reservation map.");
    }
}
