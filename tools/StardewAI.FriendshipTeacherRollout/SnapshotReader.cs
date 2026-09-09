using System.Text.Json;

namespace StardewAI.FriendshipTeacherRollout;

public static class SnapshotReader
{
    public static JsonElement FieldValue(
        JsonElement snapshot,
        string domain,
        string field)
    {
        if (snapshot.ValueKind != JsonValueKind.Object ||
            !snapshot.TryGetProperty("state", out var state) ||
            !state.TryGetProperty(domain, out var domainNode) ||
            !domainNode.TryGetProperty(field, out var fieldNode) ||
            !fieldNode.TryGetProperty("value", out var value) ||
            !fieldNode.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived"))
        {
            throw new InvalidDataException(
                "Required transparent field is unavailable: " +
                domain + "." + field + ".");
        }
        return value;
    }

    public static int IntField(
        JsonElement snapshot,
        string domain,
        string field)
    {
        var value = FieldValue(snapshot, domain, field);
        return value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                "Required transparent field is not an integer: " +
                domain + "." + field + ".");
    }

    public static bool ActiveMenuOpen(JsonElement snapshot)
    {
        var menu = FieldValue(snapshot, "menus", "active_menu");
        return menu.ValueKind == JsonValueKind.Object &&
               menu.TryGetProperty("is_open", out var open) &&
               open.ValueKind == JsonValueKind.True;
    }

    public static JsonElement StoryEvent(JsonElement snapshot) =>
        FieldValue(snapshot, "player", "story_event");

    public static bool ActiveStoryEvent(JsonElement snapshot)
    {
        var storyEvent = StoryEvent(snapshot);
        return storyEvent.ValueKind == JsonValueKind.Object &&
               storyEvent.TryGetProperty("active", out var active) &&
               active.ValueKind == JsonValueKind.True &&
               storyEvent.TryGetProperty("event_up", out var eventUp) &&
               eventUp.ValueKind == JsonValueKind.True;
    }

    public static string StoryEventString(
        JsonElement snapshot,
        string property)
    {
        var storyEvent = StoryEvent(snapshot);
        return storyEvent.ValueKind == JsonValueKind.Object &&
               storyEvent.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new InvalidDataException(
                "Required story-event field is unavailable: " + property + ".");
    }

    public static int StoryEventInt(
        JsonElement snapshot,
        string property)
    {
        var storyEvent = StoryEvent(snapshot);
        return storyEvent.ValueKind == JsonValueKind.Object &&
               storyEvent.TryGetProperty(property, out var value) &&
               value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                "Required story-event integer is unavailable: " + property + ".");
    }

    public static int QualifyingCount(JsonElement snapshot)
    {
        var progress = FieldValue(
            snapshot,
            "npcs",
            "grandpa_friendship_progress");
        return progress.TryGetProperty("qualifying_count", out var count) &&
               count.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                "Grandpa friendship qualifying_count is unavailable.");
    }

    public static int FriendshipPointSum(JsonElement snapshot)
    {
        var progress = FieldValue(
            snapshot,
            "npcs",
            "grandpa_friendship_progress");
        if (!progress.TryGetProperty(
                "eligible_villager_rows",
                out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Grandpa friendship population rows are unavailable.");
        }
        return rows.EnumerateArray().Sum(row =>
            row.TryGetProperty("friendship_points", out var points) &&
            points.ValueKind == JsonValueKind.Number &&
            points.TryGetInt32(out var value)
                ? value
                : 0);
    }

    public static string SelectedNpcName(
        StardewAI.Core.Training.CurrentSocialDayTeacherLabel label) =>
        label.SelectedCandidate?.Parameters
            .FirstOrDefault(parameter => parameter.Name == "npc_name")
            ?.Value ?? string.Empty;
}
