using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static (MaterialInventoryNode Before, MaterialInventoryNode After) VerifyPickupGraph(
        SnapshotEnvelope before, SnapshotEnvelope after, NativePickupInventorySummary pickup)
    {
        var (first, next, priorPlayer, player) = ReadStockGraphs(before, after);
        var priorSlots = PickupGraphSlots(before, pickup.Payload.Before, priorPlayer);
        var nextSlots = PickupGraphSlots(after, pickup.Payload.After, player);
        var envelope = Field(before, "farm", "material_inventory_graph")!;
        var expected = Value(envelope).AsObject();
        var rawPlayer = Unique(Array(expected["inventory_nodes"], "pickup nodes"), "node_id", priorPlayer.NodeId);
        var slots = Array(rawPlayer["slots"], "pickup player slots");
        var projected = new JsonArray();
        foreach (var row in nextSlots.OrderBy(row => row.Key))
        {
            var value = priorSlots.ContainsKey(row.Key) ? Slot(slots, row.Key).DeepClone().AsObject() : row.Value.DeepClone().AsObject();
            value["stack"] = player.Slots.Single(slot => slot.SlotIndex == row.Key).Stack;
            projected.Add(value);
        }
        rawPlayer["slots"] = projected;
        ProjectPickupQuantities(expected, first!, next!, pickup);
        EqualField(envelope, Field(after, "farm", "material_inventory_graph"), "pickup material graph");
        return (priorPlayer, player);
    }

    private static Dictionary<int, JsonObject> PickupGraphSlots(SnapshotEnvelope snapshot,
        NativeObservedInventoryPayloadState payload, MaterialInventoryNode player)
    {
        var raw = Rows(Value(Field(snapshot, "player", "inventory")!));
        var graph = Value(Field(snapshot, "farm", "material_inventory_graph")!);
        var slots = Rows(Unique(Array(graph["inventory_nodes"], "pickup graph nodes"), "node_id", player.NodeId)["slots"]!);
        var projected = new Dictionary<int, JsonObject>();
        foreach (var slot in player.Slots)
        {
            var row = raw[slot.SlotIndex];
            var value = new JsonObject { ["slot_index"] = slot.SlotIndex };
            foreach (var name in new[] { "item_id", "qualified_item_id", "display_name", "runtime_type", "context_tags",
                         "stack", "maximum_stack_size", "quality", "sale_price" })
            {
                Require(row.ContainsKey(name) && row[name] is not null, "Pickup raw graph projection is unavailable: " + name);
                value[name] = row[name]!.DeepClone();
            }
            Require(row["is_object"] is JsonValue objectValue && objectValue.TryGetValue<bool>(out _),
                "Pickup raw object classification is unavailable.");
            var isObject = row["is_object"]!.GetValue<bool>();
            value["context_tags_projection_status"] = "exact_item_get_context_tags";
            value["edibility_projection_status"] = isObject ? "exact_object_edibility" : "not_applicable_non_object_item";
            if (isObject)
            {
                var xml = XElement.Parse(payload.Slots.Single(item => item.SlotIndex == slot.SlotIndex).Xml ??
                    throw new InvalidDataException("Pickup original object XML is unavailable."));
                var fields = xml.Elements("edibility").ToArray();
                Require(fields is [var field] && !field.HasElements && int.TryParse(field.Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out _), "Pickup original object edibility is unavailable.");
                value["edibility"] = int.Parse(fields[0].Value, CultureInfo.InvariantCulture);
            }
            else value["edibility"] = null;
            Require(slots.TryGetValue(slot.SlotIndex, out var actual) && value.All(property =>
                    actual.ContainsKey(property.Key) && JsonNode.DeepEquals(property.Value, actual[property.Key])),
                "Pickup material slot metadata differs from original raw inventory/persisted item.");
            projected.Add(slot.SlotIndex, value);
        }
        return projected;
    }

    private static void ProjectPickupQuantities(JsonObject graph, MaterialInventoryGraph first,
        MaterialInventoryGraph next, NativePickupInventorySummary pickup)
        => ProjectStockQuantities(graph, first, next, pickup.Slots.Select(slot =>
            new PhysicalStockDelta(slot.SlotIndex, slot.QualifiedItemId, slot.Quality, slot.StackBefore, slot.StackAfter)));
}
