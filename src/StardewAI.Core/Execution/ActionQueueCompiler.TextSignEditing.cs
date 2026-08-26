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
    private const string TextSignRuntimeType = "StardewValley.Object";
    private const string TextSignNativeContract =
        "GameLocation.checkAction->Object.CheckForActionOnTextSign->TitleTextInputMenu(textLimit=60,minLength=0,paste=false)->NamingMenu.textBoxEnter(FilterDirtyWords)->signText=text.Trim()->TokenParser.ParseText+FilterDirtyWords->showNextIndex=IsNullOrEmpty(SignText)";

    private static CompiledActionStep[] CompileEditTextSignStep(SmallModelAction action)
    {
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(location))
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "edit_text_sign",
                location + "(" + x.Value + "," + y.Value + ")",
                "current_location.objects[" + x.Value + "," + y.Value + "].sign_state.sign_text=native_menu_receipt" +
                    ";show_next_index=string.IsNullOrEmpty(SignText)",
                45)
        };
    }

    private static string[] ValidateEditTextSignPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.edit_text_sign")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var location = ReadParameter(action, "target_location");
        var requestedText = ReadParameter(action, "requested_sign_text");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            string.IsNullOrWhiteSpace(location) || requestedText is null ||
            !TryBoolParameter(action, "expected_show_next_index_before", out var expectedShowNextIndexBefore) ||
            !TryBoolParameter(action, "replaces_existing_text", out var replacesExisting) ||
            !TryBoolParameter(action, "allow_replace_existing_text", out var allowReplacement))
        {
            return new[] { "edit_text_sign_typed_projection_required" };
        }

        if (!string.Equals(ReadParameter(action, "target_runtime_type"), TextSignRuntimeType, StringComparison.Ordinal))
        {
            reasons.Add("edit_text_sign_exact_base_object_required");
        }
        if (requestedText.Length > 60 || requestedText.Any(character => character == '"' || char.IsControl(character)))
        {
            reasons.Add("edit_text_sign_native_keyboard_input_invalid");
        }
        if (replacesExisting != allowReplacement)
        {
            reasons.Add(replacesExisting
                ? "edit_text_sign_replacement_not_authorized"
                : "edit_text_sign_unexpected_replacement_authorization");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "text_edit_reason")))
        {
            reasons.Add("edit_text_sign_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), TextSignNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("edit_text_sign_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("edit_text_sign_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("edit_text_sign_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("edit_text_sign_adjacent_stand_geometry_invalid");
        }

        var target = FindSignDisplayTarget(snapshot, targetX.Value, targetY.Value);
        if (!target.HasValue ||
            !string.Equals(ReadString(target.Value, "type"), TextSignRuntimeType, StringComparison.Ordinal) ||
            !target.Value.TryGetProperty("sign_state", out var signState) || signState.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(signState, "placement_kind"), "text_sign", StringComparison.Ordinal) ||
            !string.Equals(ReadString(signState, "status"), "available", StringComparison.Ordinal) ||
            !signState.TryGetProperty("text_editing", out var editing) || editing.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(editing, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("edit_text_sign_target_not_ready_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (!string.Equals(ReadString(editing, "target_location"), location, StringComparison.OrdinalIgnoreCase) ||
            ReadInt(editing, "target_tile_x") != targetX.Value || ReadInt(editing, "target_tile_y") != targetY.Value ||
            !string.Equals(ReadString(editing, "target_runtime_type"), TextSignRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "target_qualified_item_id"), ReadParameter(action, "target_qualified_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "target_state_sha256"), ReadParameter(action, "target_state_sha256"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "target_projection_fingerprint"), ReadParameter(action, "target_projection_fingerprint"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "native_contract"), TextSignNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("edit_text_sign_target_projection_drifted");
        }
        if (!string.Equals(ReadString(editing, "raw_sign_text_before"), ReadParameter(action, "raw_sign_text_before"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "display_sign_text_before"), ReadParameter(action, "display_sign_text_before"), StringComparison.Ordinal) ||
            ReadBool(editing, "show_next_index_before") != expectedShowNextIndexBefore ||
            ReadBool(editing, "replaces_existing_text") != replacesExisting)
        {
            reasons.Add("edit_text_sign_previous_text_drifted");
        }
        if (ReadInt(editing, "text_limit_utf16_code_units") != 60 ||
            ReadInt(editing, "minimum_length", -1) != 0 || ReadBool(editing, "paste_button_visible", true) ||
            !string.Equals(ReadString(editing, "input_filter"), "Utility.FilterDirtyWords", StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "display_pipeline"), "TokenParser.ParseText_then_Utility.FilterDirtyWords", StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "trim_pipeline"), "System.String.Trim", StringComparison.Ordinal) ||
            !string.Equals(ReadString(editing, "show_next_index_rule"), "string.IsNullOrEmpty(SignText)", StringComparison.Ordinal))
        {
            reasons.Add("edit_text_sign_native_menu_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

}
