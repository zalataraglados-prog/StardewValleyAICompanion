using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed partial class MaterialTransferProjectorTests
{
    [Fact]
    public void CustodyDepositIncludesOtherGoalsCompactionWithoutChangingAnyClaimMetadata()
    {
        var (graph, intent, ledger, moves) = CustodyTransferProjectionFixture();
        var originalGraph = JsonSerializer.Serialize(graph);
        var originalLedger = JsonSerializer.Serialize(ledger);
        var originalMoves = JsonSerializer.Serialize(moves);

        var result = new MaterialTransferProjector().ProjectReservations(graph, intent, ledger, moves);

        Assert.Equal("projected", result.Status);
        Assert.Empty(result.BlockingReasons);
        Assert.Equal(ledger.LedgerId, result.LedgerId);
        Assert.Equal(ledger.Revision, result.ExpectedLedgerRevision);
        Assert.Equal(4, result.ReservationTransitions.Length);
        Assert.Equal(new[] { "chest:Farm:4,5", "player:1" }, result.ReservationTransitions
            .Select(row => row.SourceNodeId).Distinct().Order(StringComparer.Ordinal));
        var custody = Assert.Single(ledger.MaterialReservations, row => row.OutputCustody is not null);
        var moved = Assert.Single(result.ReservationTransitions, row => row.ReservationId == custody.ReservationId);
        Assert.Equal("chest:Farm:4,5", moved.DestinationNodeId);
        Assert.Equal(2, moved.DestinationSlotIndex);
        Assert.Equal(custody.Revision, moved.ExpectedReservationRevision);
        Assert.Equal(1, moved.Quantity);
        Assert.Equal(0, result.ReservationTransitions.Single(row => row.ReservationId == "wood-other-goal").DestinationSlotIndex);
        Assert.Equal(1, result.ReservationTransitions.Single(row => row.ReservationId == "stone-other-goal").DestinationSlotIndex);
        Assert.Equal(2, result.ReservationTransitions.Single(row => row.ReservationId == "same-stack-other-goal").DestinationSlotIndex);
        Assert.DoesNotContain(result.ReservationTransitions, row => row.ReservationId == "outside-transfer");
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(custody, ledger, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.Equal(originalGraph, JsonSerializer.Serialize(graph));
        Assert.Equal(originalLedger, JsonSerializer.Serialize(ledger));
        Assert.Equal(originalMoves, JsonSerializer.Serialize(moves));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    public void WithdrawalRebindsAllRetainedSourceClaimsIncludingAfterFullStackRemoval(int quantity)
    {
        var graph = ReservationGraph(Node("player:1", "player_inventory", 12, "Farm"),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(3, 4, "(O)388"), Slot(7, 12), OtherSlot(9, 1, "(O)330")));
        var claims = new List<MaterialReservation>
        {
            ReservationClaim("moved", "chest:Farm:4,5", 7, quantity),
            ReservationClaim("wood", "chest:Farm:4,5", 3, 4, "(O)388"),
            ReservationClaim("clay", "chest:Farm:4,5", 9, 1, "(O)330")
        };
        if (quantity < 12) claims.Add(ReservationClaim("retained", "chest:Farm:4,5", 7, 12 - quantity));
        var ledger = ReservationLedger(claims.ToArray());
        var moves = new[] { ReservationMove(claims[0], "player:1", 0) };

        var result = new MaterialTransferProjector().ProjectReservations(graph, Withdrawal(quantity), ledger, moves);

        Assert.Equal("projected", result.Status);
        Assert.Equal(claims.Count, result.ReservationTransitions.Length);
        Assert.Equal(0, result.ReservationTransitions.Single(row => row.ReservationId == "wood").DestinationSlotIndex);
        Assert.Equal(quantity == 12 ? 1 : 2, result.ReservationTransitions.Single(row => row.ReservationId == "clay").DestinationSlotIndex);
        Assert.Equal("player:1", result.ReservationTransitions.Single(row => row.ReservationId == "moved").DestinationNodeId);
        if (quantity < 12)
            Assert.Equal(1, result.ReservationTransitions.Single(row => row.ReservationId == "retained").DestinationSlotIndex);
    }

    [Fact]
    public void ExplicitWholeClaimsCanFollowDifferentNativeDestinationContributions()
    {
        var graph = ReservationGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(5, 7, "(O)388"), Slot(9, 995)));
        var first = ReservationClaim("first", "player:1", 2, 4);
        var second = ReservationClaim("second", "player:1", 2, 6);
        var existing = ReservationClaim("existing", "chest:Farm:4,5", 9, 995);
        var retained = ReservationClaim("retained", "player:1", 2, 30);
        var ledger = ReservationLedger(first, second, existing, retained);
        var moves = new[] { ReservationMove(first, "chest:Farm:4,5", 1), ReservationMove(second, "chest:Farm:4,5", 2) };

        var result = new MaterialTransferProjector().ProjectReservations(graph, Intent(10), ledger, moves);

        Assert.Equal("projected", result.Status);
        Assert.Equal(1, result.ReservationTransitions.Single(row => row.ReservationId == "first").DestinationSlotIndex);
        Assert.Equal(2, result.ReservationTransitions.Single(row => row.ReservationId == "second").DestinationSlotIndex);
        Assert.Equal(1, result.ReservationTransitions.Single(row => row.ReservationId == "existing").DestinationSlotIndex);
        Assert.Equal(2, result.ReservationTransitions.Single(row => row.ReservationId == "retained").DestinationSlotIndex);
    }

    [Fact]
    public void FreeStockTransferCanCompactClaimsWithoutMovingTheirReservedUnits()
    {
        var graph = ReservationGraph(Node("player:1", "player_inventory", 12, "Farm"),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(7, 12), OtherSlot(9, 1, "(O)330")));
        var ledger = ReservationLedger(ReservationClaim("retained", "chest:Farm:4,5", 7, 7),
            ReservationClaim("clay", "chest:Farm:4,5", 9, 1, "(O)330"));

        var result = new MaterialTransferProjector().ProjectReservations(graph, Withdrawal(5), ledger,
            Array.Empty<ReservationPortfolioMaterialRelocation>());

        Assert.Equal("projected", result.Status);
        Assert.Equal(0, result.ReservationTransitions.Single(row => row.ReservationId == "retained").DestinationSlotIndex);
        Assert.All(result.ReservationTransitions, row => Assert.Equal(row.SourceNodeId, row.DestinationNodeId));
    }

    [Theory]
    [InlineData("missing-move")]
    [InlineData("duplicate-move")]
    [InlineData("unknown-reservation")]
    [InlineData("wrong-source-node")]
    [InlineData("wrong-source-slot")]
    [InlineData("wrong-destination-node")]
    [InlineData("wrong-destination-slot")]
    [InlineData("wrong-item")]
    [InlineData("partial-claim")]
    [InlineData("duplicate-claim")]
    [InlineData("foreign-owner")]
    [InlineData("foreign-player")]
    [InlineData("missing-history")]
    [InlineData("custody-receipt")]
    [InlineData("overbooked-source")]
    [InlineData("outside-claim-drift")]
    [InlineData("inactive-move")]
    [InlineData("future-revision")]
    [InlineData("schema")]
    [InlineData("missing-save")]
    [InlineData("missing-goal")]
    public void InvalidReservationProjectionReturnsNoPartialMapAndDoesNotMutateInputs(string failure)
    {
        var (graph, intent, ledger, moves) = CustodyTransferProjectionFixture();
        switch (failure)
        {
            case "missing-move": moves = Array.Empty<ReservationPortfolioMaterialRelocation>(); break;
            case "duplicate-move": moves = new[] { moves[0], moves[0] }; break;
            case "unknown-reservation": moves[0].MaterialReservationId = "absent"; break;
            case "wrong-source-node": moves[0].SourceNodeId = "chest:elsewhere"; break;
            case "wrong-source-slot": moves[0].SourceSlotIndex++; break;
            case "wrong-destination-node": moves[0].DestinationNodeId = "player:other"; break;
            case "wrong-destination-slot": moves[0].DestinationSlotIndex = 0; break;
            case "wrong-item": moves[0].QualifiedItemId = "(O)388"; break;
            case "partial-claim": moves[0].Quantity = 0; break;
            case "duplicate-claim": ledger.MaterialReservations = ledger.MaterialReservations.Append(ledger.MaterialReservations[0]).ToArray(); break;
            case "foreign-owner": ledger.MaterialReservations[0].OwnerPlayerId = 99; break;
            case "foreign-player": ledger.PlayerId = "99"; break;
            case "missing-history": ledger.History = Array.Empty<StrategyCommitmentHistoryEntry>(); break;
            case "custody-receipt": ledger.MaterialReservations[0].OutputCustody!.FreshTerminalReceiptSha256 = "forged"; break;
            case "overbooked-source": ledger.MaterialReservations[1].Quantity = 2; break;
            case "outside-claim-drift": ledger.MaterialReservations.Single(row => row.ReservationId == "outside-transfer").SlotIndex = 99; break;
            case "inactive-move": ledger.MaterialReservations[0].Status = StrategyCommitmentStatuses.Completed; break;
            case "future-revision": ledger.MaterialReservations[0].Revision = ledger.Revision + 1; break;
            case "schema": ledger.SchemaVersion = "unknown"; break;
            case "missing-save": ledger.SaveId = ""; break;
            case "missing-goal": ledger.MaterialReservations[0].GoalId = ""; break;
        }
        var original = JsonSerializer.Serialize(ledger);
        var originalGraph = JsonSerializer.Serialize(graph);

        var result = new MaterialTransferProjector().ProjectReservations(graph, intent, ledger, moves);

        Assert.Equal("blocked", result.Status);
        Assert.NotEmpty(result.BlockingReasons);
        Assert.Empty(result.ReservationTransitions);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
        Assert.Equal(originalGraph, JsonSerializer.Serialize(graph));
    }

    [Fact]
    public void ExistingDestinationStockCannotSubstituteForActualTransferredContribution()
    {
        var graph = ReservationGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(9, 995)));
        var claim = ReservationClaim("move-five", "player:1", 2, 5);
        var ledger = ReservationLedger(claim);

        var result = new MaterialTransferProjector().ProjectReservations(graph, Intent(10), ledger,
            new[] { ReservationMove(claim, "chest:Farm:4,5", 0) });

        Assert.Equal("blocked", result.Status);
        Assert.Contains("material_transfer_reservation_destination_contribution_exceeded", result.BlockingReasons);
        Assert.Empty(result.ReservationTransitions);
    }

    [Fact]
    public void ReservationMapHasStableOrderAndIgnoresInactiveHistoricalClaims()
    {
        var (graph, intent, ledger, moves) = CustodyTransferProjectionFixture();
        var projector = new MaterialTransferProjector();
        var original = projector.ProjectReservations(graph, intent, ledger, moves);
        var inactive = ReservationClaim("completed", "absent-node", 99, 100);
        inactive.Status = StrategyCommitmentStatuses.Completed;
        ledger.MaterialReservations = ledger.MaterialReservations.Reverse().Append(inactive).ToArray();

        var reordered = projector.ProjectReservations(graph, intent, ledger, moves.Reverse().ToArray());

        Assert.Equal("projected", reordered.Status);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(reordered));
        Assert.DoesNotContain(reordered.ReservationTransitions, row => row.ReservationId == inactive.ReservationId);
    }

    [Theory]
    [InlineData(-8577884337094229036L)]
    [InlineData(long.MinValue)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void PlayerIdentityUsesTheExactSignedNativeLongInsteadOfAPositiveCounter(long playerId)
    {
        var (graph, intent, ledger, moves) = CustodyTransferProjectionFixture();
        graph.PlayerId = playerId;
        ledger.PlayerId = playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var node in graph.InventoryNodes) node.OwnerPlayerId = playerId;
        foreach (var claim in ledger.MaterialReservations) claim.OwnerPlayerId = playerId;

        var result = new MaterialTransferProjector().ProjectReservations(graph, intent, ledger, moves);

        Assert.Equal("projected", result.Status);
        Assert.Empty(result.BlockingReasons);
    }
}
