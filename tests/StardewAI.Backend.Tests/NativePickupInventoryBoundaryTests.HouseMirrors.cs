using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Theory]
    [InlineData("farm", "science_house_not_current_location")]
    [InlineData("no-action-tile", "carpenter_action_tile_unavailable")]
    [InlineData("no-robin", "robin_not_present_at_counter")]
    [InlineData("menu", "carpenter_menu_or_dialogue_not_clear")]
    [InlineData("dialogue", "carpenter_menu_or_dialogue_not_clear")]
    [InlineData("ready", "ready")]
    [InlineData("upgrade-underway", "farmhouse_upgrade_already_in_progress")]
    [InlineData("building-underway", "another_building_under_construction")]
    [InlineData("money", "insufficient_money")]
    public void HouseMaterialThresholdCrossingReconcilesNativeDerivedActionStatus(string branch, string status)
    {
        var f = HouseThresholdFixture(branch, status);
        Assert.True(Resources(f).MaterialGraphAndMirrorsVerified);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HouseMaterialThresholdStillRejectsForgedActionStatus(bool before)
    {
        var f = HouseThresholdFixture("farm", "science_house_not_current_location");
        Edit(before ? f.Before : f.After, "world_progress", "marriage_house", house => house["house_upgrade"]!["action_status"] = "ready");
        Rebind(f);
        Assert.Throws<InvalidDataException>(() => Resources(f));
    }

    private static PickupFixture HouseThresholdFixture(string branch, string afterStatus)
    {
        var f = ResourceFixture(woodBefore: 444);
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            Put(snapshot, "player", "money", JsonValue.Create(branch == "money" ? 500 : 20000)!);
            Put(snapshot, "player", "active_menu", JsonValue.Create(branch == "menu" ? "StardewValley.Menus.InventoryPage" : "none")!);
            Put(snapshot, "player", "movement_timing_context", new JsonObject { ["dialogue_up"] = branch == "dialogue" });
            Edit(snapshot, "world_progress", "marriage_house", house =>
            {
                house["days_until_farmhouse_upgrade"] = branch == "upgrade-underway" ? 1 : -1;
                house["building_under_construction"] = branch == "building-underway";
                house["money"] = branch == "money" ? 500 : 20000;
                house["is_current_location"] = branch != "farm";
                house["carpenter_action_tile_x"] = branch == "no-action-tile" ? null : JsonValue.Create(1);
                house["carpenter_action_tile_y"] = branch == "no-action-tile" ? null : JsonValue.Create(2);
                house["robin_present_at_counter"] = branch != "no-robin";
                house["house_upgrade"]!["required_item_count"] = 450;
                house["house_upgrade"]!["action_status"] = ReferenceEquals(snapshot, f.After) ||
                    branch is "upgrade-underway" or "building-underway" or "money" ? afterStatus : "insufficient_required_material";
            });
        }
        Rebind(f);
        return f;
    }
}
