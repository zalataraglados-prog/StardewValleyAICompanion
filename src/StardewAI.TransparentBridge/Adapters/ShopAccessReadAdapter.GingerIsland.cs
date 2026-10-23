using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter
{
    private const string WillyInvitationMail = "willyBackRoomInvitation";
    private const string BoatBatteryMail = "willyBoatTicketMachine";
    private const string BoatHullMail = "willyBoatHull";
    private const string BoatAnchorMail = "willyBoatAnchor";
    private const string BoatFixedMail = "willyBoatFixed";
    private const string FirstParrotMail = "Island_FirstParrot";
    private const string ResortMail = "Island_Resort";
    private const string WesternTurtleMail = "Island_Turtle";
    private const string IslandHouseMail = "Island_UpgradeHouse";
    private static readonly Point BoatHullTile = new(6, 8);
    private static readonly Point BoatAnchorTile = new(8, 10);
    private static readonly Point FirstParrotTile = new(7, 6);
    private static readonly Point ResortParrotTile = new(17, 22);
    private static readonly Point WesternTurtleParrotTile = new(5, 9);
    private static readonly Point IslandHouseParrotTile = new(81, 40);

    private static object ReadGingerIslandAccess()
    {
        var player = Game1.player;
        var master = Game1.MasterPlayer;
        var fishShop = Game1.getLocationFromName("FishShop");
        var boatTunnel = Game1.getLocationFromName("BoatTunnel") as BoatTunnel;
        var islandHut = Game1.getLocationFromName("IslandHut") as IslandHut;
        var islandSouth = Game1.getLocationFromName("IslandSouth") as IslandSouth;
        var islandWest = Game1.getLocationFromName("IslandWest") as IslandWest;
        var currentLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;

        var communityCenterOrWarehouseDone =
            master.eventsSeen.Contains("191393") ||
            master.eventsSeen.Contains("502261") ||
            master.hasCompletedCommunityCenter();
        var invitationReceived = player.mailReceived.Contains(WillyInvitationMail);
        var invitationPending = player.mailForTomorrow.Any(row =>
            string.Equals(row, WillyInvitationMail, StringComparison.Ordinal) ||
            row.StartsWith(WillyInvitationMail + "%&NL&%", StringComparison.Ordinal));
        var invitationInMailbox = player.mailbox.Contains(WillyInvitationMail);
        var fishShopWarp = FindExactMapAction(fishShop, "WarpBoatTunnel");
        var ticketAction = FindExactMapAction(boatTunnel, "BoatTicket");

        var batteryRepair = master.hasOrWillReceiveMail(BoatBatteryMail);
        var hullRepair = master.hasOrWillReceiveMail(BoatHullMail);
        var anchorRepair = master.hasOrWillReceiveMail(BoatAnchorMail);
        var allRepairs = batteryRepair && hullRepair && anchorRepair;
        var fixedReceived = master.mailReceived.Contains(BoatFixedMail);
        var fixedPending = master.hasOrWillReceiveMail(BoatFixedMail);
        var firstParrotDone = master.hasOrWillReceiveMail(FirstParrotMail) ||
            islandHut?.firstParrotDone.Value == true;
        var resortDone = master.hasOrWillReceiveMail(ResortMail) ||
            islandSouth?.resortRestored.Value == true;
        var westernTurtleDone = master.hasOrWillReceiveMail(WesternTurtleMail) ||
            islandSouth?.westernTurtleMoved.Value == true;
        var islandHouseDone = master.hasOrWillReceiveMail(IslandHouseMail) ||
            islandWest?.farmhouseRestored.Value == true;
        var firstPerch = FindPerch(islandHut, "Hut", FirstParrotTile);
        var resortPerch = FindPerch(islandSouth, "Resort", ResortParrotTile);
        var westernTurtlePerch = FindPerch(islandSouth, "Turtle", WesternTurtleParrotTile);
        var islandHousePerch = FindPerch(islandWest, "House", IslandHouseParrotTile);
        var boatJourneyActive = Game1.currentMinigame?.GetType().FullName ==
            "StardewValley.Minigames.BoatJourney";
        var boatDepartureActive = Game1.eventUp &&
            string.Equals(Game1.CurrentEvent?.id, "-78765", StringComparison.Ordinal);
        var islandReached = player.mailReceived.Contains("Visited_Island") ||
            Game1.stats.Get("boatRidesToIsland") > 0 ||
            currentLocation.StartsWith("Island", StringComparison.Ordinal);

        var status = GingerIslandAccessStatus(
            currentLocation,
            communityCenterOrWarehouseDone,
            invitationReceived,
            invitationPending,
            invitationInMailbox,
            fishShopWarp.IdentityExact,
            boatTunnel is not null,
            ticketAction.IdentityExact,
            batteryRepair,
            hullRepair,
            anchorRepair,
            allRepairs,
            fixedReceived,
            fixedPending,
            player.Items.CountId("787"),
            player.Items.CountId("709"),
            player.Items.CountId("337"),
            player.Money,
            boatDepartureActive,
            boatJourneyActive,
            islandReached,
            firstParrotDone,
            westernTurtleDone,
            islandHouseDone,
            resortDone,
            Game1.netWorldState.Value.GoldenWalnuts,
            firstPerch.IdentityExact,
            westernTurtlePerch.IdentityExact,
            islandHousePerch.IdentityExact,
            resortPerch.IdentityExact);

        return new
        {
            schema_version = "ginger_island_access.v1",
            projection_status = "complete_locked_base_1.6.15_native_contract",
            status,
            current_location_id = currentLocation,
            community_center_or_warehouse_done = communityCenterOrWarehouseDone,
            invitation = new
            {
                mail_id = WillyInvitationMail,
                received = invitationReceived,
                pending_for_tomorrow = invitationPending,
                present_in_mailbox = invitationInMailbox
            },
            fish_shop_back_room = new
            {
                source_location = "FishShop",
                raw_action = fishShopWarp.RawAction,
                tile_x = fishShopWarp.Tile.X,
                tile_y = fishShopWarp.Tile.Y,
                exact_action_count = fishShopWarp.Count,
                identity_exact = fishShopWarp.IdentityExact,
                target_location = "BoatTunnel",
                arrival_tile_x = 6,
                arrival_tile_y = 12
            },
            repairs = new
            {
                battery = RepairRow(BoatBatteryMail, "(O)787", 5, player.Items.CountId("787"), ticketAction.Tile, batteryRepair, "WillyBoatDonateBatteries"),
                hull = RepairRow(BoatHullMail, "(O)709", 200, player.Items.CountId("709"), BoatHullTile, hullRepair, "WillyBoatDonateHardwood"),
                anchor = RepairRow(BoatAnchorMail, "(O)337", 5, player.Items.CountId("337"), BoatAnchorTile, anchorRepair, "WillyBoatDonateIridium"),
                all_complete_or_pending = allRepairs,
                boat_fixed_received = fixedReceived,
                boat_fixed_pending = fixedPending
            },
            ticket = new
            {
                source_location = "BoatTunnel",
                raw_action = ticketAction.RawAction,
                tile_x = ticketAction.Tile.X,
                tile_y = ticketAction.Tile.Y,
                exact_action_count = ticketAction.Count,
                identity_exact = ticketAction.IdentityExact,
                price = boatTunnel?.TicketPrice ?? -1,
                money_available = player.Money,
                expected_dialogue_key = "Boat",
                expected_action_type = "WillyBoatTicket"
            },
            journey = new
            {
                departure_event_active = boatDepartureActive,
                departure_event_id = Game1.CurrentEvent?.id ?? string.Empty,
                boat_journey_active = boatJourneyActive,
                active_minigame_runtime_type = Game1.currentMinigame?.GetType().FullName ?? string.Empty,
                boat_rides_to_island = Game1.stats.Get("boatRidesToIsland"),
                island_reached = islandReached
            },
            walnuts = new
            {
                current = Game1.netWorldState.Value.GoldenWalnuts,
                found = Game1.netWorldState.Value.GoldenWalnutsFound
            },
            first_parrot = PerchRow(firstPerch, 1, firstParrotDone, string.Empty),
            resort_parrot = PerchRow(resortPerch, 20, resortDone, "Island_UpgradeHouse"),
            western_turtle_parrot = PerchRow(westernTurtlePerch, 10, westernTurtleDone, FirstParrotMail),
            island_house_parrot = PerchRow(islandHousePerch, 20, islandHouseDone, string.Empty),
            target_access = new
            {
                island_south_accessible = islandReached,
                island_north_accessible = firstParrotDone,
                island_west_accessible = westernTurtleDone,
                island_south_east_accessible = resortDone,
                island_south_east_cave_accessible = resortDone
            },
            native_contract = "FishShop_WarpBoatTunnel_then_BoatTunnel_checkAction_and_answerDialogue_then_WorldChangeEvent_13_then_BoatTunnel_StartDeparture_BoatJourney_then_ParrotUpgradePerch_CheckAction_AnswerQuestion_and_native_animation",
            fresh_snapshot_after_each_primitive = true
        };
    }

    private static string GingerIslandAccessStatus(
        string currentLocation,
        bool communityDone,
        bool invitationReceived,
        bool invitationPending,
        bool invitationInMailbox,
        bool fishShopWarpExact,
        bool boatTunnelLoaded,
        bool ticketExact,
        bool batteryRepair,
        bool hullRepair,
        bool anchorRepair,
        bool allRepairs,
        bool fixedReceived,
        bool fixedPending,
        int batteries,
        int hardwood,
        int iridiumBars,
        int money,
        bool departureActive,
        bool journeyActive,
        bool islandReached,
        bool firstParrotDone,
        bool westernTurtleDone,
        bool islandHouseDone,
        bool resortDone,
        int walnuts,
        bool firstPerchExact,
        bool westernTurtlePerchExact,
        bool islandHousePerchExact,
        bool resortPerchExact)
    {
        if (resortDone)
            return "resort_accessible";
        if (islandReached)
        {
            if (!firstParrotDone)
            {
                if (!firstPerchExact)
                    return "blocked_first_parrot_identity_drifted";
                if (walnuts < 1)
                    return "requires_first_parrot_walnut";
                return string.Equals(currentLocation, "IslandHut", StringComparison.Ordinal)
                    ? "ready_to_unlock_first_parrot"
                    : "route_to_island_hut";
            }
            if (!westernTurtleDone)
            {
                if (!westernTurtlePerchExact)
                    return "blocked_western_turtle_identity_drifted";
                if (walnuts < 10)
                    return "requires_western_turtle_walnuts";
                return string.Equals(currentLocation, "IslandSouth", StringComparison.Ordinal)
                    ? "ready_to_move_western_turtle"
                    : "route_to_western_turtle";
            }
            if (!islandHouseDone)
            {
                if (!islandHousePerchExact)
                    return "blocked_island_house_identity_drifted";
                if (walnuts < 20)
                    return "requires_island_house_walnuts";
                return string.Equals(currentLocation, "IslandWest", StringComparison.Ordinal)
                    ? "ready_to_restore_island_house"
                    : "route_to_island_house";
            }
            if (!resortPerchExact)
                return "blocked_resort_parrot_identity_drifted";
            if (walnuts < 20)
                return "requires_resort_walnuts";
            return string.Equals(currentLocation, "IslandSouth", StringComparison.Ordinal)
                ? "ready_to_restore_resort"
                : "route_to_resort_parrot";
        }
        if (journeyActive)
            return "advance_boat_journey";
        if (departureActive)
            return "advance_boat_departure_event";
        if (!communityDone)
            return "requires_community_center_or_joja_completion";
        if (!invitationReceived)
        {
            if (invitationInMailbox)
                return "process_invitation_mail";
            return invitationPending ? "wait_for_invitation_mail" : "blocked_invitation_not_scheduled";
        }
        if (!fishShopWarpExact)
            return "blocked_fish_shop_back_room_identity_drifted";
        if (!boatTunnelLoaded || !ticketExact)
            return "blocked_boat_tunnel_identity_drifted";
        if (!batteryRepair)
            return batteries >= 5 ? "ready_to_donate_batteries" : "requires_battery_packs";
        if (!hullRepair)
            return hardwood >= 200 ? "ready_to_donate_hardwood" : "requires_hardwood";
        if (!anchorRepair)
            return iridiumBars >= 5 ? "ready_to_donate_iridium" : "requires_iridium_bars";
        if (allRepairs && !fixedReceived)
            return fixedPending ? "wait_for_boat_repair_mail" : "wait_for_boat_repair_night";
        if (fixedReceived && money < 1000)
            return "requires_ticket_money";
        return "ready_to_buy_ticket";
    }

    private static object RepairRow(
        string mailId,
        string qualifiedItemId,
        int required,
        int available,
        Point tile,
        bool completeOrPending,
        string actionType) => new
        {
            mail_id = mailId,
            qualified_item_id = qualifiedItemId,
            required_count = required,
            available_count = available,
            tile_x = tile.X,
            tile_y = tile.Y,
            complete_or_pending = completeOrPending,
            expected_action_type = actionType
        };

    private static object PerchRow(
        PerchProjection perch,
        int expectedNuts,
        bool completeOrPending,
        string expectedRequiredMail) => new
        {
            source_location = perch.LocationId,
            tile_x = perch.Tile.X,
            tile_y = perch.Tile.Y,
            upgrade_name = perch.UpgradeName,
            required_nuts = perch.RequiredNuts,
            expected_required_nuts = expectedNuts,
            required_mail = perch.RequiredMail,
            expected_required_mail = expectedRequiredMail,
            available = perch.Available,
            current_state = perch.CurrentState,
            complete_or_pending = completeOrPending,
            identity_exact = perch.IdentityExact
        };

    private static MapActionProjection FindExactMapAction(GameLocation? location, string expected)
    {
        if (location?.map?.GetLayer("Buildings") is not { } layer)
            return new MapActionProjection(Point.Zero, string.Empty, 0, false);
        var matches = new List<(Point Tile, string Action)>();
        for (var x = 0; x < layer.LayerWidth; x++)
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (string.Equals(action, expected, StringComparison.Ordinal))
                matches.Add((new Point(x, y), action));
        }
        return matches.Count == 1
            ? new MapActionProjection(matches[0].Tile, matches[0].Action, 1, true)
            : new MapActionProjection(Point.Zero, string.Empty, matches.Count, false);
    }

    private static PerchProjection FindPerch(IslandLocation? location, string name, Point tile)
    {
        var matches = location?.parrotUpgradePerches.Where(perch =>
            string.Equals(perch.upgradeName.Value, name, StringComparison.Ordinal) &&
            perch.tilePosition.Value == tile).ToArray() ?? Array.Empty<ParrotUpgradePerch>();
        if (matches.Length != 1)
            return new PerchProjection(location?.NameOrUniqueName ?? string.Empty, tile, string.Empty, -1, string.Empty, false, string.Empty, false);
        var match = matches[0];
        var expectedNuts = name switch { "Hut" => 1, "Turtle" => 10, _ => 20 };
        var expectedMail = name switch { "Resort" => "Island_UpgradeHouse", "Turtle" => FirstParrotMail, _ => string.Empty };
        return new PerchProjection(
            location!.NameOrUniqueName,
            tile,
            match.upgradeName.Value,
            match.requiredNuts.Value,
            match.requiredMail.Value,
            match.IsAvailable(),
            match.currentState.Value.ToString(),
            match.requiredNuts.Value == expectedNuts &&
            string.Equals(match.requiredMail.Value, expectedMail, StringComparison.Ordinal));
    }

    private sealed record MapActionProjection(Point Tile, string RawAction, int Count, bool IdentityExact);
    private sealed record PerchProjection(
        string LocationId,
        Point Tile,
        string UpgradeName,
        int RequiredNuts,
        string RequiredMail,
        bool Available,
        string CurrentState,
        bool IdentityExact);
}
