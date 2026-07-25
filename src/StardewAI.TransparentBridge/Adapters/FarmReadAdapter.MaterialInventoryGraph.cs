using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    internal static readonly Vector2[] WorkbenchChestOffsets =
    {
        new(-1f, 1f), new(0f, 1f), new(1f, 1f),
        new(-1f, 0f), new(1f, 0f),
        new(-1f, -1f), new(0f, -1f), new(1f, -1f)
    };

    internal static MaterialInventoryGraph ReadMaterialInventoryGraph(Farm farm, Farmer player)
    {
        var nodes = new Dictionary<string, MaterialInventoryNode>(StringComparer.Ordinal);
        var accessPoints = new List<MaterialInventoryAccessPoint>();
        var chestNodeByTile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var locations = MachineLocationTopology.ReadPersistentLocations(farm, player);

        AddInventoryNode(
            nodes,
            NodeId("player", player.UniqueMultiplayerID.ToString()),
            "player_inventory",
            "available",
            Game1.currentLocation?.NameOrUniqueName ?? string.Empty,
            null,
            null,
            player.UniqueMultiplayerID,
            string.Empty,
            player.MaxItems,
            player.Items);

        foreach (var locationRef in locations)
        {
            var location = locationRef.Location;
            foreach (var pair in location.objects.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
            {
                if (pair.Value is Chest chest)
                {
                    if (chest.playerChest.Value)
                    {
                        AddChestAccess(nodes, accessPoints, chestNodeByTile, locationRef, pair.Key, chest, player, "placed_chest");
                    }
                    continue;
                }

                if (locationRef.IsPlayerControlled || pair.Value.owner.Value == player.UniqueMultiplayerID)
                {
                    AddObjectBuffer(nodes, accessPoints, locationRef, pair.Key, pair.Value, player);
                }
            }

            var fridge = location.GetFridge(onlyUnlocked: true);
            var fridgeTile = location.GetFridgePosition();
            if (location is FarmHouse farmHouse && farmHouse.IsOwnedByCurrentPlayer &&
                fridge is not null && fridgeTile.HasValue)
            {
                AddChestAccess(
                    nodes,
                    accessPoints,
                    chestNodeByTile,
                    locationRef,
                    fridgeTile.Value.ToVector2(),
                    fridge,
                    player,
                    "built_in_fridge");
            }
        }

        var workbenchLinks = ReadWorkbenchLinks(locations, chestNodeByTile, nodes);
        var nodeRows = nodes.Values.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
        var accessRows = accessPoints.OrderBy(row => row.AccessPointId, StringComparer.Ordinal).ToArray();
        var distinctAccessNodes = accessRows.Select(row => row.NodeId).Distinct(StringComparer.Ordinal).Count();
        return new MaterialInventoryGraph
        {
            PlayerId = player.UniqueMultiplayerID,
            InventoryNodes = nodeRows,
            AccessPoints = accessRows,
            WorkbenchLinks = workbenchLinks,
            QuantityRows = BuildQuantityRows(nodeRows),
            PhysicalInventoryCount = nodeRows.Length,
            AccessPointCount = accessRows.Length,
            DeduplicatedAccessPointCount = Math.Max(0, accessRows.Length - distinctAccessNodes),
            DefaultSharedResourcePolicy = "deny_without_explicit_authorization"
        };
    }

    private static void AddChestAccess(
        IDictionary<string, MaterialInventoryNode> nodes,
        ICollection<MaterialInventoryAccessPoint> accessPoints,
        IDictionary<string, string> chestNodeByTile,
        MachineLocationRef locationRef,
        Vector2 tile,
        Chest chest,
        Farmer player,
        string accessKind)
    {
        var locationId = locationRef.Location.NameOrUniqueName;
        var globalInventoryId = ResolveGlobalInventoryId(chest);
        var nodeId = globalInventoryId.Length > 0
            ? NodeId("global", globalInventoryId)
            : NodeId("chest", locationId, TileText(tile));
        var supplyState = chest.SpecialChestType == Chest.SpecialChestTypes.MiniShippingBin
            ? "shipping_pending"
            : "available";
        var inventoryKind = accessKind == "built_in_fridge" || chest.fridge.Value
            ? "fridge"
            : globalInventoryId.Length > 0
                ? "global_chest_inventory"
                : "chest";
        var ownership = ClassifyMaterialOwnership(
            chest.owner.Value,
            player.UniqueMultiplayerID,
            globalInventoryId,
            actorOwnedContext: accessKind == "built_in_fridge");
        AddInventoryNode(
            nodes,
            nodeId,
            inventoryKind,
            supplyState,
            locationId,
            (int)tile.X,
            (int)tile.Y,
            chest.owner.Value,
            globalInventoryId,
            chest.GetActualCapacity(),
            chest.GetItemsForPlayer(player.UniqueMultiplayerID),
            ownership);

        var accessPointId = NodeId("access", accessKind, locationId, TileText(tile));
        accessPoints.Add(new MaterialInventoryAccessPoint
        {
            AccessPointId = accessPointId,
            NodeId = nodeId,
            AccessKind = accessKind,
            LocationId = locationId,
            LocationKind = locationRef.Kind,
            TileX = (int)tile.X,
            TileY = (int)tile.Y,
            QualifiedItemId = chest.QualifiedItemId,
            SpecialChestType = chest.SpecialChestType.ToString(),
            LockedByOtherPlayer = chest.GetMutex().IsLocked() && !chest.GetMutex().IsLockHeld(),
            ActorUseAuthorized = ownership.ActorUseAuthorized
        });
        chestNodeByTile[LocationTileKey(locationId, tile)] = nodeId;
    }

    private static string ResolveGlobalInventoryId(Chest chest)
    {
        if (!string.IsNullOrWhiteSpace(chest.GlobalInventoryId))
        {
            return chest.GlobalInventoryId;
        }

        return chest.SpecialChestType == Chest.SpecialChestTypes.JunimoChest
            ? FarmerTeam.GlobalInventoryId_JunimoChest
            : string.Empty;
    }

    private static void AddObjectBuffer(
        IDictionary<string, MaterialInventoryNode> nodes,
        ICollection<MaterialInventoryAccessPoint> accessPoints,
        MachineLocationRef locationRef,
        Vector2 tile,
        StardewObject item,
        Farmer player)
    {
        var locationId = locationRef.Location.NameOrUniqueName;
        if (item.QualifiedItemId == "(BC)165" && item.heldObject.Value is Chest autoGrabberChest)
        {
            var nodeId = NodeId("auto_grabber", locationId, TileText(tile));
            AddInventoryNode(
                nodes,
                nodeId,
                "auto_grabber",
                "available",
                locationId,
                (int)tile.X,
                (int)tile.Y,
                item.owner.Value,
                string.Empty,
                autoGrabberChest.GetActualCapacity(),
                autoGrabberChest.Items,
                ClassifyMaterialOwnership(
                    item.owner.Value,
                    player.UniqueMultiplayerID,
                    string.Empty));
            accessPoints.Add(new MaterialInventoryAccessPoint
            {
                AccessPointId = NodeId("access", "auto_grabber", locationId, TileText(tile)),
                NodeId = nodeId,
                AccessKind = "auto_grabber",
                LocationId = locationId,
                LocationKind = locationRef.Kind,
                TileX = (int)tile.X,
                TileY = (int)tile.Y,
                QualifiedItemId = item.QualifiedItemId,
                LockedByOtherPlayer = false,
                ActorUseAuthorized = item.owner.Value == player.UniqueMultiplayerID
            });
            return;
        }

        var heldItem = item.heldObject.Value;
        if (heldItem is null || item.GetMachineData() is null)
        {
            return;
        }

        var supplyState = item.readyForHarvest.Value ? "ready_output" : "in_process";
        var nodeIdForBuffer = NodeId("machine_buffer", locationId, TileText(tile));
        var slots = heldItem is Chest internalChest
            ? ReadSlots(internalChest.Items)
            : new[] { ReadSlot(heldItem, 0) };
        nodes[nodeIdForBuffer] = new MaterialInventoryNode
        {
            NodeId = nodeIdForBuffer,
            InventoryKind = heldItem is Chest ? "machine_internal_inventory" : "machine_buffer",
            SupplyState = supplyState,
            LocationId = locationId,
            TileX = (int)tile.X,
            TileY = (int)tile.Y,
            OwnerPlayerId = item.owner.Value == 0 ? player.UniqueMultiplayerID : item.owner.Value,
            OwnershipClass = item.owner.Value == 0 || item.owner.Value == player.UniqueMultiplayerID
                ? "actor_owned"
                : "other_player_owned",
            ActorUseAuthorized = item.owner.Value == 0 || item.owner.Value == player.UniqueMultiplayerID,
            Capacity = slots.Length,
            Slots = slots
        };
    }

    private static MaterialWorkbenchLink[] ReadWorkbenchLinks(
        IEnumerable<MachineLocationRef> locations,
        IReadOnlyDictionary<string, string> chestNodeByTile,
        IReadOnlyDictionary<string, MaterialInventoryNode> nodes)
    {
        return locations
            .SelectMany(locationRef => locationRef.Location.objects.Pairs
                .Where(pair => pair.Value is Workbench)
                .Select(pair =>
                {
                    var locationId = locationRef.Location.NameOrUniqueName;
                    var workbench = (Workbench)pair.Value;
                    var nativeChests = WorkbenchChestOffsets
                        .Select(offset => pair.Key + offset)
                        .Where(tile => locationRef.Location.objects.TryGetValue(tile, out var value) &&
                            value is Chest chest &&
                            chest.SpecialChestType is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest)
                        .Select(tile => new
                        {
                            Tile = tile,
                            Chest = (Chest)locationRef.Location.objects[tile],
                            NodeId = chestNodeByTile.TryGetValue(LocationTileKey(locationId, tile), out var nodeId)
                                ? nodeId
                                : string.Empty
                        })
                        .ToArray();
                    var blockingReasons = new List<string>();
                    if (nativeChests.Any(row => row.NodeId.Length == 0))
                    {
                        blockingReasons.Add("workbench_native_container_not_owned_or_unmapped");
                    }
                    if (nativeChests.Any(row =>
                        row.NodeId.Length > 0 &&
                        (!nodes.TryGetValue(row.NodeId, out var node) || !node.ActorUseAuthorized)))
                    {
                        blockingReasons.Add("workbench_native_container_not_actor_authorized");
                    }
                    if (nativeChests.Any(row => row.Chest.GetMutex().IsLocked() && !row.Chest.GetMutex().IsLockHeld()))
                    {
                        blockingReasons.Add("workbench_native_container_locked_by_other_player");
                    }
                    if (workbench.mutex.IsLocked() && !workbench.mutex.IsLockHeld())
                    {
                        blockingReasons.Add("workbench_locked_by_other_player");
                    }
                    var nativeContainerNodeIds = nativeChests
                        .Select(row => row.NodeId)
                        .Where(nodeId => nodeId.Length > 0)
                        .ToArray();
                    var connectedNodeIds = nativeContainerNodeIds
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
                        .ToArray();
                    return new MaterialWorkbenchLink
                    {
                        WorkbenchAccessPointId = NodeId("access", "workbench", locationId, TileText(pair.Key)),
                        LocationId = locationId,
                        TileX = (int)pair.Key.X,
                        TileY = (int)pair.Key.Y,
                        ConnectedNodeIds = connectedNodeIds,
                        NativeContainerNodeIds = nativeContainerNodeIds,
                        ProjectionStatus = blockingReasons.Count == 0
                            ? "exact_native_container_order"
                            : "blocked_native_container_ownership_or_lock",
                        BlockingReasons = blockingReasons.ToArray(),
                        LockedByOtherPlayer = blockingReasons.Any(reason =>
                            reason.EndsWith("locked_by_other_player", StringComparison.Ordinal))
                    };
                }))
            .OrderBy(row => row.WorkbenchAccessPointId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddInventoryNode(
        IDictionary<string, MaterialInventoryNode> nodes,
        string nodeId,
        string inventoryKind,
        string supplyState,
        string locationId,
        int? tileX,
        int? tileY,
        long ownerPlayerId,
        string globalInventoryId,
        int capacity,
        IList<Item?> inventory,
        MaterialOwnership? ownership = null)
    {
        if (nodes.ContainsKey(nodeId))
        {
            return;
        }

        var effectiveOwnership = ownership ??
            new MaterialOwnership(
                ownerPlayerId == Game1.player.UniqueMultiplayerID ? "actor_owned" : "other_player_owned",
                ownerPlayerId == Game1.player.UniqueMultiplayerID);
        nodes[nodeId] = new MaterialInventoryNode
        {
            NodeId = nodeId,
            InventoryKind = inventoryKind,
            SupplyState = supplyState,
            LocationId = locationId,
            TileX = tileX,
            TileY = tileY,
            OwnerPlayerId = ownerPlayerId,
            OwnershipClass = effectiveOwnership.OwnershipClass,
            ActorUseAuthorized = effectiveOwnership.ActorUseAuthorized,
            GlobalInventoryId = globalInventoryId,
            Capacity = capacity,
            Slots = ReadSlots(inventory)
        };
    }

    private static MaterialOwnership ClassifyMaterialOwnership(
        long ownerPlayerId,
        long actorPlayerId,
        string globalInventoryId,
        bool actorOwnedContext = false)
    {
        if (!string.IsNullOrWhiteSpace(globalInventoryId))
        {
            return new MaterialOwnership("shared_team_global", false);
        }

        if (actorOwnedContext || ownerPlayerId == actorPlayerId)
        {
            return new MaterialOwnership("actor_owned", true);
        }

        return ownerPlayerId == 0
            ? new MaterialOwnership("shared_unowned", false)
            : new MaterialOwnership("other_player_owned", false);
    }

    private static MaterialInventorySlot[] ReadSlots(IList<Item?> inventory) => inventory
        .Select((item, index) => item is null ? null : ReadSlot(item, index))
        .Where(slot => slot is not null)
        .Cast<MaterialInventorySlot>()
        .ToArray();

    private static MaterialInventorySlot ReadSlot(Item item, int index) => new()
    {
        SlotIndex = index,
        ItemId = item.ItemId,
        QualifiedItemId = item.QualifiedItemId,
        DisplayName = item.DisplayName,
        RuntimeType = item.GetType().FullName ?? string.Empty,
        Stack = item.Stack,
        MaximumStackSize = item.maximumStackSize(),
        Quality = item.Quality,
        SalePrice = item.salePrice()
    };

    private static MaterialQuantityRow[] BuildQuantityRows(IEnumerable<MaterialInventoryNode> nodes) => nodes
        .SelectMany(node => node.Slots.Select(slot => new { Node = node, Slot = slot }))
        .Where(row => row.Slot.Stack > 0 && row.Slot.QualifiedItemId.Length > 0)
        .GroupBy(row => (row.Slot.QualifiedItemId, row.Slot.Quality))
        .Select(group => new MaterialQuantityRow
        {
            QualifiedItemId = group.Key.QualifiedItemId,
            Quality = group.Key.Quality,
            AvailableQuantity = group.Where(row =>
                row.Node.SupplyState == "available" &&
                row.Node.ActorUseAuthorized).Sum(row => row.Slot.Stack),
            ReadyOutputQuantity = group.Where(row =>
                row.Node.SupplyState == "ready_output" &&
                row.Node.ActorUseAuthorized).Sum(row => row.Slot.Stack),
            InProcessQuantity = group.Where(row =>
                row.Node.SupplyState == "in_process" &&
                row.Node.ActorUseAuthorized).Sum(row => row.Slot.Stack),
            RestrictedQuantity = group.Where(row =>
                !row.Node.ActorUseAuthorized).Sum(row => row.Slot.Stack),
            SourceSlotCount = group.Count()
        })
        .OrderBy(row => row.QualifiedItemId, StringComparer.Ordinal)
        .ThenBy(row => row.Quality)
        .ToArray();

    private static string NodeId(params string[] parts) => string.Join(":", parts.Select(EscapeNodePart));

    private static string EscapeNodePart(string value) => value.Replace("%", "%25").Replace(":", "%3A");

    private static string TileText(Vector2 tile) => (int)tile.X + "," + (int)tile.Y;

    private static string LocationTileKey(string locationId, Vector2 tile) => locationId + "\n" + TileText(tile);

    private sealed record MaterialOwnership(string OwnershipClass, bool ActorUseAuthorized);
}
