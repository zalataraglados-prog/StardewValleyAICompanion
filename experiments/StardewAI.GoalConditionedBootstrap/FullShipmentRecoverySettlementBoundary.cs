using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record FullShipmentRecoverySettlementBoundary(
    string ContractVersion,
    string SaveId,
    string PlayerId,
    string GameVersion,
    int TotalDay,
    string InventorySha256,
    string CurrencySha256,
    string PersistentStateSha256);

internal static class FullShipmentRecoverySettlementBoundaryProjector
{
    internal const string ContractVersion =
        "full_shipment_recovery_settlement_boundary.v1";

    private static readonly HashSet<string> VolatileDomains = new(
        StringComparer.Ordinal)
    {
        "current_location",
        "fishing",
        "locations",
        "menus",
        "mining",
        "npcs",
        "volcano",
        "machine_probe_fixture"
    };

    private static readonly HashSet<string> VolatileTimeFields = new(
        StringComparer.Ordinal)
    {
        "time"
    };

    // These fields describe movement, open UI, or location-dependent action
    // affordances. New player fields are persistent unless added here.
    private static readonly HashSet<string> VolatilePlayerFields = new(
        StringComparer.Ordinal)
    {
        "active_menu",
        "active_object_qualified_id",
        "bobber_selection",
        "book_candidates",
        "building_construction_catalog",
        "building_paint_catalog",
        "building_skin_catalog",
        "calico_jack",
        "cooking",
        "cookout_kit_placement",
        "crab_pot_network",
        "crab_pot_placement",
        "crane_game",
        "current_item_qualified_id",
        "current_mine_level",
        "current_tool",
        "current_tool_index",
        "customization",
        "darts_game",
        "emote",
        "facing_direction",
        "fair_fishing_game",
        "fair_slingshot_game",
        "fair_strength_game",
        "fair_wheel_spin",
        "fence_placement",
        "firework_placement",
        "flooring_placement",
        "forge",
        "furniture_placement",
        "geode_processing",
        "grange_display",
        "grass_placement",
        "horse_flute",
        "jukebox_selection",
        "location_id",
        "machine_crafting",
        "machine_placement",
        "monster_musk",
        "movie_theater",
        "movement_timing_context",
        "multiplayer_chat",
        "multiplayer_runtime",
        "object_trap_recovery",
        "prairie_king",
        "quest_building_construction",
        "quest_crafting",
        "rain_totem",
        "return_scepter",
        "safe_item_context",
        "secret_note_candidates",
        "sign_placement",
        "slots",
        "storage_crafting",
        "storage_placement",
        "story_event",
        "tailoring",
        "temporary_sleep",
        "tent_placement",
        "tile_x",
        "tile_y",
        "treasure_totem",
        "warp_totem"
    };

    private static readonly HashSet<string> VolatileFarmFields = new(
        StringComparer.Ordinal)
    {
        "animal_purchase_catalog",
        "animals",
        "crop_catalog",
        "debris",
        "initial_pet_adoption",
        "material_inventory_graph",
        "pets",
        "warps"
    };

    private static readonly HashSet<string> VolatileQuestFields = new(
        StringComparer.Ordinal)
    {
        "adventure_guild_reward"
    };

    internal static bool TryProject(
        SnapshotEnvelope snapshot,
        out FullShipmentRecoverySettlementBoundary? projection)
    {
        projection = null;
        if (!ReadableIdentity(snapshot.SaveId, out var saveId) ||
            !ReadableIdentity(snapshot.PlayerId, out var playerId) ||
            string.IsNullOrWhiteSpace(snapshot.GameVersion) ||
            !TryStateValue(snapshot, "time", "total_days", out var totalDay) ||
            !totalDay.TryGetInt32(out var totalDayValue) ||
            totalDayValue < 0 ||
            !TryStateValue(snapshot, "player", "inventory", out var inventory) ||
            inventory.ValueKind != JsonValueKind.Array ||
            !TryStateValue(snapshot, "farm", "shipping_bins", out _) ||
            !snapshot.State.ContainsKey("world_progress"))
        {
            return false;
        }

        var currencyFields = new[]
        {
            "money",
            "club_coins",
            "shop_currency_balances",
            "total_money_earned"
        };
        var currencyValues = new List<KeyValuePair<string, JsonElement>>();
        foreach (var field in currencyFields)
        {
            if (!TryStateValue(snapshot, "player", field, out var value))
                return false;
            currencyValues.Add(new KeyValuePair<string, JsonElement>(
                field,
                value.Clone()));
        }

        projection = new FullShipmentRecoverySettlementBoundary(
            ContractVersion,
            saveId,
            playerId,
            snapshot.GameVersion,
            totalDayValue,
            HashCanonical(new[]
            {
                new KeyValuePair<string, JsonElement>(
                    "player.inventory",
                    inventory.Clone())
            }),
            HashCanonical(currencyValues),
            HashPersistentState(snapshot));
        return true;
    }

