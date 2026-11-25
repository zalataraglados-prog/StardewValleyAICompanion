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
    private static string? OrdinaryClearanceToolBlockReason(
        SmallModelAction action, SnapshotEnvelope snapshot, string clearKind)
    {
        if (!TryReadOrdinaryClearanceTools(snapshot, out var tools))
            return "clear_obstacle_inventory_projection_unavailable";

        var compatible = tools.Where(tool => clearKind == "weeds" || tool.Kind == "pickaxe").ToArray();
        if (compatible.Length == 0)
            return "clear_obstacle_matching_tool_missing:" + clearKind;

        var requestedSlot = ReadParameter(action, "tool_slot_index");
        if (requestedSlot is not null)
        {
            if (!int.TryParse(requestedSlot, out var slot) ||
                !compatible.Any(tool => tool.SlotIndex == slot))
                return "clear_obstacle_tool_slot_incompatible";
            compatible = compatible.Where(tool => tool.SlotIndex == slot).ToArray();
        }
        var requestedKind = ReadParameter(action, "required_tool_kind");
        return requestedKind is not null && !compatible.Any(tool => tool.Kind == requestedKind)
            ? "clear_obstacle_required_tool_kind_incompatible"
            : null;
    }

    private static NativeOrdinaryClearanceTool? SelectOrdinaryClearanceTool(
        SnapshotEnvelope snapshot, string clearKind)
    {
        if (clearKind is not ("weeds" or "stone") ||
            !TryReadOrdinaryClearanceTools(snapshot, out var tools))
            return null;
        return tools.Where(tool => clearKind == "weeds" || tool.Kind == "pickaxe")
            .OrderBy(tool => clearKind == "weeds" && tool.Kind == "scythe" ? 0 : 1)
            .ThenBy(tool => tool.SlotIndex)
            .FirstOrDefault();
    }

    private static bool TryReadOrdinaryClearanceTools(
        SnapshotEnvelope snapshot, out NativeOrdinaryClearanceTool[] tools)
    {
        tools = Array.Empty<NativeOrdinaryClearanceTool>();
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "inventory")) ||
            !inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            return false;

        var result = new List<NativeOrdinaryClearanceTool>();
        var slots = new HashSet<int>();
        foreach (var row in inventory.Value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) return false;
            var slot = NullableReadInt(row, "slot_index");
            if (!slot.HasValue || slot.Value < 0 || !slots.Add(slot.Value)) return false;
            if (row.TryGetProperty("is_empty", out var empty) && empty.ValueKind == JsonValueKind.True) continue;
            if (NullableReadInt(row, "stack") != 1) continue;
            var id = ReadString(row, "qualified_item_id");
            var kind = ReadString(row, "runtime_type") switch
            {
                "StardewValley.Tools.Pickaxe" when IsVanillaTieredToolId(id, "Pickaxe") => "pickaxe",
                "StardewValley.Tools.Axe" when IsVanillaTieredToolId(id, "Axe") => "axe",
                "StardewValley.Tools.Hoe" when IsVanillaTieredToolId(id, "Hoe") => "hoe",
                "StardewValley.Tools.MeleeWeapon" when id.StartsWith("(W)", StringComparison.Ordinal) =>
                    id is "(W)47" or "(W)53" or "(W)66" ? "scythe" : "melee_weapon",
                _ => string.Empty
            };
            // Locked vanilla 1.6.15: Tool.isHeavyHitter is true for exactly
            // MeleeWeapon/Hoe/Axe/Pickaxe; Object weeds require that predicate,
            // while ordinary breakable stone specifically requires Pickaxe.
            if (kind.Length > 0) result.Add(new(slot.Value, kind));
        }
        tools = result.ToArray();
        return true;
    }

    private static bool IsVanillaTieredToolId(string id, string toolName) =>
        id == "(T)" + toolName || id == "(T)Copper" + toolName ||
        id == "(T)Steel" + toolName || id == "(T)Gold" + toolName ||
        id == "(T)Iridium" + toolName;

    private sealed class NativeOrdinaryClearanceTool
    {
        public NativeOrdinaryClearanceTool(int slotIndex, string kind)
        {
            SlotIndex = slotIndex;
            Kind = kind;
        }
        public int SlotIndex { get; }
        public string Kind { get; }
    }
}
