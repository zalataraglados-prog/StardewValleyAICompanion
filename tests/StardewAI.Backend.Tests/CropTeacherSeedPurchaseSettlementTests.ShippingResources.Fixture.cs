using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class ShippingResourcesFixture : IDisposable
    {
        private readonly OwnedRouteFixture shared = new(prepareSource: HarvestFixture.PrepareSource);
        internal SnapshotEnvelope Before { get; }
        internal SnapshotEnvelope After { get; }
        internal ActionQueueEnvelope Queue { get; }
        internal QueueExecutionReceiptEnvelope Receipt { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; set; } = null!;
        internal string Item { get; }
        internal int Index { get; }
        private readonly int stack;

        internal ShippingResourcesFixture(int stack = 2, int bankCount = 0)
        {
            this.stack = stack;
            Before = Clone(shared.Source.Before);
            Item = shared.Source.Ledger.CropPlantingCommitments.Single().HarvestItemQualifiedId;
            var inventory = Get(Before, "player", "inventory");
            var raw = inventory.AsArray().OfType<JsonObject>().Single(row => row["qualified_item_id"]?.GetValue<string>() == Item);
            Index = raw["slot_index"]!.GetValue<int>();
            raw["stack"] = stack; raw["sell_to_store_price"] = 50; raw["shipping_unknown"] = "retained";
            Set(Before, "player", "inventory", inventory);
            var graph = Get(Before, "farm", "material_inventory_graph");
            var player = graph["inventory_nodes"]![0]!;
            player["slots"]!.AsArray().Single(row => row!["slot_index"]!.GetValue<int>() == Index)!["stack"] = stack;
            graph["quantity_rows"]!.AsArray().Single(row => row!["qualified_item_id"]!.GetValue<string>() == Item)!["available_quantity"] = stack;
            // A foreign empty custody node must remain untouched, including unknown metadata.
            var foreign = player.DeepClone(); foreign["node_id"] = "foreign:shipping-fixture";
            foreign["inventory_kind"] = "storage_chest"; foreign["owner_player_id"] = 456;
            foreign["actor_use_authorized"] = false; foreign["ownership_class"] = "other_owned";
            foreign["slots"] = new JsonArray(); foreign["shipping_unknown"] = "retained";
            graph["inventory_nodes"]!.AsArray().Add(foreign);
            Set(Before, "farm", "material_inventory_graph", graph);
            Set(Before, "farm", "shipping_bins", Bins(bankCount));
            Before.StateHash = SnapshotHash.ComputeStateHash(Before.State);
            After = Clone(Before); After.GameTick++;
            inventory = Get(After, "player", "inventory");
            raw = inventory.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == Index);
            if (stack > 1) raw["stack"] = stack - 1;
            else
            {
                // Declared null Item row from PlayerReadAdapter, retaining unfamiliar metadata.
                foreach (var key in raw.Select(pair => pair.Key).ToArray())
                    if (key is not "slot_index" and not "shipping_unknown") raw[key] = null;
                foreach (var key in new[] { "is_object", "is_furniture", "is_wallpaper", "protected_from_auto_sell" })
                    if (raw.ContainsKey(key)) raw[key] = false;
                if (raw.ContainsKey("auto_sell_protection_reasons")) raw["auto_sell_protection_reasons"] = new JsonArray();
                if (raw.ContainsKey("donate_color_context")) raw["donate_color_context"] = new JsonObject {
                    ["is_colored_object"] = false, ["preserved_parent_item_id"] = null,
                    ["preserved_parent_base_context_tags"] = new JsonArray(), ["projection_status"] = "not_applicable_not_colored_object" };
                raw["is_empty"] = true;
            }
            Set(After, "player", "inventory", inventory);
            graph = Get(After, "farm", "material_inventory_graph");
            var slots = graph["inventory_nodes"]![0]!["slots"]!.AsArray();
            var slot = slots.Single(row => row!["slot_index"]!.GetValue<int>() == Index)!;
            var quantities = graph["quantity_rows"]!.AsArray();
            var quantity = quantities.Single(row => row!["qualified_item_id"]!.GetValue<string>() == Item)!;
            if (stack == 1) { slots.Remove(slot); quantities.Remove(quantity); }
            else { slot["stack"] = stack - 1; quantity["available_quantity"] = stack - 1; }
            Set(After, "farm", "material_inventory_graph", graph);
            if (stack == 1)
            {
                var capacity = Get(After, "player", "inventory_capacity");
                capacity["occupied_item_stacks"] = capacity["occupied_item_stacks"]!.GetValue<int>() - 1;
                capacity["empty_slots"] = capacity["empty_slots"]!.GetValue<int>() + 1; capacity["has_empty_slot"] = true;
                Set(After, "player", "inventory_capacity", capacity);
            }
            Set(After, "farm", "shipping_bins", Bins(bankCount + 1));
            Queue = MoneyCropTeacherFixture.Create<ActionQueueEnvelope>("MoneyCropShippingDepositQueue", Before, Item, 50, "shipping-resources");
            Queue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "slot_index").Value = Index.ToString();
            Rebind(bankCount);
        }

        internal void Rebind(int bankCount = 0)
        {
            After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            Receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("MoneyCropQueueReceipt", Queue, Before, After,
                "shipping-resources-candidate", "ship_inventory_item_to_bin", new[] {
                    new { path = "player.inventory." + Item + ".count", before = stack, after = stack - 1 },
                    new { path = "farm.shipping_bin." + Item + ".count", before = bankCount, after = bankCount + 1 } });
            Transition = new("shipping_deposit", Get(Before, "time", "total_days").GetValue<int>(), "queue", "before", "receipt", "after",
                Receipt.RunId, shared.Files.Options.ExecutorVersion, Receipt.SelectedCandidateId);
        }

        private JsonArray Bins(int count)
        {
            var contents = new JsonArray();
            if (count > 0) contents.Add(new JsonObject { ["item_id"] = Item[3..], ["qualified_item_id"] = Item,
                ["quality"] = 0, ["count"] = count, ["unit_sell_to_store_price"] = 50, ["total_sell_to_store_price"] = count * 50 });
            var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(count == 0 ? "" : $"{Item}|0|50|{count}\n"))).ToLowerInvariant();
            JsonObject Bin(int x, int y) => new() { ["tile_x"] = x, ["tile_y"] = y, ["tiles_wide"] = 2, ["tiles_high"] = 1,
                ["completed"] = true, ["days_of_construction_left"] = 0, ["bin_scope"] = "shared", ["player_id"] = 123,
                ["contents"] = contents.DeepClone(), ["contents_total_count"] = count, ["contents_distinct_item_count"] = count > 0 ? 1 : 0,
                ["contents_distinct_settlement_variant_count"] = count > 0 ? 1 : 0, ["pending_settlement_value"] = count * 50,
                ["pending_settlement_value_complete"] = true, ["pending_settlement_timing"] = "next_native_day_transition",
                ["contents_signature"] = signature, ["contents_truncated"] = false, ["shipping_unknown"] = "retained" };
            return new JsonArray(Bin(71, 14), Bin(80, 20));
        }

        internal CropTeacherShippingDepositResourceEffects Verify() => CropTeacherShippingDepositResources.Verify(Before, After, Queue, Receipt, Transition, Item);
        internal void Mutate(string domain, string field, Action<JsonNode> change)
        { var value = Get(After, domain, field); change(value); Set(After, domain, field, value); Rebind(); }
        internal static JsonNode Get(SnapshotEnvelope snapshot, string domain, string field) => JsonNode.Parse(snapshot.State[domain].GetProperty(field).GetProperty("value").GetRawText())!;
        private static void Set(SnapshotEnvelope snapshot, string domain, string field, JsonNode value)
        {
            var fields = JsonNode.Parse(snapshot.State[domain].GetRawText())!.AsObject();
            if (fields[field] is null) fields[field] = new JsonObject { ["status"] = "available", ["value"] = value };
            else fields[field]!["value"] = value;
            snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
        }
        private static SnapshotEnvelope Clone(SnapshotEnvelope snapshot) => JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        public void Dispose() => shared.Dispose();
    }
}
