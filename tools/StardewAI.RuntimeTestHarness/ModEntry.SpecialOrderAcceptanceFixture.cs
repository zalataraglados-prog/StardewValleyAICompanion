using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.SpecialOrders;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSpecialOrderAcceptanceFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var town = Game1.getLocationFromName("Town");
        if (town is null)
        {
            reasons.Add("special_order_fixture_town_missing");
        }
        if (reasons.Count > 0 || town is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_special_order_acceptance",
                "ordinary_special_order_board=ready",
                "ordinary_special_order_board=unavailable",
                reasons.ToArray());
        }

        if (Game1.stats.DaysPlayed < 58)
        {
            Game1.stats.DaysPlayed = 58;
        }
        town.MakeMapModifications(force: true);
        var endpoint = FindSpecialOrderFixtureEndpoint(town, "SpecialOrders");
        if (!endpoint.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_special_order_acceptance",
                "ordinary_special_order_board=ready",
                "ordinary_special_order_board=endpoint_missing",
                "special_order_fixture_board_endpoint_missing");
        }

        var team = Game1.player.team;
        team.specialOrders.RemoveWhere(order => string.IsNullOrEmpty(order.orderType.Value));
        SpecialOrder.RemoveAllSpecialOrders(string.Empty);
        SpecialOrder.UpdateAvailableSpecialOrders(string.Empty, forceRefresh: true);
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.currentLocation = town;
        Game1.player.currentLocation = town;
        town.currentEvent = null;
        Game1.player.Position = endpoint.Value.Stand.ToVector2() * Game1.tileSize;
        Game1.player.faceDirection(DirectionTo(endpoint.Value.Stand, endpoint.Value.Action));
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;

        var offers = team.availableSpecialOrders
            .Where(order => string.IsNullOrEmpty(order.orderType.Value))
            .Take(2)
            .ToArray();
        var verified = offers.Length > 0 &&
            !team.acceptedSpecialOrderTypes.Contains(string.Empty) &&
            Game1.player.TilePoint == endpoint.Value.Stand;
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
            PrimitiveKind = "debug_setup_special_order_acceptance",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_available_special_orders_refreshed", "live_SpecialOrders_endpoint_selected" }
                : new[] { "special_order_fixture_receipt_mismatch" },
            RequestedEffect = "ordinary_special_order_board=ready",
            ObservedEffect = "offer_count=" + offers.Length +
                ";offer_keys=" + string.Join(",", offers.Select(order => order.questKey.Value)) +
                ";accepted_type=" + team.acceptedSpecialOrderTypes.Contains(string.Empty).ToString().ToLowerInvariant() +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";action_tile=" + endpoint.Value.Action.X + "," + endpoint.Value.Action.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "special_order_fixture_receipt_mismatch" }
        };
    }

    private static (Point Action, Point Stand)? FindSpecialOrderFixtureEndpoint(
        GameLocation location,
        string actionToken)
    {
        var buildings = location.Map?.GetLayer("Buildings");
        if (buildings is null) return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var raw = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                var token = raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.Equals(token, actionToken, StringComparison.Ordinal)) continue;
                foreach (var stand in new[]
                         {
                             new Point(x, y + 1), new Point(x - 1, y),
                             new Point(x + 1, y), new Point(x, y - 1)
                         })
                {
                    if (location.isTilePassable(new xTile.Dimensions.Location(stand.X, stand.Y), Game1.viewport))
                        return (new Point(x, y), stand);
                }
            }
        }
        return null;
    }
}
