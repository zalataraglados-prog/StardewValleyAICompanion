using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPrizeTicketRewardFixture(TrainingExecutionRequest request)
    {
        var cases = new Dictionary<string, (string Stage, int Level, bool Full, int HouseLevel)>(StringComparer.Ordinal)
        {
            ["collect_pending"] = ("collect_pending_ticket", 0, false, 0),
            ["redeem_space_level_0"] = ("redeem_prize", 0, false, 0),
            ["redeem_space_level_5_upgraded"] = ("redeem_prize", 5, false, 1),
            ["redeem_full_level_21"] = ("redeem_prize", 21, true, 1),
            ["redeem_cycle_level_22"] = ("redeem_prize", 22, false, 1)
        };
        var reasons = ValidateExecutionRequest(request);
        if (!cases.TryGetValue(request.PrizeTicketFixtureCase, out var fixture))
            reasons.Add("prize_ticket_fixture_case_invalid");
        var town = Game1.getLocationFromName("Town") as Town;
        var manor = Game1.getLocationFromName("ManorHouse") as ManorHouse;
        if (town?.GetType() != typeof(Town) || manor?.GetType() != typeof(ManorHouse))
            reasons.Add("prize_ticket_fixture_base_locations_unavailable");
        if (reasons.Count > 0 || town is null || manor is null)
            return BlockedWithPrimitive(request, "debug_setup_prize_ticket_reward",
                "prize_ticket_fixture=ready", "prize_ticket_fixture=blocked", reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.currentMinigame = null;
        StopAllMovement();
        if (town.doesTileHaveProperty(60, 93, "Action", "Buildings") != "SpecialOrdersPrizeTickets")
            town.setMapTile(60, 93, 2034, "Buildings", "Town", "SpecialOrdersPrizeTickets");

        Game1.player.HouseUpgradeLevel = fixture.HouseLevel;
        Game1.stats.Set("ticketPrizesClaimed", (uint)fixture.Level);
        Game1.player.stats.Set("specialOrderPrizeTickets", fixture.Stage == "collect_pending_ticket" ? 1u : 0u);
        for (var index = 0; index < Math.Min(Game1.player.MaxItems, Game1.player.Items.Count); index++)
            Game1.player.Items[index] = null;
        if (fixture.Stage == "redeem_prize")
            Game1.player.Items[0] = ItemRegistry.Create("(O)PrizeTicket");
        if (fixture.Full)
        {
            for (var index = 1; index < Math.Min(Game1.player.MaxItems, Game1.player.Items.Count); index++)
                Game1.player.Items[index] = ItemRegistry.Create("(O)388", 999);
        }

        var targetLocation = fixture.Stage == "redeem_prize" ? (GameLocation)manor : town;
        var token = fixture.Stage == "redeem_prize" ? "PrizeMachine" : "SpecialOrdersPrizeTickets";
        var endpoint = FindPrizeTicketFixtureEndpoint(targetLocation, token);
        if (endpoint is null)
            return BlockedWithPrimitive(request, "debug_setup_prize_ticket_reward",
                "prize_ticket_fixture=ready", "prize_ticket_fixture=endpoint_missing", "prize_ticket_fixture_native_endpoint_unavailable");
        var reward = PrizeTicketMenu.getPrizeItem(fixture.Level);
        for (var index = targetLocation.debris.Count - 1; index >= 0; index--)
            if (DebrisQualifiedItemId(targetLocation.debris[index]) == reward.QualifiedItemId)
                targetLocation.debris.RemoveAt(index);
        Game1.currentLocation = targetLocation;
        Game1.player.currentLocation = targetLocation;
        targetLocation.currentEvent = null;
        Game1.player.Position = endpoint.Value.Stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.player.CurrentToolIndex = 0;

        var projection = ReadLivePrizeTicketRewardProjection();
        var verified = projection is not null && projection.Stage == fixture.Stage && projection.ServiceStatus == "ready" &&
            projection.CurrentPrizeLevel == fixture.Level && projection.CurrentReward?.QualifiedItemId == reward.QualifiedItemId &&
            projection.CurrentReward.Stack == reward.Stack && projection.PreviewTrack.Length == 4 &&
            projection.InventoryTicketCount == (fixture.Stage == "redeem_prize" ? 1 : 0) &&
            projection.PendingSpecialOrderTicketCount == (fixture.Stage == "collect_pending_ticket" ? 1 : 0) &&
            (!fixture.Full || projection.InventoryOccupiedSlots == Game1.player.MaxItems);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_prize_ticket_reward",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_native_prize_ticket_fixture_ready:" + request.PrizeTicketFixtureCase }
                : new[] { "prize_ticket_fixture_receipt_mismatch" },
            RequestedEffect = "prize_ticket_fixture=" + request.PrizeTicketFixtureCase,
            ObservedEffect = "stage=" + projection?.Stage + ";level=" + projection?.CurrentPrizeLevel +
                ";inventory_tickets=" + projection?.InventoryTicketCount + ";pending_tickets=" + projection?.PendingSpecialOrderTicketCount,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "prize_ticket_fixture_receipt_mismatch" }
        };
    }

    private static (Point Action, Point Stand)? FindPrizeTicketFixtureEndpoint(GameLocation location, string token)
    {
        var buildings = location.Map?.GetLayer("Buildings");
        if (buildings is null) return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            if (location.doesTileHaveProperty(x, y, "Action", "Buildings") != token) continue;
            var action = new Point(x, y);
            foreach (var stand in Neighbors(action))
                if (IsTileOnMap(location, stand) && IsTileWalkable(location, stand) && !IsTileOccupiedByCharacter(location, stand))
                    return (action, stand);
        }
        return null;
    }
}
