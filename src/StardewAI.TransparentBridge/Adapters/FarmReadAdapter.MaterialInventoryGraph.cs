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
    private static readonly Vector2[] WorkbenchChestOffsets =
    {
        new(-1f, 1f), new(0f, 1f), new(1f, 1f),
        new(-1f, 0f), new(1f, 0f),
        new(-1f, -1f), new(0f, -1f), new(1f, -1f)
    };

    private static MaterialInventoryGraph ReadMaterialInventoryGraph(Farm farm, Farmer player)
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
                    if (IsPlayerStorageChest(locationRef, chest, player))
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

        var workbenchLinks = ReadWorkbenchLinks(locations, chestNodeByTile);
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
            DeduplicatedAccessPointCount = Math.Max(0, accessRows.Length - distinctAccessNodes)
        };
    }

    private static bool IsPlayerStorageChest(MachineLocationRef locationRef, Chest chest, Farmer player)
    {
        if (!chest.playerChest.Value)
        {
            return false;
        }

        var ownerId = chest.owner.Value;
        return ownerId == 0 || ownerId == player.UniqueMultiplayerID || locationRef.IsPlayerControlled;
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
            chest.GetItemsForPlayer(player.UniqueMultiplayerID));

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
            LockedByOtherPlayer = chest.GetMutex().IsLocked() && !chest.GetMutex().IsLockHeld()
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
                autoGrabberChest.Items);
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
                LockedByOtherPlayer = false
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
            Capacity = slots.Length,
            Slots = slots
        };
    }

    private static MaterialWorkbenchLink[] ReadWorkbenchLinks(
        IEnumerable<MachineLocationRef> locations,
        IReadOnlyDictionary<string, string> chestNodeByTile)
    {
        return locations
            .SelectMany(locationRef => locationRef.Location.objects.Pairs
                .Where(pair => pair.Value is Workbench)
                .Select(pair =>
                {
                    var locationId = locationRef.Location.NameOrUniqueName;
                    var connectedNodeIds = WorkbenchChestOffsets
                        .Select(offset => pair.Key + offset)
                        .Where(tile => locationRef.Location.objects.TryGetValue(tile, out var value) &&
                            value is Chest chest &&
                            chest.SpecialChestType is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest)
                        .Select(tile => chestNodeByTile.TryGetValue(LocationTileKey(locationId, tile), out var nodeId)
                            ? nodeId
                            : string.Empty)
                        .Where(nodeId => nodeId.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
                        .ToArray();
                    return new MaterialWorkbenchLink
                    {
                        WorkbenchAccessPointId = NodeId("access", "workbench", locationId, TileText(pair.Key)),
                        LocationId = locationId,
                        TileX = (int)pair.Key.X,
                        TileY = (int)pair.Key.Y,
                        ConnectedNodeIds = connectedNodeIds
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
        IList<Item?> inventory)
    {
        if (nodes.ContainsKey(nodeId))
        {
            return;
        }

        nodes[nodeId] = new MaterialInventoryNode
        {
            NodeId = nodeId,
            InventoryKind = inventoryKind,
            SupplyState = supplyState,
            LocationId = locationId,
            TileX = tileX,
            TileY = tileY,
            OwnerPlayerId = ownerPlayerId,
            GlobalInventoryId = globalInventoryId,
            Capacity = capacity,
            Slots = ReadSlots(inventory)
        };
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
            AvailableQuantity = group.Where(row => row.Node.SupplyState == "available").Sum(row => row.Slot.Stack),
            ReadyOutputQuantity = group.Where(row => row.Node.SupplyState == "ready_output").Sum(row => row.Slot.Stack),
            InProcessQuantity = group.Where(row => row.Node.SupplyState == "in_process").Sum(row => row.Slot.Stack),
            SourceSlotCount = group.Count()
        })
        .OrderBy(row => row.QualifiedItemId, StringComparer.Ordinal)
        .ThenBy(row => row.Quality)
        .ToArray();

    private static string NodeId(params string[] parts) => string.Join(":", parts.Select(EscapeNodePart));

    private static string EscapeNodePart(string value) => value.Replace("%", "%25").Replace(":", "%3A");

    private static string TileText(Vector2 tile) => (int)tile.X + "," + (int)tile.Y;

    private static string LocationTileKey(string locationId, Vector2 tile) => locationId + "\n" + TileText(tile);
}
