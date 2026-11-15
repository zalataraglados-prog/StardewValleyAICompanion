using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ExplicitSaleStockHarvestPersistsItsOriginalOwnerModeAndClaimsAcrossReopen()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var operation = await f.PrepareHarvest(owner, retainSaleStock: true);
        await owner.ExecuteOriginalHarvestAsync(operation, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, operation);
        Assert.Equal(1, f.NativeHarvestCalls); Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
        var before = ReadLedger(owner.CurrentLedgerPath);
        Assert.Equal(2, before.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            CropSaleStockReservationContract.TryRead(row, out _)).Sum(row => row.Quantity));
        var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000005-intent.json")))!;
        Assert.Equal("native_sale_stock.v1", intent["harvest_output_reservation_protocol"]!.GetValue<string>());
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(JsonSerializer.Serialize(before, JsonOptions), JsonSerializer.Serialize(ReadLedger(reopened.CurrentLedgerPath), JsonOptions));
        Assert.Null(reopened.Pending); Assert.Equal(5, f.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.NativeHarvestCalls);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void ExplicitHarvestSaleStockReservesOnlyTheAppliedNativeProducerMinimum(int applied, bool failedSuffix)
    {
        using var f = new HarvestFixture();
        var start = CropTeacherHarvest.Preparation.OpenRetainingSaleStock(f.Shared.Inputs.PlanPath, f.SourcePath,
            f.LedgerPath, [Id], Id, "harvest.sale-stock.1", f.Day, f.Batch.BatchIndex, f.ModelPath, f.QueuePath);
        f.Emit(applied, failedSuffix);
        var scope = start.VerifyTransition(f.Transition);
        var request = scope.RebindRequest;
        Assert.Equal(applied, request.MaterialClaims.Sum(row => row.Quantity));
        Assert.Equal(applied, scope.RetainedQuantityIncrease);
        Assert.Empty(f.Ledger.MaterialReservations.Where(row => CropSaleStockReservationContract.TryRead(row, out _)));
        var reply = new ReservationPortfolioLedgerService().Commit(scope.BaseLedger, f.After, request, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var after = scope.VerifyRebindReply(reply);
        var sale = after.MaterialReservations.Where(row => CropSaleStockReservationContract.TryRead(row, out _)).ToArray();
        Assert.Equal(applied, sale.Sum(row => row.Quantity));
        Assert.All(sale, row =>
        {
            Assert.Equal(StrategyCommitmentStatuses.Active, row.Status);
            Assert.Equal(f.Output, row.QualifiedItemId);
            Assert.Equal(f.After.StateHash, row.SourceStateHash);
            Assert.True(CropSaleStockReservationContract.TryRead(row, out var purpose));
            Assert.Equal(Id, purpose!.CommitmentId);
            Assert.Equal(start.OutputRetentionKey, purpose.OutputRetentionKey);
        });
        Assert.True(MaterialInventoryGraphReader.TryRead(f.After, out var graph));
        var projected = new MaterialSupplyProjection().Project(graph!, after.MaterialReservations);
        Assert.Equal("available", projected.Status);
        // One pre-existing fixture item is NOT this producer's output and remains unreserved.
        var slot = Assert.Single(projected.Slots.Where(row => row.QualifiedItemId == f.Output));
        Assert.Equal(1, slot.AvailableQuantity);
        Assert.False(scope.WholeWaveOutputRetentionVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void LegacyHarvestKeepsItsOriginalClaimlessMarkerAndSchema()
    {
        using var f = new HarvestFixture(); var start = f.Open(); f.Emit();
        var request = start.VerifyTransition(f.Transition).RebindRequest;
        Assert.Empty(request.MaterialClaims);
        Assert.Null(request.MaterialConsumption);
        Assert.DoesNotContain("material_consumption", JsonSerializer.Serialize(request, JsonOptions));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("slot")]
    [InlineData("owner")]
    [InlineData("purpose")]
    public void SaleStockClassificationCannotBorrowAnOrdinaryOrMalformedReservation(string fault)
    {
        var claim = new MaterialReservation
        {
            ReservationId = CropSaleStockReservationContract.ReservationId("producer", 0), SourceDecisionId = "producer",
            GoalId = "earn_money", OwnerPlayerId = 123, NodeId = "player:123", SlotIndex = 0, Quantity = 1,
            Purpose = CropSaleStockReservationContract.Purpose(Id, "wave", new string('a', 64))
        };
        Assert.True(CropSaleStockReservationContract.TryRead(claim, out _));
        if (fault == "schema") claim.Purpose = "{\"commitment_id\":\"crop\",\"output_retention_key\":\"wave\",\"inventory_identity_sha256\":\"" + new string('a', 64) + "\"}";
        if (fault == "slot") claim.SlotIndex++;
        if (fault == "owner") claim.NodeId = "player:456";
        if (fault == "purpose") claim.Purpose = "ordinary crop material";
        Assert.False(CropSaleStockReservationContract.TryRead(claim, out _));
    }
}
