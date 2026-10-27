using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class CropTeacherControlInterruptClassifier
{
    public static bool HasSafeSpeakerlessDialogue(JsonObject snapshot)
    {
        var activeMenu = snapshot["state"]?["menus"]?["active_menu"]
            as JsonObject;
        if (!string.Equals(
                ReadString(activeMenu, "status"),
                "available",
                StringComparison.Ordinal))
        {
            return false;
        }

        var value = activeMenu?["value"] as JsonObject;
        return ReadBool(value, "is_open") == true &&
            string.Equals(
                ReadString(value, "type"),
                "DialogueBox",
                StringComparison.Ordinal) &&
            ReadBool(value, "is_sleep_prompt") == false &&
            ReadBool(value, "event_up") == false &&
            ReadBool(value, "dialogue_is_question") == false &&
            ReadInt(value, "dialogue_response_count") == 0 &&
            ReadBool(value, "dialogue_transitioning").HasValue &&
            ReadBool(value, "dialogue_character_present") == false &&
            HasProperty(value, "dialogue_speaker_name") &&
            string.IsNullOrWhiteSpace(
                ReadNullableString(value, "dialogue_speaker_name")) &&
            !IsTerminalSleepQuestionKey(
                ReadNullableString(value, "last_question_key"));
    }

    private static bool HasProperty(JsonObject? value, string name) =>
        value is not null && value.TryGetPropertyValue(name, out _);

    private static bool IsTerminalSleepQuestionKey(string? value) =>
        string.Equals(value, "Sleep", StringComparison.Ordinal) ||
        string.Equals(value, "SleepTent", StringComparison.Ordinal);

    private static string ReadString(JsonObject? value, string name) =>
        ReadNullableString(value, name) ?? string.Empty;

    private static string? ReadNullableString(
        JsonObject? value,
        string name)
    {
        if (value is null ||
            !value.TryGetPropertyValue(name, out var node) ||
            node is null)
        {
            return null;
        }
        return node is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var result)
            ? result
            : null;
    }

    private static bool? ReadBool(JsonObject? value, string name)
    {
        if (value is null ||
            !value.TryGetPropertyValue(name, out var node) ||
            node is not JsonValue jsonValue)
        {
            return null;
        }
        return jsonValue.TryGetValue<bool>(out var result)
            ? result
            : null;
    }

    private static int? ReadInt(JsonObject? value, string name)
    {
        if (value is null ||
            !value.TryGetPropertyValue(name, out var node) ||
            node is not JsonValue jsonValue)
        {
            return null;
        }
        return jsonValue.TryGetValue<int>(out var result)
            ? result
            : null;
    }
}
