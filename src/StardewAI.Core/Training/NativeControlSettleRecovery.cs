using System;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public static class NativeControlSettleRecovery
    {
        public const string CandidateId =
            "recovery:wait_for_native_control_settle";
        public const string CandidateKind =
            "recovery_native_control_settle";
        public const int WaitTicks = 30;

        private const string MovementProjection =
            "exact_current_player_native_cardinal_movement_context";
        private const string MovementScope =
            "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay";

        public static EventCandidate? TryCreate(SnapshotEnvelope snapshot)
        {
            var movement = ReadStateFieldValue(
                snapshot,
                "player",
                "movement_timing_context");
            var activeMenu = ReadStateFieldValue(
                snapshot,
                "menus",
                "active_menu");
            if (!movement.HasValue ||
                movement.Value.ValueKind != JsonValueKind.Object ||
                !activeMenu.HasValue ||
                activeMenu.Value.ValueKind != JsonValueKind.Object ||
                !ReadableStatus(ReadStateFieldStatus(
                    snapshot,
                    "player",
                    "movement_timing_context")) ||
                !ReadableStatus(ReadStateFieldStatus(
                    snapshot,
                    "menus",
                    "active_menu")))
            {
                return null;
            }

            var context = movement.Value;
            if (!HasString(context, "projection_status", MovementProjection) ||
                !HasString(context, "scope", MovementScope) ||
                !HasBool(context, "runtime_calibration_compatible", true) ||
                !HasBool(context, "route_timing_ready_now", false) ||
                !HasBool(context, "player_using_tool_now", false) ||
                !HasBool(context, "immobilizing_buff_19_active", false) ||
                !HasBool(context, "current_event_active", false) ||
                !HasBool(context, "event_up", false) ||
                !HasBool(context, "game_paused", false) ||
                !HasBool(context, "freeze_controls", false) ||
                !HasBool(context, "is_warping", false) ||
                !HasBool(activeMenu.Value, "is_open", false))
            {
                return null;
            }

            var dialogueClear = HasBool(context, "dialogue_up", false) &&
                HasIntAtMost(context, "player_freeze_pause", 0);
            var postSleepControlGap = dialogueClear &&
                HasBool(context, "native_control_gate_open", true) &&
                HasBool(context, "player_can_move_now", false) &&
                HasBool(context, "global_fade", false);
            var dayStartFadeControlGap = dialogueClear &&
                HasBool(context, "native_control_gate_open", false) &&
                HasBool(context, "player_can_move_now", true) &&
                HasBool(context, "global_fade", true);
            // The v93 day-start snapshot can retain dialogueUp with a native
            // freeze timer even though CanMove has already been restored. This
            // authorizes observation only, never dialogue acknowledgement or
            // movement: the existing wait requires a fresh snapshot/replan.
            var postSleepFreezeControlGap =
                HasBool(context, "dialogue_up", true) &&
                HasBool(context, "native_control_gate_open", false) &&
                HasBool(context, "player_can_move_now", true) &&
                HasBool(context, "global_fade", false) &&
                context.TryGetProperty("player_freeze_pause", out var freezePause) &&
                freezePause.ValueKind == JsonValueKind.Number &&
                freezePause.TryGetInt32(out var pause) && pause > 0 &&
                ReadableStatus(ReadStateFieldStatus(snapshot, "time", "time")) &&
                ReadStateFieldIntOptional(snapshot, "time", "time") == 600 &&
                ReadableStatus(ReadStateFieldStatus(snapshot, "player", "location_id")) &&
                ReadStateFieldString(snapshot, "player", "location_id") == "FarmHouse" &&
                HasString(activeMenu.Value, "type", "none") &&
                HasString(activeMenu.Value, "last_question_key", "Sleep");
            if (!postSleepControlGap && !dayStartFadeControlGap && !postSleepFreezeControlGap)
                return null;

            return new EventCandidate
            {
                CandidateId = CandidateId,
                Kind = CandidateKind,
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                LocationId = ReadStateFieldString(
                    snapshot,
                    "player",
                    "location_id"),
                ExpectedEffect =
                    "executor.wait_ticks=30;native_control_settle_rechecked;fresh_snapshot_replan_required=true",
                EstimatedTicks = WaitTicks,
                EnergyCost = 0,
                Parameters = new[]
                {
                    Parameter("execution_option_id", "executor.wait_ticks"),
                    Parameter("wait_ticks", WaitTicks.ToString())
                }
            };
        }

        private static bool HasBool(
            JsonElement source,
            string property,
            bool expected) =>
            source.TryGetProperty(property, out var value) &&
            value.ValueKind == (expected
                ? JsonValueKind.True
                : JsonValueKind.False);

        private static bool HasString(
            JsonElement source,
            string property,
            string expected) =>
            source.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            string.Equals(
                value.GetString(),
                expected,
                StringComparison.Ordinal);

        private static bool HasIntAtMost(
            JsonElement source,
            string property,
            int maximum) =>
            source.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var parsed) &&
            parsed <= maximum;
    }
}
