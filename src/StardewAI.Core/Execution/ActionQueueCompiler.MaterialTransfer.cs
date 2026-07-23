using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidateMaterialTransferPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.transfer_material")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        if (!TryReadMaterialTransferGraph(snapshot, out var graph))
        {
            reasons.Add("material_transfer_graph_unavailable");
            return reasons.ToArray();
        }

        if (!TryBuildMaterialTransferIntent(action, out var intent))
        {
            reasons.Add("material_transfer_typed_intent_required");
            return reasons.ToArray();
        }

        var projection = new MaterialTransferProjector().Project(graph!, intent!);
        reasons.AddRange(projection.BlockingReasons);
        var access = graph!.AccessPoints
            .Where(row =>
                row.NodeId == (intent!.SourceNodeId.StartsWith("chest:", StringComparison.Ordinal)
                    ? intent.SourceNodeId
                    : intent.DestinationNodeId))
            .ToArray();
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var accessTileX = access.Length == 1 ? access[0].TileX : null;
        var accessTileY = access.Length == 1 ? access[0].TileY : null;
        if (!accessTileX.HasValue || !accessTileY.HasValue)
        {
            reasons.Add("material_transfer_chest_access_not_unique");
        }
        else if (!standX.HasValue ||
            !standY.HasValue ||
            Math.Abs(standX.Value - accessTileX.Value) +
            Math.Abs(standY.Value - accessTileY.Value) != 1)
        {
            reasons.Add("material_transfer_adjacent_stand_tile_required");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelActionParameter[] BuildMaterialTransferParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = new List<SmallModelActionParameter>(action.Parameters);
        parameters.RemoveAll(parameter =>
            string.Equals(parameter.Name, "material_transfer_intent_json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameter.Name, "material_transfer_projection_json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameter.Name, "location_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameter.Name, "target_tile_x", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameter.Name, "target_tile_y", StringComparison.OrdinalIgnoreCase));
        if (!TryReadMaterialTransferGraph(snapshot, out var graph) ||
            !TryBuildMaterialTransferIntent(action, out var intent))
        {
            return parameters.ToArray();
        }

        var projection = new MaterialTransferProjector().Project(graph!, intent!);
        if (projection.Status != "projected")
        {
            return parameters.ToArray();
        }
        var chestNodeId = graph!.InventoryNodes
            .FirstOrDefault(node =>
                node.NodeId == intent!.SourceNodeId &&
                node.InventoryKind == "chest" ||
                node.NodeId == intent.DestinationNodeId &&
                node.InventoryKind == "chest")
            ?.NodeId;
        var access = graph.AccessPoints.FirstOrDefault(row => row.NodeId == chestNodeId);
        if (access is not null)
        {
            AddParameterIfMissing(parameters, "location_id", access.LocationId);
            if (access.TileX.HasValue && access.TileY.HasValue)
            {
                AddParameterIfMissing(parameters, "target_tile_x", access.TileX.Value.ToString());
                AddParameterIfMissing(parameters, "target_tile_y", access.TileY.Value.ToString());
            }
        }

        AddParameterIfMissing(
            parameters,
            "material_transfer_intent_json",
            JsonSerializer.Serialize(intent));
        AddParameterIfMissing(
            parameters,
            "material_transfer_projection_json",
            JsonSerializer.Serialize(projection));
        AddParameterIfMissing(parameters, "max_movement_tiles", "512");
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileMaterialTransferStep(SmallModelAction action)
    {
        var source = ReadParameter(action, "source_node_id");
        var destination = ReadParameter(action, "destination_node_id");
        var quantity = ReadIntParameter(action, "quantity");
        if (string.IsNullOrWhiteSpace(source) ||
            string.IsNullOrWhiteSpace(destination) ||
            !quantity.HasValue ||
            quantity.Value <= 0)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "transfer_material",
                source + "->" + destination,
                "material_inventory_graph transfer=" + quantity.Value +
                ";native_chest_menu=true;source_and_destination_verified=true",
                Math.Max(120, quantity.Value * 2 + 120))
        };
    }

    private static bool TryReadMaterialTransferGraph(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph)
    {
        graph = null;
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "farm", "material_inventory_graph")))
        {
            return false;
        }

        var value = ReadStateFieldValue(snapshot, "farm", "material_inventory_graph");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(value.Value.GetRawText());
            return graph is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryBuildMaterialTransferIntent(
        SmallModelAction action,
        out MaterialTransferIntent? intent)
    {
        intent = null;
        var sourceNodeId = ReadParameter(action, "source_node_id");
        var destinationNodeId = ReadParameter(action, "destination_node_id");
        var sourceSlotIndex = ReadIntParameter(action, "source_slot_index");
        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        var quality = ReadIntParameter(action, "quality");
        var quantity = ReadIntParameter(action, "quantity");
        var expectedSourceStack = ReadIntParameter(action, "expected_source_stack");
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId) ||
            !sourceSlotIndex.HasValue ||
            string.IsNullOrWhiteSpace(qualifiedItemId) ||
            !quality.HasValue ||
            !quantity.HasValue ||
            !expectedSourceStack.HasValue)
        {
            return false;
        }

        intent = new MaterialTransferIntent
        {
            SourceNodeId = sourceNodeId,
            DestinationNodeId = destinationNodeId,
            SourceSlotIndex = sourceSlotIndex.Value,
            QualifiedItemId = qualifiedItemId,
            Quality = quality.Value,
            Quantity = quantity.Value,
            ExpectedSourceStack = expectedSourceStack.Value
        };
        return true;
    }
}
