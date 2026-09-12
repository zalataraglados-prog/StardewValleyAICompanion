using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.SpecialOrders;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static object? ReadGameStateQueryUnlockState()
    {
        var current = Context.IsWorldReady ? Game1.player : null;
        var host = Context.IsWorldReady ? Game1.MasterPlayer : null;
        if (current is null || host is null)
        {
            return null;
        }

        var currentPlayerId = current.UniqueMultiplayerID;
        var hostPlayerId = host.UniqueMultiplayerID;
        var players = Game1.getAllFarmers()
            .Where(player => player is not null)
            .GroupBy(player => player.UniqueMultiplayerID)
            .Select(group => group.First())
            .OrderBy(player => player.UniqueMultiplayerID)
            .Select(player => new
            {
                player_id = player.UniqueMultiplayerID.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                is_current = player.UniqueMultiplayerID == currentPlayerId,
                is_host = player.UniqueMultiplayerID == hostPlayerId,
                mail_received = player.mailReceived
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                mail_for_tomorrow = player.mailForTomorrow
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                mailbox = player.mailbox
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                stats = player.stats.Values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal),
                active_special_order_ids = player.team.specialOrders
                    .Where(order => order.questState.Value ==
                        SpecialOrderStatus.InProgress)
                    .Select(order => order.questKey.Value ?? string.Empty)
                    .Where(id => id.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                active_special_order_rules = player.team.specialOrders
                    .Where(order => order.questState.Value ==
                        SpecialOrderStatus.InProgress &&
                        order.specialRule.Value is not null)
                    .SelectMany(order => order.specialRule.Value!
                        .Split(',', StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray()
            })
            .ToArray();
        var islandNorth = Game1.getLocationFromName("IslandNorth") as IslandNorth;

        return new
        {
            current_player_id = currentPlayerId.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            host_player_id = hostPlayerId.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            target_player_resolution_status = "source_context_required",
            players,
            island_north_bridge_fixed = islandNorth?.bridgeFixed.Value,
            island_north_bridge_state_available = islandNorth is not null
        };
    }
}
