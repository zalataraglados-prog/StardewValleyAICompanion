using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string TentSleepRuntimeType = "StardewValley.TerrainFeatures.Tent";
    private const string TentSleepNativeContract =
        "GameLocation.checkAction->Tent.performUseAction->SleepTent_Yes->startSleep->CanWakeUpHere(sleptInTemporaryBed)->Tent.dayUpdate/tickUpdate";

    private static CompiledActionStep[] CompileSleepInTentSteps(SmallModelAction action)
    {
        var location = ReadParameter(action, "target_location");
        var anchorX = ReadIntParameter(action, "target_tile_x");
        var anchorY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        if (string.IsNullOrWhiteSpace(location) || !anchorX.HasValue || !anchorY.HasValue ||
            !standX.HasValue || !standY.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "sleep_in_tent",
                location + ":tent(" + anchorX.Value + "," + anchorY.Value + "):stand(" + standX.Value + "," + standY.Value + ")",
                "time.total_days=increases_by_1;player.location_id=" + location + ";current_location.large_terrain_features[" +
                    anchorX.Value + "," + anchorY.Value + "]=destroyed;player.temporary_sleep.slept_in_temporary_bed=false",
                240)
        };
    }

    private static string[] ValidateSleepInTentPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "recovery.sleep_in_tent")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        if (!string.Equals(ReadParameter(action, "compiler_context.is_terminal_step"), "true", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("tent_sleep_action_must_be_terminal");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("tent_sleep_menu_must_be_clear");
        }

        var location = ReadParameter(action, "target_location");
        var anchorX = ReadIntParameter(action, "target_tile_x");
        var anchorY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var direction = ReadIntParameter(action, "direction");
        var health = ReadIntParameter(action, "tent_health_before");
        if (string.IsNullOrWhiteSpace(location) || !anchorX.HasValue || !anchorY.HasValue ||
            !standX.HasValue || !standY.HasValue || !direction.HasValue || !health.HasValue)
        {
            reasons.Add("tent_sleep_typed_target_fields_required");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("tent_sleep_requires_loaded_target_location");
        }
        if (standX.Value != anchorX.Value || standY.Value != anchorY.Value + 1 || direction.Value != 0)
        {
            reasons.Add("tent_sleep_canonical_grab_geometry_required");
        }
        if (!string.Equals(ReadParameter(action, "target_runtime_type"), TentSleepRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_contract"), TentSleepNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_question_key"), "SleepTent", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_confirm_action_key"), "SleepTent_Yes", StringComparison.Ordinal))
        {
            reasons.Add("tent_sleep_native_contract_mismatch");
        }

        var row = FindExactTentRow(snapshot, anchorX.Value, anchorY.Value);
        if (!row.HasValue)
        {
            reasons.Add("tent_sleep_exact_tent_not_found");
        }
        else
        {
            var value = row.Value;
            if (!ReadBool(value, "is_tent") ||
                !string.Equals(ReadString(value, "runtime_type"), TentSleepRuntimeType, StringComparison.Ordinal) ||
                ReadInt(value, "health") != health.Value || health.Value <= 0 ||
                !ReadBool(value, "passable_for_player") || ReadBool(value, "passable_without_character"))
            {
                reasons.Add("tent_sleep_identity_or_health_drifted");
            }
            if (!string.Equals(ReadString(value, "sleep_location_id"), location, StringComparison.Ordinal) ||
                ReadInt(value, "sleep_interaction_tile_x") != anchorX.Value ||
                ReadInt(value, "sleep_interaction_tile_y") != anchorY.Value ||
                ReadInt(value, "canonical_sleep_stand_tile_x") != standX.Value ||
                ReadInt(value, "canonical_sleep_stand_tile_y") != standY.Value ||
                ReadInt(value, "canonical_sleep_facing_direction", -1) != direction.Value)
            {
                reasons.Add("tent_sleep_projection_geometry_drifted");
            }
            if (ReadBool(value, "game_new_day") || !ReadBool(value, "time_should_pass") ||
                !ReadBool(value, "player_has_moved") || ReadBool(value, "player_passed_out") ||
                ReadBool(value, "slept_in_temporary_bed"))
            {
                reasons.Add("tent_sleep_native_prompt_gate_closed");
            }
        }
        if (!SleepStandTileReachable(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("tent_sleep_stand_tile_unreachable");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? FindExactTentRow(SnapshotEnvelope snapshot, int anchorX, int anchorY)
    {
        var field = ReadStateFieldValue(snapshot, "current_location", "large_terrain_features");
        if (!field.HasValue || field.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in field.Value.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadBool(row, "is_tent") &&
                ReadInt(row, "tile_x") == anchorX && ReadInt(row, "tile_y") == anchorY)
            {
                return row;
            }
        }
        return null;
    }
}
