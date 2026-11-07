using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    private sealed partial class Fixture
    {
        private void CompleteNativeEchoes(JsonObject farm, JsonObject actor, MaterialInventoryGraph graph)
        {
            graph.InventoryNodes[0].Slots = graph.InventoryNodes[0].Slots.Append(new MaterialInventorySlot
            {
                SlotIndex = 3, QualifiedItemId = "(T)Pickaxe", RuntimeType = "StardewValley.Tools.Pickaxe",
                MaximumStackSize = 1, Stack = 1
            }).ToArray();
            foreach (var slot in graph.InventoryNodes.SelectMany(row => row.Slots))
            {
                slot.ItemId = slot.QualifiedItemId[(slot.QualifiedItemId.IndexOf(')') + 1)..];
                slot.DisplayName = "fixture:" + slot.QualifiedItemId;
            }
            graph.QuantityRows = graph.InventoryNodes.SelectMany(row => row.Slots).GroupBy(row => (row.QualifiedItemId, row.Quality))
                .Select(rows => new MaterialQuantityRow { QualifiedItemId = rows.Key.QualifiedItemId, Quality = rows.Key.Quality,
                    AvailableQuantity = rows.Sum(row => row.Stack), SourceSlotCount = rows.Count() }).ToArray();
            foreach (var access in graph.AccessPoints) access.RelocationHeavyToolSlotIndices = new[] { 3 };
            farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph, JsonOptions);
            ApplyNativeEchoes(farm, actor);
        }

        private static void ApplyNativeEchoes(JsonObject farm, JsonObject actor)
        {
            var graph = farm["material_inventory_graph"]!["value"]!;
            var nodes = graph["inventory_nodes"]!.AsArray().OfType<JsonObject>().ToArray();
            var accesses = graph["access_points"]!.AsArray().OfType<JsonObject>().ToArray();
            foreach (var access in accesses)
            {
                var node = nodes.Single(row => row["node_id"]!.GetValue<string>() == access["node_id"]!.GetValue<string>());
                var count = node["slots"]!.AsArray().Count;
                access["occupied_slot_count"] = count;
                access["free_slot_count"] = Math.Max(0, node["capacity"]!.GetValue<int>() - count);
                access["relocation_status"] = count == 0 ? "native_remove_available_empty" : "native_shove_available_nonempty";
                access["native_hit_behavior"] = count == 0 ? "empty_heavy_tool_remove_and_drop_chest_item" : "nonempty_heavy_tool_second_hit_or_hold_shove";
            }
            foreach (var row in graph["quantity_rows"]!.AsArray().OfType<JsonObject>())
                row["source_slot_count"] = nodes.SelectMany(node => node["slots"]!.AsArray()).Count(slot =>
                    slot!["qualified_item_id"]!.GetValue<string>() == row["qualified_item_id"]!.GetValue<string>() &&
                    slot["quality"]!.GetValue<int>() == row["quality"]!.GetValue<int>());
            var player = nodes.Single(row => row["inventory_kind"]!.GetValue<string>() == "player_inventory");
            var slots = player["slots"]!.AsArray().OfType<JsonObject>().ToArray();
            var capacity = player["capacity"]!.GetValue<int>();
            var inventory = new JsonArray();
            for (var index = 0; index < capacity; index++)
            {
                var slot = slots.SingleOrDefault(row => row["slot_index"]!.GetValue<int>() == index);
                inventory.Add(JsonSerializer.SerializeToNode(new
                {
                    slot_index = index, item_id = slot?["item_id"]?.GetValue<string>(),
                    qualified_item_id = slot?["qualified_item_id"]?.GetValue<string>(), display_name = slot?["display_name"]?.GetValue<string>(),
                    stack = slot?["stack"]?.GetValue<int>(), quality = slot?["quality"]?.GetValue<int>(),
                    maximum_stack_size = slot?["maximum_stack_size"]?.GetValue<int>(), runtime_type = slot?["runtime_type"]?.GetValue<string>(),
                    sale_price = slot?["sale_price"]?.GetValue<int>(), is_empty = slot is null
                }, JsonOptions));
            }
            actor["inventory"] = NativeField(inventory);
            actor["inventory_capacity"] = NativeField(new
            {
                max_items = capacity, occupied_item_stacks = slots.Length, empty_slots = capacity - slots.Length,
                has_empty_slot = capacity > slots.Length
            });
            actor["seed_inventory"] = NativeField(slots.Where(row => row["qualified_item_id"]!.GetValue<string>() == "(O)745").Select(row => new
            {
                slot_index = row["slot_index"]!.GetValue<int>(), item_id = "745", qualified_item_id = "(O)745",
                display_name = row["display_name"]!.GetValue<string>(), stack = row["stack"]!.GetValue<int>(),
                quality = row["quality"]!.GetValue<int>(), seed_id = "745", crop_catalog_match = true
            }).ToArray());
            farm["chests"] = NativeField(new StorageInfrastructureProjection
            {
                SourceGraphPlayerId = 123, AccessPoints = accesses.Select(row => row.Deserialize<MaterialInventoryAccessPoint>(JsonOptions)!).ToArray(),
                AccessPointCount = accesses.Length, DistinctInventoryNodeCount = accesses.Length,
                ActorAuthorizedAccessPointCount = accesses.Length,
                RemovableEmptyAccessPointCount = accesses.Count(row => row["occupied_slot_count"]!.GetValue<int>() == 0),
                NonemptyShoveAccessPointCount = accesses.Count(row => row["occupied_slot_count"]!.GetValue<int>() > 0)
            });
        }

        private static JsonObject NativeField(object value) => JsonSerializer.SerializeToNode(new
        {
            status = "available", confidence = 1d, value, read_at_tick = 1,
            source = new { kind = "game_object", path = "fixture.native.transfer" }, adapter = "test"
        }, JsonOptions)!.AsObject();
    }
}
