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
    private const string HorseFluteNativeContract =
        "Object.performUseAction((O)911)->Utility.GetHorseWarpRestrictionsForFarmer(start+delayed)->FarmerTeam.requestHorseWarpEvent->OnRequestHorseWarp->Horse.mutex->Game1.warpCharacter";

    private static CompiledActionStep[] CompileUseHorseFluteStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(location))
            return Array.Empty<CompiledActionStep>();

        return new[]
        {
            Step(
                "use_horse_flute",
                location + ":slot" + slot.Value + ":(O)911",
                "inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                    ";result=" + ReadParameter(action, "expected_result") +
                    ";delay_ms=" + ReadParameter(action, "use_delay_ms") +
                    ";owned_horse_id=" + ReadParameter(action, "owned_horse_id"),
                120)
        };
    }

    private static string[] ValidateUseHorseFlutePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_horse_flute")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var stackBefore = ReadIntParameter(action, "inventory_stack_before");
        var stackAfter = ReadIntParameter(action, "inventory_stack_after");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !stackBefore.HasValue || stackBefore < 1 || !stackAfter.HasValue ||
            !string.Equals(ReadParameter(action, "item_id"), "911", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "qualified_item_id"), "(O)911", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(location))
        {
            return new[] { "use_horse_flute_typed_fields_required" };
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("use_horse_flute_menu_must_be_clear");
        if (!TargetLocationMatchesCurrent(action, snapshot))
            reasons.Add("use_horse_flute_requires_loaded_target_location");

        var context = ReadStateFieldValue(snapshot, "player", "horse_flute");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("use_horse_flute_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "horse_flute_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("use_horse_flute_projection_fingerprint_drifted");
        if (!string.Equals(ReadString(context.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_horse_flute_native_use_gate_blocked");
        if (ReadIntParameter(action, "horse_warp_restrictions") != 0 ||
            ReadInt(context.Value, "horse_warp_restrictions", -1) != 0 ||
            !string.Equals(ReadParameter(action, "horse_warp_restriction_names"), "none", StringComparison.Ordinal))
            reasons.Add("use_horse_flute_native_restrictions_drifted");

        JsonElement? inventoryRow = null;
        if (context.Value.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            inventoryRow = rows.EnumerateArray().FirstOrDefault(row =>
                ReadInt(row, "inventory_slot_index", -1) == slot.Value &&
                string.Equals(ReadString(row, "qualified_item_id"), "(O)911", StringComparison.Ordinal));
        }
        if (!inventoryRow.HasValue || inventoryRow.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(inventoryRow.Value, "item_id"), "911", StringComparison.Ordinal) ||
            !string.Equals(ReadString(inventoryRow.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            ReadBool(inventoryRow.Value, "temporarily_invisible") == true ||
            ReadInt(inventoryRow.Value, "stack_before", -1) != stackBefore ||
            ReadInt(inventoryRow.Value, "stack_after", -1) != stackAfter)
            reasons.Add("use_horse_flute_inventory_identity_drifted");

        JsonElement? horse = context.Value.TryGetProperty("owned_horse", out var horseValue) &&
            horseValue.ValueKind == JsonValueKind.Object ? horseValue : null;
        if (!horse.HasValue ||
            !string.Equals(ReadParameter(action, "owned_horse_id"), ReadString(horse.Value, "horse_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "owned_horse_location_id"), ReadString(horse.Value, "location_id"), StringComparison.Ordinal) ||
            ReadIntParameter(action, "owned_horse_tile_x") != ReadInt(horse.Value, "tile_x") ||
            ReadIntParameter(action, "owned_horse_tile_y") != ReadInt(horse.Value, "tile_y") ||
            ReadBoolParameter(action, "owned_horse_nearby") != ReadBool(horse.Value, "is_nearby"))
            reasons.Add("use_horse_flute_owned_horse_identity_drifted");

        var nearby = horse.HasValue && ReadBool(horse.Value, "is_nearby") == true;
        JsonElement? stableBinding = context.Value.TryGetProperty("team_event_stable_binding", out var stableValue) &&
            stableValue.ValueKind == JsonValueKind.Object ? stableValue : null;
        var ownedHorseId = horse.HasValue ? ReadString(horse.Value, "horse_id") : string.Empty;
        if (!stableBinding.HasValue ||
            !string.Equals(ReadParameter(action, "team_event_stable_horse_id"), ReadString(stableBinding.Value, "stable_horse_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "team_event_stable_location_id"), ReadString(stableBinding.Value, "stable_location_id"), StringComparison.Ordinal) ||
            ReadIntParameter(action, "team_event_stable_tile_x") != ReadInt(stableBinding.Value, "stable_tile_x") ||
            ReadIntParameter(action, "team_event_stable_tile_y") != ReadInt(stableBinding.Value, "stable_tile_y") ||
            ReadBoolParameter(action, "team_event_stable_matches_owned_horse") != ReadBool(stableBinding.Value, "matches_owned_horse") ||
            (!nearby && (ReadBool(stableBinding.Value, "matches_owned_horse") != true ||
                !string.Equals(ReadString(stableBinding.Value, "stable_horse_id"), ownedHorseId, StringComparison.Ordinal))))
            reasons.Add("use_horse_flute_team_event_stable_binding_drifted");
        var expectedResult = nearby ? "already_adjacent_no_warp" : "summon_after_1500ms";
        var expectedDelay = nearby ? 0 : 1500;
        var expectedDuck = nearby ? 0 : 2000;
        var expectedFacingDirection = ReadInt(context.Value, "facing_direction", -1);
        if (!string.Equals(ReadParameter(action, "expected_result"), expectedResult, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "expected_result"), expectedResult, StringComparison.Ordinal))
            reasons.Add("use_horse_flute_result_projection_drifted");
        if (ReadIntParameter(action, "use_delay_ms") != expectedDelay ||
            ReadInt(context.Value, "use_delay_ms", -1) != expectedDelay ||
            ReadIntParameter(action, "freeze_pause_ms") != expectedDelay ||
            ReadInt(context.Value, "freeze_pause_ms", -1) != expectedDelay ||
            ReadIntParameter(action, "music_duck_ms") != expectedDuck ||
            ReadInt(context.Value, "music_duck_ms", -1) != expectedDuck ||
            expectedFacingDirection is < 0 or > 3 ||
            ReadIntParameter(action, "facing_direction") != expectedFacingDirection ||
            (!nearby && expectedFacingDirection != 2))
            reasons.Add("use_horse_flute_native_timing_drifted");
        if (!string.Equals(ReadParameter(action, "native_contract"), HorseFluteNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "native_contract"), HorseFluteNativeContract, StringComparison.Ordinal))
            reasons.Add("use_horse_flute_native_contract_drifted");

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
