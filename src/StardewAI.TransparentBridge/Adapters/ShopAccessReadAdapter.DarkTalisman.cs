using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter
{
    private const string DarkTalismanEventId = "529952";
    private const string DarkTalismanQuestId = "28";
    private const int DarkTalismanSpecialItemId = 6;
    private static readonly Point DarkTalismanChestTile = new(31, 5);
    private static readonly Point WitchStatueTile = new(54, 35);
    private static readonly Point WitchWarpTile = new(4, 5);

    private static object ReadDarkTalismanAccess()
    {
        var player = Game1.player;
        var master = Game1.MasterPlayer;
        var railroad = Game1.getLocationFromName("Railroad") as Railroad;
        var sewer = Game1.getLocationFromName("Sewer");
        var bugLand = Game1.getLocationFromName("BugLand");
        var witchWarpCave = Game1.getLocationFromName("WitchWarpCave");
        var witchSwamp = Game1.getLocationFromName("WitchSwamp");
        var currentLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;

        var communityCenterOrWarehouseDone =
            master.eventsSeen.Contains("191393") ||
            master.eventsSeen.Contains("502261") ||
            master.hasCompletedCommunityCenter();
        var railroadEventSeen = player.eventsSeen.Contains(DarkTalismanEventId);
        var railroadEventActive = Game1.eventUp &&
            string.Equals(Game1.CurrentEvent?.id, DarkTalismanEventId, StringComparison.Ordinal);
        var railroadEventEligible = railroad is not null &&
            !railroadEventSeen &&
            Event.CheckPrecondition(railroad, DarkTalismanEventId, "C");
        var questActive = player.hasQuest(DarkTalismanQuestId);
        var sewerUnsealed = player.mailReceived.Contains("krobusUnseal");
        var hasDarkTalisman = player.hasDarkTalisman;
        var statueGoneMail = player.mailReceived.Contains("witchStatueGone") ||
            master.mailReceived.Contains("witchStatueGone");
        var statueGoneNetField = railroad?.witchStatueGone.Value == true;
        var statueGone = statueGoneMail || statueGoneNetField;

        var krobus = Game1.getCharacterFromName("Krobus");
        var krobusLocation = krobus?.currentLocation?.NameOrUniqueName ?? string.Empty;
        var krobusInSewer = krobus is not null &&
            string.Equals(krobusLocation, "Sewer", StringComparison.Ordinal);

        Chest? darkTalismanChest = null;
        StardewValley.Object? chestObject = null;
        var chestObjectPresent = bugLand?.overlayObjects.TryGetValue(
            DarkTalismanChestTile.ToVector2(),
            out chestObject) == true;
        if (chestObjectPresent)
            darkTalismanChest = chestObject as Chest;
        var specialItems = darkTalismanChest?.Items
            .OfType<SpecialItem>()
            .Select(item => item.which.Value)
            .OrderBy(value => value)
            .ToArray() ?? Array.Empty<int>();
        var darkTalismanChestExact = darkTalismanChest is not null &&
            darkTalismanChest.Items.Count == 1 &&
            specialItems.Length == 1 &&
            specialItems[0] == DarkTalismanSpecialItemId;

        var statueTileIndex = railroad?.getTileIndexAt(
            WitchStatueTile.X,
            WitchStatueTile.Y,
            "Buildings",
            "untitled tile sheet") ?? -1;
        var statueIdentityExact = statueGone || statueTileIndex == 287;

        var witchWarpAction = witchWarpCave?.doesTileHaveProperty(
            WitchWarpTile.X,
            WitchWarpTile.Y,
            "TouchAction",
            "Back");
        var witchWarpIdentityExact = string.Equals(
            witchWarpAction,
            "MagicWarp WitchSwamp 20 42",
            StringComparison.Ordinal);

        var status = DarkTalismanAccessStatus(
            currentLocation,
            communityCenterOrWarehouseDone,
            railroadEventSeen,
            railroadEventActive,
            railroadEventEligible,
            questActive,
            sewerUnsealed,
            krobusInSewer,
            hasDarkTalisman,
            darkTalismanChestExact,
            statueGone,
            statueIdentityExact,
            witchWarpIdentityExact);

        return new
        {
            schema_version = "dark_talisman_access.v1",
            projection_status = "complete_locked_base_1.6.15_native_contract",
            status,
            current_location_id = currentLocation,
            community_center_or_warehouse_done = communityCenterOrWarehouseDone,
            railroad_event_id = DarkTalismanEventId,
            railroad_event_key = "529952/C",
            railroad_event_seen = railroadEventSeen,
            railroad_event_active = railroadEventActive,
            railroad_event_eligible = railroadEventEligible,
            dark_talisman_quest_id = DarkTalismanQuestId,
            dark_talisman_quest_active = questActive,
            krobus_unseal_mail_received = sewerUnsealed,
            krobus = new
            {
                present = krobus is not null,
                location_id = krobusLocation,
                in_sewer = krobusInSewer,
                tile_x = krobus?.TilePoint.X,
                tile_y = krobus?.TilePoint.Y,
                runtime_type = krobus?.GetType().FullName ?? string.Empty
            },
            bug_lair = new
            {
                location_loaded = bugLand is not null,
                chest_tile_x = DarkTalismanChestTile.X,
                chest_tile_y = DarkTalismanChestTile.Y,
                overlay_object_present = chestObjectPresent,
                chest_runtime_type = darkTalismanChest?.GetType().FullName ?? string.Empty,
                chest_item_count = darkTalismanChest?.Items.Count,
                special_item_ids = specialItems,
                contains_exact_dark_talisman = darkTalismanChestExact,
                expected_special_item_which = DarkTalismanSpecialItemId
            },
            player_has_dark_talisman = hasDarkTalisman,
            railroad_statue = new
            {
                location_loaded = railroad is not null,
                tile_x = WitchStatueTile.X,
                tile_y = WitchStatueTile.Y,
                buildings_tile_index = statueTileIndex,
                expected_buildings_tile_index = 287,
                expected_tilesheet = "untitled tile sheet",
                identity_exact = statueIdentityExact,
                witch_statue_gone_net_field = statueGoneNetField,
                witch_statue_gone_mail_received = statueGoneMail,
                gone = statueGone
            },
            witch_warp = new
            {
                source_location = "WitchWarpCave",
                source_tile_x = WitchWarpTile.X,
                source_tile_y = WitchWarpTile.Y,
                source_property = "Back.TouchAction",
                raw_action = witchWarpAction ?? string.Empty,
                expected_raw_action = "MagicWarp WitchSwamp 20 42",
                target_location = "WitchSwamp",
                target_tile_x = 20,
                target_tile_y = 42,
                identity_exact = witchWarpIdentityExact,
                target_location_loaded = witchSwamp is not null
            },
            native_contract = "Events_Railroad_529952_C_addQuest_28_then_NPC_Krobus_checkAction_removes_quest_and_adds_krobusUnseal_then_BugLand_overlay_Chest_SpecialItem_6_sets_HasDarkTalisman_then_Railroad_checkAction_tile_287_sets_witchStatueGone_then_WitchWarpCave_MagicWarp_WitchSwamp_20_42",
            fresh_snapshot_after_each_primitive = true
        };
    }

    private static string DarkTalismanAccessStatus(
        string currentLocation,
        bool communityCenterOrWarehouseDone,
        bool railroadEventSeen,
        bool railroadEventActive,
        bool railroadEventEligible,
        bool questActive,
        bool sewerUnsealed,
        bool krobusInSewer,
        bool hasDarkTalisman,
        bool darkTalismanChestExact,
        bool statueGone,
        bool statueIdentityExact,
        bool witchWarpIdentityExact)
    {
        if (statueGone)
            return witchWarpIdentityExact
                ? "witch_swamp_accessible"
                : "blocked_witch_magic_warp_identity_drifted";
        if (hasDarkTalisman)
            return statueIdentityExact
                ? "ready_to_remove_railroad_statue"
                : "blocked_railroad_statue_identity_drifted";
        if (sewerUnsealed)
        {
            if (!string.Equals(currentLocation, "BugLand", StringComparison.Ordinal))
                return "route_to_bug_lair_chest";
            return darkTalismanChestExact
                ? "ready_to_claim_dark_talisman"
                : "blocked_dark_talisman_chest_identity_drifted";
        }
        if (questActive)
            return krobusInSewer
                ? "ready_to_talk_to_krobus"
                : "blocked_krobus_sewer_identity_drifted";
        if (railroadEventActive)
            return "advance_railroad_event";
        if (!communityCenterOrWarehouseDone)
            return "requires_community_center_or_joja_completion";
        if (!railroadEventSeen && railroadEventEligible)
            return "route_to_railroad_event";
        return railroadEventSeen
            ? "blocked_railroad_event_quest_transition_drifted"
            : "blocked_railroad_event_precondition_drifted";
    }
}
