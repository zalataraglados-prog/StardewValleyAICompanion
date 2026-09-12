using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record UnlockPlayerState(
    string PlayerId,
    HashSet<string> MailReceived,
    HashSet<string> MailForTomorrow,
    HashSet<string> Mailbox,
    Dictionary<string, long> Stats,
    HashSet<string> ActiveSpecialOrderIds,
    HashSet<string> ActiveSpecialOrderRules);

internal sealed record UnlockSnapshotState(
    bool Available,
    string BlockingReason,
    string CurrentPlayerId,
    string HostPlayerId,
    UnlockPlayerState[] Players,
    bool? IslandNorthBridgeFixed)
{
    private const string FieldName = "game_state_query_unlock_state";
    private const string ExpectedAdapter = "vanilla_1_6_15_gsq_unlock";

    public static UnlockSnapshotState Read(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("state", out var state) ||
            state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("world_progress", out var world) ||
            world.ValueKind != JsonValueKind.Object ||
            !world.TryGetProperty(FieldName, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object)
        {
            return Unavailable("game_state_query_unlock_state_missing");
        }
        if (!envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() != "available")
        {
            return Unavailable("game_state_query_unlock_state_unavailable");
        }
        var value = default(JsonElement);
        Require(envelope.TryGetProperty("adapter", out var adapter) &&
                adapter.ValueKind == JsonValueKind.String &&
                adapter.GetString() == ExpectedAdapter &&
                envelope.TryGetProperty("value", out value) &&
                value.ValueKind == JsonValueKind.Object,
            "Available unlock state lacks the exact bridge adapter or object value.");

        var currentPlayerId = RequiredString(value, "current_player_id");
        var hostPlayerId = RequiredString(value, "host_player_id");
        var playersValue = default(JsonElement);
        Require(value.TryGetProperty("target_player_resolution_status",
                    out var targetStatus) &&
                targetStatus.ValueKind == JsonValueKind.String &&
                targetStatus.GetString() == "source_context_required" &&
                value.TryGetProperty("players", out playersValue) &&
                playersValue.ValueKind == JsonValueKind.Array &&
                playersValue.GetArrayLength() > 0,
            "Unlock state player metadata is incomplete.");

        var players = playersValue.EnumerateArray()
            .Select(ReadPlayer)
            .ToArray();
        Require(players.Select(player => player.PlayerId)
                    .Distinct(StringComparer.Ordinal).Count() == players.Length,
            "Unlock state contains duplicate player IDs.");
        Require(playersValue.EnumerateArray().Count(row =>
                    RequiredBoolean(row, "is_current")) == 1 &&
                playersValue.EnumerateArray().Count(row =>
                    RequiredBoolean(row, "is_host")) == 1 &&
                playersValue.EnumerateArray().Single(row =>
                    RequiredBoolean(row, "is_current"))
                    .GetProperty("player_id").GetString() == currentPlayerId &&
                playersValue.EnumerateArray().Single(row =>
                    RequiredBoolean(row, "is_host"))
                    .GetProperty("player_id").GetString() == hostPlayerId,
            "Unlock state Current/Host identity flags disagree with IDs.");

        var bridgeFixed = default(JsonElement);
        Require(value.TryGetProperty("island_north_bridge_state_available",
                    out var bridgeAvailable) &&
                bridgeAvailable.ValueKind is JsonValueKind.True or
                    JsonValueKind.False &&
                value.TryGetProperty("island_north_bridge_fixed",
                    out bridgeFixed),
            "Unlock state island bridge metadata is incomplete.");
        bool? bridge = null;
        if (bridgeAvailable.GetBoolean())
        {
            Require(bridgeFixed.ValueKind is JsonValueKind.True or
                    JsonValueKind.False,
                "Available island bridge state is not boolean.");
            bridge = bridgeFixed.GetBoolean();
        }
        else
        {
            Require(bridgeFixed.ValueKind == JsonValueKind.Null,
                "Unavailable island bridge state must be null.");
        }

        return new UnlockSnapshotState(
            true,
            string.Empty,
            currentPlayerId,
            hostPlayerId,
            players,
            bridge);
    }

    private static UnlockPlayerState ReadPlayer(JsonElement row)
    {
        Require(row.ValueKind == JsonValueKind.Object,
            "Unlock state player row is not an object.");
        var playerId = RequiredString(row, "player_id");
        _ = RequiredBoolean(row, "is_current");
        _ = RequiredBoolean(row, "is_host");
        return new UnlockPlayerState(
            playerId,
            RequiredStringSet(row, "mail_received"),
            RequiredStringSet(row, "mail_for_tomorrow"),
            RequiredStringSet(row, "mailbox"),
            RequiredStats(row),
            RequiredStringSet(row, "active_special_order_ids"),
            RequiredStringSet(row, "active_special_order_rules"));
    }

    private static Dictionary<string, long> RequiredStats(JsonElement row)
    {
        Require(row.TryGetProperty("stats", out var value) &&
                value.ValueKind == JsonValueKind.Object,
            "Unlock state player stats are missing.");
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            Require(property.Name.Length > 0 &&
                    property.Value.TryGetInt64(out var count) && count >= 0 &&
                    result.TryAdd(property.Name, count),
                "Unlock state player stats contain an invalid row.");
        }
        return result;
    }

    private static HashSet<string> RequiredStringSet(
        JsonElement row,
        string propertyName)
    {
        Require(row.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.Array,
            "Unlock state player " + propertyName + " is missing.");
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            Require(item.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(item.GetString()) &&
                    result.Add(item.GetString()!),
                "Unlock state player " + propertyName +
                " contains an invalid or duplicate value.");
        }
        return result;
    }

    private static string RequiredString(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()),
            "Unlock state " + name + " is missing.");
        return property.GetString()!;
    }

    private static bool RequiredBoolean(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var property) &&
                property.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Unlock state " + name + " is missing.");
        return property.GetBoolean();
    }

    private static UnlockSnapshotState Unavailable(string reason) => new(
        false,
        reason,
        string.Empty,
        string.Empty,
        Array.Empty<UnlockPlayerState>(),
        null);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
