using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class StorageInfrastructureCapacityProjectionTests
{
    [Fact]
    public void CurrentLocationStorageReadIsPurposeScopedAndFailVisible()
    {
        var root = FindRepositoryRoot();
        var profileSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "State",
            "SnapshotProfileContext.cs"));
        var currentLocationSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.cs"));

        Assert.Contains(
            "IncludesPersistentMaterialInventoryGraph",
            profileSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Current is \"daily\" or \"training_machine\" or \"fishing\" or \"full\"",
            profileSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "location is null || !storageRequested",
            currentLocationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"not_requested_by_snapshot_profile\"",
            currentLocationSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectsOnlyAuthorizedUnlockedAvailableStorage()
    {
        var graph = Graph(
            Node("chest:Farm:1,1", 36, true, "available",
                Slot(0, "(O)388", 20)),
            Node("chest:Farm:2,2", 70, false, "available"),
            Node("chest:Farm:3,3", 36, true, "shipping_pending"));
        var storage = Storage(
            Access(
                "access:placed_chest:Farm:1,1",
                "chest:Farm:1,1",
                36,
                1,
                35,
                true,
                false),
            Access(
                "access:placed_chest:Farm:2,2",
                "chest:Farm:2,2",
                70,
                0,
                70,
                false,
                false),
            Access(
                "access:placed_chest:Farm:3,3",
                "chest:Farm:3,3",
                36,
                0,
                36,
                true,
                false));

        var result =
            new StorageInfrastructureCapacityProjection()
                .Project(storage, graph);

        Assert.Equal("available", result.Status);
        Assert.Equal(3, result.Rows.Length);
        Assert.Equal(1, result.ImmediatelyUsableAccessPointCount);
        Assert.Equal(35, result.ImmediatelyUsableFreeStackSlotCount);
    }

    [Fact]
    public void FailsClosedOnDanglingNodeAndCapacityDrift()
    {
        var graph = Graph(
            Node("chest:Farm:1,1", 36, true, "available"));
        var storage = Storage(
            Access(
                "access:placed_chest:Farm:1,1",
                "chest:Farm:1,1",
                70,
                0,
                70,
                true,
                false),
            Access(
                "access:placed_chest:Farm:9,9",
                "chest:Farm:9,9",
                36,
                0,
                36,
                true,
                false));

        var result =
            new StorageInfrastructureCapacityProjection()
                .Project(storage, graph);

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "storage_access_capacity_drift:" +
            "access:placed_chest:Farm:1,1",
            result.BlockingReasons);
        Assert.Contains(
            "storage_access_node_unresolved:" +
            "access:placed_chest:Farm:9,9",
            result.BlockingReasons);
    }

    [Fact]
    public void FailsClosedOnGlobalIdentityDrift()
    {
        var node = Node(
            "global:JunimoChests",
            9,
            false,
            "available",
            Slot(0, "(O)390", 5));
        node.GlobalInventoryId = "JunimoChests";
        var access = Access(
            "access:placed_chest:Farm:4,4",
            node.NodeId,
            9,
            1,
            8,
            false,
            false);
        access.GlobalInventoryId = "wrong";

        var result =
            new StorageInfrastructureCapacityProjection()
                .Project(Storage(access), Graph(node));

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "storage_access_global_inventory_drift:" +
            access.AccessPointId,
            result.BlockingReasons);
    }

    private static StorageInfrastructureProjection Storage(
        params MaterialInventoryAccessPoint[] rows) => new()
        {
            SourceGraphPlayerId = 1,
            AccessPoints = rows,
            AccessPointCount = rows.Length,
            DistinctInventoryNodeCount = rows
                .Select(row => row.NodeId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ActorAuthorizedAccessPointCount = rows.Count(row =>
                row.ActorUseAuthorized),
            LockedAccessPointCount = rows.Count(row =>
                row.LockedByOtherPlayer),
            RemovableEmptyAccessPointCount = rows.Count(row =>
                string.Equals(
                    row.RelocationStatus,
                    "native_remove_available_empty",
                    StringComparison.Ordinal)),
            NonemptyShoveAccessPointCount = rows.Count(row =>
                string.Equals(
                    row.RelocationStatus,
                    "native_shove_available_nonempty",
                    StringComparison.Ordinal))
        };

    private static MaterialInventoryGraph Graph(
        params MaterialInventoryNode[] nodes) => new()
        {
            PlayerId = 1,
            InventoryNodes = nodes
        };

    private static MaterialInventoryNode Node(
        string nodeId,
        int capacity,
        bool authorized,
        string supplyState,
        params MaterialInventorySlot[] slots) => new()
        {
            NodeId = nodeId,
            Capacity = capacity,
            ActorUseAuthorized = authorized,
            SupplyState = supplyState,
            Slots = slots
        };

    private static MaterialInventorySlot Slot(
        int index,
        string qualifiedItemId,
        int stack) => new()
        {
            SlotIndex = index,
            QualifiedItemId = qualifiedItemId,
            Stack = stack
        };

    private static MaterialInventoryAccessPoint Access(
        string accessPointId,
        string nodeId,
        int capacity,
        int occupied,
        int free,
        bool authorized,
        bool locked) => new()
        {
            AccessPointId = accessPointId,
            NodeId = nodeId,
            AccessKind = "placed_chest",
            LocationId = "Farm",
            Capacity = capacity,
            OccupiedSlotCount = occupied,
            FreeSlotCount = free,
            ActorUseAuthorized = authorized,
            LockedByOtherPlayer = locked
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException(
                "Cannot find repository root.");
    }
}
