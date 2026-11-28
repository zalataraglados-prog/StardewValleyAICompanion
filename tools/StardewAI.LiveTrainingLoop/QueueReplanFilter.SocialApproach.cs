using System.Globalization;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
    public static bool NeedsMovedNpcApproachRebind(
        JsonObject move, JsonObject? next, JsonObject freshSnapshot)
    {
        var selectedIndex = ReadAcceptedCandidateIndex(move);
        var candidateId = EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(move);
        if (ReadString(move, "option_id") != "executor.move_to_tile" ||
            ReadString(next, "option_id") != "executor.social_interact" ||
            selectedIndex < 0 || selectedIndex != ReadAcceptedCandidateIndex(next) ||
            string.IsNullOrWhiteSpace(candidateId) ||
            candidateId != EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(next) ||
            ReadParameter(next, "social_action_kind") != "talk" ||
            !int.TryParse(ReadParameter(next, "npc_tile_x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(ReadParameter(next, "npc_tile_y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            return false;

        var state = freshSnapshot["state"] as JsonObject;
        var location = ReadSnapshotField(state?["player"] as JsonObject, "location_id");
        var npcName = ReadParameter(next, "npc_name");
        if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(location) ||
            location != ReadParameter(next, "target_location") ||
            state?["npcs"]?["social_interaction"]?["value"] is not JsonArray rows)
            return false;

        var matches = rows.OfType<JsonObject>().Where(row =>
            ReadString(row, "name") == npcName && ReadString(row, "location_id") == location &&
            row["current_instance_loaded"] is JsonValue loaded && loaded.TryGetValue<bool>(out var isLoaded) && isLoaded)
            .Take(2).ToArray();
        if (matches is not [var npc] || npc["tile_x"] is not JsonValue freshX || npc["tile_y"] is not JsonValue freshY ||
            !freshX.TryGetValue<int>(out var xNow) || !freshY.TryGetValue<int>(out var yNow))
            return false;

        return x != xNow || y != yNow;
    }
}
