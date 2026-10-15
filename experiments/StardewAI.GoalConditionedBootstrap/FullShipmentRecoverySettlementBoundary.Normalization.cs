using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class FullShipmentRecoverySettlementBoundaryProjector
{
    private static JsonElement NormalizePersistentEnvelope(
        string domain,
        string field,
        JsonElement envelope)
    {
        var normalizedEnvelope = NormalizeEnvelope(envelope);
        if (normalizedEnvelope.ValueKind != JsonValueKind.Object)
            return normalizedEnvelope;
        var normalized = JsonNode.Parse(normalizedEnvelope.GetRawText())
            as JsonObject ?? throw new InvalidDataException(
                $"Persistent boundary field {domain}.{field} is invalid.");
        if (normalized["value"] is not JsonNode value)
            return JsonSerializer.SerializeToElement(normalized);

        switch (domain, field)
        {
            case ("farm", "material_inventory_graph"):
                NormalizeMaterialInventoryGraph(value);
                break;
            case ("farm", "shipping_bins"):
                NormalizeShippingBins(value);
                break;
            case ("quests", "adventure_guild_reward"):
                NormalizeAdventureGuildReward(value);
                break;
            case ("world_progress", "game_state_query_unlock_state"):
                NormalizeGameStateQueryUnlockState(value);
                break;
        }
        return JsonSerializer.SerializeToElement(normalized);
    }

    private static void NormalizeMaterialInventoryGraph(JsonNode value)
    {
        if (value["inventory_nodes"] is not JsonArray nodes)
            return;
        foreach (var node in nodes.OfType<JsonObject>().Where(node =>
                     string.Equals(
                         node["inventory_kind"]?.GetValue<string>(),
                         "player_inventory",
                         StringComparison.Ordinal)))
        {
            node.Remove("location_id");
        }
    }

    private static void NormalizeShippingBins(JsonNode value)
    {
        if (value is not JsonArray bins)
            return;
        string[] routeFields =
        [
            "distance_to_player",
            "player_within_shipping_range",
            "interaction_stand_tile_x",
            "interaction_stand_tile_y",
            "interaction_stand_tile_blocked_reason",
            "stand_tiles"
        ];
        foreach (var bin in bins.OfType<JsonObject>())
        {
            foreach (var field in routeFields)
                bin.Remove(field);
        }
    }

    private static void NormalizeAdventureGuildReward(JsonNode value)
    {
        if (value is not JsonObject reward)
            return;
        string[] routeFields =
        [
            "current_location_matches",
            "action_tile_x",
            "action_tile_y",
            "action_tile_index",
            "stand_tile_x",
            "stand_tile_y",
            "menu_clear"
        ];
        foreach (var field in routeFields)
            reward.Remove(field);
    }

    private static void NormalizeGameStateQueryUnlockState(JsonNode value)
    {
        if (value["players"] is not JsonArray players)
            return;
        foreach (var player in players.OfType<JsonObject>())
        {
            if (player["stats"] is JsonObject stats)
                stats.Remove("stepsTaken");
        }
    }
}
