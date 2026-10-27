using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class MachineCraftingMaterialReservationGuardTests
{
    [Fact]
    public void CompetingReservationStillBlocksExactCraftingConsumption()
    {
        using var ingredients = Ingredients();
        var result = new MachineCraftingMaterialReservationGuard().Evaluate(
            Snapshot(),
            ingredients.RootElement,
            usesWorkbench: false,
            Ledger(
                goalId: "goal.other",
                sourceDecisionId: "decision:other",
                purpose: "other purpose"),
            new MaterialReservationConsumptionAuthorization(
                "goal.fishing.complete_master_angler",
                "decision:rain",
                "consume rain totem"));

        Assert.False(result.Ready);
        Assert.Contains(
            "machine_recipe_material_reserved_for_other_goal:player:123#0",
            result.BlockingReasons);
        Assert.Empty(result.ReservationIds);
    }

    [Fact]
    public void ExactAuthorizedReservationCanBeConsumedByItsOwnCraft()
    {
        using var ingredients = Ingredients();
        var authorization = new MaterialReservationConsumptionAuthorization(
            "goal.fishing.complete_master_angler",
            "decision:rain",
            "consume rain totem");
        var result = new MachineCraftingMaterialReservationGuard().Evaluate(
            Snapshot(),
            ingredients.RootElement,
            usesWorkbench: false,
            Ledger(
                authorization.GoalId,
                authorization.SourceDecisionId,
                authorization.Purpose),
            authorization);

        Assert.True(result.Ready);
        Assert.Equal("ready", result.Status);
        Assert.Equal(["reservation:wood"], result.ReservationIds);
    }

    private static JsonDocument Ingredients() => JsonDocument.Parse(
        """
        [{
          "requirement_id_or_category":"388",
          "required_count":1,
          "reverse_slot_consumption_plan":[{
            "slot_index":0,
            "qualified_item_id":"(O)388",
            "amount":1
          }]
        }]
        """);

    private static StrategyCommitmentLedger Ledger(
        string goalId,
        string sourceDecisionId,
        string purpose) => new()
        {
            LedgerId = "ledger:test",
            Revision = 3,
            MaterialReservations =
            [
                new MaterialReservation
                {
                    ReservationId = "reservation:wood",
                    Status = StrategyCommitmentStatuses.Active,
                    GoalId = goalId,
                    SourceDecisionId = sourceDecisionId,
                    Purpose = purpose,
                    NodeId = "player:123",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 1
                }
            ]
        };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "farm":{
                "material_inventory_graph":{"value":{
                  "schema_version":"material_inventory_graph.v1",
                  "status":"available",
                  "player_id":123,
                  "inventory_nodes":[{
                    "node_id":"player:123",
                    "inventory_kind":"player_inventory",
                    "supply_state":"available",
                    "owner_player_id":123,
                    "actor_use_authorized":true,
                    "capacity":12,
                    "slots":[{
                      "slot_index":0,
                      "item_id":"388",
                      "qualified_item_id":"(O)388",
                      "stack":1,
                      "maximum_stack_size":999,
                      "quality":0
                    }]
                  }],
                  "access_points":[],
                  "workbench_links":[],
                  "quantity_rows":[]
                },"status":"available"}
              }
            }
            """)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            PlayerId = new FieldEnvelope<string?> { Value = "123" },
            GameTick = 1,
            RealTimestamp = "2026-10-23T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
