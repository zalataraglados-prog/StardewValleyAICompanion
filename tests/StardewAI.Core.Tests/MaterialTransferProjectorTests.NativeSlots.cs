using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class MaterialTransferProjectorTests
{
    [Fact]
    public void TypedTransferIntentCodecRoundTripsWithoutExposingMutableRequiredKeys()
    {
        var intent = Withdrawal(3);
        var parameters = MaterialTransferIntentBinder.ToParameters(intent);
        Assert.True(MaterialTransferIntentBinder.TryBuildIntent(parameters, out var rebuilt));
        Assert.Equal(JsonSerializer.Serialize(intent), JsonSerializer.Serialize(rebuilt));
        var names = Assert.IsAssignableFrom<IList<string>>(MaterialTransferIntentBinder.RequiredParameterNames);
        Assert.True(names.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => names[0] = "overwritten");
        Assert.Equal("source_node_id", MaterialTransferIntentBinder.RequiredParameterNames[0]);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    public void SparseWithdrawalMapsRemainingAndRemovedSourceAlongsideUnrelatedStock(int quantity)
    {
        var wood = OtherSlot(3, 4, "(O)388");
        var clay = OtherSlot(9, 1, "(O)330");
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm"),
            Node("chest:Farm:4,5", "chest", 36, "Farm", wood, Slot(7, 12), clay));
        var untouched = JsonSerializer.Serialize(before);
        var intent = Withdrawal(quantity);
        var result = new MaterialTransferProjector().Project(before, intent);
        Assert.Equal("projected", result.Status);
        var source = Assert.Single(result.NativeSlotTransitions!, row => row.NodeId == intent.SourceNodeId && row.BeforeSlotIndex == 7);
        Assert.Equal(quantity == 12 ? (int?)null : 1, source.AfterSlotIndex);
        Assert.Equal(12 - quantity, source.StackAfter);
        Assert.Equal(0, Assert.Single(result.NativeSlotTransitions!, row => row.NodeId == intent.SourceNodeId && row.BeforeSlotIndex == 3).AfterSlotIndex);
        var chestSlots = quantity == 12
            ? new[] { OtherSlot(0, 4, "(O)388"), OtherSlot(1, 1, "(O)330") }
            : new[] { OtherSlot(0, 4, "(O)388"), Slot(1, 7), OtherSlot(2, 1, "(O)330") };
        var after = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(0, quantity)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", chestSlots));
        Assert.Empty(MaterialTransferProjector.VerifyNativeSlots(before, after, result));
        Assert.Empty(MaterialTransferProjector.VerifyNativeBeforeSlots(before, before, result));
        Assert.Contains("material_transfer_native_slot_transition_mismatch", MaterialTransferProjector.VerifyNativeBeforeSlots(before, after, result));
        Assert.Equal(untouched, JsonSerializer.Serialize(before));
    }

    [Fact]
    public void SparseDepositMapsOriginalStackBeforeCompactionAndNewSplitStack()
    {
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(5, 7, "(O)388"), Slot(9, 995)));
        var result = new MaterialTransferProjector().Project(before, Intent(10));
        var existing = Assert.Single(result.NativeSlotTransitions!, row => row.NodeId.StartsWith("chest:") && row.BeforeSlotIndex == 9);
        Assert.Equal(1, existing.AfterSlotIndex);
        Assert.Equal(995, existing.StackBefore);
        Assert.Equal(999, existing.StackAfter);
        var inserted = Assert.Single(result.NativeSlotTransitions!, row => !row.BeforeSlotIndex.HasValue);
        Assert.Equal(2, inserted.AfterSlotIndex);
        Assert.Equal(6, inserted.StackAfter);
        var after = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 30)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(0, 7, "(O)388"), Slot(1, 999), Slot(2, 6)));
        Assert.Empty(MaterialTransferProjector.VerifyNativeSlots(before, after, result));
    }

    [Theory]
    [InlineData("unrelated-slot")]
    [InlineData("unrelated-stack")]
    [InlineData("quality")]
    [InlineData("runtime")]
    [InlineData("owner")]
    [InlineData("missing-node")]
    public void SameTransferredQuantityDoesNotHideOtherInventoryDrift(string change)
    {
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(5, 7, "(O)388")));
        var projection = new MaterialTransferProjector().Project(before, Intent(1));
        var chest = Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(0, 7, "(O)388"), Slot(1, 1));
        var after = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 39)), chest);
        switch (change)
        {
            case "unrelated-slot": chest.Slots[0].SlotIndex = 4; break;
            case "unrelated-stack": chest.Slots[0].Stack++; break;
            case "quality": chest.Slots[1].Quality = 4; break;
            case "runtime": chest.Slots[1].RuntimeType = "different-runtime"; break;
            case "owner": chest.OwnerPlayerId = 99; break;
            case "missing-node": after.InventoryNodes = after.InventoryNodes.Take(1).ToArray(); break;
        }
        Assert.NotEmpty(MaterialTransferProjector.VerifyNativeSlots(before, after, projection));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmbiguousOrEmptySlotsCannotProduceNativeLineage(bool empty)
    {
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(5, empty ? 0 : 1), Slot(5, 1)));
        Assert.Contains("material_transfer_slot_domain_invalid", new MaterialTransferProjector().Project(before, Intent(1)).BlockingReasons);
    }

    [Fact]
    public void MissingPostInventoryNodesAreBlockedWithoutInventingAnEmptyInventory()
    {
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm"));
        var result = new MaterialTransferProjector().Project(before, Intent(1));
        var after = new MaterialInventoryGraph { InventoryNodes = null! };
        Assert.Contains("material_transfer_native_slot_lineage_unavailable", MaterialTransferProjector.VerifyNativeSlots(before, after, result));
    }

    [Fact]
    public void NullSlotBindingsAreExplicitButLegacyProjectionRemainsUnchanged()
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var legacy = JsonSerializer.SerializeToNode(new MaterialTransferProjection(), options)!.AsObject();
        Assert.False(legacy.ContainsKey("native_slot_transitions"));
        var before = TransferGraph(Node("player:1", "player_inventory", 12, "Farm"),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(7, 12)));
        var result = new MaterialTransferProjector().Project(before, Withdrawal(12));
        var rows = JsonSerializer.SerializeToNode(result, options)!["native_slot_transitions"]!.AsArray();
        var removed = Assert.Single(rows, row => row!["node_id"]!.GetValue<string>().StartsWith("chest:"))!.AsObject();
        Assert.True(removed.ContainsKey("after_slot_index"));
        Assert.Null(removed["after_slot_index"]);
        Assert.NotEmpty(MaterialTransferProjector.VerifyNativeSlots(before, before, new MaterialTransferProjection { Status = "projected" }));
    }

    private static MaterialInventoryGraph TransferGraph(params MaterialInventoryNode[] nodes)
    {
        var graph = Graph(nodes);
        graph.AccessPoints = new[] { NormalChestAccess() };
        return graph;
    }

    private static MaterialInventorySlot OtherSlot(int index, int stack, string qualified)
    {
        var slot = Slot(index, stack);
        slot.QualifiedItemId = qualified;
        return slot;
    }

    private static MaterialTransferIntent Withdrawal(int quantity)
    {
        var intent = Intent(quantity);
        intent.SourceNodeId = "chest:Farm:4,5";
        intent.DestinationNodeId = "player:1";
        intent.SourceSlotIndex = 7;
        intent.ExpectedSourceStack = 12;
        return intent;
    }
}
