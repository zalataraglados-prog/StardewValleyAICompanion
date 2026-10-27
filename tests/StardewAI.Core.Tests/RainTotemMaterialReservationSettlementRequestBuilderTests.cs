using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed class RainTotemMaterialReservationSettlementRequestBuilderTests
{
    [Fact]
    public void BuildBindsExactCommittedConsumptionsToNativeReceipt()
    {
        var item = QueueItem();

        var result =
            RainTotemMaterialReservationSettlementRequestBuilder.Build(
                item,
                "before-state",
                "after-state",
                new string('a', 64));

        Assert.True(result.Valid, string.Join(";", result.Errors));
        var request = Assert.IsType<
            ReservationPortfolioSupportingTransitionSettlementRequest>(
                result.Request);
        Assert.Equal("after-state", request.StateHash);
        Assert.Equal(7, request.ExpectedLedgerRevision);
        Assert.Equal(
            RainTotemMaterialReservationContract.PortfolioId(101, "Default"),
            request.PortfolioId);
        Assert.Equal(3, request.MaterialConsumptions.Length);
        Assert.All(request.MaterialConsumptions, row =>
            Assert.StartsWith(
                "material:rain-totem:",
                row.MaterialReservationId));
        Assert.Equal(64, request.SupportingTransitionReceiptSha256.Length);
    }

    [Fact]
    public void BuildRejectsTamperedReservationIdentity()
    {
        var item = QueueItem();
        var rows = JsonSerializer.Deserialize<
            ReservationPortfolioMaterialConsumption[]>(Parameter(
                item,
                "material_reservation_consumptions_json"))!;
        rows[0].MaterialReservationId = "material:rain-totem:tampered";
        Set(
            item,
            "material_reservation_consumptions_json",
            JsonSerializer.Serialize(rows));

        var result =
            RainTotemMaterialReservationSettlementRequestBuilder.Build(
                item,
                "before-state",
                "after-state",
                new string('a', 64));

        Assert.False(result.Valid);
        Assert.Contains(
            "rain_totem_settlement_consumption_identity_invalid",
            result.Errors);
    }

    private static ActionQueueItem QueueItem()
    {
        var authorization = RainTotemMaterialReservationContract.Authorization(
            101,
            "Default");
        var consumptions =
            RainTotemMaterialReservationContract.SettlementConsumptions(
                authorization,
                [
                    new ExactCraftingMaterialConsumption(
                        "player:123", 0, "(O)709", 1),
                    new ExactCraftingMaterialConsumption(
                        "player:123", 1, "(O)432", 1),
                    new ExactCraftingMaterialConsumption(
                        "player:123", 2, "(O)726", 5)
                ]);
        return new ActionQueueItem
        {
            QueueItemId = "queue-item-rain-totem",
            SourceActionId = "action-rain-totem",
            OptionId = "executor.craft_rain_totem",
            NormalizedCommand = new NormalizedCommand
            {
                Parameters =
                [
                    P("weather_supply_target_total_day", "101"),
                    P("weather_supply_affected_context_id", "Default"),
                    P("material_reservation_goal_id", authorization.GoalId),
                    P(
                        "material_reservation_source_decision_id",
                        authorization.SourceDecisionId),
                    P(
                        "material_reservation_purpose",
                        authorization.Purpose),
                    P(
                        "material_reservation_portfolio_id",
                        RainTotemMaterialReservationContract.PortfolioId(
                            101,
                            "Default")),
                    P("material_reservation_ledger_revision", "7"),
                    P(
                        "material_reservation_ids_json",
                        JsonSerializer.Serialize(consumptions.Select(row =>
                            row.MaterialReservationId))),
                    P(
                        "material_reservation_consumptions_json",
                        JsonSerializer.Serialize(consumptions))
                ]
            }
        };
    }

    private static SmallModelActionParameter P(string name, string value) =>
        new() { Name = name, Value = value };

    private static string Parameter(ActionQueueItem item, string name) =>
        item.NormalizedCommand.Parameters.Single(row => row.Name == name).Value;

    private static void Set(
        ActionQueueItem item,
        string name,
        string value) =>
        item.NormalizedCommand.Parameters.Single(row => row.Name == name)
            .Value = value;
}
