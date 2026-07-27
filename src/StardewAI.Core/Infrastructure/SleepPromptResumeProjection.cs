using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal static class SleepPromptResumeProjection
{
    public const string ResumeMode = "existing_exact_prompt";

    public static bool IsAvailable(SnapshotEnvelope snapshot)
    {
        return BlockReasons(snapshot).Length == 0;
    }

    public static string[] BlockReasons(SnapshotEnvelope snapshot)
    {
        var reasons = new List<string>();
        var activeMenu = ReadStateFieldValue(
            snapshot,
            "menus",
            "active_menu");
        if (!activeMenu.HasValue ||
            activeMenu.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("sleep_resume_active_menu_state_missing");
        }
        else
        {
            if (ReadBool(activeMenu.Value, "is_open") != true)
            {
                reasons.Add("sleep_resume_active_menu_not_open");
            }
            if (!string.Equals(
                    ReadString(activeMenu.Value, "type"),
                    "DialogueBox",
                    StringComparison.Ordinal))
            {
                reasons.Add("sleep_resume_active_menu_not_dialogue_box");
            }
            if (!string.Equals(
                    ReadString(activeMenu.Value, "last_question_key"),
                    "Sleep",
                    StringComparison.Ordinal))
            {
                reasons.Add("sleep_resume_question_key_not_sleep");
            }
            if (ReadBool(activeMenu.Value, "is_sleep_prompt") != true)
            {
                reasons.Add("sleep_resume_active_menu_not_sleep_prompt");
            }
            if (ReadBool(activeMenu.Value, "event_up") == true)
            {
                reasons.Add("sleep_resume_event_active");
            }
        }

        var prompt = ReadStateFieldValue(
            snapshot,
            "menus",
            "sleep_prompt_context");
        if (!prompt.HasValue ||
            prompt.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("sleep_resume_prompt_context_missing");
        }
        else
        {
            if (ReadBool(prompt.Value, "prompt_open") != true)
            {
                reasons.Add("sleep_resume_prompt_not_open");
            }
            if (!string.Equals(
                    ReadString(prompt.Value, "active_menu_type"),
                    "DialogueBox",
                    StringComparison.Ordinal))
            {
                reasons.Add("sleep_resume_prompt_type_not_dialogue_box");
            }
            if (!string.Equals(
                    ReadString(prompt.Value, "last_question_key"),
                    "Sleep",
                    StringComparison.Ordinal))
            {
                reasons.Add("sleep_resume_prompt_question_key_not_sleep");
            }
            if (!string.Equals(
                    ReadString(prompt.Value, "confirm_action_key"),
                    "Sleep_Yes",
                    StringComparison.Ordinal))
            {
                reasons.Add("sleep_resume_confirm_action_not_verified");
            }
            if (ReadBool(prompt.Value, "can_confirm_sleep") != true ||
                ReadBool(
                    prompt.Value,
                    "confirm_executor_enabled") != true)
            {
                reasons.Add("sleep_resume_executor_not_enabled");
            }
        }

        var home = ReadStateFieldValue(
            snapshot,
            "current_location",
            "home_context");
        if (!home.HasValue ||
            home.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("sleep_resume_home_context_missing");
        }
        else
        {
            var homeLocation = ReadString(
                home.Value,
                "home_location_id");
            var playerLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            if (ReadBool(home.Value, "home_available") != true ||
                ReadBool(
                    home.Value,
                    "current_location_is_home") != true ||
                string.IsNullOrWhiteSpace(homeLocation) ||
                !string.Equals(
                    playerLocation,
                    homeLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("sleep_resume_player_not_at_home");
            }
            if (ReadBool(home.Value, "bed_tile_has_bed") != true ||
                ReadBool(home.Value, "sleep_executor_enabled") != true)
            {
                reasons.Add("sleep_resume_bed_not_verified");
            }

            var playerX = ReadStateFieldIntOptional(
                snapshot,
                "player",
                "tile_x");
            var playerY = ReadStateFieldIntOptional(
                snapshot,
                "player",
                "tile_y");
            if (!playerX.HasValue || !playerY.HasValue)
            {
                reasons.Add("sleep_resume_player_tile_missing");
            }
            else
            {
                var bedX = ReadInt(home.Value, "bed_tile_x");
                var bedY = ReadInt(home.Value, "bed_tile_y");
                var distance = Math.Abs(playerX.Value - bedX) +
                    Math.Abs(playerY.Value - bedY);
                if (distance > 1)
                {
                    reasons.Add(
                        "sleep_resume_player_not_at_or_adjacent_to_bed");
                }
            }
        }

        return reasons
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
