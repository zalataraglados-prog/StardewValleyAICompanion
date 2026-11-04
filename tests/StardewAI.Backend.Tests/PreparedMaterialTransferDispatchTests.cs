using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed class PreparedMaterialTransferDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactPreparationCanDispatchBeforeOrAfterOrdinaryMovement(bool afterMovement)
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var ledger = fixture.PreparedLedger;
        var item = Item(fixture);
        var stateHash = afterMovement ? new string('f', 64) : fixture.Before.StateHash;
        item.NormalizedCommand.StateHash = stateHash;
        var before = JsonSerializer.Serialize(ledger);
        var result = new ActionQueueDispatchReadinessService().Evaluate(new ActionQueueEnvelope(), item, ledger, stateHash);
        Assert.True(result.Ready, string.Join(",", result.BlockingReasons));
        Assert.Equal("ready", result.Status);
        Assert.Equal(before, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData("missing-binding")]
    [InlineData("empty-binding")]
    [InlineData("wrong-binding")]
    [InlineData("duplicate-binding")]
    [InlineData("missing-preparation")]
    [InlineData("missing-goal")]
    [InlineData("wrong-goal")]
    [InlineData("wrong-portfolio")]
    [InlineData("wrong-revision")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-intent")]
    [InlineData("stale-state")]
    [InlineData("not-pending")]
    [InlineData("ledger-drift")]
    [InlineData("passive-claim-drift")]
    [InlineData("missing-origin")]
    [InlineData("map-drift")]
    [InlineData("unprepared-reserved-downgrade")]
    public void InvalidBindingNeverDispatchesOrMutatesLedger(string mutation)
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var ledger = fixture.PreparedLedger;
        var item = Item(fixture);
        switch (mutation)
        {
            case "missing-binding": Remove(item, PreparedMaterialTransferContract.BindingParameter); break;
            case "empty-binding": Set(item, PreparedMaterialTransferContract.BindingParameter, ""); break;
            case "wrong-binding": Set(item, PreparedMaterialTransferContract.BindingParameter, new string('b', 64)); break;
            case "duplicate-binding": item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Append(
                new SmallModelActionParameter { Name = PreparedMaterialTransferContract.BindingParameter, Value = ledger.PreparedMaterialTransfer!.BindingSha256 }).ToArray(); break;
            case "missing-preparation": ledger.PreparedMaterialTransfer = null; break;
            case "missing-goal": item.NormalizedCommand.GoalId = null; break;
            case "wrong-goal": item.NormalizedCommand.GoalId = "other.goal"; break;
            case "wrong-portfolio": Set(item, "acquisition_reservation_portfolio_id", "other:portfolio"); break;
            case "wrong-revision": Set(item, "acquisition_reservation_ledger_revision", "2"); break;
            case "wrong-kind": Set(item, "acquisition_support_transition_kind", "machine_input_load"); break;
            case "wrong-intent": Set(item, "quantity", "1"); break;
            case "stale-state": item.NormalizedCommand.StateHash = "stale"; break;
            case "not-pending": item.Status = "completed"; break;
            case "ledger-drift": ledger.Revision++; break;
            case "passive-claim-drift": ledger.MaterialReservations.Single(row => row.ReservationId == "stone").Quantity--; break;
            case "missing-origin": ledger.History = ledger.History.Where(row => row.Operation != "material_transfer_prepared").ToArray(); break;
            case "map-drift": ledger.PreparedMaterialTransfer!.ReservationProjection.ReservationTransitions[0].DestinationSlotIndex++; break;
            case "unprepared-reserved-downgrade":
                ledger.PreparedMaterialTransfer = null;
                item.NormalizedCommand.GoalId = null;
                item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row =>
                    row.Name != PreparedMaterialTransferContract.BindingParameter && !row.Name.StartsWith("acquisition_", StringComparison.Ordinal)).ToArray();
                break;
        }
        var before = JsonSerializer.Serialize(ledger);
        var result = new ActionQueueDispatchReadinessService().Evaluate(new ActionQueueEnvelope(), item, ledger, fixture.Before.StateHash);
        Assert.False(result.Ready);
        Assert.NotEmpty(result.BlockingReasons);
        Assert.Equal(before, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnreservedLegacyTransferDoesNotRequireUnrelatedReservations(bool unrelatedClaim)
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var item = Item(fixture);
        item.NormalizedCommand.GoalId = null;
        item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row =>
            row.Name != PreparedMaterialTransferContract.BindingParameter && !row.Name.StartsWith("acquisition_", StringComparison.Ordinal)).ToArray();
        var ledger = fixture.BaseLedger;
        ledger.MaterialReservations = unrelatedClaim
            ? ledger.MaterialReservations.Where(row => row.ReservationId == "outside").ToArray()
            : Array.Empty<MaterialReservation>();
        var result = new ActionQueueDispatchReadinessService().Evaluate(new ActionQueueEnvelope(), item, ledger, fixture.Before.StateHash);
        Assert.True(result.Ready, string.Join(",", result.BlockingReasons));
        Assert.Equal("not_applicable", result.Status);
    }

    internal static ActionQueueItem Item(PreparedMaterialTransferTestFixture fixture) => new()
    {
        QueueItemId = "prepared-transfer-item", OptionId = "executor.transfer_material", Status = "pending",
        NormalizedCommand = new NormalizedCommand
        {
            OptionId = "executor.transfer_material", GoalId = "goal.grandpa_21", StateHash = fixture.Before.StateHash,
            Parameters = new[]
            {
                P("source_node_id", "chest:Farm:4,5"), P("destination_node_id", "player:123"), P("source_slot_index", "5"),
                P("qualified_item_id", "(O)388"), P("quality", "0"), P("quantity", "2"), P("expected_source_stack", "11"),
                P(PreparedMaterialTransferContract.BindingParameter, fixture.PreparedLedger.PreparedMaterialTransfer!.BindingSha256),
                P("acquisition_reservation_portfolio_id", "support:atomic-transfer"), P("acquisition_reservation_ledger_revision", "3"),
                P("acquisition_support_transition_kind", "machine_input_material_transfer")
            }
        }
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static void Set(ActionQueueItem item, string name, string value) => item.NormalizedCommand.Parameters.Single(row => row.Name == name).Value = value;
    private static void Remove(ActionQueueItem item, string name) => item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row => row.Name != name).ToArray();
}
