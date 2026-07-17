using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter : ReadAdapterBase
{
    public override string Domain => "locations";
    public override int Priority => 34;

    public override StateAdapterResult Collect(long tick)
    {
        if (!Context.IsWorldReady)
        {
            return Section("locations", new Dictionary<string, object>
            {
                ["shops"] = Unavailable("world_not_ready", "DataLoader.Shops(Game1.content)", tick, "vanilla_1_6_shops"),
                ["collision_grid"] = Unavailable("world_not_ready", "Game1.currentLocation.isCollidingPosition", tick, "vanilla_1_6_route"),
                ["route_connectors"] = Unavailable("world_not_ready", "Game1.currentLocation.warps/doors/interiorDoors/map Action", tick, "vanilla_1_6_route"),
                ["route_blockers"] = Unavailable("world_not_ready", "Game1.currentLocation collision participants", tick, "vanilla_1_6_route"),
                ["route_gate_context"] = Unavailable("world_not_ready", "GameLocation LockedDoorWarp/ConditionalDoor/Warp action gates", tick, "vanilla_1_6_route"),
                ["route_action_branch_coverage"] = Unavailable("world_not_ready", "GameLocation performAction branch coverage audit", tick, "vanilla_1_6_route"),
                ["route_graph"] = Unavailable("world_not_ready", "Game1.locations route graph preview", tick, "vanilla_1_6_route"),
                ["route_map_summaries"] = Unavailable("world_not_ready", "Game1.locations route map summaries", tick, "vanilla_1_6_route")
            }, new[] { "locations.shops", "locations.collision_grid", "locations.route_connectors", "locations.route_blockers", "locations.route_gate_context", "locations.route_action_branch_coverage", "locations.route_graph", "locations.route_map_summaries" }, "unavailable");
        }

        return Section("locations", new Dictionary<string, object>
        {
            ["shops"] = Field(ReadShopAccess(), "DataLoader.Shops(Game1.content); ShopBuilder.GetCurrentOwners; Utility.isFestivalDay; GameLocation.AreStoresClosedForFestival", tick, "vanilla_1_6_shops"),
            ["collision_grid"] = Field(ReadCollisionGrid(), "Game1.currentLocation.isCollidingPosition compressed current map grid", tick, "vanilla_1_6_route"),
            ["route_connectors"] = Field(ReadRouteConnectors(), "Game1.currentLocation.warps/doors/interiorDoors and map Action connector index", tick, "vanilla_1_6_route"),
            ["route_blockers"] = Field(ReadRouteBlockers(), "Game1.currentLocation characters/objects/terrain/resource clumps/furniture collision participants", tick, "vanilla_1_6_route"),
            ["route_gate_context"] = Field(ReadRouteGateContext(), "GameLocation LockedDoorWarp/ConditionalDoor/Warp action gates and map BuildConditions", tick, "vanilla_1_6_route"),
            ["route_action_branch_coverage"] = Field(ReadRouteActionBranchCoverage(), "GameLocation performAction branch coverage audit for current map actions", tick, "vanilla_1_6_route"),
            ["route_graph"] = Field(ReadRouteGraph(), "Game1.locations warps/doors/action-warp route graph preview", tick, "vanilla_1_6_route"),
            ["route_map_summaries"] = Field(ReadRouteMapSummaries(), "Game1.locations map dimensions and route connector/action summaries", tick, "vanilla_1_6_route")
        }, Array.Empty<string>(), "partial");
    }

    private static object ReadShopAccess()
    {
        var location = Game1.currentLocation;
        var currentCharacters = location.characters
            .Select(character => new
            {
                name = character.Name,
                tile_x = character.TilePoint.X,
                tile_y = character.TilePoint.Y,
                is_villager = character.IsVillager
            })
            .OrderBy(character => character.name, StringComparer.Ordinal)
            .ToArray();

        var shops = DataLoader.Shops(Game1.content)
            .Select(pair => ReadShopSummary(pair.Key, pair.Value))
            .OrderBy(shop => shop.shop_id, StringComparer.Ordinal)
            .ToArray();

        return new
        {
            current_location_id = location.NameOrUniqueName,
            current_time = Game1.timeOfDay,
            current_day = Game1.dayOfMonth,
            current_season = Game1.currentSeason,
            festival_day = Utility.isFestivalDay(),
            stores_closed_for_festival = GameLocation.AreStoresClosedForFestival(),
            current_location_character_count = currentCharacters.Length,
            current_location_characters = currentCharacters,
            shop_count = shops.Length,
            shops
        };
    }

}
