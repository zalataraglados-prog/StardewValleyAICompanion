using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void StoredCustodyCannotBeConsumedWhenAnUnreservedDuplicateWasDonated()
    {
        var (fixture, _, after, _, stored) = StoredCustodyDonationAttempt();
        var original = JsonSerializer.Serialize(fixture.Ledger);
        var result = fixture.Service.SettleSupportingTransition(fixture.Ledger, after, new()
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            PortfolioId = stored.SourceDecisionId, RouteSourceDecisionId = stored.SourceDecisionId,
            GoalId = stored.GoalId, ConsumerOptionId = MaterialOutputCustodyContract.MuseumConsumer,
            SupportingTransitionReceiptSha256 = new string('f', 64),
            Reason = "native_museum_donation_consumed_retained_output",
            MaterialConsumptions = new[] { new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = stored.ReservationId, NodeId = stored.NodeId, SlotIndex = stored.SlotIndex,
                QualifiedItemId = stored.QualifiedItemId, ConsumedQuantity = 1
            } }
        }, "duplicate-donated");
        Assert.False(result.Accepted);
        Assert.Contains("output_custody_consumer_not_authorized", result.Errors);
        Assert.Equal(original, JsonSerializer.Serialize(result.Ledger));
        Assert.Equal(StrategyCommitmentStatuses.Active, stored.Status);
    }

    [Fact]
    public void DonationBuilderCannotAttachStoredCustodyToARealPlayerInventoryDecrement()
    {
        var (fixture, before, after, item, stored) = StoredCustodyDonationAttempt();
        Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(stored, fixture.Ledger, out _));
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(stored, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.False(MaterialOutputCustodyContract.IsAuthorizedForConsumption(stored, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
        var original = JsonSerializer.Serialize(fixture.Ledger);
        var built = MuseumDonationCustodySettlementRequestBuilder.Build(item, CustodyReceipt(item, before, after),
            before, after, fixture.Ledger);
        Assert.Null(built.Request);
        Assert.Equal(new[] { "museum_custody_settlement_authorization_invalid" }, built.Errors);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Ledger));
    }

    private static (CustodyTransferFixture Fixture, SnapshotEnvelope Before, SnapshotEnvelope After,
        ActionQueueItem Item, MaterialReservation Stored) StoredCustodyDonationAttempt()
    {
        var fixture = new CustodyTransferFixture(protectSpare: false);
        fixture.Transfer(withdraw: false);
        var stored = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        Assert.Equal(CustodyTransferFixture.Chest, stored.NodeId);
        var before = fixture.AtMuseum();
        var world = JsonSerializer.SerializeToNode(before.State["world_progress"])!;
        var candidate = world["museum"]!["value"]!["donation_candidates"]![0]!;
        candidate["stack_before"] = 1;
        candidate["stack_after"] = 0;
        before.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        before.StateHash = SnapshotHash.ComputeStateHash(before.State);
        var item = CustodyDonationItem(new CustodyFixture(before, fixture.Ledger, fixture.Service));
        Assert.Equal("pending", item.Status);
        Assert.DoesNotContain(item.NormalizedCommand.Parameters, row => row.Name == "museum_output_custody_reservation_id");
        // Forge only the lease annotation; the duplicate's compiled native donation remains otherwise valid.
        item.NormalizedCommand.GoalId = stored.GoalId;
        item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Concat(new[]
        {
            new SmallModelActionParameter { Name = "museum_output_custody_reservation_id", Value = stored.ReservationId },
            new SmallModelActionParameter { Name = "museum_output_custody_reservation_revision", Value = stored.Revision.ToString() }
        }).ToArray();
        var after = AfterCustodyDonation(before);
        var player = JsonSerializer.SerializeToNode(after.State["player"])!;
        player["inventory"]!["value"] = new JsonArray();
        after.State["player"] = JsonSerializer.SerializeToElement(player);
        var farm = JsonSerializer.SerializeToNode(after.State["farm"])!;
        farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray().RemoveAt(1);
        after.State["farm"] = JsonSerializer.SerializeToElement(farm);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        Assert.Equal(1, graph!.InventoryNodes.Single(row => row.NodeId == CustodyTransferFixture.Chest)
            .Slots.Single(row => row.QualifiedItemId == stored.QualifiedItemId).Stack);
        return (fixture, before, after, item, stored);
    }
}
