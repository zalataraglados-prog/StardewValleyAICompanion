using System;
using System.Text.Json;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal static class StoryEventQuestionKey
{
    internal static bool TryRead(JsonElement projection, out string key)
    {
        key = string.Empty;
        if (!projection.TryGetProperty("dialogue_question_key", out var field) ||
            field.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        key = field.GetString()!;
        // A native Speak script can own a question without setting the map's lastQuestionKey.
        var command = ReadString(projection, "current_command_raw");
        return !string.IsNullOrWhiteSpace(key) ||
            key.Length == 0 && ReadBool(projection, "dialogue_is_question") == true &&
            ReadString(projection, "current_command_resolved_name") == "Speak" &&
            (command.Contains("$q ", StringComparison.Ordinal) || command.Contains("$y ", StringComparison.Ordinal));
    }
}
