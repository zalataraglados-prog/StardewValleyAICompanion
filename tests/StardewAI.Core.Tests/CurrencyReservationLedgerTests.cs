using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed class CurrencyReservationLedgerTests
{
    [Fact]
    public void ProjectionSubtractsOnlyValidActiveActorReservations()
    {
        var result = new NativeCurrencySupplyProjection().Project(
            Balances(),
            123,
            new[]
            {
                Reservation("active", 123, 0, "money", 300, "active"),
                Reservation("cancelled", 123, 0, "money", 400, "cancelled")
            });

        Assert.Equal("available", result.Status);
        var money = result.Balances.Single(row =>
            row.CurrencyId == NativeShopCurrencies.Money);
        Assert.Equal(500, money.TotalAmount);
        Assert.Equal(300, money.ReservedAmount);
        Assert.Equal(200, money.AvailableAmount);

        var wrongOwner = new NativeCurrencySupplyProjection().Project(
            Balances(),
            123,
            new[]
            {
                Reservation("other", 456, 0, "money", 1, "active")
            });
        Assert.Equal("blocked", wrongOwner.Status);
        Assert.Contains(
            "active_currency_reservation_contract_invalid:other",
            wrongOwner.BlockingReasons);
    }

    [Fact]
    public void ServicePreventsOverbookingAndCancellationReleasesBalance()
    {
        var snapshot = CurrencySnapshot();
        var service = new CurrencyReservationLedgerService();
        var first = service.Upsert(
            null,
            snapshot,
            Request(snapshot, 0, "first", 300),
            "2026-09-13T00:00:00Z");
        Assert.True(first.Accepted, string.Join(";", first.Errors));
        var reservation = Assert.Single(first.Ledger!.CurrencyReservations);
        Assert.Equal("money", reservation.CurrencyKey);
        Assert.Equal(123, reservation.OwnerPlayerId);

        var overbooked = service.Upsert(
            first.Ledger,
            snapshot,
            Request(snapshot, 1, "second", 201),
            "2026-09-13T00:01:00Z");
        Assert.False(overbooked.Accepted);
        Assert.Contains(
            "currency_reservation_insufficient_unreserved_amount",
            overbooked.Errors);

        var cancelled = service.Cancel(
            first.Ledger,
            snapshot,
            "first",
            new StrategyCommitmentCancelRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 1,
                Reason = "route_replanned"
            },
            "2026-09-13T00:02:00Z");
        Assert.True(cancelled.Accepted, string.Join(";", cancelled.Errors));

        var replacement = service.Upsert(
            cancelled.Ledger,
            snapshot,
            Request(snapshot, 2, "second", 500),
            "2026-09-13T00:03:00Z");
        Assert.True(replacement.Accepted, string.Join(";", replacement.Errors));
        Assert.Equal(
            new[]
            {
                "currency_reservation_upsert",
                "currency_reservation_cancel",
                "currency_reservation_upsert"
            },
            replacement.Ledger!.History.Select(row => row.Operation));
    }

    [Fact]
    public void ServiceRejectsUnsupportedCurrencyAndIncompleteProjection()
    {
        var snapshot = CurrencySnapshot();
        var unsupported = Request(snapshot, 0, "unsupported", 1);
        unsupported.CurrencyId = 3;
        var result = new CurrencyReservationLedgerService().Upsert(
            null,
            snapshot,
            unsupported,
            "2026-09-13T00:00:00Z");
        Assert.False(result.Accepted);
        Assert.Contains(
            "currency_reservation_currency_id_unsupported",
            result.Errors);

        var state = snapshot.State.ToDictionary(pair => pair.Key, pair => pair.Value);
        using var player = JsonDocument.Parse(
            "{\"shop_currency_balances\":{\"value\":{},\"status\":\"available\"}}");
        state["player"] = player.RootElement.Clone();
        var incomplete = Snapshot(state);
        var blocked = new CurrencyReservationLedgerService().Upsert(
            null,
            incomplete,
            Request(incomplete, 0, "invalid", 1),
            "2026-09-13T00:01:00Z");
        Assert.False(blocked.Accepted);
        Assert.Contains("shop_currency_balances_invalid", blocked.Errors);
    }

    private static IReadOnlyDictionary<int, int> Balances() =>
        new Dictionary<int, int>
        {
            [0] = 500,
            [1] = 20,
            [2] = 30,
            [4] = 40
        };

    private static CurrencyReservation Reservation(
        string id,
        long owner,
        int currencyId,
        string currencyKey,
        int amount,
        string status) => new()
        {
            ReservationId = id,
            Status = status,
            SourceDecisionId = "route:" + id,
            GoalId = "goal.grandpa_21",
            OwnerPlayerId = owner,
            CurrencyId = currencyId,
            CurrencyKey = currencyKey,
            Amount = amount,
            Purpose = "test reservation"
        };

    private static CurrencyReservationUpsertRequest Request(
        SnapshotEnvelope snapshot,
        int revision,
        string id,
        int amount) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = revision,
            ReservationId = id,
            SourceDecisionId = "route:" + id,
            GoalId = "goal.grandpa_21",
            CurrencyId = NativeShopCurrencies.Money,
            Amount = amount,
            Purpose = "reserve route payment"
        };

    private static SnapshotEnvelope CurrencySnapshot()
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
              }
            }
            """)!;
        return Snapshot(state);
    }

    private static SnapshotEnvelope Snapshot(
        Dictionary<string, JsonElement> state) => new()
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
