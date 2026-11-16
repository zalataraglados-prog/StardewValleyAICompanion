using System.Text.Json;
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleStockPreparationPreviewDoesNotMutateOrAuthorizeEitherLedger(bool deposit)
    {
        var (snapshot, ledger, fixture) = SaleStockPreparationBasis(deposit);
        var parameters = MaterialTransferIntentBinder.ToParameters(fixture.PreparationRequest.MaterialTransferPreparation!.Intent);
        var original = JsonSerializer.Serialize(ledger); var originalSnapshot = JsonSerializer.Serialize(snapshot);
        var preview = CropSaleStockTransferPreparation.Create(snapshot, ledger, parameters, out var reasons);
        Assert.NotNull(preview); Assert.Empty(reasons);
        var available = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = parameters,
                AllowCropSaleStockPreparationPreview = true }], commitmentLedger: ledger);
        var option = Assert.Single(available.Options); Assert.True(option.Available, string.Join(",", option.BlockingReasons));
        Assert.Equal("denied", option.ExecutionAuthorization);
        Assert.Equal("requires_original_preparation_commit", option.CompileStatus);
        var candidate = Assert.Single(option.EventCandidates);
        var marker = Assert.Single(candidate.Parameters.Where(row => row.Name == CropSaleStockTransferPreparation.RequestParameter));
        Assert.Equal(JsonSerializer.Serialize(preview!.Request), marker.Value);
        var rank = new EventCandidateRanker().Rank(new BaselineTrainingReport(), available, "earn_money");
        var plan = new DailyPlanCompiler().Compile(rank, snapshot.StateHash, goalId: "earn_money", availableMinutes: 600, energyBudget: 270);
        Assert.Contains(Assert.Single(plan.Steps.Where(step => step.Kind == "transfer_material")).Parameters,
            row => row.Name == CropSaleStockTransferPreparation.RequestParameter);
        foreach (var source in new[] { ledger, preview.PreviewLedger })
        {
            var queue = new ActionQueueCompiler().Compile(plan, snapshot, source);
            var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.transfer_material"));
            Assert.False(new ActionQueueDispatchReadinessService().Evaluate(queue, item, source, snapshot.StateHash).Ready);
        }
        Assert.Equal(original, JsonSerializer.Serialize(ledger)); Assert.Equal(originalSnapshot, JsonSerializer.Serialize(snapshot));
        Assert.Null(ledger.PreparedMaterialTransfer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleStockPreparationPreviewNeedsTheOriginalCommitBeforeFreshExecutableCompilation(bool deposit)
    {
        var (snapshot, ledger, fixture) = SaleStockPreparationBasis(deposit);
        var preview = CropSaleStockTransferPreparation.Create(snapshot, ledger,
            MaterialTransferIntentBinder.ToParameters(fixture.PreparationRequest.MaterialTransferPreparation!.Intent), out _)!;
        var result = fixture.Service.Commit(ledger, snapshot, preview.Request, "actual-original-preparation");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var prepared = result.Ledger!.PreparedMaterialTransfer!;
        Assert.True(PreparedMaterialTransferContract.IsBound(result.Ledger, prepared.BindingSha256,
            prepared.PortfolioId, prepared.GoalId, prepared.RouteSourceDecisionId));
        Assert.Equal(ledger.Revision + 1, result.Ledger.Revision);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item",
                Parameters = CropSaleStockMaterialTransferBinding.ToParameters(result.Ledger) }], commitmentLedger: result.Ledger);
        Assert.True(Assert.Single(availability.Options).Available,
            string.Join(",", Assert.Single(availability.Options).BlockingReasons));
        var ranks = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "earn_money");
        var plan = new DailyPlanCompiler().Compile(ranks, snapshot.StateHash, goalId: "earn_money", availableMinutes: 600, energyBudget: 270);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, result.Ledger);
        var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.transfer_material"));
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, item, result.Ledger, snapshot.StateHash).Ready,
            string.Join(",", item.BlockingReasons));
        Assert.DoesNotContain(item.NormalizedCommand.Parameters, row => row.Name == CropSaleStockTransferPreparation.RequestParameter);
    }

    [Fact]
    public void SaleStockPreparationPreviewIsExplicitAndDoesNotSanitizeControlMetadata()
    {
        var (snapshot, ledger, fixture) = SaleStockPreparationBasis(false);
        var parameters = MaterialTransferIntentBinder.ToParameters(fixture.PreparationRequest.MaterialTransferPreparation!.Intent);
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var legacy = Assert.Single(evaluator.Evaluate(snapshot,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = parameters }], commitmentLedger: ledger).Options);
        Assert.False(legacy.Available); Assert.Contains("crop_sale_stock_transfer_preparation_required", legacy.BlockingReasons);
        foreach (var name in new[] { "crop_seed_transfer_version", "crop_sale_stock_transfer_unknown", "acquisition_support_transition_kind" })
        {
            var option = Assert.Single(evaluator.Evaluate(snapshot, [new OptionAvailabilityCandidate
            {
                OptionId = "inventory.transfer_item", AllowCropSaleStockPreparationPreview = true,
                Parameters = parameters.Append(new SmallModelActionParameter { Name = name, Value = "1" }).ToArray()
            }], commitmentLedger: ledger).Options);
            Assert.False(option.Available);
            Assert.DoesNotContain(option.EventCandidates.SelectMany(row => row.Parameters), row => row.Name == CropSaleStockTransferPreparation.RequestParameter);
        }
        Assert.DoesNotContain("allow_crop_sale_stock_preparation_preview", JsonSerializer.Serialize(new OptionAvailabilityCandidate()));
    }

    private static (SnapshotEnvelope Snapshot, StrategyCommitmentLedger Ledger, PreparedMaterialTransferTestFixture Fixture)
        SaleStockPreparationBasis(bool deposit)
    {
        var fixture = deposit ? SaleStockDepositFixture() : SaleStockTransferFixture();
        var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        // Original non-consuming source rebind, not an edited ledger head.
        var result = fixture.Service.Commit(fixture.BaseLedger, snapshot, new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = fixture.BaseLedger.Revision,
            PortfolioId = "declared-transfer-planning-source", SourceDecisionId = "declared-transfer-planning-source", GoalId = "earn_money"
        }, "declared-planning-source");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        return (snapshot, result.Ledger!, fixture);
    }
}
