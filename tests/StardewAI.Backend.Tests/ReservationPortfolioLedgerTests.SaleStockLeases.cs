using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleStockLeasesReserveOnlyActualFreeUnitsAndUseOriginalTransferSettlementAndRelease(bool deposit)
    {
        var (before, ledger, f) = SaleStockPreparationBasis(deposit);
        MaterialInventoryGraphReader.TryRead(before, out var graph);
        var intent = f.PreparationRequest.MaterialTransferPreparation!.Intent;
        var slot = graph!.InventoryNodes.Single(row => row.NodeId == intent.SourceNodeId).Slots.Single(row => row.SlotIndex == intent.SourceSlotIndex);
        slot.Stack++; intent.Quantity = 1; intent.ExpectedSourceStack = slot.Stack;
        SetPartitionPlayer(before, graph); StockGraph(before, graph);
        var source = f.Service.Commit(ledger, before, new ReservationPortfolioCommitRequest
        { StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = "lease-source",
            SourceDecisionId = "lease-source", GoalId = "earn_money" }, "source");
        Assert.True(source.Accepted, string.Join(",", source.Errors)); ledger = source.Ledger!;
        var original = JsonSerializer.Serialize(ledger);
        var preview = CropSaleStockTransferPreparation.Create(before, ledger, MaterialTransferIntentBinder.ToParameters(intent), out var reasons);
        Assert.NotNull(preview); Assert.Empty(reasons); Assert.True(CropSaleStockTransferLeaseContract.IsLeaseRequest(preview!.Request));
        Assert.Single(preview.Request.MaterialClaims); Assert.Single(preview.Request.MaterialTransferPreparation!.MaterialRelocations);
        Assert.Equal(1, preview.Request.MaterialClaims.Sum(row => row.Quantity));
        Assert.Equal(original, JsonSerializer.Serialize(ledger)); Assert.Null(ledger.PreparedMaterialTransfer);
        var commit = f.Service.Commit(ledger, before, preview.Request, "lease-prepared");
        Assert.True(commit.Accepted, string.Join(",", commit.Errors));
        Assert.True(CropSaleStockTransferLeaseContract.IsBound(commit.Ledger!));
        var parameters = CropSaleStockMaterialTransferBinding.ToParameters(commit.Ledger!).Concat(new SmallModelActionParameter[]
            { new() { Name = "stand_tile_x", Value = "3" }, new() { Name = "stand_tile_y", Value = "5" } }).ToArray();
        Assert.Equal("2", parameters.Single(row => row.Name == "crop_sale_stock_transfer_version").Value);
        var queue = new ActionQueueCompiler().Compile(SaleStockTransferAction(before, parameters), before, commit.Ledger);
        var transfer = Assert.Single(queue.Items);
        var readiness = new ActionQueueDispatchReadinessService().Evaluate(queue, transfer, commit.Ledger!, before.StateHash);
        Assert.True(readiness.Ready, string.Join(",", readiness.BlockingReasons.Concat(transfer.BlockingReasons)));
        var after = ProjectPartitionAfter(before, commit.Ledger!.PreparedMaterialTransfer!);
        var settled = SettleStock(f, commit.Ledger, after);
        var ids = commit.Ledger.PreparedMaterialTransfer!.MaterialRelocations.Select(row => row.MaterialReservationId).ToArray();
        var released = f.Service.Commit(settled, after, new ReservationPortfolioCommitRequest
        { StateHash = after.StateHash, ExpectedLedgerRevision = settled.Revision, PortfolioId = "lease-complete",
            SourceDecisionId = "lease-complete", GoalId = "earn_money", ReleaseReservationIds = ids }, "released");
        Assert.True(released.Accepted, string.Join(",", released.Errors));
        Assert.All(ids, id => Assert.Equal(StrategyCommitmentStatuses.Cancelled,
            released.Ledger!.MaterialReservations.Single(row => row.ReservationId == id).Status));
        foreach (var claim in ledger.MaterialReservations)
        {
            var current = released.Ledger!.MaterialReservations.Single(row => row.ReservationId == claim.ReservationId);
            Assert.Equal(claim.Quantity, current.Quantity); Assert.Equal(claim.Status, current.Status); Assert.Equal(claim.Purpose, current.Purpose);
        }
        if (deposit)
        {
            var start = StockFrame(before, 1, 0); start.GameTick = before.GameTick - 1;
            var account = StockAccount(start); StockObserve(account, start, before, true);
            var stock = StockTrace();
            StockObserveTransfer(account, before, after, StockRead(stock, before, ledger), StockRead(stock, after, released.Ledger!));
            Assert.Equal(2, StockQuantity(account)); // The one moved free unit is not crop production.
        }
        else Assert.Equal(0, released.Ledger!.MaterialReservations.Single(row =>
            CropSaleStockReservationContract.TryRead(row, out _)).SlotIndex); // Original passive chest compaction.
    }

    [Fact]
    public void SaleStockLeasesMoveActualUnreservedMaterialAcrossMultipleDestinationsAndPreservePassiveClaims()
    {
        var (before, ledger, f) = SaleStockPreparationBasis(false);
        MaterialInventoryGraphReader.TryRead(before, out var graph);
        var chest = graph!.InventoryNodes.Single(row => row.NodeId == "chest:Farm:4,5");
        chest.Slots = chest.Slots.Append(new() { SlotIndex = 2, QualifiedItemId = "(O)382", Stack = 2,
            Quality = 0, MaximumStackSize = 999, RuntimeType = "StardewValley.Object" }).ToArray();
        graph.InventoryNodes.Single(row => row.NodeId == "player:123").Slots =
            [new() { SlotIndex = 0, QualifiedItemId = "(O)382", Stack = 998, Quality = 0,
                MaximumStackSize = 999, RuntimeType = "StardewValley.Object" }];
        SetPartitionPlayer(before, graph); StockGraph(before, graph);
        var anchored = f.Service.Commit(ledger, before, new ReservationPortfolioCommitRequest
        { StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = "lease-multiple-source",
            SourceDecisionId = "lease-multiple-source", GoalId = "earn_money" }, "source");
        Assert.True(anchored.Accepted, string.Join(",", anchored.Errors)); ledger = anchored.Ledger!;
        var intent = new MaterialTransferIntent { SourceNodeId = chest.NodeId, SourceSlotIndex = 2, DestinationNodeId = "player:123",
            QualifiedItemId = "(O)382", Quantity = 2, ExpectedSourceStack = 2 };
        var preview = CropSaleStockTransferPreparation.Create(before, ledger, MaterialTransferIntentBinder.ToParameters(intent), out var reasons);
        Assert.NotNull(preview); Assert.Empty(reasons); Assert.Equal(2, preview!.Request.MaterialClaims.Length);
        Assert.All(preview.Request.MaterialClaims, row => Assert.Equal(1, row.Quantity));
        var prepared = f.Service.Commit(ledger, before, preview.Request, "prepared");
        Assert.True(prepared.Accepted, string.Join(",", prepared.Errors)); Assert.True(CropSaleStockTransferLeaseContract.IsBound(prepared.Ledger!));
        var after = ProjectPartitionAfter(before, prepared.Ledger!.PreparedMaterialTransfer!);
        var settled = SettleStock(f, prepared.Ledger, after);
        foreach (var original in ledger.MaterialReservations.Where(row => row.NodeId == chest.NodeId))
        {
            var claim = settled.MaterialReservations.Single(row => row.ReservationId == original.ReservationId);
            Assert.Equal(original.QualifiedItemId == "(O)388" ? 0 : 1, claim.SlotIndex);
            Assert.Equal(original.Quantity, claim.Quantity); Assert.Equal(original.Purpose, claim.Purpose);
        }
        Assert.Equal(2, prepared.Ledger.PreparedMaterialTransfer!.MaterialRelocations.Length);
    }

    [Fact]
    public void SaleStockLeasesCannotMoveReservedUnitsOrInferMissingLeaseSchema()
    {
        var (before, ledger, f) = SaleStockPreparationBasis(true);
        var intent = f.PreparationRequest.MaterialTransferPreparation!.Intent; intent.Quantity = 1;
        Assert.Null(CropSaleStockTransferPreparation.Create(before, ledger, MaterialTransferIntentBinder.ToParameters(intent), out var reasons));
        Assert.Contains("crop_sale_stock_transfer_lease_free_quantity_unavailable", reasons);
        var request = new ReservationPortfolioCommitRequest
        { MaterialClaims = [new() { Purpose = "{\"portfolio_id\":\"x\",\"source_state_hash\":\"x\",\"destination_slot_index\":0}" }] };
        Assert.False(CropSaleStockTransferLeaseContract.IsLeaseRequest(request));
    }
}
