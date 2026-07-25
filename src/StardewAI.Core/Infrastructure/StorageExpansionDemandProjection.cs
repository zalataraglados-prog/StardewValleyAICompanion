using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static class StorageExpansionDemandProjection
{
    public static StorageExpansionDemandResult Evaluate(
        SnapshotEnvelope snapshot)
    {
        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "storage_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            !placement.Value.TryGetProperty("rows", out var inventoryRows) ||
            inventoryRows.ValueKind != JsonValueKind.Array)
        {
            return Blocked(
                "storage_inventory_placement_projection_unavailable");
        }

        var inventoryOrdinaryCount = inventoryRows
            .EnumerateArray()
            .Count(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadBool(row, "ordinary_material_storage"));
        if (inventoryOrdinaryCount > 0)
        {
            return new StorageExpansionDemandResult
            {
                Status = "available",
                DemandClass = "placement_pending",
                AcquisitionRequired = false,
                PlacementRequired = true,
                InventoryOrdinaryStorageCount =
                    inventoryOrdinaryCount
            };
        }

        var storageValue = ReadStateFieldValue(
            snapshot,
            "farm",
            "chests");
        var graphValue = ReadStateFieldValue(
            snapshot,
            "farm",
            "material_inventory_graph");
        if (!storageValue.HasValue || !graphValue.HasValue)
        {
            return Blocked(
                "storage_capacity_source_projection_unavailable");
        }

        StorageInfrastructureProjection? storage;
        MaterialInventoryGraph? graph;
        try
        {
            storage = JsonSerializer.Deserialize<
                StorageInfrastructureProjection>(
                storageValue.Value.GetRawText());
            graph = JsonSerializer.Deserialize<
                MaterialInventoryGraph>(
                graphValue.Value.GetRawText());
        }
        catch (JsonException)
        {
            return Blocked(
                "storage_capacity_source_projection_invalid");
        }
        if (storage is null || graph is null)
        {
            return Blocked(
                "storage_capacity_source_projection_invalid");
        }

        var capacity =
            new StorageInfrastructureCapacityProjection()
                .Project(storage, graph);
        if (!string.Equals(
                capacity.Status,
                "available",
                StringComparison.Ordinal))
        {
            return new StorageExpansionDemandResult
            {
                Status = "blocked",
                DemandClass = "blocked_capacity_projection",
                BlockingReasons = capacity.BlockingReasons
            };
        }

        var accessCount =
            capacity.ImmediatelyUsableOrdinaryAccessPointCount;
        var freeSlots =
            capacity.ImmediatelyUsableOrdinaryFreeStackSlotCount;
        return new StorageExpansionDemandResult
        {
            Status = "available",
            DemandClass = accessCount == 0
                ? "bootstrap_ordinary_storage"
                : freeSlots == 0
                    ? "ordinary_storage_capacity_exhausted"
                    : "ordinary_storage_capacity_available",
            AcquisitionRequired =
                accessCount == 0 || freeSlots == 0,
            PlacementRequired = false,
            InventoryOrdinaryStorageCount = 0,
            ImmediatelyUsableOrdinaryAccessPointCount =
                accessCount,
            ImmediatelyUsableOrdinaryFreeStackSlotCount =
                freeSlots
        };
    }

    private static StorageExpansionDemandResult Blocked(
        string reason) => new()
    {
        Status = "blocked",
        DemandClass = "blocked_incomplete_storage_projection",
        BlockingReasons = new[] { reason }
    };
}

public sealed class StorageExpansionDemandResult
{
    public string Status { get; set; } = "blocked";
    public string DemandClass { get; set; } =
        "blocked_incomplete_storage_projection";
    public bool AcquisitionRequired { get; set; }
    public bool PlacementRequired { get; set; }
    public int InventoryOrdinaryStorageCount { get; set; }
    public int ImmediatelyUsableOrdinaryAccessPointCount {
        get; set;
    }
    public int ImmediatelyUsableOrdinaryFreeStackSlotCount {
        get; set;
    }
    public string[] BlockingReasons { get; set; } =
        Array.Empty<string>();
}
