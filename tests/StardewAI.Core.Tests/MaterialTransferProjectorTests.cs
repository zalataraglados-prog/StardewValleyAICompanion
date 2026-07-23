using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class MaterialTransferProjectorTests
{
    [Fact]
    public void ProjectsPlayerDepositUsingNativeStackThenEmptySlotOrder()
    {
        var graph = Graph(
            Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(0, 995)));
        graph.AccessPoints = new[] { NormalChestAccess() };

        var result = new MaterialTransferProjector().Project(graph, Intent(10));

        Assert.Equal("projected", result.Status);
        Assert.Equal(10, result.DestinationQuantityAfter - result.DestinationQuantityBefore);
        Assert.Collection(
            result.DestinationSlotChanges,
            change =>
            {
                Assert.Equal(0, change.SlotIndex);
                Assert.Equal(995, change.StackBefore);
                Assert.Equal(999, change.StackAfter);
            },
            change =>
            {
                Assert.Equal(1, change.SlotIndex);
                Assert.Equal(0, change.StackBefore);
                Assert.Equal(6, change.StackAfter);
            });
    }

    [Fact]
    public void BlocksLockedChestBeforeExecution()
    {
        var graph = Graph(
            Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm"));
        var access = NormalChestAccess();
        access.LockedByOtherPlayer = true;
        graph.AccessPoints = new[] { access };

        var result = new MaterialTransferProjector().Project(graph, Intent(10));

        Assert.Contains("material_transfer_chest_locked_by_other_player", result.BlockingReasons);
    }

    [Fact]
    public void BlocksSourceStackAndQuantityDrift()
    {
        var graph = Graph(
            Node("player:1", "player_inventory", 12, "Farm", Slot(2, 9)),
            Node("chest:Farm:4,5", "chest", 36, "Farm"));
        graph.AccessPoints = new[] { NormalChestAccess() };
        var intent = Intent(10);
        intent.ExpectedSourceStack = 40;

        var result = new MaterialTransferProjector().Project(graph, intent);

        Assert.Contains("material_transfer_source_stack_drifted", result.BlockingReasons);
        Assert.Contains("material_transfer_quantity_invalid", result.BlockingReasons);
    }

    [Fact]
    public void ProjectsChestWithdrawalIntoPlayerInventory()
    {
        var graph = Graph(
            Node("player:1", "player_inventory", 12, "Farm"),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(7, 12)));
        graph.AccessPoints = new[] { NormalChestAccess() };
        var intent = Intent(5);
        intent.SourceNodeId = "chest:Farm:4,5";
        intent.DestinationNodeId = "player:1";
        intent.SourceSlotIndex = 7;
        intent.ExpectedSourceStack = 12;

        var result = new MaterialTransferProjector().Project(graph, intent);

        Assert.Equal("projected", result.Status);
        Assert.Equal(7, result.SourceStackAfter);
        Assert.Equal(0, Assert.Single(result.DestinationSlotChanges).SlotIndex);
        Assert.Contains("Chest.grabItemFromChest", result.NativeBranch);
    }

    [Fact]
    public void BlocksWhenDestinationHasNoNativeCapacity()
    {
        var graph = Graph(
            Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 1, "Farm", Slot(0, 999)));
        graph.AccessPoints = new[] { NormalChestAccess() };

        var result = new MaterialTransferProjector().Project(graph, Intent(1));

        Assert.Contains(
            "material_transfer_destination_capacity_insufficient",
            result.BlockingReasons);
    }

    [Fact]
    public void CompactsSparseChestSlotsBeforeNativeInsertion()
    {
        var graph = Graph(
            Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40)),
            Node("chest:Farm:4,5", "chest", 36, "Farm", Slot(5, 999)));
        graph.AccessPoints = new[] { NormalChestAccess() };

        var result = new MaterialTransferProjector().Project(graph, Intent(1));

        var change = Assert.Single(result.DestinationSlotChanges);
        Assert.Equal(1, change.SlotIndex);
        Assert.Equal(0, change.StackBefore);
        Assert.Equal(1, change.StackAfter);
    }

    [Fact]
    public void BlocksTransferToOrFromNonActorOwnedChest()
    {
        var player = Node("player:1", "player_inventory", 12, "Farm", Slot(2, 40));
        var chest = Node("chest:Farm:4,5", "chest", 36, "Farm");
        chest.ActorUseAuthorized = false;
        chest.OwnershipClass = "other_player_owned";
        var graph = Graph(player, chest);
        graph.AccessPoints = new[] { NormalChestAccess() };

        var result = new MaterialTransferProjector().Project(graph, Intent(10));

        Assert.Contains("material_transfer_node_not_actor_authorized", result.BlockingReasons);
    }

    private static MaterialTransferIntent Intent(int quantity) => new()
    {
        SourceNodeId = "player:1",
        DestinationNodeId = "chest:Farm:4,5",
        SourceSlotIndex = 2,
        QualifiedItemId = "(O)390",
        Quality = 0,
        Quantity = quantity,
        ExpectedSourceStack = 40
    };

    private static MaterialInventoryGraph Graph(params MaterialInventoryNode[] nodes) => new()
    {
        InventoryNodes = nodes
    };

    private static MaterialInventoryNode Node(
        string id,
        string kind,
        int capacity,
        string location,
        params MaterialInventorySlot[] slots) => new()
    {
        NodeId = id,
        InventoryKind = kind,
        SupplyState = "available",
        OwnershipClass = "actor_owned",
        ActorUseAuthorized = true,
        Capacity = capacity,
        LocationId = location,
        Slots = slots
    };

    private static MaterialInventorySlot Slot(int index, int stack) => new()
    {
        SlotIndex = index,
        QualifiedItemId = "(O)390",
        RuntimeType = "StardewValley.Object",
        Stack = stack,
        MaximumStackSize = 999,
        Quality = 0
    };

    private static MaterialInventoryAccessPoint NormalChestAccess() => new()
    {
        AccessPointId = "access:placed_chest:Farm:4,5",
        NodeId = "chest:Farm:4,5",
        AccessKind = "placed_chest",
        LocationId = "Farm",
        SpecialChestType = "None"
    };
}
