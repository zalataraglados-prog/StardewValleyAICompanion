using System.IO;
using System.Text.Json;

namespace StardewAI.Core.Training;

public static class NativeObjectiveContinuationJournal
{
    // The executor clears the active cursor after completion; this is a view of
    // the recorded journal, not proof that the selected objective was achieved.
    public static JsonElement ReadRecordedObjective(JsonElement receipt)
    {
        if (receipt.TryGetProperty("objective_continuation", out var active) && active.ValueKind == JsonValueKind.Object)
            return active.Clone();
        if (receipt.TryGetProperty("selected_candidate_completed", out var selected) && selected.ValueKind == JsonValueKind.True &&
            receipt.TryGetProperty("objective_continuation_completed", out var complete) && complete.ValueKind == JsonValueKind.True &&
            receipt.TryGetProperty("completed_objective_continuations", out var completed) && completed.ValueKind == JsonValueKind.Array &&
            completed.GetArrayLength() == 1 && completed[0].ValueKind == JsonValueKind.Object)
            return completed[0].Clone();
        throw new InvalidDataException("Native work lacks its recorded active or sole completed objective.");
    }
}
