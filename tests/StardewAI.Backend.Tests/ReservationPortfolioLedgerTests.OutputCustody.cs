using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void AcquisitionSettlementAtomicallyRetainsOutputAtTheSameRevision()
    {
        var fixture = MintMuseumCustody();
        Assert.Equal(2, fixture.Ledger.Revision);
        var output = Assert.Single(fixture.Ledger.MaterialReservations.Where(row => row.OutputCustody is not null));
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(output, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer, "goal.grandpa_21"));
        Assert.All(fixture.Ledger.MaterialReservations.Where(row => row.OutputCustody is null), row => Assert.Equal(StrategyCommitmentStatuses.Completed, row.Status));
        Assert.All(fixture.Ledger.History.Where(row => row.LedgerRevision > 1), row => Assert.Equal(2, row.LedgerRevision));
        Assert.True(MaterialInventoryGraphReader.TryRead(fixture.Snapshot, out var graph));
        Assert.Equal(1, new MaterialSupplyProjection().Project(graph!, fixture.Ledger.MaterialReservations)
            .Slots.Single(row => row.SlotIndex == 1).AvailableQuantity);
    }

    [Theory]
    [InlineData("overbook")]
    [InlineData("foreign-node")]
    [InlineData("wrong-item")]
    [InlineData("unknown-consumer")]
    [InlineData("duplicate")]
    public void FailedOutputCustodyCannotPartiallyCompleteInputClaims(string failure)
    {
        var snapshot = MuseumCustodySnapshot();
        var service = new ReservationPortfolioLedgerService();
        var ledger = service.Commit(null, snapshot, Request(snapshot, 20, 300), "initial").Ledger!;
        var original = JsonSerializer.Serialize(ledger);
        var request = SettlementRequest(snapshot, 1);
        var claim = MuseumOutputClaim();
        if (failure == "overbook") claim.SlotIndex = 25;
        if (failure == "foreign-node") claim.NodeId = "player:999";
        if (failure == "wrong-item") claim.QualifiedItemId = "(O)388";
        if (failure == "unknown-consumer") claim.ConsumerOptionId = "executor.ship_inventory_item_to_bin";
        request.OutputCustodyClaims = failure == "duplicate" ? new[] { claim, claim } : new[] { claim };
        var result = service.SettleCompletedRoute(ledger, snapshot, request, "failed");
        Assert.False(result.Accepted);
        Assert.Same(ledger, result.Ledger);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Fact]
    public void CustodyCannotBeReleasedOrReassignedThroughOrdinaryMaterialWrites()
    {
        var fixture = MintMuseumCustody();
        var output = fixture.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
        var service = new MaterialReservationLedgerService();
        var cancel = service.Cancel(fixture.Ledger, fixture.Snapshot, output.ReservationId, new()
        {
            StateHash = fixture.Snapshot.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision, Reason = "bypass"
        }, "cancel");
        Assert.False(cancel.Accepted);
        var overwrite = service.Upsert(fixture.Ledger, fixture.Snapshot, new()
        {
            StateHash = fixture.Snapshot.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            ReservationId = output.ReservationId, SourceDecisionId = "other", GoalId = "other", NodeId = output.NodeId,
            SlotIndex = output.SlotIndex, QualifiedItemId = output.QualifiedItemId, Quantity = 1, Purpose = "bypass"
        }, "overwrite");
        Assert.False(overwrite.Accepted);
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(output, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
    }

    [Fact]
    public void DonationConsumesOnlyItsCustodyWhileProtectingTheOtherUnit()
    {
        var fixture = MintMuseumCustody(protectSpare: true);
        var item = CustodyDonationItem(fixture);
        Assert.Equal("pending", item.Status);
        var output = fixture.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
        Assert.Contains(item.NormalizedCommand.Parameters, row => row.Name == "museum_output_custody_reservation_id" && row.Value == output.ReservationId);
        var readiness = new ActionQueueDispatchReadinessService().Evaluate(new(), item, fixture.Ledger, fixture.Snapshot.StateHash);
        Assert.True(readiness.Ready, string.Join(";", readiness.BlockingReasons));
        var after = AfterCustodyDonation(fixture.Snapshot);
        var build = MuseumDonationCustodySettlementRequestBuilder.Build(item, CustodyReceipt(item, fixture.Snapshot, after),
            fixture.Snapshot, after, fixture.Ledger);
        Assert.NotNull(build.Request);
        Assert.Empty(build.Errors);
        var result = fixture.Service.SettleSupportingTransition(fixture.Ledger, after, build.Request!, "native-donation");
        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(StrategyCommitmentStatuses.Completed, result.Ledger!.MaterialReservations.Single(row => row.ReservationId == output.ReservationId).Status);
        Assert.Equal(StrategyCommitmentStatuses.Active, result.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-goal-spare").Status);
        Assert.Equal(1, result.Ledger.Revision - fixture.Ledger.Revision);
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        Assert.Equal("available", new MaterialSupplyProjection().Project(graph!, result.Ledger.MaterialReservations).Status);
    }

    [Theory]
    [InlineData("goal")]
    [InlineData("marker")]
    [InlineData("revision")]
    [InlineData("consumer")]
    [InlineData("missing-binding")]
    public void CustodyDonationRejectsForeignOrTamperedAuthority(string failure)
    {
        var fixture = MintMuseumCustody(protectSpare: true);
        var item = CustodyDonationItem(fixture);
        var output = fixture.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
        if (failure == "goal") item.NormalizedCommand.GoalId = "other.goal";
        if (failure == "marker") fixture.Ledger.History = fixture.Ledger.History.Where(row => row.Operation != MaterialOutputCustodyContract.CreatedOperation).ToArray();
        if (failure == "revision") output.Revision++;
        if (failure == "consumer") output.OutputCustody!.ConsumerOptionId = "executor.load_machine_input";
        if (failure == "missing-binding") item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters
            .Where(row => !row.Name.StartsWith("museum_output_custody_", StringComparison.Ordinal)).ToArray();
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(new(), item, fixture.Ledger, fixture.Snapshot.StateHash).Ready);
    }

    [Fact]
    public void SameGoalDoesNotAuthorizeAnOrdinaryReservationAndWrongGoalCannotCompileCustody()
    {
        var fixture = MintMuseumCustody(protectSpare: true);
        Assert.Equal("blocked", CustodyDonationItem(fixture, "other.goal").Status);
        fixture.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null).OutputCustody = null;
        var availability = new StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot, new[] { "museum.donate_items" }, true, fixture.Ledger);
        Assert.False(Assert.Single(Assert.Single(availability.Options).EventCandidates).Available);
    }

    [Theory]
    [InlineData("failed-receipt")]
    [InlineData("stale")]
    [InlineData("same-progress")]
    [InlineData("same-inventory")]
    [InlineData("actor")]
    public void CustodySettlementRequiresTheNativeDonationTransition(string failure)
    {
        var fixture = MintMuseumCustody();
        var item = CustodyDonationItem(fixture);
        var after = AfterCustodyDonation(fixture.Snapshot);
        var receipt = CustodyReceipt(item, fixture.Snapshot, after);
        if (failure == "failed-receipt") receipt.Status = "blocked";
        if (failure == "stale") receipt.AfterSnapshotFresh = false;
        if (failure == "same-progress") after.State["world_progress"] = fixture.Snapshot.State["world_progress"];
        if (failure == "same-inventory") after.State["player"] = fixture.Snapshot.State["player"];
        if (failure == "actor") after.State["identity"] = JsonSerializer.SerializeToElement(new { save_id = new { value = "other", status = "available" } });
        var build = MuseumDonationCustodySettlementRequestBuilder.Build(item, receipt, fixture.Snapshot, after, fixture.Ledger);
        Assert.Null(build.Request);
        Assert.NotEmpty(build.Errors);
        Assert.Equal(StrategyCommitmentStatuses.Active, fixture.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null).Status);
    }
}
