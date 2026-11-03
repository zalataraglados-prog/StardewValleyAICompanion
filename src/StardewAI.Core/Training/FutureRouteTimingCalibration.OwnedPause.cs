using System;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed partial class FutureRouteTimingCalibrationLoader
    {
        private static bool HasExecutorOwnedIdlePauseRecovery(JsonElement movement)
        {
            if (!movement.TryGetProperty("executor_owned_idle_pause", out var pause) ||
                pause.ValueKind != JsonValueKind.Object ||
                ReadString(pause, "schema_version") != "executor_owned_idle_pause.v1" ||
                ReadString(pause, "owner_mod_id") != "StardewAI.RuntimeTestHarness" ||
                ReadString(pause, "release_policy") != "release_before_pending_execution_update" ||
                ReadBool(pause, "applied") != true || ReadBool(movement, "game_paused") != true ||
                ReadBool(movement, "player_can_move_now") != true ||
                ReadBool(movement, "player_using_tool_now") != false ||
                ReadInt(movement, "player_freeze_pause") != 0)
                return false;
            foreach (var field in new[] { "dialogue_up", "global_fade", "freeze_controls", "is_warping",
                         "current_event_active", "event_up", "immobilizing_buff_19_active" })
                if (ReadBool(movement, field) != false) return false;
            foreach (var field in new[] { "player_id", "game_unique_id" })
            {
                var current = ReadString(movement, field);
                if (string.IsNullOrWhiteSpace(current) ||
                    !string.Equals(current, ReadString(pause, field), StringComparison.Ordinal))
                    return false;
            }
            return pause.TryGetProperty("pause_epoch", out var epoch) &&
                epoch.ValueKind == JsonValueKind.Number && epoch.TryGetInt64(out var value) && value > 0;
        }
    }
}
