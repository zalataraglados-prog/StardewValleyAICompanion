using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Theory]
    [InlineData("crop-target")]
    [InlineData("extra-crop")]
    [InlineData("crop-metadata")]
    [InlineData("water")]
    [InlineData("tool-metadata")]
    [InlineData("energy")]
    [InlineData("bowl-watered")]
    [InlineData("bowl-friendship")]
    [InlineData("bowl-resource-echo")]
    [InlineData("graph-unknown-field")]
    [InlineData("foreign-node")]
    [InlineData("graph-slot-metadata")]
    [InlineData("available-quantity")]
    [InlineData("restricted-quantity")]
    [InlineData("source-slot-count")]
    [InlineData("capacity")]
    [InlineData("house-count")]
    [InlineData("house-other-field")]
    [InlineData("money")]
    [InlineData("club-coins")]
    [InlineData("total-earned")]
    [InlineData("unknown-persistent")]
    public void WateringResourceCompositionRejectsUnverifiedPrimaryAndUnrelatedResourceChanges(string change)
    {
        var f = WateringResourceFixture();
        VerifyWateringResources(f);
        var after = f.Raw.After;
        switch (change)
        {
            case "crop-target": Edit(after, "farm", "crops", rows => rows[0]!["tile_x"] = 99); break;
            case "extra-crop": Edit(after, "farm", "crops", rows => rows[1]!["watered"] = true); break;
            case "crop-metadata": Edit(after, "farm", "crops", rows => rows[0]!["unknown_native_crop_detail"] = "changed"); break;
            case "water": Edit(after, "player", "watering_can_state", can => can["water_left"] = 18); break;
            case "tool-metadata": Edit(after, "player", "watering_can_state", can => can["upgrade_level"] = 4); break;
            case "energy": Put(after, "player", "energy", JsonValue.Create(99f)!); break;
            case "bowl-watered": Bowl(row => row["watered"] = true); break;
            case "bowl-friendship": Bowl(row => row["friendship_before_next_day"] = 11); break;
            case "bowl-resource-echo": Bowl(row => row["watering_can_water_left"] = 20); break;
            case "graph-unknown-field": Graph(node => node["future_graph_field"] = "changed"); break;
            case "foreign-node": Graph(node => node["inventory_nodes"]![1]!["capacity"] = 99); break;
            case "graph-slot-metadata": Graph(node => node["inventory_nodes"]![0]!["slots"]![0]!["display_name"] = "invented"); break;
            case "available-quantity": Graph(node => node["quantity_rows"]![0]!["available_quantity"] = 999); break;
            case "restricted-quantity": Graph(node => node["quantity_rows"]![0]!["restricted_quantity"] = 999); break;
            case "source-slot-count": Graph(node => node["quantity_rows"]![0]!["source_slot_count"] = 999); break;
            case "capacity": Edit(after, "player", "inventory_capacity", node => node["empty_slots"] = 99); break;
            case "house-count": Edit(after, "world_progress", "marriage_house", node => node["house_upgrade"]!["inventory_item_count"] = 99); break;
            case "house-other-field": Edit(after, "world_progress", "marriage_house", node => node["house_upgrade"]!["price"] = 1); break;
            case "money": Put(after, "player", "money", JsonValue.Create(999)!); break;
            case "club-coins": Put(after, "player", "club_coins", JsonValue.Create(1)!); break;
            case "total-earned": Put(after, "player", "total_money_earned", JsonValue.Create(999)!); break;
            case "unknown-persistent": Put(after, "farm", "future_persistent", JsonValue.Create("changed")!); break;
        }
        Rebind(f.Raw);
        var raw = WateringResourceRaw(f);
        Assert.Throws<InvalidDataException>(() => VerifyWateringResources(f));
        Assert.Equal(raw, WateringResourceRaw(f));

        void Graph(Action<JsonNode> action) => Edit(after, "farm", "material_inventory_graph", action);
        void Bowl(Action<JsonNode> action) => Edit(after, "farm", "pet_bowls", rows => action(rows[0]!));
    }

    [Theory]
    [InlineData("farm", "shipping_bins")]
    [InlineData("player", "club_coins")]
    [InlineData("player", "total_money_earned")]
    public void WateringResourceCompositionRetainsOriginalPersistentBoundaryAvailability(string domain, string field)
    {
        var f = WateringResourceFixture();
        VerifyWateringResources(f);
        var data = System.Text.Json.Nodes.JsonNode.Parse(f.Raw.After.State[domain].GetRawText())!.AsObject();
        data.Remove(field);
        f.Raw.After.State[domain] = System.Text.Json.JsonSerializer.SerializeToElement(data);
        Rebind(f.Raw);
        var error = Assert.Throws<InvalidDataException>(() => VerifyWateringResources(f));
        Assert.Contains("original readable persistent resource boundary", error.Message);
    }
}
