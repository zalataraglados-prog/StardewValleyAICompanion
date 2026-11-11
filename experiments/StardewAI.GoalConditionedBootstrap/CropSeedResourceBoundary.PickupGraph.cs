using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static (MaterialInventoryNode Before, MaterialInventoryNode After) VerifyPickupGraph(
        SnapshotEnvelope before, SnapshotEnvelope after, NativePickupInventorySummary pickup)
    {
        var hasBefore = MaterialTransferIntentBinder.TryReadGraph(before, out var first);
        var hasAfter = MaterialTransferIntentBinder.TryReadGraph(after, out var next);
        Require(hasBefore && hasAfter, "Pickup native material graphs are unavailable.");
        Require(long.TryParse(before.PlayerId.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actor) &&
                first!.PlayerId == actor && next!.PlayerId == actor, "Pickup graph actor differs from the original snapshot.");
        MaterialInventoryNode Player(MaterialInventoryGraph graph)
        {
            Require(graph.InventoryNodes.All(node => node is not null) && graph.InventoryNodes.Select(node => node.NodeId)
                    .Distinct(StringComparer.Ordinal).Count() == graph.InventoryNodes.Length,
                "Pickup graph node identities are invalid.");
            var players = graph.InventoryNodes.Where(node => node.InventoryKind == "player_inventory").ToArray();
            Require(players is [var player] && player.OwnerPlayerId == actor && player.ActorUseAuthorized &&
                    player.OwnershipClass == "actor_owned" && player.SupplyState == "available" &&
                    player.GlobalInventoryId.Length == 0, "Pickup player inventory authority is unavailable.");
            return players[0];
        }
        var priorPlayer = Player(first!); var player = Player(next!);
        Require(priorPlayer.NodeId == player.NodeId, "Pickup changed its player inventory identity.");
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(before, priorPlayer);
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(after, player);
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
    {
        var quantities = Array(graph["quantity_rows"], "pickup quantity rows");
        void VerifyAll(MaterialInventoryGraph source, JsonArray rows)
        {
            var keys = source.InventoryNodes.SelectMany(node => node.Slots).Where(slot => slot.Stack > 0)
                .Select(slot => (slot.QualifiedItemId, slot.Quality)).Distinct().ToHashSet();
            Require(rows.All(row => row is JsonObject) && rows.Count == keys.Count,
                "Pickup material aggregates omit or duplicate an inventory identity.");
            foreach (var row in rows.OfType<JsonObject>())
            {
                var id = row["qualified_item_id"]!.GetValue<string>(); var quality = Int(row, "quality");
                Require(keys.Remove((id, quality)), "Pickup material aggregate identity is invalid.");
                VerifySourceQuantity(row, source, id, quality);
            }
        }
        VerifyAll(first, quantities);
        foreach (var group in pickup.Slots.GroupBy(slot => (slot.QualifiedItemId, slot.Quality)))
        {
            var found = quantities.OfType<JsonObject>().Where(row => row["qualified_item_id"]?.GetValue<string>() ==
                group.Key.QualifiedItemId && Int(row, "quality") == group.Key.Quality).ToArray();
            var row = found.SingleOrDefault();
            if (row is null)
            {
                row = JsonSerializer.SerializeToNode(new MaterialQuantityRow { QualifiedItemId = group.Key.QualifiedItemId,
                    Quality = group.Key.Quality })!.AsObject();
                quantities.Add(row);
            }
            row["available_quantity"] = checked(Int(row, "available_quantity") + group.Sum(slot => slot.StackAfter - slot.StackBefore));
            row["source_slot_count"] = checked(Int(row, "source_slot_count") + group.Count(slot => slot.StackBefore == 0));
        }
        graph["quantity_rows"] = new JsonArray(quantities.OfType<JsonObject>()
            .OrderBy(row => row["qualified_item_id"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => Int(row, "quality")).Select(row => row.DeepClone()).ToArray());
        VerifyAll(next, graph["quantity_rows"]!.AsArray());
    }
}
