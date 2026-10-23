using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter : ReadAdapterBase
{
    public override string Domain => "locations";
    public override int Priority => 34;

    public override StateAdapterResult Collect(long tick)
    {
        var socialRouteEvidenceRequested =
            SnapshotProfileContext.IncludesSocialFutureRouteDateEvidence;
        if (!Context.IsWorldReady)
        {
            var unavailableFields = new Dictionary<string, object>
            {
                ["shops"] = Unavailable("world_not_ready", "DataLoader.Shops(Game1.content)", tick, "vanilla_1_6_shops"),
                ["collision_grid"] = Unavailable("world_not_ready", "Game1.currentLocation.isCollidingPosition", tick, "vanilla_1_6_route"),
                ["route_connectors"] = Unavailable("world_not_ready", "Game1.currentLocation.warps/doors/interiorDoors/map Action", tick, "vanilla_1_6_route"),
                ["route_blockers"] = Unavailable("world_not_ready", "Game1.currentLocation collision participants", tick, "vanilla_1_6_route"),
                ["route_gate_context"] = Unavailable("world_not_ready", "GameLocation LockedDoorWarp/ConditionalDoor/Warp action gates", tick, "vanilla_1_6_route"),
                ["route_action_branch_coverage"] = Unavailable("world_not_ready", "GameLocation performAction branch coverage audit", tick, "vanilla_1_6_route"),
                ["route_graph"] = Unavailable("world_not_ready", "Game1.locations route graph preview", tick, "vanilla_1_6_route"),
                ["route_map_summaries"] = Unavailable("world_not_ready", "Game1.locations route map summaries", tick, "vanilla_1_6_route"),
                ["secret_woods_access"] = Unavailable("world_not_ready", "Forest resource clump and active farm type", tick, "vanilla_1_6_15_secret_woods_access"),
                ["dark_talisman_access"] = Unavailable("world_not_ready", "Railroad event 529952, quest 28, Krobus sewer unseal, BugLand SpecialItem 6, Railroad statue and WitchWarpCave MagicWarp", tick, "vanilla_1_6_15_dark_talisman_access"),
                ["ginger_island_access"] = Unavailable("world_not_ready", "Willy invitation, BoatTunnel repairs/ticket, boat journey and Island parrot perches", tick, "vanilla_1_6_15_ginger_island_access")
            };
            var unavailablePaths = new List<string>
            {
                "locations.shops",
                "locations.collision_grid",
                "locations.route_connectors",
                "locations.route_blockers",
                "locations.route_gate_context",
                "locations.route_action_branch_coverage",
                "locations.route_graph",
                "locations.route_map_summaries",
                "locations.secret_woods_access",
                "locations.dark_talisman_access",
                "locations.ginger_island_access"
            };
            if (socialRouteEvidenceRequested)
            {
                unavailableFields["social_route_date_evidence"] = Unavailable(
                    "world_not_ready",
                    "Game1.locations date-bound static native walkability and route action gates",
                    tick,
                    "vanilla_1_6_social_route_date_evidence");
                unavailablePaths.Add("locations.social_route_date_evidence");
            }
            return Section("locations", unavailableFields, unavailablePaths, "unavailable");
        }

        var fields = new Dictionary<string, object>
        {
            ["shops"] = Field(ReadShopAccess(), "DataLoader.Shops(Game1.content); ShopBuilder.GetCurrentOwners; Utility.isFestivalDay; GameLocation.AreStoresClosedForFestival", tick, "vanilla_1_6_shops"),
            ["collision_grid"] = Field(ReadCollisionGrid(), "Game1.currentLocation.isCollidingPosition compressed current map grid", tick, "vanilla_1_6_route"),
            ["route_connectors"] = Field(ReadRouteConnectors(), "Game1.currentLocation.warps/doors/interiorDoors and map Action connector index", tick, "vanilla_1_6_route"),
            ["route_blockers"] = Field(ReadRouteBlockers(), "Game1.currentLocation characters/objects/terrain/resource clumps/furniture collision participants", tick, "vanilla_1_6_route"),
            ["route_gate_context"] = Field(ReadRouteGateContext(), "GameLocation LockedDoorWarp/ConditionalDoor/Warp action gates and map BuildConditions", tick, "vanilla_1_6_route"),
            ["route_action_branch_coverage"] = Field(ReadRouteActionBranchCoverage(), "GameLocation performAction branch coverage audit for current map actions", tick, "vanilla_1_6_route"),
            ["route_graph"] = Field(ReadRouteGraph(), "Game1.locations warps/doors/action-warp route graph preview", tick, "vanilla_1_6_route"),
            ["route_map_summaries"] = Field(ReadRouteMapSummaries(), "Game1.locations map dimensions and route connector/action summaries", tick, "vanilla_1_6_route"),
            ["secret_woods_access"] = Field(ReadSecretWoodsAccess(), "Game1.GetFarmTypeKey; Forest constructor ResourceClump(602,2,2,(1,6)); ResourceClump.performToolAction Steel Axe gate; Forest to Woods native warps", tick, "vanilla_1_6_15_secret_woods_access"),
            ["dark_talisman_access"] = Field(ReadDarkTalismanAccess(), "Events/Railroad 529952/C; quest 28; NPC.checkAction Krobus; BugLand overlay Chest SpecialItem(6); Railroad.checkAction tile 287; WitchWarpCave Back.TouchAction MagicWarp WitchSwamp 20 42", tick, "vanilla_1_6_15_dark_talisman_access"),
            ["ginger_island_access"] = Field(ReadGingerIslandAccess(), "Willy invitation; FishShop WarpBoatTunnel; BoatTunnel BoatTicket/checkAction/answerDialogue; WorldChangeEvent 13; BoatJourney; IslandHut and IslandSouth ParrotUpgradePerch", tick, "vanilla_1_6_15_ginger_island_access")
        };
        if (socialRouteEvidenceRequested)
        {
            fields["social_route_date_evidence"] = Field(
                ReadSocialRouteDateEvidence(),
                "Game1.Date/Game1.locations/GameLocation.IsTileBlockedBy excluding Characters and Farmers plus native route action gates",
                tick,
                "vanilla_1_6_social_route_date_evidence");
        }
        return Section("locations", fields, Array.Empty<string>(), "partial");
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
