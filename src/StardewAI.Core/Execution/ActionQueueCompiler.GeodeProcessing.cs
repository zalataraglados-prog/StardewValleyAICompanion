using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string GeodeCompilerNativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Process)->GeodeMenu_inventory_click->GeodeMenu_geodeSpot_click->2700ms_native_animation->inventory_receipt";

    private static readonly string[] GeodeBoundParameterNames =
    {
        "geode_slot_index", "geode_input_quality", "geode_stack_before", "geode_free_slots_before", "geode_money_before", "geode_price_gold",
        "geodes_cracked_before", "mystery_boxes_opened_before", "golden_coconut_cracked_before", "geode_prediction_kind",
        "golden_walnuts_before", "golden_walnuts_found_before", "geode_archaeology_found_count",
        "geode_save_id_half", "geode_player_id_half", "geode_season", "geode_deepest_mine_level", "geode_skill_1_level",
        "geode_farming_mastery_unlocked", "geode_qi_beans_rule_active", "geode_got_mystery_book_mail_before", "geode_artifact_found_mail_before",
        "geode_expected_output_qid", "geode_expected_output_stack", "geode_expected_output_quality",
        "geode_accepted_outputs_json", "geode_expected_mail_additions_json", "geode_projection_fingerprint",
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "geode_action_raw", "geode_action_token", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildGeodeProcessingParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "geode_processing");
        var qid = ReadParameter(action, "geode_qualified_item_id");
        var purpose = ReadParameter(action, "geode_purpose");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(qid) ||
            string.IsNullOrWhiteSpace(purpose) || !TryFindGeodeInput(projection.Value, qid, out var input) ||
            !TryResolveGeodeCompilerTarget(projection.Value, action, snapshot, out var target)) return action.Parameters;
        var primary = input.TryGetProperty("expected_output", out var output) && output.ValueKind == JsonValueKind.Object
            ? output : default;
        var context = projection.Value.TryGetProperty("predictor_context", out var predictor) && predictor.ValueKind == JsonValueKind.Object
            ? predictor : default;
        var parameters = action.Parameters.Where(parameter =>
            !GeodeBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        parameters.AddRange(new[]
        {
            Parameter("geode_purpose", purpose), Parameter("geode_qualified_item_id", qid),
            Parameter("geode_slot_index", ReadInt(input, "slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_input_quality", ReadInt(input, "quality").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_stack_before", ReadInt(input, "stack_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_free_slots_before", ReadInt(projection.Value, "free_inventory_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_money_before", ReadInt(projection.Value, "money_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_price_gold", ReadInt(projection.Value, "price_gold", 25).ToString(CultureInfo.InvariantCulture)),
            Parameter("geodes_cracked_before", ReadInt(projection.Value, "geodes_cracked_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("mystery_boxes_opened_before", ReadInt(projection.Value, "mystery_boxes_opened_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("golden_coconut_cracked_before", (ReadBool(projection.Value, "golden_coconut_cracked_before") == true).ToString().ToLowerInvariant()),
            Parameter("golden_walnuts_before", ReadInt(projection.Value, "golden_walnuts_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("golden_walnuts_found_before", ReadInt(projection.Value, "golden_walnuts_found_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_archaeology_found_count", ReadInt(projection.Value, "archaeology_found_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_save_id_half", context.ValueKind == JsonValueKind.Object ? GeodeCompilerReadLong(context, "save_id_half").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_player_id_half", context.ValueKind == JsonValueKind.Object ? GeodeCompilerReadLong(context, "player_id_half").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_season", context.ValueKind == JsonValueKind.Object ? ReadString(context, "season") : string.Empty),
            Parameter("geode_deepest_mine_level", context.ValueKind == JsonValueKind.Object ? ReadInt(context, "deepest_mine_level").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_skill_1_level", context.ValueKind == JsonValueKind.Object ? ReadInt(context, "skill_1_unmodified_level").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_farming_mastery_unlocked", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "farming_mastery_unlocked") == true).ToString().ToLowerInvariant()),
            Parameter("geode_qi_beans_rule_active", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "qi_beans_rule_active") == true).ToString().ToLowerInvariant()),
            Parameter("geode_got_mystery_book_mail_before", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "got_mystery_book_mail") == true).ToString().ToLowerInvariant()),
            Parameter("geode_artifact_found_mail_before", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "artifact_found_mail") == true).ToString().ToLowerInvariant()),
            Parameter("geode_prediction_kind", ReadString(input, "kind")),
            Parameter("geode_expected_output_qid", primary.ValueKind == JsonValueKind.Object ? ReadString(primary, "qualified_item_id") : string.Empty),
            Parameter("geode_expected_output_stack", primary.ValueKind == JsonValueKind.Object ? ReadInt(primary, "stack").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_expected_output_quality", primary.ValueKind == JsonValueKind.Object ? ReadInt(primary, "quality").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_accepted_outputs_json", input.TryGetProperty("accepted_outputs", out var accepted) ? accepted.GetRawText() : "[]"),
            Parameter("geode_expected_mail_additions_json", input.TryGetProperty("expected_mail_additions", out var mail) ? mail.GetRawText() : "[]"),
            Parameter("geode_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)), Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)), Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_action_raw", target.ActionRaw), Parameter("geode_action_token", "Blacksmith"),
            Parameter("native_contract", GeodeCompilerNativeContract), Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileGeodeProcessingStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = new SmallModelAction { ActionId = action.ActionId, OptionId = action.OptionId,
            Rationale = action.Rationale, Parameters = BuildGeodeProcessingParameters(action, snapshot) };
        var qid = ReadParameter(bound, "geode_qualified_item_id");
        return string.IsNullOrWhiteSpace(qid) ? Array.Empty<CompiledActionStep>() : new[]
        {
            Step("crack_geode", qid + ":slot=" + ReadParameter(bound, "geode_slot_index"),
                "one_native_geode_consumed_money_stats_and_projected_output_receipt_verified", 420)
        };
    }

    private static string[] ValidateGeodeProcessingPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("processing.crack_geode" or "executor.crack_geode")) return Array.Empty<string>();
        var reasons = new List<string>();
        var projection = ReadStateFieldValue(snapshot, "player", "geode_processing");
        var qid = ReadParameter(action, "geode_qualified_item_id");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(ReadParameter(action, "geode_purpose")))
            return new[] { "geode_processing_complete_typed_projection_required" };
        if (ReadString(projection.Value, "base_service_status") != "ready" ||
            ReadStateFieldString(snapshot, "player", "location_id") != ReadString(projection.Value, "location_id"))
            reasons.Add("geode_processing_native_blacksmith_service_not_ready");
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("geode_processing_menu_must_be_clear");
        if (!TryFindGeodeInput(projection.Value, qid, out var input) || ReadString(input, "status") != "available" ||
            ReadBool(input, "locked_base_1_6_15") != true || ReadBool(input, "output_capacity_allowed") != true)
            return reasons.Append("geode_processing_selected_input_not_ready").Distinct(StringComparer.Ordinal).ToArray();
        if (!TryResolveGeodeCompilerTarget(projection.Value, action, snapshot, out var target))
            return reasons.Append("geode_processing_counter_endpoint_unreachable").Distinct(StringComparer.Ordinal).ToArray();
        var bound = new SmallModelAction { ActionId = action.ActionId, OptionId = action.OptionId,
            Rationale = action.Rationale, Parameters = BuildGeodeProcessingParameters(action, snapshot) };
        var primary = input.TryGetProperty("expected_output", out var output) && output.ValueKind == JsonValueKind.Object ? output : default;
        var context = projection.Value.TryGetProperty("predictor_context", out var predictor) && predictor.ValueKind == JsonValueKind.Object
            ? predictor : default;
        bool Exact(string name, string expected) => ReadParameter(bound, name) == expected;
        bool ExactInt(string name, int expected) => ReadIntParameter(bound, name) == expected;
        if (!ExactInt("geode_slot_index", ReadInt(input, "slot_index")) ||
            !ExactInt("geode_input_quality", ReadInt(input, "quality")) ||
            !ExactInt("geode_stack_before", ReadInt(input, "stack_before")) ||
            !ExactInt("geode_free_slots_before", ReadInt(projection.Value, "free_inventory_slots")) ||
            !ExactInt("geode_money_before", ReadInt(projection.Value, "money_before")) || !ExactInt("geode_price_gold", 25) ||
            !ExactInt("geodes_cracked_before", ReadInt(projection.Value, "geodes_cracked_before")) ||
            !ExactInt("mystery_boxes_opened_before", ReadInt(projection.Value, "mystery_boxes_opened_before")) ||
            !Exact("golden_coconut_cracked_before", (ReadBool(projection.Value, "golden_coconut_cracked_before") == true).ToString().ToLowerInvariant()) ||
            !ExactInt("golden_walnuts_before", ReadInt(projection.Value, "golden_walnuts_before")) ||
            !ExactInt("golden_walnuts_found_before", ReadInt(projection.Value, "golden_walnuts_found_before")) ||
            !ExactInt("geode_archaeology_found_count", ReadInt(projection.Value, "archaeology_found_count")) ||
            !Exact("geode_save_id_half", (context.ValueKind == JsonValueKind.Object ? GeodeCompilerReadLong(context, "save_id_half") : 0L).ToString(CultureInfo.InvariantCulture)) ||
            !Exact("geode_player_id_half", (context.ValueKind == JsonValueKind.Object ? GeodeCompilerReadLong(context, "player_id_half") : 0L).ToString(CultureInfo.InvariantCulture)) ||
            !Exact("geode_season", context.ValueKind == JsonValueKind.Object ? ReadString(context, "season") : string.Empty) ||
            !ExactInt("geode_deepest_mine_level", context.ValueKind == JsonValueKind.Object ? ReadInt(context, "deepest_mine_level") : 0) ||
            !ExactInt("geode_skill_1_level", context.ValueKind == JsonValueKind.Object ? ReadInt(context, "skill_1_unmodified_level") : 0) ||
            !Exact("geode_farming_mastery_unlocked", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "farming_mastery_unlocked") == true).ToString().ToLowerInvariant()) ||
            !Exact("geode_qi_beans_rule_active", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "qi_beans_rule_active") == true).ToString().ToLowerInvariant()) ||
            !Exact("geode_got_mystery_book_mail_before", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "got_mystery_book_mail") == true).ToString().ToLowerInvariant()) ||
            !Exact("geode_artifact_found_mail_before", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "artifact_found_mail") == true).ToString().ToLowerInvariant()) ||
            !Exact("geode_prediction_kind", ReadString(input, "kind")) ||
            !Exact("geode_expected_output_qid", primary.ValueKind == JsonValueKind.Object ? ReadString(primary, "qualified_item_id") : string.Empty) ||
            !Exact("geode_accepted_outputs_json", input.GetProperty("accepted_outputs").GetRawText()) ||
            !Exact("geode_expected_mail_additions_json", input.GetProperty("expected_mail_additions").GetRawText()) ||
            !Exact("geode_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")) ||
            !Exact("target_location", "Blacksmith") || !ExactInt("target_tile_x", target.TargetX) || !ExactInt("target_tile_y", target.TargetY) ||
            !ExactInt("stand_tile_x", target.StandX) || !ExactInt("stand_tile_y", target.StandY) ||
            !Exact("geode_action_raw", target.ActionRaw) || !Exact("geode_action_token", "Blacksmith") ||
            !Exact("native_contract", GeodeCompilerNativeContract))
            reasons.Add("geode_processing_complete_fresh_binding_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryFindGeodeInput(JsonElement projection, string qid, out JsonElement result)
    {
        result = default;
        if (!projection.TryGetProperty("inventory_inputs", out var rows) || rows.ValueKind != JsonValueKind.Array) return false;
        foreach (var row in rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
                     .OrderBy(row => ReadInt(row, "slot_index")))
            if (ReadString(row, "qualified_item_id") == qid) { result = row.Clone(); return true; }
        return false;
    }

    private static bool TryResolveGeodeCompilerTarget(JsonElement projection, SmallModelAction action,
        SnapshotEnvelope snapshot, out GeodeCompilerTarget target)
    {
        target = default!;
        if (!projection.TryGetProperty("counter_action_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array) return false;
        var requestedX = ReadIntParameter(action, "stand_tile_x"); var requestedY = ReadIntParameter(action, "stand_tile_y");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x"); var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var candidates = new List<GeodeCompilerTarget>();
        foreach (var row in rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object && ReadString(row, "action_token") == "Blacksmith"))
        {
            var x = ReadInt(row, "tile_x"); var y = ReadInt(row, "tile_y");
            SleepStandTile? stand = requestedX.HasValue && requestedY.HasValue && Math.Abs(x - requestedX.Value) + Math.Abs(y - requestedY.Value) == 1 &&
                SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                ? new SleepStandTile(requestedX.Value, requestedY.Value) : FindBestSleepStandTile(snapshot, x, y);
            if (stand is not null) candidates.Add(new(x, y, stand.X, stand.Y, ReadString(row, "action_raw"),
                Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y)));
        }
        var selected = candidates.OrderBy(row => row.Distance).ThenBy(row => row.TargetY).ThenBy(row => row.TargetX).FirstOrDefault();
        if (selected is null) return false; target = selected; return true;
    }

    private sealed record GeodeCompilerTarget(int TargetX, int TargetY, int StandX, int StandY, string ActionRaw, int Distance);
    private static long GeodeCompilerReadLong(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt64(out var result) ? result : 0L;
}
