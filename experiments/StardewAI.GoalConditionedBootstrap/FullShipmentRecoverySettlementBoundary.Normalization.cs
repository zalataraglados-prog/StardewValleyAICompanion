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
            case ("farm", "machines"):
                NormalizeMachineProjectionCache(value);
                break;
            case ("farm", "material_inventory_graph"):
                NormalizeMaterialInventoryGraph(value);
                break;
            case ("farm", "shipping_bins"):
                NormalizeShippingBins(value);
                break;
            case ("player", "mastery_claim"):
            case ("player", "prize_ticket_reward"):
                NormalizeRemoteRewardRuntimeState(value);
                break;
            case ("quests", "adventure_guild_reward"):
                NormalizeAdventureGuildReward(value);
                break;
            case ("quests", "daily_quest_offer"):
                NormalizeDailyQuestOffer(value);
                break;
            case ("quests", "mailbox_processing"):
                NormalizeRemoteQuestRuntimeState(value);
                break;
            case ("quests", "special_order_boards"):
                NormalizeSpecialOrderBoardRuntimeState(value);
                break;
            case ("world_progress", "game_state_query_unlock_state"):
                NormalizeGameStateQueryUnlockState(value);
                break;
            case ("world_progress", "game_state_query_calendar_state"):
                NormalizeGameStateQueryCalendarState(value);
                break;
            case ("world_progress", "island_field_office"):
                NormalizeIslandFieldOfficeRuntimeState(value);
                break;
            case ("world_progress", "marriage_house"):
                NormalizeMarriageHouse(value);
                break;
        }
        RuntimeSnapshotBoundaryNormalization.RemoveNonSemanticMetadata(value);
        return JsonSerializer.SerializeToElement(normalized);
    }

    private static void NormalizeMachineProjectionCache(JsonNode value)
    {
        if (value is not JsonArray machines)
            return;
        foreach (var machine in machines.OfType<JsonObject>())
            machine.Remove("machine_probe_cache_tick");
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
        RemoveFields(reward, "status", "blocked_diagnostics");
    }

    private static void NormalizeDailyQuestOffer(JsonNode value)
    {
        if (value is not JsonObject offer)
            return;
        RemoveFields(offer,
            "can_accept",
            "menu_clear",
            "status",
            "blocked_diagnostics");
        offer.Remove("offer_fingerprint");
        if (offer["quest"] is not JsonObject quest)
            return;
        foreach (var field in new[]
                 {
                     "title",
                     "title_available",
                     "description",
                     "description_available",
                     "current_objective",
                     "current_objective_available",
                     "reward_description"
                 })
        {
            quest.Remove(field);
        }
        if (quest["per_type_fields"] is JsonObject perTypeFields)
            perTypeFields.Remove("target_message");
    }

    private static void NormalizeRemoteRewardRuntimeState(JsonNode value)
    {
        if (value is not JsonObject reward)
            return;
        RemoveFields(reward,
            "current_location_matches",
            "menu_clear",
            "status",
            "blocked_diagnostics");
    }

    private static void NormalizeRemoteQuestRuntimeState(JsonNode value)
    {
        if (value is not JsonObject quest)
            return;
        RemoveFields(quest,
            "menu_clear",
            "status",
            "blocked_diagnostics");
    }

    private static void NormalizeSpecialOrderBoardRuntimeState(JsonNode value)
    {
        if (value is not JsonArray boards)
            return;
        foreach (var board in boards.OfType<JsonObject>())
        {
            RemoveFields(board,
                "menu_open",
                "dialogue_ready_for_board",
                "status",
                "blocked_diagnostics");
        }
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

    private static void NormalizeGameStateQueryCalendarState(JsonNode value)
    {
        if (value is JsonObject calendar)
            calendar.Remove("time_of_day");
    }

    private static void NormalizeIslandFieldOfficeRuntimeState(JsonNode value)
    {
        if (value is not JsonObject fieldOffice)
            return;
        foreach (var field in new[]
                 {
                     "is_current_location",
                     "mutex_locked",
                     "menu_clear"
                 })
        {
            fieldOffice.Remove(field);
        }
    }

    private static void NormalizeMarriageHouse(JsonNode value)
    {
        if (value is not JsonObject marriageHouse)
            return;
        marriageHouse.Remove("robin_present_at_counter");
        if (marriageHouse["home_renovations"] is JsonObject renovations)
            renovations.Remove("robin_present_at_service");
    }
}
