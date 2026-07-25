using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed class StorageInfrastructureCapacityProjection
{
    public StorageInfrastructureCapacityResult Project(
        StorageInfrastructureProjection storage,
        MaterialInventoryGraph graph)
    {
        var blockingReasons = new List<string>();
        if (!string.Equals(
                storage.SchemaVersion,
                "storage_infrastructure.v1",
                StringComparison.Ordinal))
        {
            blockingReasons.Add(
                "storage_infrastructure_schema_unsupported");
        }
        if (!string.Equals(
                graph.SchemaVersion,
                "material_inventory_graph.v1",
                StringComparison.Ordinal))
        {
            blockingReasons.Add(
                "storage_material_graph_schema_unsupported");
        }
        if (!string.Equals(
                storage.SourceGraphSchemaVersion,
                graph.SchemaVersion,
                StringComparison.Ordinal))
        {
            blockingReasons.Add(
                "storage_source_graph_schema_drift");
        }
        if (storage.SourceGraphPlayerId != graph.PlayerId)
        {
            blockingReasons.Add(
                "storage_source_graph_player_drift");
        }
        if (!string.Equals(
                storage.InventoryNodeReference,
                "farm.material_inventory_graph.inventory_nodes[node_id]",
                StringComparison.Ordinal) ||
            !string.Equals(
                storage.ContentDuplicationPolicy,
                "reference_canonical_material_graph_nodes",
                StringComparison.Ordinal))
        {
            blockingReasons.Add(
                "storage_canonical_node_reference_invalid");
        }
        if (!string.Equals(
                storage.Status,
                "available",
                StringComparison.Ordinal) ||
            !string.Equals(
                graph.Status,
                "available",
                StringComparison.Ordinal))
        {
            blockingReasons.Add(
                "storage_infrastructure_or_material_graph_unavailable");
        }

        var graphNodes = graph.InventoryNodes ??
            Array.Empty<MaterialInventoryNode>();
        var storageAccessPoints = storage.AccessPoints ??
            Array.Empty<MaterialInventoryAccessPoint>();
        var nodes = graphNodes
            .GroupBy(row => row.NodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        foreach (var duplicate in nodes.Where(
                     pair => pair.Key.Length == 0 ||
                         pair.Value.Length != 1))
        {
            blockingReasons.Add(
                duplicate.Key.Length == 0
                    ? "storage_material_graph_node_id_missing"
                    : "storage_material_graph_node_id_duplicate:" +
                    duplicate.Key);
        }

        if (storage.AccessPointCount != storageAccessPoints.Length ||
            storage.DistinctInventoryNodeCount != storageAccessPoints
                .Select(row => row.NodeId)
                .Distinct(StringComparer.Ordinal)
                .Count() ||
            storage.ActorAuthorizedAccessPointCount !=
                storageAccessPoints.Count(row =>
                    row.ActorUseAuthorized) ||
            storage.LockedAccessPointCount !=
                storageAccessPoints.Count(row =>
                    row.LockedByOtherPlayer) ||
            storage.RemovableEmptyAccessPointCount !=
                storageAccessPoints.Count(row =>
                    string.Equals(
                        row.RelocationStatus,
                        "native_remove_available_empty",
                        StringComparison.Ordinal)) ||
            storage.NonemptyShoveAccessPointCount !=
                storageAccessPoints.Count(row =>
                    string.Equals(
                        row.RelocationStatus,
                        "native_shove_available_nonempty",
                        StringComparison.Ordinal)))
        {
            blockingReasons.Add(
                "storage_infrastructure_summary_drift");
        }

        var accessGroups = storageAccessPoints
            .GroupBy(
                row => row.AccessPointId,
                StringComparer.Ordinal)
            .ToArray();
        foreach (var duplicate in accessGroups.Where(
                     group => string.IsNullOrWhiteSpace(group.Key) ||
                         group.Count() != 1))
        {
            blockingReasons.Add(
                string.IsNullOrWhiteSpace(duplicate.Key)
                    ? "storage_access_point_id_missing"
                    : "storage_access_point_id_duplicate:" +
                      duplicate.Key);
        }

        var rows = new List<StorageCapacityRow>();
        foreach (var access in accessGroups
                     .Where(group =>
                         !string.IsNullOrWhiteSpace(group.Key) &&
                         group.Count() == 1)
                     .Select(group => group.Single())
                     .OrderBy(
                         row => row.AccessPointId,
                         StringComparer.Ordinal))
        {
            if (access.AccessKind is not (
                    "placed_chest" or
                    "built_in_fridge"))
            {
                blockingReasons.Add(
                    "storage_access_kind_unsupported:" +
                    access.AccessPointId);
                continue;
            }
            if (!nodes.TryGetValue(
                    access.NodeId,
                    out var matches) ||
                matches.Length != 1)
            {
                blockingReasons.Add(
                    "storage_access_node_unresolved:" +
                    access.AccessPointId);
                continue;
            }

            var node = matches[0];
            var nodeSlots = node.Slots ??
                Array.Empty<MaterialInventorySlot>();
            if (nodeSlots.Any(slot =>
                    slot.SlotIndex < 0 ||
                    slot.SlotIndex >= node.Capacity ||
                    slot.Stack <= 0 ||
                    string.IsNullOrWhiteSpace(
                        slot.QualifiedItemId)) ||
                nodeSlots
                    .GroupBy(slot => slot.SlotIndex)
                    .Any(group => group.Count() != 1))
            {
                blockingReasons.Add(
                    "storage_material_graph_slot_invalid:" +
                    access.AccessPointId);
                continue;
            }
            var occupiedSlotCount = nodeSlots.Length;
            if (access.Capacity != node.Capacity)
            {
                blockingReasons.Add(
                    "storage_access_capacity_drift:" +
                    access.AccessPointId);
                continue;
            }
            if (access.OccupiedSlotCount != occupiedSlotCount ||
                access.FreeSlotCount != Math.Max(
                    0,
                    node.Capacity - occupiedSlotCount))
            {
                blockingReasons.Add(
                    "storage_access_slot_count_drift:" +
                    access.AccessPointId);
                continue;
            }
            if (!string.Equals(
                    access.GlobalInventoryId,
                    node.GlobalInventoryId,
                    StringComparison.Ordinal))
            {
                blockingReasons.Add(
                    "storage_access_global_inventory_drift:" +
                    access.AccessPointId);
                continue;
            }

            var immediatelyUsable =
                access.ActorUseAuthorized &&
                node.ActorUseAuthorized &&
                !access.LockedByOtherPlayer &&
                string.Equals(
                    node.SupplyState,
                    "available",
                    StringComparison.Ordinal);
            rows.Add(new StorageCapacityRow
            {
                AccessPointId = access.AccessPointId,
                NodeId = access.NodeId,
                LocationId = access.LocationId,
                LocationKind = access.LocationKind,
                TileX = access.TileX,
                TileY = access.TileY,
                Capacity = node.Capacity,
                OccupiedSlotCount = occupiedSlotCount,
                FreeStackSlotCount = Math.Max(
                    0,
                    node.Capacity - occupiedSlotCount),
                ImmediatelyUsable = immediatelyUsable,
                RelocationStatus = access.RelocationStatus
            });
        }

        return new StorageInfrastructureCapacityResult
        {
            Status = blockingReasons.Count == 0
                ? "available"
                : "blocked",
            Rows = rows.ToArray(),
            ImmediatelyUsableAccessPointCount = rows.Count(
                row => row.ImmediatelyUsable),
            ImmediatelyUsableFreeStackSlotCount = rows
                .Where(row => row.ImmediatelyUsable)
                .Sum(row => row.FreeStackSlotCount),
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reason => reason, StringComparer.Ordinal)
                .ToArray()
        };
    }
}

public sealed class StorageInfrastructureCapacityResult
{
    public string Status { get; set; } = "blocked";
    public StorageCapacityRow[] Rows { get; set; } =
        Array.Empty<StorageCapacityRow>();
    public int ImmediatelyUsableAccessPointCount { get; set; }
    public int ImmediatelyUsableFreeStackSlotCount { get; set; }
    public string[] BlockingReasons { get; set; } =
        Array.Empty<string>();
}

public sealed class StorageCapacityRow
{
    public string AccessPointId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationKind { get; set; } = string.Empty;
    public int? TileX { get; set; }
    public int? TileY { get; set; }
    public int Capacity { get; set; }
    public int OccupiedSlotCount { get; set; }
    public int FreeStackSlotCount { get; set; }
    public bool ImmediatelyUsable { get; set; }
    public string RelocationStatus { get; set; } = string.Empty;
}