    private static string HashPersistentState(SnapshotEnvelope snapshot)
    {
        var rows = new List<KeyValuePair<string, JsonElement>>();
        foreach (var domain in snapshot.State.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (VolatileDomains.Contains(domain.Key))
                continue;
            if (domain.Value.ValueKind != JsonValueKind.Object)
            {
                rows.Add(new KeyValuePair<string, JsonElement>(
                    domain.Key,
                    domain.Value.Clone()));
                continue;
            }

            foreach (var field in domain.Value.EnumerateObject()
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                if (IsVolatile(domain.Key, field.Name))
                    continue;
                rows.Add(new KeyValuePair<string, JsonElement>(
                    domain.Key + "." + field.Name,
                    NormalizeEnvelope(domain.Key, field.Name, field.Value)));
            }
        }
        return HashCanonical(rows);
    }

    private static bool IsVolatile(string domain, string field) =>
        domain == "time" && VolatileTimeFields.Contains(field) ||
        domain == "player" && VolatilePlayerFields.Contains(field) ||
        domain == "farm" && VolatileFarmFields.Contains(field) ||
        domain == "quests" && VolatileQuestFields.Contains(field);

    private static JsonElement NormalizeEnvelope(
        string domain,
        string field,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("status", out var status))
        {
            return value.Clone();
        }

        value.TryGetProperty("value", out var fieldValue);
        value.TryGetProperty("reason", out var reason);
        return JsonSerializer.SerializeToElement(new
        {
            status = status.ValueKind == JsonValueKind.String
                ? status.GetString()
                : status.GetRawText(),
            value = fieldValue.ValueKind == JsonValueKind.Undefined
                ? (JsonElement?)null
                : NormalizeFieldValue(domain, field, fieldValue),
            reason = reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null
        });
    }

    private static JsonElement NormalizeFieldValue(
        string domain,
        string field,
        JsonElement value)
    {
        var node = JsonNode.Parse(value.GetRawText());
        if (node is null)
            return value.Clone();

        if (domain == "farm" && field == "machines" &&
            node is JsonArray machines)
        {
            foreach (var machine in machines.OfType<JsonObject>())
                machine.Remove("machine_probe_cache_tick");
        }
        else if (domain == "farm" && field == "shipping_bins" &&
                 node is JsonArray bins)
        {
            foreach (var bin in bins.OfType<JsonObject>())
            {
                bin.Remove("distance_to_player");
                bin.Remove("player_within_shipping_range");
                bin.Remove("interaction_stand_tile_x");
                bin.Remove("interaction_stand_tile_y");
                bin.Remove("interaction_stand_tile_blocked_reason");
                bin.Remove("stand_tiles");
            }
        }
        else if (domain == "player" && field == "active_dialogue_events" &&
                 node is JsonArray dialogueEvents)
        {
            for (var index = dialogueEvents.Count - 1; index >= 0; index--)
            {
                if (dialogueEvents[index]?.GetValueKind() ==
                        JsonValueKind.String &&
                    dialogueEvents[index]!.GetValue<string>()
                        .StartsWith("firstVisit_", StringComparison.Ordinal))
                {
                    dialogueEvents.RemoveAt(index);
                }
            }
        }
        else if (domain == "world_progress" &&
                 field == "game_state_query_unlock_state" &&
                 node is JsonObject unlockState &&
                 unlockState["players"] is JsonArray players)
        {
            foreach (var player in players.OfType<JsonObject>())
            {
                if (player["stats"] is JsonObject stats)
                    stats.Remove("stepsTaken");
            }
        }

        return JsonSerializer.SerializeToElement(node);
    }

    private static string HashCanonical(
        IEnumerable<KeyValuePair<string, JsonElement>> values)
    {
        var builder = new StringBuilder();
        foreach (var value in values.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            builder.Append(value.Key);
            builder.Append('=');
            builder.Append(SnapshotHash.Canonicalize(value.Value));
            builder.Append('\n');
        }
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static bool TryStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is FieldStatus.Available or FieldStatus.Derived &&
            envelope.TryGetProperty("value", out value);
    }

    private static bool ReadableIdentity(
        FieldEnvelope<string?> field,
        out string value)
    {
        value = field.Value ?? string.Empty;
        return FieldEnvelopeValidator.IsReadableStatus(field.Status) &&
            !string.IsNullOrWhiteSpace(value);
    }
}
