using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PickupConservesCanonicalGraphCapacityForeignStockAndHouseMirror(bool newSlot, bool newKind)
    {
        var f = ResourceFixture(newSlot, newKind);
        var beforeHash = f.Before.StateHash; var afterHash = f.After.StateHash;
        var result = Resources(f);
        Assert.Equal(6, result.Inventory.Evidence.ObservedPickupUnits);
        Assert.True(result.PhysicalInventoryDeltaVerified);
        Assert.True(result.MaterialGraphAndMirrorsVerified);
        Assert.True(result.UnrelatedPersistentStateVerified);
        Assert.False(result.ProducerProvenanceVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Equal(beforeHash, f.Before.StateHash);
        Assert.Equal(afterHash, f.After.StateHash);
        Assert.Equal(beforeHash, SnapshotHash.ComputeStateHash(f.Before.State));
        Assert.Equal(afterHash, SnapshotHash.ComputeStateHash(f.After.State));
    }

    [Theory]
    [InlineData("graph-actor")]
    [InlineData("owner")]
    [InlineData("authority")]
    [InlineData("player-location")]
    [InlineData("slot-metadata")]
    [InlineData("slot-edibility")]
    [InlineData("new-slot-unknown-field")]
    [InlineData("graph-unknown-field")]
    [InlineData("foreign-node")]
    [InlineData("foreign-lock")]
    [InlineData("available-quantity")]
    [InlineData("restricted-quantity")]
    [InlineData("source-slot-count")]
    [InlineData("before-quantity")]
    [InlineData("duplicate-quantity")]
    [InlineData("missing-quantity")]
    [InlineData("capacity")]
    [InlineData("before-capacity")]
    [InlineData("house-before-count")]
    [InlineData("house-after-count")]
    [InlineData("house-other-field")]
    [InlineData("money")]
    [InlineData("currency")]
    [InlineData("wallet")]
    [InlineData("seed-mirror")]
    [InlineData("energy")]
    [InlineData("unknown-persistent")]
    [InlineData("inventory-envelope")]
    public void PickupRejectsUnaccountedGraphMirrorOrPersistentDrift(string change)
    {
        var f = ResourceFixture(newSlot: change == "new-slot-unknown-field");
        var snapshot = change is "before-quantity" or "before-capacity" or "house-before-count" ? f.Before : f.After;
        switch (change)
        {
            case "graph-actor": Graph(node => node["player_id"] = 999L); break;
            case "owner": Player(node => node["owner_player_id"] = 999L); break;
            case "authority": Player(node => node["actor_use_authorized"] = false); break;
            case "player-location": Player(node => node["location_id"] = "Town"); break;
            case "slot-metadata": Player(node => node["slots"]![0]!["display_name"] = "invented"); break;
            case "slot-edibility": Player(node => node["slots"]![0]!["edibility"] = 99); break;
            case "new-slot-unknown-field": Player(node => node["slots"]![0]!["unknown"] = "unwitnessed"); break;
            case "graph-unknown-field": Graph(node => node["future_graph_field"] = "changed"); break;
            case "foreign-node": Graph(node => node["inventory_nodes"]![1]!["capacity"] = 99); break;
            case "foreign-lock": Graph(node => node["access_points"]![0]!["locked_by_other_player"] = false); break;
            case "available-quantity":
            case "restricted-quantity":
            case "source-slot-count":
            case "before-quantity":
                Graph(node => node["quantity_rows"]![0]![change switch { "restricted-quantity" => "restricted_quantity",
                    "source-slot-count" => "source_slot_count", _ => "available_quantity" }] = 999); break;
            case "duplicate-quantity": Graph(node => node["quantity_rows"]!.AsArray().Add(node["quantity_rows"]![0]!.DeepClone())); break;
            case "missing-quantity": Graph(node => node["quantity_rows"]!.AsArray().Clear()); break;
            case "capacity":
            case "before-capacity": Edit(snapshot, "player", "inventory_capacity", value => value["empty_slots"] = 99); break;
            case "house-before-count":
            case "house-after-count": Edit(snapshot, "world_progress", "marriage_house", value => value["house_upgrade"]!["inventory_item_count"] = 99); break;
            case "house-other-field": Edit(snapshot, "world_progress", "marriage_house", value => value["house_upgrade"]!["price"] = 1); break;
            case "money": Put(snapshot, "player", "money", JsonValue.Create(999)!); break;
            case "currency": Edit(snapshot, "player", "shop_currency_balances", value => value["qi_gems"] = 1); break;
            case "wallet": Edit(snapshot, "player", "multiplayer_wallet", value => value["separate"] = true); break;
            case "seed-mirror": Edit(snapshot, "player", "seed_inventory", value => value.AsArray().Add(new JsonObject { ["stack"] = 1 })); break;
            case "energy": Put(snapshot, "player", "energy", JsonValue.Create(99)!); break;
            case "unknown-persistent": Put(snapshot, "farm", "future_persistent", JsonValue.Create("changed")!); break;
            case "inventory-envelope":
                var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
                player["inventory"]!["future_semantic"] = "changed";
                snapshot.State["player"] = JsonSerializer.SerializeToElement(player); break;
        }
        Rebind(f);
        Assert.Throws<InvalidDataException>(() => Resources(f));
        void Graph(Action<JsonNode> edit) => Edit(snapshot, "farm", "material_inventory_graph", edit);
        void Player(Action<JsonNode> edit) => Graph(graph => edit(graph["inventory_nodes"]![0]!));
    }

    private static NativePickupResourceSummary Resources(PickupFixture f) =>
        NativeOutputObservationBoundary.ReadPickupResources(f.Request, f.Receipt, f.Before, f.After, "Farm");

    private static PickupFixture ResourceFixture(bool newSlot = false, bool newKind = false, int woodBefore = 10)
    {
        var f = new PickupFixture(newSlot);
        if (!newSlot && woodBefore != 10)
        {
            SetSlotStack(f.Receipt.InventoryPayloadBefore!.Slots![0], woodBefore);
            SetSlotStack(f.Receipt.InventoryPayloadAfter!.Slots![0], woodBefore + 6);
            f.Receipt.InventoryBefore[0].Quantity = woodBefore;
            f.Receipt.InventoryAfter[0].Quantity = woodBefore + 6;
            f.Receipt.Collections[0].InventoryAdmissions[0].InventoryBefore = woodBefore;
            f.Receipt.Collections[0].InventoryAdmissions[0].InventoryAfter = woodBefore + 6;
        }
        foreach (var payload in new[] { f.Receipt.InventoryPayloadBefore!, f.Receipt.InventoryPayloadAfter! })
            foreach (var slot in payload.Slots!.Where(slot => slot.IsTool == false && slot.IsEmpty == false))
            {
                var xml = XElement.Parse(slot.SerializedStateXml!); xml.Add(new XElement("edibility", -300)); SetXml(slot, xml);
            }
        var incoming = f.Receipt.Collections[0].InventoryAdmissions[0].Item!;
        var input = XElement.Parse(incoming.SerializedStateXml); input.Add(new XElement("edibility", -300)); SetXml(incoming, input);
        f.RefreshSnapshots();
        Enrich(f.Before, f.Receipt.InventoryPayloadBefore!);
        Enrich(f.After, f.Receipt.InventoryPayloadAfter!);
        Rebind(f);
        return f;

        void Enrich(SnapshotEnvelope snapshot, StardewAI.Contracts.Training.ActionOriginInventoryPayload payload)
        {
            var raw = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
            var rows = raw["inventory"]!["value"]!.AsArray();
            var slots = new JsonArray();
            foreach (var row in rows.OfType<JsonObject>().Where(row => row["is_empty"]!.GetValue<bool>() == false))
            {
                var index = row["slot_index"]!.GetValue<int>();
                var isObject = payload.Slots![index].IsTool != true;
                row["item_id"] = isObject ? "388" : "Axe";
                row["display_name"] = isObject ? "Wood" : "Axe";
                row["context_tags"] = JsonSerializer.SerializeToNode(new[] { isObject ? "item_wood" : "item_axe" });
                row["sale_price"] = isObject ? 2 : 0;
                row["is_object"] = isObject;
                var graphSlot = new JsonObject { ["slot_index"] = index, ["context_tags_projection_status"] = "exact_item_get_context_tags",
                    ["edibility"] = isObject ? JsonValue.Create(-300) : null,
                    ["edibility_projection_status"] = isObject ? "exact_object_edibility" : "not_applicable_non_object_item" };
                foreach (var name in new[] { "item_id", "qualified_item_id", "display_name", "runtime_type", "context_tags", "stack",
                             "maximum_stack_size", "quality", "sale_price" }) graphSlot[name] = row[name]!.DeepClone();
                slots.Add(graphSlot);
            }
            var occupied = slots.Count;
            raw["inventory_capacity"]!["value"] = JsonSerializer.SerializeToNode(new { max_items = 3,
                occupied_item_stacks = occupied, empty_slots = 3 - occupied, has_empty_slot = occupied < 3 });
            snapshot.State["player"] = JsonSerializer.SerializeToElement(raw);
            Put(snapshot, "player", "money", JsonValue.Create(500)!);
            Put(snapshot, "player", "energy", JsonValue.Create(100)!);
            Put(snapshot, "player", "shop_currency_balances", new JsonObject { ["qi_gems"] = 0 });
            Put(snapshot, "player", "multiplayer_wallet", new JsonObject { ["separate"] = false });
            Put(snapshot, "player", "seed_inventory", new JsonArray());
            var foreign = new JsonObject { ["slot_index"] = 0, ["qualified_item_id"] = newKind ? "(O)92" : "(O)388", ["stack"] = 7, ["quality"] = 0 };
            var nodes = new JsonArray(new JsonObject { ["node_id"] = "player:123", ["inventory_kind"] = "player_inventory",
                    ["supply_state"] = "available", ["location_id"] = "Farm", ["owner_player_id"] = 123L,
                    ["ownership_class"] = "actor_owned", ["actor_use_authorized"] = true, ["global_inventory_id"] = "", ["capacity"] = 3, ["slots"] = slots },
                new JsonObject { ["node_id"] = "foreign", ["inventory_kind"] = "chest", ["supply_state"] = "available",
                    ["owner_player_id"] = 999L, ["actor_use_authorized"] = false, ["capacity"] = 36, ["slots"] = new JsonArray(foreign) });
            var quantities = nodes.OfType<JsonObject>().SelectMany(node => node["slots"]!.AsArray().OfType<JsonObject>()
                    .Select(slot => (Node: node, Slot: slot))).GroupBy(pair => (Id: pair.Slot["qualified_item_id"]!.GetValue<string>(), Quality: 0))
                .OrderBy(group => group.Key.Id, StringComparer.Ordinal).Select(group => (JsonNode)new JsonObject
                {
                    ["qualified_item_id"] = group.Key.Id, ["quality"] = 0, ["available_quantity"] = group.Where(pair => pair.Node["actor_use_authorized"]!.GetValue<bool>()).Sum(pair => pair.Slot["stack"]!.GetValue<int>()),
                    ["ready_output_quantity"] = 0, ["in_process_quantity"] = 0,
                    ["restricted_quantity"] = group.Where(pair => !pair.Node["actor_use_authorized"]!.GetValue<bool>()).Sum(pair => pair.Slot["stack"]!.GetValue<int>()), ["source_slot_count"] = group.Count()
                }).ToArray();
            Put(snapshot, "farm", "material_inventory_graph", new JsonObject { ["schema_version"] = "material_inventory_graph.v1",
                ["status"] = "available", ["player_id"] = 123L, ["inventory_nodes"] = nodes, ["quantity_rows"] = new JsonArray(quantities),
                ["access_points"] = new JsonArray(new JsonObject { ["access_point_id"] = "foreign-access", ["node_id"] = "foreign", ["locked_by_other_player"] = true }),
                ["workbench_links"] = new JsonArray(), ["future_graph_field"] = "retained" });
            Put(snapshot, "farm", "future_persistent", JsonValue.Create("retained")!);
            var wood = slots.OfType<JsonObject>().Where(slot => slot["qualified_item_id"]!.GetValue<string>() == "(O)388").Sum(slot => slot["stack"]!.GetValue<int>());
            Put(snapshot, "world_progress", "marriage_house", new JsonObject { ["house_upgrade"] = new JsonObject {
                ["required_item_id"] = "(O)388", ["inventory_item_count"] = wood, ["price"] = 10000 }, ["spouse"] = "" });
        }
    }

    private static void Put(SnapshotEnvelope snapshot, string domain, string name, JsonNode value)
    {
        var data = snapshot.State.TryGetValue(domain, out var original) ? JsonNode.Parse(original.GetRawText())!.AsObject() : new JsonObject();
        data[name] = new JsonObject { ["status"] = "available", ["value"] = value };
        snapshot.State[domain] = JsonSerializer.SerializeToElement(data);
    }
    private static void Edit(SnapshotEnvelope snapshot, string domain, string name, Action<JsonNode> change)
    {
        var data = JsonNode.Parse(snapshot.State[domain].GetRawText())!;
        change(data[name]!["value"]!);
        snapshot.State[domain] = JsonSerializer.SerializeToElement(data);
    }
    private static void Rebind(PickupFixture f)
    {
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State);
        f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        f.Request.BeforeStateHash = f.Receipt.BeforeStateHash = f.Before.StateHash;
    }
}
