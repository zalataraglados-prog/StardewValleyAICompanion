using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void PreparedTransferRetainsOnlyTwoInventoriesAndAtomicallyRebindsEveryAffectedGoal()
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var ledger = fixture.PreparedLedger;
        var prepared = ledger.PreparedMaterialTransfer!;
        var original = JsonSerializer.Serialize(ledger);
        Assert.Equal(3, ledger.Revision);
        Assert.Equal(2, prepared.BeforeInventoryGraph.InventoryNodes.Length);
        Assert.Single(prepared.BeforeInventoryGraph.AccessPoints);
        Assert.Empty(prepared.BeforeInventoryGraph.QuantityRows);
        Assert.Equal(3, prepared.ReservationProjection.ReservationTransitions.Length);
        Assert.Equal(ledger.Revision, prepared.ExpectedLedgerRevision);
        Assert.Equal(64, prepared.BindingSha256.Length);
        fixture.PreparationRequest.MaterialTransferPreparation!.Intent.Quantity = 99;
        Assert.Equal(2, prepared.Intent.Quantity);
        fixture.PreparationRequest.MaterialTransferPreparation.Intent.Quantity = 2;

        var result = fixture.Service.SettleSupportingTransition(ledger, fixture.After, fixture.Settlement(), "settled");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(4, result.Ledger!.Revision);
        Assert.Null(result.Ledger.PreparedMaterialTransfer);
        Assert.Equal(3, result.MaterialTransferReservations!.ReservationTransitions.Length);
        Assert.Empty(result.CompletedMaterialReservationIds);
        Assert.Equal(0, result.ConsumedQuantity);
        foreach (var previous in ledger.MaterialReservations)
        {
            var current = result.Ledger.MaterialReservations.Single(row => row.ReservationId == previous.ReservationId);
            if (current.ReservationId == "outside")
            {
                Assert.Equal(JsonSerializer.Serialize(previous), JsonSerializer.Serialize(current));
                continue;
            }
            Assert.Equal(previous.Revision + 1, current.Revision);
            Assert.Equal(previous.GoalId, current.GoalId);
            Assert.Equal(previous.SourceDecisionId, current.SourceDecisionId);
            Assert.Equal(previous.Quantity, current.Quantity);
            Assert.Equal(previous.Purpose, current.Purpose);
            Assert.Equal(fixture.After.StateHash, current.SourceStateHash);
            Assert.Equal(current.ReservationId == "moved" ? "player:123" : "chest:Farm:4,5", current.NodeId);
            Assert.Equal(current.ReservationId == "stone" ? 1 : 0, current.SlotIndex);
        }
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
        var history = result.Ledger.History.Where(row => row.LedgerRevision == 4).ToArray();
        Assert.Equal(5, history.Length);
        Assert.Equal(2, history.Count(row => row.Operation == "material_reservation_rebound_after_native_transfer" && row.SourceDecisionId == "other:decision"));
        Assert.DoesNotContain(history, row => row.Operation == "reservation_portfolio_route_complete");
    }

    [Theory]
    [InlineData("omitted-binding")]
    [InlineData("other-marker-omitted-binding")]
    [InlineData("wrong-binding")]
    [InlineData("wrong-goal")]
    [InlineData("wrong-route")]
    [InlineData("wrong-portfolio")]
    [InlineData("partial-move")]
    [InlineData("ledger-version")]
    [InlineData("claim-metadata")]
    [InlineData("prepared-intent")]
    [InlineData("prepared-map")]
    [InlineData("prepared-history")]
    [InlineData("forged-rehashed-map")]
    [InlineData("source-stock")]
    [InlineData("unrelated-stock")]
    [InlineData("unrelated-slot")]
    [InlineData("outside-stock-late-rollback")]
    [InlineData("native-owner")]
    [InlineData("stale-tick")]
    [InlineData("actor")]
    public void PreparedTransferRejectionCannotPartiallyMoveAnyReservation(string failure)
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var ledger = fixture.PreparedLedger;
        var request = fixture.Settlement();
        var after = fixture.After;
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        switch (failure)
        {
            case "omitted-binding": request.PreparedMaterialTransferSha256 = null; break;
            case "other-marker-omitted-binding":
                request.PreparedMaterialTransferSha256 = null;
                request.PortfolioId = "support:other";
                ledger.History = ledger.History.Append(new StardewAI.Contracts.Strategy.StrategyCommitmentHistoryEntry
                {
                    CommitmentId = request.PortfolioId, SourceDecisionId = request.PortfolioId,
                    Operation = "reservation_portfolio_commit", LedgerRevision = 2
                }).ToArray();
                break;
            case "wrong-binding": request.PreparedMaterialTransferSha256 = new string('b', 64); break;
            case "wrong-goal": request.GoalId = "other.goal"; break;
            case "wrong-route": request.RouteSourceDecisionId = "other:decision"; break;
            case "wrong-portfolio": request.PortfolioId = "other:portfolio"; break;
            case "partial-move": request.MaterialRelocations[0].Quantity = 1; break;
            case "ledger-version": ledger.Revision++; request.ExpectedLedgerRevision++; break;
            case "claim-metadata": ledger.MaterialReservations.Single(row => row.ReservationId == "stone").GoalId = "hijacked"; break;
            case "prepared-intent": ledger.PreparedMaterialTransfer!.Intent.Quantity++; break;
            case "prepared-map": ledger.PreparedMaterialTransfer!.ReservationProjection.ReservationTransitions = Array.Empty<StardewAI.Contracts.Execution.MaterialTransferReservationTransition>(); break;
            case "prepared-history": ledger.History = ledger.History.Where(row => row.Operation != "material_transfer_prepared").ToArray(); break;
            case "forged-rehashed-map":
                ledger.PreparedMaterialTransfer!.ReservationProjection.ReservationTransitions = ledger.PreparedMaterialTransfer.ReservationProjection.ReservationTransitions.Take(1).ToArray();
                var hash = PreparedMaterialTransferContract.ComputeBindingSha256(ledger.PreparedMaterialTransfer);
                ledger.PreparedMaterialTransfer.BindingSha256 = hash;
                ledger.History.Single(row => row.Operation == "material_transfer_prepared").Reason = hash;
                request.PreparedMaterialTransferSha256 = hash;
                break;
            case "source-stock": graph!.InventoryNodes[1].Slots[0].Stack++; break;
            case "unrelated-stock": graph!.InventoryNodes[1].Slots[1].Stack--; break;
            case "unrelated-slot": graph!.InventoryNodes[1].Slots[1].SlotIndex = 9; break;
            case "outside-stock-late-rollback": graph!.InventoryNodes[2].Slots[0].Stack = 1; break;
            case "native-owner": graph!.InventoryNodes[1].OwnerPlayerId = 99; break;
            case "stale-tick": after.GameTick = 99; break;
            case "actor": after.SaveId.Value = "other"; break;
        }
        if (failure is "source-stock" or "unrelated-stock" or "unrelated-slot" or "outside-stock-late-rollback" or "native-owner")
            after = PreparedMaterialTransferTestFixture.WithGraph(graph!, 200);
        request.StateHash = after.StateHash;
        var original = JsonSerializer.Serialize(ledger);

        var result = fixture.Service.SettleSupportingTransition(ledger, after, request, "rejected");

        Assert.False(result.Accepted);
        Assert.NotEmpty(result.Errors);
        Assert.Same(ledger, result.Ledger);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
        Assert.NotNull(ledger.PreparedMaterialTransfer);
        Assert.Null(result.MaterialTransferReservations);
        if (failure == "other-marker-omitted-binding")
            Assert.Contains("prepared_material_transfer_binding_required", result.Errors);
    }

    [Theory]
    [InlineData("partial-claim")]
    [InlineData("marker-source")]
    public void FailedOrDuplicatePreparationCannotPublishOrOverwriteAPreparedLedger(string failure)
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var original = JsonSerializer.Serialize(fixture.BaseLedger);
        var preparation = fixture.PreparationRequest.MaterialTransferPreparation!;
        if (failure == "partial-claim")
            preparation.MaterialRelocations[0].Quantity = 1;
        else
            fixture.PreparationRequest.SourceDecisionId = "not-the-support-marker";
        var rejected = fixture.Service.Commit(fixture.BaseLedger, fixture.Before, fixture.PreparationRequest, "invalid");
        Assert.False(rejected.Accepted);
        Assert.Same(fixture.BaseLedger, rejected.Ledger);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.BaseLedger));
        preparation.MaterialRelocations[0].Quantity = 2;
        fixture.PreparationRequest.PortfolioId = fixture.PreparationRequest.SourceDecisionId = "support:second";
        fixture.PreparationRequest.ExpectedLedgerRevision = fixture.PreparedLedger.Revision;
        var preparedOriginal = JsonSerializer.Serialize(fixture.PreparedLedger);
        var duplicate = fixture.Service.Commit(fixture.PreparedLedger, fixture.Before, fixture.PreparationRequest, "second");
        Assert.False(duplicate.Accepted);
        Assert.Contains("material_transfer_preparation_already_pending", duplicate.Errors);
        Assert.Equal(preparedOriginal, JsonSerializer.Serialize(fixture.PreparedLedger));
    }

    [Fact]
    public void LegacyWireDoesNotAcquirePreparationOrProjectionFields()
    {
        Assert.False(JsonSerializer.SerializeToNode(new StardewAI.Contracts.Strategy.ReservationPortfolioCommitRequest())!.AsObject().ContainsKey("material_transfer_preparation"));
        Assert.False(JsonSerializer.SerializeToNode(new StardewAI.Contracts.Strategy.StrategyCommitmentLedger())!.AsObject().ContainsKey("prepared_material_transfer"));
        Assert.False(JsonSerializer.SerializeToNode(new StardewAI.Contracts.Strategy.ReservationPortfolioSupportingTransitionSettlementRequest())!.AsObject().ContainsKey("prepared_material_transfer_sha256"));
        Assert.False(JsonSerializer.SerializeToNode(new StardewAI.Contracts.Strategy.ReservationPortfolioSupportingTransitionSettlementResult())!.AsObject().ContainsKey("material_transfer_reservations"));
    }

    [Fact]
    public void PreparationKeepsNormalPortfolioBudgetClaimsAndTransferOnlyRebindsTheirIdentity()
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        fixture.PreparationRequest.CurrencyClaims = new[] { new CurrencyReservationUpsertRequest
        {
            StateHash = fixture.Before.StateHash, ExpectedLedgerRevision = 2, ReservationId = "planned-budget",
            GoalId = "goal.grandpa_21", SourceDecisionId = "route:a", CurrencyId = NativeShopCurrencies.Money,
            Amount = 100, Purpose = "later route budget"
        } };
        var prepared = fixture.Service.Commit(fixture.BaseLedger, fixture.Before, fixture.PreparationRequest, "with-budget");
        Assert.True(prepared.Accepted, string.Join(";", prepared.Errors));
        var request = fixture.Settlement();
        request.PreparedMaterialTransferSha256 = prepared.Ledger!.PreparedMaterialTransfer!.BindingSha256;
        request.RebindActiveReservationIds = new[] { "planned-budget" };

        var settled = fixture.Service.SettleSupportingTransition(prepared.Ledger, fixture.After, request, "rebound-budget");

        Assert.True(settled.Accepted, string.Join(";", settled.Errors));
        var budget = Assert.Single(settled.Ledger!.CurrencyReservations);
        Assert.Equal(100, budget.Amount);
        Assert.Equal(2, budget.Revision);
        Assert.Equal(fixture.After.StateHash, budget.SourceStateHash);
        Assert.Empty(settled.CurrencySettlements);
        Assert.Null(settled.Ledger.PreparedMaterialTransfer);
    }
}
