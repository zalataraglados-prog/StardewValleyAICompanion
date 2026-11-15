using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedSaleStockDispatchUsesItsOwnBindingThroughCandidateAndFreshCompilation(bool deposit)
    {
        var fixture = deposit ? SaleStockDepositFixture() : SaleStockTransferFixture();
        var ledger = fixture.PreparedLedger; var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        var parameters = BoundSaleStockTransferParameters(fixture);
        var original = JsonSerializer.Serialize(ledger);
        var compiled = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot, parameters), snapshot, ledger);
        var item = Assert.Single(compiled.Items);
        Assert.True(CropSaleStockMaterialTransferBinding.IsBound(item, ledger));
        var dispatch = new ActionQueueDispatchReadinessService().Evaluate(compiled, item, ledger, snapshot.StateHash);
        Assert.True(dispatch.Ready, string.Join(",", dispatch.BlockingReasons));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { new OptionAvailabilityCandidate
            { OptionId = "inventory.transfer_item", Parameters = parameters } }, commitmentLedger: ledger);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.All(parameters.Where(row => CropSaleStockMaterialTransferBinding.IsControlParameter(row.Name)), binding =>
            Assert.Equal(binding.Value, Assert.Single(candidate.Parameters.Where(row => row.Name == binding.Name)).Value));
        var refreshed = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot, candidate.Parameters), snapshot, ledger);
        var ready = new ActionQueueDispatchReadinessService().Evaluate(refreshed, Assert.Single(refreshed.Items), ledger, snapshot.StateHash);
        Assert.True(ready.Ready, string.Join(",", ready.BlockingReasons));
        var prepared = ledger.PreparedMaterialTransfer!;
        var settled = fixture.Service.SettleSupportingTransition(ledger, fixture.After,
            new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = fixture.After.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = prepared.PortfolioId,
                GoalId = prepared.GoalId, RouteSourceDecisionId = prepared.RouteSourceDecisionId,
                SupportingTransitionReceiptSha256 = new string('c', 64), PreparedMaterialTransferSha256 = prepared.BindingSha256,
                MaterialRelocations = prepared.MaterialRelocations, Reason = "verified_supporting_transition_relocation"
            }, "declared-settled");
        Assert.True(settled.Accepted, string.Join(",", settled.Errors));
        Assert.Null(settled.Ledger!.PreparedMaterialTransfer);
        foreach (var move in prepared.MaterialRelocations)
        {
            var row = Assert.Single(settled.Ledger.MaterialReservations.Where(row => row.ReservationId == move.MaterialReservationId));
            Assert.Equal(move.DestinationNodeId, row.NodeId); Assert.Equal(move.Quantity, row.Quantity);
            Assert.True(CropSaleStockReservationContract.TryRead(row, out _));
        }
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("portfolio")]
    [InlineData("source")]
    [InlineData("revision")]
    [InlineData("ids")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("case_alias")]
    [InlineData("seed")]
    [InlineData("acquisition")]
    public void PreparedSaleStockDispatchRejectsChangedOrMixedControlBindings(string fault)
    {
        var fixture = SaleStockTransferFixture(); var ledger = fixture.PreparedLedger;
        var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        var parameters = BoundSaleStockTransferParameters(fixture);
        const string prefix = "crop_sale_stock_transfer_";
        if (fault == "version") parameters.Single(row => row.Name == prefix + "version").Value = "2";
        if (fault == "portfolio") parameters.Single(row => row.Name == prefix + "portfolio_id").Value = "other";
        if (fault == "source") parameters.Single(row => row.Name == prefix + "source_decision_id").Value = "other";
        if (fault == "revision") parameters.Single(row => row.Name == prefix + "ledger_revision").Value = "999";
        if (fault == "ids") parameters.Single(row => row.Name == prefix + "reservation_ids_json").Value = "[]";
        if (fault == "missing") parameters = parameters.Where(row => row.Name != prefix + "portfolio_id").ToArray();
        if (fault == "duplicate") parameters = parameters.Append(parameters.Single(row => row.Name == prefix + "portfolio_id")).ToArray();
        if (fault == "unknown") parameters = parameters.Append(new() { Name = prefix + "unknown", Value = "1" }).ToArray();
        if (fault == "case_alias") parameters = parameters.Append(new() { Name = "CROP_SALE_STOCK_TRANSFER_VERSION", Value = "1" }).ToArray();
        if (fault == "seed") parameters = parameters.Append(new() { Name = "crop_seed_transfer_version", Value = "1" }).ToArray();
        if (fault == "acquisition") parameters = parameters.Append(new() { Name = "acquisition_support_transition_kind", Value = "machine_input_material_transfer" }).ToArray();
        var queue = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot, parameters), snapshot, ledger);
        var item = Assert.Single(queue.Items);
        Assert.Contains("crop_sale_stock_transfer_control_binding_drifted", item.BlockingReasons);
        var dispatch = new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, snapshot.StateHash);
        Assert.False(dispatch.Ready);
        Assert.Contains("dispatch_crop_sale_stock_transfer_binding_drifted", dispatch.BlockingReasons);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { new OptionAvailabilityCandidate
            { OptionId = "inventory.transfer_item", Parameters = parameters } }, commitmentLedger: ledger);
        Assert.Contains("crop_sale_stock_transfer_control_binding_drifted", Assert.Single(availability.Options).BlockingReasons);
    }

    [Fact]
    public void SaleStockControlCannotDowngradeToUnreservedLegacyDispatch()
    {
        var fixture = SaleStockTransferFixture(); var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        var queue = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot,
            BoundSaleStockTransferParameters(fixture)), snapshot, fixture.PreparedLedger);
        var item = Assert.Single(queue.Items); item.NormalizedCommand.GoalId = null;
        item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row =>
            !CropSaleStockMaterialTransferBinding.IsControlParameter(row.Name) && row.Name != PreparedMaterialTransferContract.BindingParameter)
            .Append(new() { Name = "crop_sale_stock_transfer_version", Value = "1" }).ToArray();
        var result = new ActionQueueDispatchReadinessService().Evaluate(queue, item, new StrategyCommitmentLedger(), snapshot.StateHash);
        Assert.False(result.Ready); Assert.Contains("dispatch_crop_sale_stock_transfer_binding_drifted", result.BlockingReasons);
    }

    [Fact]
    public void SaleStockControlCannotChangeItsPreparedExecutionGoal()
    {
        var fixture = SaleStockTransferFixture(); var snapshot = SaleStockTransferPlanningSnapshot(fixture.Before);
        var queue = new ActionQueueCompiler().Compile(SaleStockTransferAction(snapshot,
            BoundSaleStockTransferParameters(fixture)), snapshot, fixture.PreparedLedger);
        var item = Assert.Single(queue.Items); item.NormalizedCommand.GoalId = "other.goal";
        Assert.False(CropSaleStockMaterialTransferBinding.IsBound(item, fixture.PreparedLedger));
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(queue, item, fixture.PreparedLedger, snapshot.StateHash).Ready);
    }

    [Fact]
    public void MuseumControlCannotAcceptSaleStockControlMetadata()
    {
        var fixture = new CustodyTransferFixture(); var attempt = PrepareCustodyControl(fixture, withdraw: false);
        Assert.True(MuseumOutputCustodyTransferBinding.IsBound(attempt.Transfer, fixture.Ledger));
        attempt.Transfer.NormalizedCommand.Parameters = attempt.Transfer.NormalizedCommand.Parameters
            .Append(new() { Name = "crop_sale_stock_transfer_version", Value = "1" }).ToArray();
        Assert.False(MuseumOutputCustodyTransferBinding.IsBound(attempt.Transfer, fixture.Ledger));
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(attempt.Compilation.Queue!, attempt.Transfer,
            fixture.Ledger, attempt.Before.StateHash).Ready);
    }

    private static SmallModelActionParameter[] BoundSaleStockTransferParameters(PreparedMaterialTransferTestFixture fixture) =>
        SaleStockTransferParameters(fixture).Concat(CropSaleStockMaterialTransferBinding.ToParameters(fixture.PreparedLedger)
            .Where(row => CropSaleStockMaterialTransferBinding.IsControlParameter(row.Name))).ToArray();

    private static PreparedMaterialTransferTestFixture SaleStockDepositFixture()
    {
        // Declared reverse physical projection; not a native deposit or production outcome.
        var before = PreparedMaterialTransferTestFixture.Snapshot(true);
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!; after.GameTick++;
        var item = JsonSerializer.SerializeToElement(new { slot_index = 0, qualified_item_id = "(O)388", item_id = "388",
            stack = 2, quality = 0, is_empty = false, can_be_shipped = true, sell_to_store_price = 2 });
        AddPlayerInventory(before, [item]); AddPlayerInventory(after, []);
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        graph!.InventoryNodes.Single(row => row.NodeId == "player:123").Slots = [];
        graph.InventoryNodes.Single(row => row.NodeId == "chest:Farm:4,5").Slots.Single(row => row.SlotIndex == 0).Stack += 2;
        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!;
        farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph);
        after.State["farm"] = JsonSerializer.SerializeToElement(farm); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var service = new ReservationPortfolioLedgerService();
        const string producer = "declared-stock-deposit";
        var stock = service.Commit(null, before, new ReservationPortfolioCommitRequest
        {
            StateHash = before.StateHash, PortfolioId = "declared-stock", SourceDecisionId = producer, GoalId = "earn_money",
            MaterialClaims = [new()
            {
                StateHash = before.StateHash, ReservationId = CropSaleStockReservationContract.ReservationId(producer, 5),
                SourceDecisionId = producer, GoalId = "earn_money", NodeId = "player:123", SlotIndex = 0, QualifiedItemId = "(O)388", Quantity = 2,
                Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-crop", "declared-wave",
                    MoneyRevenueCropRetentionAccount.InventoryIdentity(item), 5)
            }]
        }, "stock");
        Assert.True(stock.Accepted, string.Join(",", stock.Errors));
        var prepare = new ReservationPortfolioCommitRequest
        {
            StateHash = before.StateHash, ExpectedLedgerRevision = stock.Ledger!.Revision, PortfolioId = "declared-deposit",
            SourceDecisionId = "declared-deposit", GoalId = "earn_money", MaterialTransferPreparation = new()
            {
                RouteSourceDecisionId = producer, Intent = new() { SourceNodeId = "player:123", SourceSlotIndex = 0,
                    DestinationNodeId = "chest:Farm:4,5", QualifiedItemId = "(O)388", Quantity = 2, ExpectedSourceStack = 2 },
                MaterialRelocations = [new() { MaterialReservationId = stock.Ledger.MaterialReservations.Single().ReservationId,
                    SourceNodeId = "player:123", SourceSlotIndex = 0, DestinationNodeId = "chest:Farm:4,5", DestinationSlotIndex = 0,
                    QualifiedItemId = "(O)388", Quantity = 2 }]
            }
        };
        var prepared = service.Commit(stock.Ledger, before, prepare, "prepared");
        Assert.True(prepared.Accepted, string.Join(",", prepared.Errors));
        return new(before, after, service, stock.Ledger, prepared.Ledger!, prepare);
    }
}
