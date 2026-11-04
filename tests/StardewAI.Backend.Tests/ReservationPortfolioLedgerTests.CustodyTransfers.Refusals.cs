using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustodyPositionHistoryDoesNotPermitUnpreparedLegacyRelocationOrRebind(bool relocate)
    {
        var fixture = new CustodyTransferFixture();
        fixture.Transfer(false);
        fixture.Transfer(true);
        var custody = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
        var marker = fixture.Service.Commit(fixture.Ledger, fixture.Snapshot, new()
        {
            StateHash = fixture.Snapshot.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            PortfolioId = "bypass-marker", SourceDecisionId = "bypass-marker", GoalId = custody.GoalId
        }, "marker");
        Assert.True(marker.Accepted);
        var request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = fixture.Snapshot.StateHash, ExpectedLedgerRevision = marker.Ledger!.Revision,
            PortfolioId = "bypass-marker", RouteSourceDecisionId = custody.SourceDecisionId, GoalId = custody.GoalId,
            SupportingTransitionReceiptSha256 = new string('e', 64), Reason = "unprepared"
        };
        if (relocate) request.MaterialRelocations = new[] { new ReservationPortfolioMaterialRelocation
        {
            MaterialReservationId = custody.ReservationId, SourceNodeId = custody.NodeId, SourceSlotIndex = custody.SlotIndex,
            DestinationNodeId = CustodyTransferFixture.Chest, DestinationSlotIndex = 1, QualifiedItemId = custody.QualifiedItemId, Quantity = 1
        } };
        else request.RebindActiveReservationIds = new[] { custody.ReservationId };
        var before = JsonSerializer.Serialize(marker.Ledger);
        var result = fixture.Service.SettleSupportingTransition(marker.Ledger, fixture.Snapshot, request, "refused");
        Assert.False(result.Accepted);
        Assert.Contains(relocate ? "output_custody_relocation_requires_retained_terminal_lifecycle" :
            "output_custody_rebind_requires_retained_terminal_lifecycle", result.Errors);
        Assert.Equal(before, JsonSerializer.Serialize(result.Ledger));
    }

    [Theory]
    [InlineData("missing-link")]
    [InlineData("duplicate-link")]
    [InlineData("future-link")]
    [InlineData("wrong-previous")]
    [InlineData("current-slot")]
    [InlineData("current-state")]
    [InlineData("current-revision")]
    [InlineData("goal")]
    [InlineData("owner")]
    [InlineData("stripped-origin")]
    [InlineData("stripped-all-new-fields")]
    [InlineData("rehashed-wrong-from")]
    [InlineData("rehashed-fake-after")]
    [InlineData("missing-prepared-marker")]
    [InlineData("missing-preparation-complete")]
    [InlineData("missing-native-receipt-marker")]
    [InlineData("wrong-native-receipt")]
    [InlineData("wrong-prepared-source")]
    public void CustodyPositionChainRejectsBrokenOrRehashedAuthority(string mutation)
    {
        var fixture = new CustodyTransferFixture();
        fixture.Transfer(false);
        fixture.Transfer(true);
        var ledger = fixture.Ledger;
        var row = ledger.MaterialReservations.Single(value => value.ReservationId == fixture.CustodyId);
        Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(row, ledger, out _));
        var links = ledger.History.Where(value => value.OutputCustodyPositionTransition is not null).ToArray();
        var latest = links[^1];
        var proof = latest.OutputCustodyPositionTransition!;
        switch (mutation)
        {
            case "missing-link": ledger.History = ledger.History.Where(value => !ReferenceEquals(value, links[0])).ToArray(); break;
            case "duplicate-link": ledger.History = ledger.History.Append(links[0]).ToArray(); break;
            case "future-link": latest.LedgerRevision = ledger.Revision + 1; break;
            case "wrong-previous": proof.PreviousPositionSha256 = new string('0', 64); break;
            case "current-slot": row.SlotIndex++; break;
            case "current-state": row.SourceStateHash = "unverified-current-state"; break;
            case "current-revision": row.Revision++; break;
            case "goal": row.GoalId = "other.goal"; break;
            case "owner": row.OwnerPlayerId++; break;
            case "stripped-origin": row.OutputCustody!.InitialPosition = null; break;
            case "stripped-all-new-fields":
                row.OutputCustody!.InitialPosition = null;
                foreach (var entry in ledger.History) { entry.OutputCustodyInitialPositionSha256 = null; entry.OutputCustodyPositionTransition = null; }
                break;
            case "rehashed-wrong-from": proof.BeforeSlotIndex++; break;
            case "rehashed-fake-after": proof.AfterStateHash = "fake-after"; latest.SourceStateHash = "fake-after"; row.SourceStateHash = "fake-after"; break;
            case "missing-prepared-marker": ledger.History = ledger.History.Where(value => value.Operation != "material_transfer_prepared").ToArray(); break;
            case "missing-preparation-complete": ledger.History = ledger.History.Where(value => value.Operation != "material_transfer_preparation_completed").ToArray(); break;
            case "missing-native-receipt-marker": ledger.History = ledger.History.Where(value => value.Operation != "reservation_portfolio_supporting_transition_complete").ToArray(); break;
            case "wrong-native-receipt": proof.SupportingTransitionReceiptSha256 = new string('0', 64); break;
            case "wrong-prepared-source": proof.PreparedSourceStateHash = "fake-source"; break;
        }
        if (mutation.StartsWith("rehashed-", StringComparison.Ordinal) || mutation is "wrong-native-receipt" or "wrong-prepared-source")
            proof.BindingSha256 = MaterialOutputCustodyContract.ComputePositionTransitionSha256(ledger, latest, proof);
        var before = JsonSerializer.Serialize(ledger);
        Assert.False(MaterialOutputCustodyContract.TryGetPositionBinding(row, ledger, out _));
        Assert.False(MaterialOutputCustodyContract.IsAuthorized(row, ledger, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.Equal(before, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData("missing-unit")]
    [InlineData("quality")]
    [InlineData("passive-slot")]
    public void FailedCustodyNativeInventoryCheckRetainsTheExactPreparedLedger(string failure)
    {
        var fixture = new CustodyTransferFixture();
        var prepared = fixture.Prepare(false);
        var after = prepared.After;
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        var chest = graph!.InventoryNodes.Single(row => row.NodeId == CustodyTransferFixture.Chest);
        if (failure == "missing-unit") chest.Slots.Single(row => row.QualifiedItemId == "(O)96").Stack++;
        if (failure == "quality") chest.Slots.Single(row => row.QualifiedItemId == "(O)96").Quality++;
        if (failure == "passive-slot") chest.Slots.Single(row => row.QualifiedItemId == "(O)390").SlotIndex = 9;
        var farm = System.Text.Json.Nodes.JsonNode.Parse(after.State["farm"].GetRawText())!;
        farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph);
        after.State["farm"] = JsonSerializer.SerializeToElement(farm);
        after.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(after.State);
        prepared.Request.StateHash = after.StateHash;
        var original = JsonSerializer.Serialize(prepared.Before);
        var result = fixture.Service.SettleSupportingTransition(prepared.Before, after, prepared.Request, "refused");
        Assert.False(result.Accepted);
        Assert.Same(prepared.Before, result.Ledger);
        Assert.Equal(original, JsonSerializer.Serialize(prepared.Before));
        Assert.NotNull(result.Ledger!.PreparedMaterialTransfer);
        Assert.DoesNotContain(result.Ledger.History, row => row.OutputCustodyPositionTransition is not null);
    }
}
