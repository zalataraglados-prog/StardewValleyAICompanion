using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void OtherGoalTransferPassivelyRebindsStoredCustodyWithoutChangingItsOriginOrConsumer()
    {
        var fixture = new CustodyTransferFixture();
        fixture.Transfer(false);
        var custody = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        var origin = JsonSerializer.Serialize(custody.OutputCustody);
        var stone = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-goal-stone");
        var move = new ReservationPortfolioMaterialRelocation
        {
            MaterialReservationId = stone.ReservationId, SourceNodeId = stone.NodeId, SourceSlotIndex = stone.SlotIndex,
            DestinationNodeId = "player:123", DestinationSlotIndex = 2, QualifiedItemId = stone.QualifiedItemId, Quantity = 3
        };
        var commit = fixture.Service.Commit(fixture.Ledger, fixture.Snapshot, new()
        {
            StateHash = fixture.Snapshot.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            PortfolioId = "stone-withdrawal", SourceDecisionId = "stone-withdrawal", GoalId = stone.GoalId,
            MaterialTransferPreparation = new()
            {
                RouteSourceDecisionId = stone.SourceDecisionId, MaterialRelocations = new[] { move },
                Intent = new MaterialTransferIntent { SourceNodeId = stone.NodeId, SourceSlotIndex = stone.SlotIndex,
                    DestinationNodeId = "player:123", QualifiedItemId = stone.QualifiedItemId, Quantity = 3, ExpectedSourceStack = 3 }
            }
        }, "prepare-stone");
        Assert.True(commit.Accepted, string.Join(",", commit.Errors));
        Assert.True(MaterialInventoryGraphReader.TryRead(fixture.Snapshot, out var graph));
        var chest = graph!.InventoryNodes.Single(row => row.NodeId == CustodyTransferFixture.Chest);
        var scroll = chest.Slots.Single(row => row.QualifiedItemId == "(O)96");
        scroll.SlotIndex = 0;
        chest.Slots = new[] { scroll };
        var player = graph.InventoryNodes.Single(row => row.NodeId == "player:123");
        player.Slots = player.Slots.Append(CustodyTransferFixture.Slot(2, "(O)390", 3)).ToArray();
        var after = CustodyTransferFixture.WithGraph(fixture.Snapshot, graph, fixture.Snapshot.GameTick + 100);
        var result = fixture.Service.SettleSupportingTransition(commit.Ledger, after, new()
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = commit.Ledger!.Revision, PortfolioId = "stone-withdrawal",
            GoalId = stone.GoalId, RouteSourceDecisionId = stone.SourceDecisionId,
            MaterialRelocations = new[] { move }, PreparedMaterialTransferSha256 = commit.Ledger.PreparedMaterialTransfer!.BindingSha256,
            SupportingTransitionReceiptSha256 = new string('e', 64), Reason = "verified_supporting_transition_relocation"
        }, "settle-stone");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var movedCustody = result.Ledger!.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        Assert.Equal(CustodyTransferFixture.Chest, movedCustody.NodeId);
        Assert.Equal(0, movedCustody.SlotIndex);
        Assert.Equal(origin, JsonSerializer.Serialize(movedCustody.OutputCustody));
        Assert.Equal(custody.GoalId, movedCustody.GoalId);
        Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(movedCustody, result.Ledger, out _));
        var entry = result.Ledger.History.Single(row => row.CommitmentId == custody.ReservationId && row.LedgerRevision == result.Ledger.Revision);
        Assert.Equal("material_reservation_rebound_after_native_transfer", entry.Operation);
        Assert.Equal(custody.SourceDecisionId, entry.SourceDecisionId);
        Assert.Equal(stone.SourceDecisionId, entry.OutputCustodyPositionTransition!.PreparedRouteSourceDecisionId);
        Assert.NotEqual(entry.SourceDecisionId, entry.OutputCustodyPositionTransition.PreparedRouteSourceDecisionId);
    }

    [Fact]
    public void CustodyRoundTripKeepsOriginAndBothOtherGoalsAndThenUsesOriginalDonationSettlement()
    {
        var fixture = new CustodyTransferFixture();
        var original = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        var origin = JsonSerializer.Serialize(original.OutputCustody);
        var deposit = fixture.Transfer(withdraw: false);
        var stored = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        Assert.Equal(CustodyTransferFixture.Chest, stored.NodeId);
        Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(stored, fixture.Ledger, out var firstBinding));
        Assert.Equal(3, deposit.Result.MaterialTransferReservations!.ReservationTransitions.Length);
        Assert.Equal(0, fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-goal-stone").SlotIndex);
        var withdrawal = fixture.Transfer(withdraw: true);
        var returned = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        Assert.Equal("player:123", returned.NodeId);
        Assert.Equal(1, returned.SlotIndex);
        Assert.Equal(original.Revision + 2, returned.Revision);
        Assert.Equal(origin, JsonSerializer.Serialize(returned.OutputCustody));
        var links = fixture.Ledger.History.Where(row => row.OutputCustodyPositionTransition is not null).ToArray();
        Assert.Equal(2, links.Length);
        Assert.Equal(firstBinding, links[1].OutputCustodyPositionTransition!.PreviousPositionSha256);
        Assert.Null(fixture.Ledger.PreparedMaterialTransfer);
        Assert.Equal(0, fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-goal-stone").SlotIndex);
        Assert.All(fixture.Ledger.MaterialReservations.Where(row => row.ReservationId is "other-goal-stone" or "other-goal-spare"),
            row => Assert.Equal(StrategyCommitmentStatuses.Active, row.Status));

        var beforeDonation = fixture.AtMuseum();
        var donationFixture = new CustodyFixture(beforeDonation, fixture.Ledger, fixture.Service);
        var item = CustodyDonationItem(donationFixture);
        Assert.Equal("pending", item.Status);
        var afterDonation = AfterCustodyDonation(beforeDonation);
        var built = MuseumDonationCustodySettlementRequestBuilder.Build(item, CustodyReceipt(item, beforeDonation, afterDonation),
            beforeDonation, afterDonation, fixture.Ledger);
        Assert.Empty(built.Errors);
        var consumed = fixture.Service.SettleSupportingTransition(fixture.Ledger, afterDonation, built.Request!, "donated");
        Assert.True(consumed.Accepted, string.Join(",", consumed.Errors));
        Assert.Equal(StrategyCommitmentStatuses.Completed, consumed.Ledger!.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId).Status);
        Assert.Equal(StrategyCommitmentStatuses.Active, consumed.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-goal-spare").Status);
        Assert.Equal(withdrawal.Before.Revision + 1, withdrawal.Result.CommittedLedgerRevision);
    }

    [Fact]
    public void CustodyPositionHistorySurvivesFileRestartAndExactSettlementReplyLookup()
    {
        var root = NewPortfolioReplyRoot();
        var fixture = new CustodyTransferFixture(new FileStrategyCommitmentRepository(root));
        var deposit = fixture.Transfer(withdraw: false);
        var path = Assert.Single(Directory.GetFiles(root));
        var bytes = File.ReadAllBytes(path);
        var reopened = new FileStrategyCommitmentRepository(root);
        fixture.Ledger = reopened.Get(fixture.Snapshot);
        var row = fixture.Ledger.MaterialReservations.Single(value => value.ReservationId == fixture.CustodyId);
        Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(row, fixture.Ledger, out _));
        var lookup = reopened.LookupPortfolioMutationReply(fixture.Snapshot, "settle-supporting-transition", ReplyElement(deposit.Request));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.False(lookup["ledger_mutation_performed"]!.GetValue<bool>());
        Assert.Equal(bytes, File.ReadAllBytes(path));
        fixture.Repository = reopened;
        fixture.Transfer(withdraw: true);
        var cloned = fixture.Service.Commit(fixture.Ledger, fixture.Snapshot, new()
        {
            StateHash = fixture.Snapshot.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            PortfolioId = "after-transfer-marker", SourceDecisionId = "after-transfer-marker", GoalId = "other.goal"
        }, "clone");
        Assert.True(cloned.Accepted);
        var original = fixture.Ledger.History.First(value => value.OutputCustodyPositionTransition is not null);
        var copy = cloned.Ledger!.History.First(value => value.OutputCustodyPositionTransition is not null);
        Assert.NotSame(original.OutputCustodyPositionTransition, copy.OutputCustodyPositionTransition);
        copy.OutputCustodyPositionTransition!.AfterSlotIndex++;
        Assert.NotEqual(original.OutputCustodyPositionTransition!.AfterSlotIndex, copy.OutputCustodyPositionTransition.AfterSlotIndex);
    }
}
