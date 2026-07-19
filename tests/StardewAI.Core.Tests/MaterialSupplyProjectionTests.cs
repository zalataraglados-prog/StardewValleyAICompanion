using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class MaterialSupplyProjectionTests
{
    [Fact]
    public void BridgeBuildsPersistentDeduplicatedGraphUsingNativeInventoryAccessors()
    {
        var source = FarmReadAdapterSources.All;

        Assert.Contains("MachineLocationTopology.ReadPersistentLocations(farm, player)", source, StringComparison.Ordinal);
        Assert.Contains("chest.GetItemsForPlayer(player.UniqueMultiplayerID)", source, StringComparison.Ordinal);
        Assert.Contains("location.GetFridge(onlyUnlocked: true)", source, StringComparison.Ordinal);
        Assert.Contains("item.QualifiedItemId == \"(BC)165\" && item.heldObject.Value is Chest", source, StringComparison.Ordinal);
        Assert.Contains("item.readyForHarvest.Value ? \"ready_output\" : \"in_process\"", source, StringComparison.Ordinal);
        Assert.Contains("chest.SpecialChestType is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest", source, StringComparison.Ordinal);
        Assert.Contains(".Distinct(StringComparer.Ordinal)", source, StringComparison.Ordinal);
        Assert.Contains("[\"material_inventory_graph\"] = Field(ReadMaterialInventoryGraph", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDeductsExactSlotReservationsWithoutCountingMachineBuffers()
    {
        var graph = Graph(
            Node("player", "available", Slot(0, "(O)388", 20)),
            Node("global:JunimoChests", "available", Slot(2, "(O)388", 30)),
            Node("machine:Farm:1,1", "ready_output", Slot(0, "(O)388", 5)),
            Node("machine:Farm:2,2", "in_process", Slot(0, "(O)388", 9)));

        var result = new MaterialSupplyProjection().Project(graph, new[]
        {
            new MaterialReservation
            {
                ReservationId = "build-keg",
                NodeId = "global:JunimoChests",
                SlotIndex = 2,
                QualifiedItemId = "(O)388",
                Quantity = 12
            }
        });

        Assert.Equal("available", result.Status);
        var quantity = Assert.Single(result.Quantities);
        Assert.Equal(50, quantity.TotalQuantity);
        Assert.Equal(12, quantity.ReservedQuantity);
        Assert.Equal(38, quantity.AvailableQuantity);
        Assert.DoesNotContain(result.Slots, slot => slot.NodeId.StartsWith("machine:", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectFailsClosedWhenReservationExceedsExactStack()
    {
        var result = new MaterialSupplyProjection().Project(
            Graph(Node("chest:Farm:1,1", "available", Slot(0, "(O)390", 8))),
            new[]
            {
                new MaterialReservation
                {
                    ReservationId = "two-furnaces",
                    NodeId = "chest:Farm:1,1",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)390",
                    Quantity = 10
                }
            });

        Assert.Equal("blocked", result.Status);
        Assert.Contains("material_reservation_exceeds_stack:chest:Farm:1,1#0", result.BlockingReasons);
        Assert.Equal(0, Assert.Single(result.Slots).AvailableQuantity);
    }

    [Fact]
    public void ProjectRejectsStaleSlotIdentity()
    {
        var result = new MaterialSupplyProjection().Project(
            Graph(Node("player", "available", Slot(0, "(O)388", 20))),
            new[]
            {
                new MaterialReservation
                {
                    ReservationId = "stale",
                    NodeId = "player",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)390",
                    Quantity = 1
                }
            });

        Assert.Equal("blocked", result.Status);
        Assert.Contains("material_reservation_item_mismatch:stale", result.BlockingReasons);
    }

    [Fact]
    public void ProjectRejectsDuplicatePhysicalNodeIds()
    {
        var graph = Graph(
            Node("global:JunimoChests", "available", Slot(0, "(O)388", 20)),
            Node("global:JunimoChests", "available", Slot(0, "(O)388", 20)));

        var result = new MaterialSupplyProjection().Project(graph);

        Assert.Equal("blocked", result.Status);
        Assert.Contains("material_inventory_duplicate_node_id:global:JunimoChests", result.BlockingReasons);
    }

    private static MaterialInventoryGraph Graph(params MaterialInventoryNode[] nodes) => new()
    {
        InventoryNodes = nodes
    };

    private static MaterialInventoryNode Node(string id, string state, params MaterialInventorySlot[] slots) => new()
    {
        NodeId = id,
        SupplyState = state,
        Slots = slots
    };

    private static MaterialInventorySlot Slot(int index, string qualifiedId, int stack) => new()
    {
        SlotIndex = index,
        ItemId = qualifiedId,
        QualifiedItemId = qualifiedId,
        Stack = stack
    };
}
