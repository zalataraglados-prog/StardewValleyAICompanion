using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleStockDailyPlanKeepsPreparedAuthorityThroughOriginalCandidateAndActionCompilation(bool deposit)
    {
        var f = deposit ? SaleStockDepositFixture() : SaleStockTransferFixture();
        var snapshot = SaleStockTransferPlanningSnapshot(f.Before);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = BoundSaleStockTransferParameters(f) }],
            commitmentLedger: f.PreparedLedger);
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "earn_money");
        var plan = new DailyPlanCompiler().Compile(rows, snapshot.StateHash, goalId: "earn_money", availableMinutes: 600, energyBudget: 270);
        var transfer = Assert.Single(plan.Steps.Where(step => step.Kind == "transfer_material"));
        Assert.Contains(transfer.Parameters, row => row.Name == "crop_sale_stock_transfer_version");
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, f.PreparedLedger);
        var item = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.transfer_material"));
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, item, f.PreparedLedger, snapshot.StateHash).Ready,
            string.Join(",", item.BlockingReasons));
    }

    [Theory]
    [InlineData("crop_seed_transfer_version")]
    [InlineData("museum_output_custody_transfer_version")]
    [InlineData("acquisition_support_transition_kind")]
    public void SaleStockDailyPlanDoesNotSanitizeConflictingControlFamilies(string name)
    {
        var f = SaleStockTransferFixture(); var snapshot = SaleStockTransferPlanningSnapshot(f.Before);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = BoundSaleStockTransferParameters(f) }],
            commitmentLedger: f.PreparedLedger);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        candidate.Parameters = candidate.Parameters.Append(new SmallModelActionParameter { Name = name, Value = "1" }).ToArray();
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "earn_money");
        var plan = new DailyPlanCompiler().Compile(rows, snapshot.StateHash, goalId: "earn_money", availableMinutes: 600, energyBudget: 270);
        Assert.Contains(Assert.Single(plan.Steps.Where(step => step.Kind == "transfer_material")).Parameters, row => row.Name == name);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, f.PreparedLedger);
        var item = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.transfer_material"));
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(queue, item, f.PreparedLedger, snapshot.StateHash).Ready);
    }
}
