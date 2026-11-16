using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void TransferableSaleStockKeepsItsCreationIdentityThroughOriginalPreparedTransferSettlement()
    {
        var fixture = SaleStockTransferFixture();
        var claim = fixture.BaseLedger.MaterialReservations.Single(row => CropSaleStockReservationContract.TryRead(row, out _));
        var settlement = fixture.Settlement(); settlement.GoalId = "earn_money";
        var result = fixture.Service.SettleSupportingTransition(fixture.PreparedLedger, fixture.After, settlement, "settled");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var moved = result.Ledger!.MaterialReservations.Single(row => row.ReservationId == claim.ReservationId);
        Assert.Equal("player:123", moved.NodeId); Assert.Equal(0, moved.SlotIndex);
        Assert.Equal(claim.SourceDecisionId, moved.SourceDecisionId); Assert.Equal(claim.Purpose, moved.Purpose);
        Assert.True(CropSaleStockReservationContract.TryRead(moved, out var purpose)); Assert.Equal(5, purpose!.OriginSlotIndex);
        Assert.Equal(2, Assert.Single(CropTeacherSaleStockInventory.ReadVerified(fixture.After, result.Ledger,
            "declared-transfer-crop", "declared-wave", "(O)388")).Quantity);
        Assert.DoesNotContain(result.Ledger.History, row => row.Operation == "reservation_portfolio_route_complete");
        Assert.Empty(result.CompletedMaterialReservationIds); Assert.Equal(0, result.ConsumedQuantity);
    }

    private static PreparedMaterialTransferTestFixture SaleStockTransferFixture()
    {
        var before = PreparedMaterialTransferTestFixture.Snapshot(false);
        var after = PreparedMaterialTransferTestFixture.Snapshot(true);
        // Declared transfer stock and item metadata; no harvest/production/native-outcome credit.
        AddPlayerInventory(before, []);
        var item = JsonSerializer.SerializeToElement(new { slot_index = 0, qualified_item_id = "(O)388", item_id = "388",
            stack = 2, quality = 0, is_empty = false, can_be_shipped = true, sell_to_store_price = 2 });
        AddPlayerInventory(after, [item]);
        var route = PreparedMaterialTransferTestFixture.RouteRequest(before);
        route.GoalId = "earn_money";
        var claim = route.MaterialClaims.Single(); claim.GoalId = route.GoalId;
        claim.ReservationId = CropSaleStockReservationContract.ReservationId(claim.SourceDecisionId, 5);
        claim.Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-transfer-crop", "declared-wave",
            MoneyRevenueCropRetentionAccount.InventoryIdentity(item), 5);
        var service = new ReservationPortfolioLedgerService();
        var first = service.Commit(null, before, route, "route");
        Assert.True(first.Accepted, string.Join(",", first.Errors));
        Assert.True(CropSaleStockReservationContract.TryRead(first.Ledger!.MaterialReservations.Single(), out _));
        var other = service.Commit(first.Ledger, before, PreparedMaterialTransferTestFixture.OtherRequest(before), "other");
        Assert.True(other.Accepted, string.Join(",", other.Errors));
        var preparation = PreparedMaterialTransferTestFixture.Preparation(before);
        preparation.GoalId = "earn_money";
        preparation.MaterialTransferPreparation!.MaterialRelocations[0].MaterialReservationId = claim.ReservationId;
        var prepared = service.Commit(other.Ledger, before, preparation, "prepared");
        Assert.True(prepared.Accepted, string.Join(",", prepared.Errors));
        return new(before, after, service, other.Ledger!, prepared.Ledger!, preparation);
    }

    [Theory]
    [InlineData("origin_missing")]
    [InlineData("origin_negative")]
    [InlineData("id")]
    [InlineData("actor")]
    [InlineData("node")]
    [InlineData("source")]
    public void TransferableSaleStockClassificationRejectsInvalidOriginOrCurrentPosition(string fault)
    {
        var claim = new StardewAI.Contracts.Strategy.MaterialReservation
        {
            ReservationId = CropSaleStockReservationContract.ReservationId("producer", 7), SourceDecisionId = "producer",
            GoalId = "earn_money", SlotIndex = 0, NodeId = "player:123", OwnerPlayerId = 123, Quantity = 2,
            Purpose = CropSaleStockReservationContract.TransferablePurpose("crop", "wave", new string('a', 64), 7)
        };
        Assert.True(CropSaleStockReservationContract.TryRead(claim, out _));
        if (fault is "origin_missing" or "origin_negative")
        {
            var purpose = JsonNode.Parse(claim.Purpose)!;
            if (fault == "origin_missing") purpose.AsObject().Remove("origin_slot_index"); else purpose["origin_slot_index"] = -1;
            claim.Purpose = purpose.ToJsonString();
        }
        if (fault == "id") claim.ReservationId = CropSaleStockReservationContract.ReservationId("producer", 0);
        if (fault == "actor") claim.OwnerPlayerId = 456;
        if (fault == "node") claim.NodeId = "unknown:inventory";
        if (fault == "source") claim.SourceDecisionId = "other";
        Assert.False(CropSaleStockReservationContract.TryRead(claim, out _));
    }

    private static void AddPlayerInventory(SnapshotEnvelope snapshot, JsonElement[] items)
    {
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["inventory"] = JsonSerializer.SerializeToNode(new { status = "available", value = items });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
}
