using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionOpportunityCostSnapshotState
{
    private AcquisitionOpportunityCostSnapshotState(
        IReadOnlyDictionary<string, AcquisitionOpportunityMaterialSlot> slots,
        string[] blockingReasons)
    {
        Slots = slots;
        BlockingReasons = blockingReasons;
    }

    public IReadOnlyDictionary<string, AcquisitionOpportunityMaterialSlot> Slots
    {
        get;
    }

    public string[] BlockingReasons { get; }

    public bool MaterialEvidenceAvailable => BlockingReasons.Length == 0;

    public static AcquisitionOpportunityCostSnapshotState Read(
        JsonElement snapshot)
    {
        var reasons = new List<string>();
        var slots = new Dictionary<string, AcquisitionOpportunityMaterialSlot>(
            StringComparer.Ordinal);
        if (!TryMaterialGraph(snapshot, out var graph) ||
            !graph.TryGetProperty("inventory_nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array)
        {
            return Blocked("material_inventory_graph_missing_or_unavailable");
        }

        foreach (var node in nodes.EnumerateArray())
        {
            var nodeId = ReadString(node, "node_id");
            var supplyState = ReadString(node, "supply_state");
            var authorized = ReadBool(node, "actor_use_authorized");
            if (string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(supplyState) ||
                !authorized.HasValue ||
                !node.TryGetProperty("slots", out var nodeSlots) ||
                nodeSlots.ValueKind != JsonValueKind.Array)
            {
                reasons.Add("material_inventory_graph_node_invalid");
                continue;
            }

            foreach (var slot in nodeSlots.EnumerateArray())
            {
                var slotIndex = ReadInt(slot, "slot_index");
                var qualifiedItemId = ReadString(slot, "qualified_item_id");
                var stack = ReadInt(slot, "stack");
                var quality = ReadInt(slot, "quality");
                var salePrice = ReadInt(slot, "sale_price");
                if (!slotIndex.HasValue || slotIndex < 0 ||
                    string.IsNullOrWhiteSpace(qualifiedItemId) ||
                    !stack.HasValue || stack <= 0 ||
                    !quality.HasValue || quality < 0 ||
                    !salePrice.HasValue || salePrice < 0)
                {
                    reasons.Add("material_inventory_graph_cost_slot_invalid");
                    continue;
                }

                var key = SlotKey(nodeId, slotIndex.Value);
                if (!slots.TryAdd(key, new AcquisitionOpportunityMaterialSlot(
                        nodeId,
                        slotIndex.Value,
                        supplyState,
                        authorized.Value,
                        qualifiedItemId,
                        stack.Value,
                        quality.Value,
                        salePrice.Value)))
                {
                    reasons.Add("material_inventory_graph_cost_slot_duplicate:" +
                        key);
                }
            }
        }

        var distinct = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AcquisitionOpportunityCostSnapshotState(slots, distinct);
    }

    public static string SlotKey(string nodeId, int slotIndex) =>
        nodeId + "#" + slotIndex;

    private static bool TryMaterialGraph(
        JsonElement snapshot,
        out JsonElement graph)
    {
        graph = default;
        return snapshot.TryGetProperty("state", out var state) &&
            state.ValueKind == JsonValueKind.Object &&
            state.TryGetProperty("farm", out var farm) &&
            farm.ValueKind == JsonValueKind.Object &&
            farm.TryGetProperty("material_inventory_graph", out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() == "available" &&
            envelope.TryGetProperty("confidence", out var confidence) &&
            confidence.TryGetDouble(out var confidenceValue) &&
            confidenceValue == 1d &&
            envelope.TryGetProperty("value", out graph) &&
            graph.ValueKind == JsonValueKind.Object &&
            ReadString(graph, "schema_version") ==
                "material_inventory_graph.v1" &&
            ReadString(graph, "status") == "available";
    }

    private static string ReadString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static int? ReadInt(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var result)
            ? result
            : null;

    private static bool? ReadBool(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

    private static AcquisitionOpportunityCostSnapshotState Blocked(
        string reason) => new(
        new Dictionary<string, AcquisitionOpportunityMaterialSlot>(
            StringComparer.Ordinal),
        new[] { reason });
}

internal sealed record AcquisitionOpportunityMaterialSlot(
    string NodeId,
    int SlotIndex,
    string SupplyState,
    bool ActorUseAuthorized,
    string QualifiedItemId,
    int Stack,
    int Quality,
    int SalePrice);
