using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class FullShipmentRecoverySettlementBoundaryProjector
{
    private const string FarmEntryTutorialFlag = "button_tut_2";

    internal static bool MatchesFirstFarmEntryTutorial(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        if (before.GameVersion != "1.6.15" || after.GameVersion != before.GameVersion ||
            !TryStateValue(before, "player", "location_id", out var from) || from.ValueKind != JsonValueKind.String ||
            from.GetString() != "FarmHouse" ||
            !TryStateValue(after, "player", "location_id", out var to) || to.ValueKind != JsonValueKind.String ||
            to.GetString() != "Farm" ||
            !TryStateValue(before, "quests", "mail_received", out _) || !TryStateValue(after, "quests", "mail_received", out _) ||
            !TryStateValue(before, "world_progress", "game_state_query_unlock_state", out _) ||
            !TryStateValue(after, "world_progress", "game_state_query_unlock_state", out _) ||
            !TryProject(before, out var beforeBoundary) ||
            !TryProject(after, out var afterBoundary)) return false;

        var beforeRows = PersistentStateRows(before).ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        var afterRows = PersistentStateRows(after).ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        if (!beforeRows.TryGetValue("quests.mail_received", out var beforeMail) ||
            !afterRows.TryGetValue("quests.mail_received", out var afterMail) ||
            !beforeRows.TryGetValue("world_progress.game_state_query_unlock_state", out var beforeUnlock) ||
            !afterRows.TryGetValue("world_progress.game_state_query_unlock_state", out var afterUnlock)) return false;
        var normalizedMail = JsonNode.Parse(afterMail.GetRawText()) as JsonObject;
        var originalMail = JsonNode.Parse(beforeMail.GetRawText()) as JsonObject;
        var normalizedUnlock = JsonNode.Parse(afterUnlock.GetRawText()) as JsonObject;
        var originalUnlock = JsonNode.Parse(beforeUnlock.GetRawText()) as JsonObject;
        if (normalizedMail is null || originalMail is null || normalizedUnlock is null || originalUnlock is null ||
            !RemoveExactAddedTutorial(originalMail["value"], normalizedMail["value"]) ||
            !JsonNode.DeepEquals(originalMail, normalizedMail) ||
            !NormalizeCurrentPlayerTutorial(originalUnlock["value"], normalizedUnlock["value"], beforeBoundary!.PlayerId) ||
            !JsonNode.DeepEquals(originalUnlock, normalizedUnlock)) return false;

        // Normalize only the comparison view; original snapshots, hashes and mail remain untouched.
        afterRows["quests.mail_received"] = JsonSerializer.SerializeToElement(normalizedMail);
        afterRows["world_progress.game_state_query_unlock_state"] = JsonSerializer.SerializeToElement(normalizedUnlock);
        return beforeBoundary == afterBoundary! with { PersistentStateSha256 = HashCanonical(afterRows) };
    }

    private static bool NormalizeCurrentPlayerTutorial(JsonNode? before, JsonNode? after, string playerId)
    {
        if (before is not JsonObject original || after is not JsonObject normalized ||
            !StringEquals(original["current_player_id"], playerId) || !StringEquals(normalized["current_player_id"], playerId) ||
            original["players"] is not JsonArray beforePlayers || normalized["players"] is not JsonArray afterPlayers ||
            beforePlayers.Any(row => row is not JsonObject) || afterPlayers.Any(row => row is not JsonObject)) return false;
        var beforeCurrent = beforePlayers.OfType<JsonObject>().Where(row => StringEquals(row["player_id"], playerId)).ToArray();
        var afterCurrent = afterPlayers.OfType<JsonObject>().Where(row => StringEquals(row["player_id"], playerId)).ToArray();
        return beforeCurrent.Length == 1 && afterCurrent.Length == 1 &&
            IsCurrentTutorialPlayer(beforeCurrent[0]) && IsCurrentTutorialPlayer(afterCurrent[0]) &&
            RemoveExactAddedTutorial(beforeCurrent[0]["mail_received"], afterCurrent[0]["mail_received"]);
    }

    private static bool RemoveExactAddedTutorial(JsonNode? before, JsonNode? after)
    {
        if (before is not JsonArray original || after is not JsonArray normalized || normalized.Count != original.Count + 1)
            return false;
        var beforeValues = TutorialMailValues(original);
        var afterValues = TutorialMailValues(normalized);
        if (beforeValues is null || afterValues is null || beforeValues.Contains(FarmEntryTutorialFlag, StringComparer.Ordinal) ||
            afterValues.Count(value => value == FarmEntryTutorialFlag) != 1) return false;
        normalized.RemoveAt(Array.IndexOf(afterValues, FarmEntryTutorialFlag));
        return JsonNode.DeepEquals(original, normalized);
    }

    private static string[]? TutorialMailValues(JsonArray values)
    {
        var result = new List<string>();
        foreach (var node in values)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)) return null;
            result.Add(text);
        }
        return result.Distinct(StringComparer.Ordinal).Count() == result.Count ? result.ToArray() : null;
    }

    private static bool StringEquals(JsonNode? node, string expected) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text == expected;

    private static bool IsCurrentTutorialPlayer(JsonObject player) =>
        player["is_current"] is JsonValue value && value.TryGetValue<bool>(out var current) && current;
}
