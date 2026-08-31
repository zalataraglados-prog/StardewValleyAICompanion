using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string PrizeTicketRewardNativeContract =
        "Town.SpecialOrdersPrizeTickets->inventory_PrizeTicket_and_pending_stat_minus_one;ManorHouse.PrizeMachine->PrizeTicketMenu.currentPrizeTrack[0]->inventory_else_debris->PrizeTicket_minus_one->ticketPrizesClaimed_plus_one";

    private static PrizeTicketRewardProjectionRef? ReadPrizeTicketReward(Farmer? player)
    {
        if (player is null) return null;

        var inventoryTickets = player.Items.CountId("PrizeTicket");
        var pendingTickets = checked((int)player.stats.Get("specialOrderPrizeTickets"));
        var claimed = checked((int)Game1.stats.Get("ticketPrizesClaimed"));
        var preview = Enumerable.Range(0, 4)
            .Select(offset => PrizeTicketRewardItem(PrizeTicketMenu.getPrizeItem(claimed + offset), claimed + offset))
            .ToArray();
        var machineTiles = FindPrizeTicketActionTiles("ManorHouse", "PrizeMachine");
        var pendingTiles = FindPrizeTicketActionTiles("Town", "SpecialOrdersPrizeTickets");
        var stage = inventoryTickets > 0 ? "redeem_prize" : pendingTickets > 0 ? "collect_pending_ticket" : "none";
        var targetLocation = stage == "redeem_prize" ? "ManorHouse" : stage == "collect_pending_ticket" ? "Town" : string.Empty;
        var targetTiles = stage == "redeem_prize" ? machineTiles : stage == "collect_pending_ticket" ? pendingTiles : Array.Empty<PrizeTicketActionTileRef>();
        var currentMatches = string.Equals(Game1.currentLocation?.NameOrUniqueName, targetLocation, StringComparison.OrdinalIgnoreCase);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var canAcceptPendingTicket = player.couldInventoryAcceptThisItem(ItemRegistry.Create("(O)PrizeTicket"));
        var reasons = new List<string>();
        if (stage == "none") reasons.Add("prize_ticket_no_inventory_or_pending_ticket");
        if (stage == "collect_pending_ticket" && !canAcceptPendingTicket) reasons.Add("prize_ticket_pending_ticket_inventory_capacity_insufficient");
        if (stage != "none" && targetTiles.Length == 0) reasons.Add("prize_ticket_native_action_endpoint_unavailable:" + targetLocation);
        if (!menuClear) reasons.Add("prize_ticket_menu_or_dialogue_not_clear");

        var projection = new PrizeTicketRewardProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15",
            NativeContract = PrizeTicketRewardNativeContract,
            Stage = stage,
            TargetLocationId = targetLocation,
            CurrentLocationMatches = currentMatches,
            MenuClear = menuClear,
            InventoryTicketCount = inventoryTickets,
            PendingSpecialOrderTicketCount = pendingTickets,
            AvailableTicketCount = inventoryTickets + pendingTickets,
            TicketPrizesClaimed = claimed,
            CurrentPrizeLevel = claimed,
            CurrentReward = preview[0],
            CurrentRewardFingerprint = PrizeTicketRewardIdentity.ComputeRewardFingerprint(preview[0]),
            PreviewTrack = preview,
            PrizeMachineActionTiles = machineTiles,
            SpecialOrderTicketActionTiles = pendingTiles,
            InventoryMaxItems = player.MaxItems,
            InventoryOccupiedSlots = player.Items.Take(player.MaxItems).Count(item => item is not null),
            PendingTicketCapacitySufficient = canAcceptPendingTicket,
            GameId = Game1.uniqueIDForThisGame,
            PlayerId = player.UniqueMultiplayerID,
            HouseUpgradeLevel = player.HouseUpgradeLevel,
            Season = Game1.currentSeason,
            DayOfMonth = Game1.dayOfMonth,
            ServiceStatus = reasons.Count > 0
                ? "blocked"
                : currentMatches ? "ready" : "route_required",
            BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
        projection.ProjectionFingerprint = PrizeTicketRewardIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static PrizeTicketRewardItemRef PrizeTicketRewardItem(Item item, int prizeLevel) => new()
    {
        PrizeLevel = prizeLevel,
        QualifiedItemId = item.QualifiedItemId,
        ItemId = item.ItemId,
        DisplayName = item.DisplayName,
        Stack = item.Stack,
        Quality = item.Quality,
        RuntimeType = item.GetType().FullName ?? string.Empty
    };

    private static PrizeTicketActionTileRef[] FindPrizeTicketActionTiles(string locationName, string actionToken)
    {
        var location = Game1.getLocationFromName(locationName);
        var buildings = location?.map?.GetLayer("Buildings");
        if (location is null || buildings is null) return Array.Empty<PrizeTicketActionTileRef>();
        var rows = new List<PrizeTicketActionTileRef>();
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (!string.Equals(action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), actionToken, StringComparison.Ordinal))
                continue;
            rows.Add(new PrizeTicketActionTileRef
            {
                LocationId = location.NameOrUniqueName,
                TileX = x,
                TileY = y,
                ActionRaw = action!
            });
        }
        return rows.OrderBy(row => row.TileY).ThenBy(row => row.TileX).ToArray();
    }
}
