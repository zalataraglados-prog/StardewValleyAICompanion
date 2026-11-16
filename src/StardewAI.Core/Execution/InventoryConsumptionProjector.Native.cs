using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class InventoryConsumptionProjector
{
    public static string[] VerifyBefore(PreparedInventoryConsumption prepared, MaterialInventoryGraph observed) => Verify(prepared, observed, true);
    public static string[] VerifyAfter(PreparedInventoryConsumption prepared, MaterialInventoryGraph observed) => Verify(prepared, observed, false);

    private static string[] Verify(PreparedInventoryConsumption prepared, MaterialInventoryGraph observed, bool before)
    {
        if (observed.SchemaVersion != "material_inventory_graph.v1" || observed.Status != "available" ||
            observed.PlayerId != prepared.BeforeInventoryGraph.PlayerId || prepared.Projection.Status != "projected" ||
            observed.InventoryNodes is null || observed.InventoryNodes.Any(node => node is null || node.Slots is null || node.Slots.Any(slot => slot is null)) ||
            observed.AccessPoints is null || observed.AccessPoints.Any(row => row is null))
            return new[] { "inventory_consumption_native_graph_unavailable" };
        var errors = new List<string>();
        var player = "player:" + observed.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var output = ReadString(prepared.SourceRecipe, "output_qualified_item_id");
        var quality = ReadInt(prepared.SourceRecipe, "output_quality");
        var count = ReadInt(prepared.SourceRecipe, "output_count_per_craft");
        foreach (var original in prepared.BeforeInventoryGraph.InventoryNodes)
        {
            var nodes = observed.InventoryNodes.Where(node => node.NodeId == original.NodeId).ToArray();
            if (nodes.Length != 1 || !nodes[0].ActorUseAuthorized || nodes[0].SupplyState != "available" ||
                nodes[0].InventoryKind != original.InventoryKind || nodes[0].OwnerPlayerId != original.OwnerPlayerId ||
                nodes[0].LocationId != original.LocationId || nodes[0].TileX != original.TileX || nodes[0].TileY != original.TileY ||
                nodes[0].GlobalInventoryId != original.GlobalInventoryId || nodes[0].OwnershipClass != original.OwnershipClass ||
                nodes[0].Slots.Select(slot => slot.SlotIndex).Distinct().Count() != nodes[0].Slots.Length)
            { errors.Add("inventory_consumption_native_node_identity_changed"); continue; }
            var actual = nodes[0].Slots;
            var expected = prepared.Projection.NativeSlotTransitions.Where(row => row.NodeId == original.NodeId &&
                (before ? row.BeforeSlotIndex.HasValue : row.AfterSlotIndex.HasValue)).ToArray();
            var allowOutput = !before && original.NodeId == player;
            long outputUnits = 0;
            foreach (var slot in actual)
            {
                var row = expected.SingleOrDefault(value => (before ? value.BeforeSlotIndex : value.AfterSlotIndex) == slot.SlotIndex);
                var baseline = row is null ? 0 : before ? row.StackBefore : row.StackAfter;
                if (row is not null && (slot.QualifiedItemId != row.QualifiedItemId || slot.Quality != row.Quality || slot.RuntimeType != row.RuntimeType) ||
                    slot.Stack < baseline || slot.Stack <= 0 || slot.SlotIndex < 0 || string.IsNullOrWhiteSpace(slot.RuntimeType))
                { errors.Add("inventory_consumption_native_slot_mismatch"); continue; }
                var extra = (long)slot.Stack - baseline;
                if (extra > 0 && (!allowOutput || slot.QualifiedItemId != output || slot.Quality != quality))
                    errors.Add("inventory_consumption_unexpected_native_gain");
                outputUnits += extra;
            }
            if (expected.Any(row => actual.Count(slot => slot.SlotIndex == (before ? row.BeforeSlotIndex : row.AfterSlotIndex)) != 1) ||
                (!allowOutput && actual.Length != expected.Length) || (allowOutput && (count <= 0 || string.IsNullOrWhiteSpace(output) || outputUnits != count)))
                errors.Add("inventory_consumption_native_slot_mismatch");
        }
        foreach (var original in prepared.BeforeInventoryGraph.AccessPoints)
        {
            var actual = observed.AccessPoints.Where(row => row.AccessPointId == original.AccessPointId).ToArray();
            if (actual.Length != 1 || actual[0].NodeId != original.NodeId || actual[0].AccessKind != original.AccessKind ||
                actual[0].LocationId != original.LocationId || actual[0].TileX != original.TileX || actual[0].TileY != original.TileY ||
                actual[0].IsFridge != original.IsFridge || !actual[0].ActorUseAuthorized || actual[0].LockedByOtherPlayer)
                errors.Add("inventory_consumption_native_access_changed");
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }
}
