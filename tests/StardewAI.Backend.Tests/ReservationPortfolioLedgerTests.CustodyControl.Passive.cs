using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private static void AssertPassiveCustodyTransferDispatches(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger)
    {
        var prepared = ledger.PreparedMaterialTransfer!;
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = MaterialTransferIntentBinder.ToParameters(prepared.Intent) }
        }, includeExecutorCalibrationOptions: true, commitmentLedger: ledger);
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, prepared.GoalId);
        var plan = new DailyPlanCompiler().Compile(candidates, snapshot.StateHash, goalId: prepared.GoalId, maxCandidates: 1);
        foreach (var step in plan.Steps.Where(step => step.Kind == "transfer_material"))
            step.Parameters = step.Parameters.Concat(new[]
            {
                new SmallModelActionParameter { Name = PreparedMaterialTransferContract.BindingParameter, Value = prepared.BindingSha256 },
                new SmallModelActionParameter { Name = "acquisition_reservation_portfolio_id", Value = prepared.PortfolioId },
                new SmallModelActionParameter { Name = "acquisition_reservation_ledger_revision", Value = ledger.Revision.ToString() },
                new SmallModelActionParameter { Name = "acquisition_support_transition_kind", Value = "machine_input_material_transfer" }
            }).ToArray();
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var item = Assert.Single(queue.Items, row => row.OptionId == "executor.transfer_material");
        Assert.Equal("pending", queue.Status);
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, snapshot.StateHash).Ready);
        var custody = ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
        Assert.DoesNotContain(prepared.MaterialRelocations, row => row.MaterialReservationId == custody.ReservationId);
        Assert.Contains(prepared.ReservationProjection.ReservationTransitions, row => row.ReservationId == custody.ReservationId);
    }
}
