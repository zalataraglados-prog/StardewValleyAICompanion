using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed class ReservationPortfolioLedgerTests
{
    [Fact]
    public void CommitAppliesMaterialAndCurrencyClaimsAtOneRevision()
    {
        var snapshot = Snapshot();
        var request = Request(snapshot, materialQuantity: 20, moneyAmount: 300);

        var result = new ReservationPortfolioLedgerService().Commit(
            null,
            snapshot,
            request,
            "2026-09-21T00:00:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(1, result.CommittedLedgerRevision);
        Assert.Equal(1, result.Ledger!.Revision);
        Assert.Single(result.Ledger.MaterialReservations);
        Assert.Single(result.Ledger.CurrencyReservations);
        Assert.Equal(3, result.Ledger.History.Length);
        Assert.All(result.Ledger.History, row =>
            Assert.Equal(1, row.LedgerRevision));
        Assert.Equal(
            "reservation_portfolio_commit",
            result.Ledger.History[^1].Operation);
    }

    [Fact]
    public void CommitRejectsWholePortfolioWhenLaterClaimOverbooks()
    {
        var snapshot = Snapshot();
        var request = Request(snapshot, materialQuantity: 20, moneyAmount: 501);

        var result = new ReservationPortfolioLedgerService().Commit(
            null,
            snapshot,
            request,
            "2026-09-21T00:00:00Z");

        Assert.False(result.Accepted);
        Assert.Contains(
            "currency_reservation_insufficient_unreserved_amount",
            result.Errors);
        Assert.Null(result.Ledger);
        Assert.Null(result.CommittedLedgerRevision);
    }

    [Fact]
    public void CommitCanReleaseAndReplaceClaimsWithoutPartialMutation()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var first = service.Commit(
            null,
            snapshot,
            Request(snapshot, materialQuantity: 20, moneyAmount: 300),
            "2026-09-21T00:00:00Z");
        Assert.True(first.Accepted, string.Join(";", first.Errors));

        var replacement = Request(
            snapshot,
            materialQuantity: 30,
            moneyAmount: 500,
            revision: 1);
        replacement.ReleaseReservationIds = new[]
        {
            "route-a-material",
            "route-a-money"
        };
        replacement.MaterialClaims[0].ReservationId = "route-b-material";
        replacement.CurrencyClaims[0].ReservationId = "route-b-money";
        var result = service.Commit(
            first.Ledger,
            snapshot,
            replacement,
            "2026-09-21T00:01:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(2, result.Ledger!.Revision);
        Assert.Equal(
            StrategyCommitmentStatuses.Cancelled,
            result.Ledger.MaterialReservations.Single(row =>
                row.ReservationId == "route-a-material").Status);
        Assert.Equal(
            StrategyCommitmentStatuses.Active,
            result.Ledger.MaterialReservations.Single(row =>
                row.ReservationId == "route-b-material").Status);
        Assert.All(result.Ledger.History.Skip(3), row =>
            Assert.Equal(2, row.LedgerRevision));
    }

    [Fact]
    public void FailedReplacementDiscardsStagedReleases()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var first = service.Commit(
            null,
            snapshot,
            Request(snapshot, materialQuantity: 20, moneyAmount: 300),
            "2026-09-21T00:00:00Z");
        var replacement = Request(
            snapshot,
            materialQuantity: 30,
            moneyAmount: 501,
            revision: 1);
        replacement.ReleaseReservationIds = new[]
        {
            "route-a-material",
            "route-a-money"
        };
        replacement.MaterialClaims[0].ReservationId = "route-b-material";
        replacement.CurrencyClaims[0].ReservationId = "route-b-money";

        var result = service.Commit(
            first.Ledger,
            snapshot,
            replacement,
            "2026-09-21T00:01:00Z");

        Assert.False(result.Accepted);
        Assert.Same(first.Ledger, result.Ledger);
        Assert.Equal(1, result.Ledger!.Revision);
        Assert.All(result.Ledger.MaterialReservations, row => Assert.Equal(
            StrategyCommitmentStatuses.Active,
            row.Status));
        Assert.All(result.Ledger.CurrencyReservations, row => Assert.Equal(
            StrategyCommitmentStatuses.Active,
            row.Status));
    }

    private static ReservationPortfolioCommitRequest Request(
        SnapshotEnvelope snapshot,
        int materialQuantity,
        int moneyAmount,
        int revision = 0) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = revision,
            PortfolioId = "portfolio:test",
            GoalId = "goal.grandpa_21",
            SourceDecisionId = "portfolio-decision:test",
            MaterialClaims = new[]
            {
                new MaterialReservationUpsertRequest
                {
                    StateHash = snapshot.StateHash,
                    ExpectedLedgerRevision = revision,
                    ReservationId = "route-a-material",
                    SourceDecisionId = "route:a",
                    GoalId = "goal.grandpa_21",
                    NodeId = "player:123",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = materialQuantity,
                    Purpose = "test route material"
                }
            },
            CurrencyClaims = new[]
            {
                new CurrencyReservationUpsertRequest
                {
                    StateHash = snapshot.StateHash,
                    ExpectedLedgerRevision = revision,
                    ReservationId = "route-a-money",
                    SourceDecisionId = "route:a",
                    GoalId = "goal.grandpa_21",
                    CurrencyId = NativeShopCurrencies.Money,
                    Amount = moneyAmount,
                    Purpose = "test route payment"
                }
            }
        };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "identity": {
                "save_id":{"value":"TestFarm","status":"available"},
                "player_id":{"value":"123","status":"available"}
              },
              "player": {
                "money":{"value":500,"status":"available"},
                "shop_currency_balances":{"value":{
                  "schema_version":"shop_currency_balances.v1",
                  "projection_status":"complete_locked_base_1.6.15_shop_menu_currency_domain",
                  "supported_currency_ids":[0,1,2,4],
                  "rows":[
                    {"currency_id":0,"currency_key":"money","balance":500},
                    {"currency_id":1,"currency_key":"star_tokens","balance":20},
                    {"currency_id":2,"currency_key":"club_coins","balance":30},
                    {"currency_id":4,"currency_key":"qi_gems","balance":40}
                  ]
                },"status":"available"}
              },
              "farm": {
                "material_inventory_graph":{"value":{
                  "schema_version":"material_inventory_graph.v1",
                  "status":"available",
                  "player_id":123,
                  "inventory_nodes":[{
                    "node_id":"player:123",
                    "inventory_kind":"player_inventory",
                    "supply_state":"available",
                    "owner_player_id":123,
                    "ownership_class":"actor_owned",
                    "actor_use_authorized":true,
                    "slots":[{
                      "slot_index":0,
                      "item_id":"388",
                      "qualified_item_id":"(O)388",
                      "stack":30
                    }]
                  }]
                },"status":"available"}
              }
            }
            """)!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "TestFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
