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
        Assert.Contains(
            "chest.GetItemsForPlayer(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "player.UniqueMultiplayerID",
            source,
            StringComparison.Ordinal);
        Assert.Contains("chest.SpecialChestType == Chest.SpecialChestTypes.JunimoChest", source, StringComparison.Ordinal);
        Assert.Contains("FarmerTeam.GlobalInventoryId_JunimoChest", source, StringComparison.Ordinal);
        Assert.Contains("location.GetFridge(onlyUnlocked: true)", source, StringComparison.Ordinal);
        Assert.Contains("item.QualifiedItemId == \"(BC)165\" && item.heldObject.Value is Chest", source, StringComparison.Ordinal);
        Assert.Contains("item.readyForHarvest.Value ? \"ready_output\" : \"in_process\"", source, StringComparison.Ordinal);
        Assert.Contains("MaximumStackSize = item.maximumStackSize()", source, StringComparison.Ordinal);
        Assert.Contains("chest.SpecialChestType is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest", source, StringComparison.Ordinal);
        Assert.Contains("chest.playerChest.Value", source, StringComparison.Ordinal);
        Assert.Contains("\"shared_team_global\"", source, StringComparison.Ordinal);
        Assert.Contains("\"other_player_owned\"", source, StringComparison.Ordinal);
        Assert.Contains("\"deny_without_explicit_authorization\"", source, StringComparison.Ordinal);
        Assert.Contains("workbench_native_container_not_actor_authorized", source, StringComparison.Ordinal);
        Assert.Contains(".Distinct(StringComparer.Ordinal)", source, StringComparison.Ordinal);
        Assert.Contains("ReadCachedMaterialInventoryGraph(", source, StringComparison.Ordinal);
        Assert.Contains("[\"material_inventory_graph\"] = Field(materialInventoryGraph", source, StringComparison.Ordinal);
        Assert.Contains("ReadStorageInfrastructure(materialInventoryGraph)", source, StringComparison.Ordinal);
        Assert.Contains("chest.GetActualCapacity()", source, StringComparison.Ordinal);
        Assert.Contains("chest.GetMutex()", source, StringComparison.Ordinal);
        Assert.Contains("\"native_remove_available_empty\"", source, StringComparison.Ordinal);
        Assert.Contains("\"native_shove_available_nonempty\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"chests\"] = Field(ReadChests(", source, StringComparison.Ordinal);
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
    public void ProjectIgnoresCancelledReservation()
    {
        var result = new MaterialSupplyProjection().Project(
            Graph(Node("player:1", "available", Slot(0, "(O)388", 20))),
            new[]
            {
                new MaterialReservation
                {
                    ReservationId = "cancelled",
                    Status = StrategyCommitmentStatuses.Cancelled,
                    NodeId = "player:1",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 20
                }
            });

        Assert.Equal("available", result.Status);
        Assert.Equal(20, Assert.Single(result.Slots).AvailableQuantity);
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

    [Fact]
    public void ProjectExcludesSharedAndOtherPlayerNodesWithoutHidingThem()
    {
        var actorNode = Node("player:1", "available", Slot(0, "(O)388", 20));
        var sharedNode = Node("global:JunimoChests", "available", Slot(0, "(O)388", 30));
        sharedNode.ActorUseAuthorized = false;
        sharedNode.OwnershipClass = "shared_team_global";
        var otherNode = Node("chest:Farm:2,2", "available", Slot(0, "(O)388", 40));
        otherNode.ActorUseAuthorized = false;
        otherNode.OwnershipClass = "other_player_owned";

        var result = new MaterialSupplyProjection().Project(Graph(actorNode, sharedNode, otherNode));

        Assert.Equal("available", result.Status);
        Assert.Equal(20, Assert.Single(result.Quantities).AvailableQuantity);
        Assert.Equal(new[] { "chest:Farm:2,2", "global:JunimoChests" }, result.ExcludedNodeIds);
        Assert.DoesNotContain(result.Slots, row => row.NodeId == sharedNode.NodeId || row.NodeId == otherNode.NodeId);
    }

    private static MaterialInventoryGraph Graph(params MaterialInventoryNode[] nodes) => new()
    {
        InventoryNodes = nodes
    };

    private static MaterialInventoryNode Node(string id, string state, params MaterialInventorySlot[] slots) => new()
    {
        NodeId = id,
        SupplyState = state,
        OwnershipClass = "actor_owned",
        ActorUseAuthorized = true,
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
