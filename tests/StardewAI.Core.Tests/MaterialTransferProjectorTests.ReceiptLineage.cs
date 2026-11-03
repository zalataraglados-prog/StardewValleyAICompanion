using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MaterialTransferProjectorTests
{
    [Theory]
    [InlineData(5, false)]
    [InlineData(12, false)]
    [InlineData(5, true)]
    public void IndependentReceiptUsesCompactedSourceAndChecksUnrelatedStock(int quantity, bool tamper)
    {
        var intent = Withdrawal(quantity);
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm"),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(3, 4, "(O)388"), Slot(7, 12)));
        var chest = quantity == 12 ? new[] { OtherSlot(0, 4, "(O)388") } :
            new[] { OtherSlot(0, 4, "(O)388"), Slot(1, 7) };
        if (tamper) chest[0].Stack++;
        var after = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(0, quantity)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", chest));
        var result = VerifyTransferReceipt(intent, before, after, 0);
        Assert.Equal(!tamper, result.Verified);
        if (tamper) Assert.Contains("material_transfer_native_slot_transition_mismatch", result.BlockingReasons);
    }

    [Fact]
    public void IndependentReceiptResolvesDestinationBeforeItsNativeCompaction()
    {
        var intent = Intent(1);
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(5, 7, "(O)388"), Slot(9, 3)));
        var after = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 39)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(0, 7, "(O)388"), Slot(1, 4)));
        var result = VerifyTransferReceipt(intent, before, after, 1);
        Assert.True(result.Verified, string.Join(";", result.BlockingReasons));
    }

    private static AcquisitionMaterialTransferTransitionEvidence VerifyTransferReceipt(MaterialTransferIntent intent,
        MaterialInventoryGraph before, MaterialInventoryGraph after, int destinationSlot)
    {
        SmallModelActionParameter P(string name, object value) => new() { Name = name, Value = value.ToString()! };
        var parameters = new[]
        {
            P("source_node_id", intent.SourceNodeId), P("destination_node_id", intent.DestinationNodeId),
            P("source_slot_index", intent.SourceSlotIndex), P("qualified_item_id", intent.QualifiedItemId),
            P("quality", intent.Quality), P("quantity", intent.Quantity), P("expected_source_stack", intent.ExpectedSourceStack),
            P("acquisition_support_material_relocations_json", JsonSerializer.Serialize(new[]
            {
                new AcquisitionSupportMaterialRelocation
                {
                    ReservationId = "fixture-preserved-claim", SourceNodeId = intent.SourceNodeId,
                    SourceSlotIndex = intent.SourceSlotIndex, DestinationNodeId = intent.DestinationNodeId,
                    DestinationSlotIndex = destinationSlot, QualifiedItemId = intent.QualifiedItemId, Quantity = intent.Quantity
                }
            }))
        };
        var queue = new ActionQueueEnvelope
        {
            Items = new[]
            {
                new ActionQueueItem { OptionId = "executor.move_to_tile" },
                new ActionQueueItem { OptionId = "executor.transfer_material", NormalizedCommand = new()
                {
                    Parameters = parameters, Steps = new[] { new CompiledActionStep { StepType = "transfer_material" } }
                } }
            }
        };
        SnapshotEnvelope Snapshot(MaterialInventoryGraph graph) => new()
        {
            State = new() { ["farm"] = JsonSerializer.SerializeToElement(new
            {
                material_inventory_graph = new { status = "available", value = graph }
            }) }
        };
        var method = typeof(AcquisitionRouteSupportingTransitionReceiptBuilder).GetMethod("VerifyMaterialTransfer",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (AcquisitionMaterialTransferTransitionEvidence)method.Invoke(null, new object[] { queue, Snapshot(before), Snapshot(after) })!;
    }
}
