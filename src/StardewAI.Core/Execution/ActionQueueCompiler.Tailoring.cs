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
    private static CompiledActionStep[] CompileTailorItemStep(SmallModelAction action)
    {
        var operation = ReadParameter(action, "tailoring_operation");
        var source = ReadParameter(action, "tailoring_source_id");
        return string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(source)
            ? Array.Empty<CompiledActionStep>()
            : new[]
            {
                Step(
                    "tailor_item",
                    source + ":" + operation,
                    "native_tailoring_input_consumption_output_domain_tailored_history_and_leftover_receipt_verified",
                    2400)
            };
    }

    private static string[] ValidateTailorItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.tailor_item")
            return Array.Empty<string>();
        var candidateId = ReadParameter(action, "tailoring_candidate_id");
        var purpose = ReadParameter(action, "tailoring_purpose");
        var location = ReadParameter(action, "location_id");
        var x = TailoringInt(action, "interaction_tile_x");
        var y = TailoringInt(action, "interaction_tile_y");
        var sx = TailoringInt(action, "stand_tile_x");
        var sy = TailoringInt(action, "stand_tile_y");
        if (string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(purpose) ||
            string.IsNullOrWhiteSpace(location) || !x.HasValue || !y.HasValue || !sx.HasValue || !sy.HasValue)
            return new[] { "tailoring_typed_projection_required" };

        var reasons = new List<string>();
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), location, StringComparison.OrdinalIgnoreCase))
            reasons.Add("tailoring_location_drifted");
        if (Math.Abs(sx.Value - x.Value) + Math.Abs(sy.Value - y.Value) != 1)
            reasons.Add("tailoring_stand_tile_not_adjacent");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("tailoring_menu_must_be_clear");
        var row = TailoringRow(snapshot, candidateId);
        if (!row.HasValue)
        {
            reasons.Add("tailoring_candidate_not_verified_by_transparent_state");
            return reasons.ToArray();
        }
        var pairs = new[]
        {
            ("tailoring_operation", "tailoring_operation"), ("tailoring_purpose", "tailoring_purpose"),
            ("tailoring_recipe_id", "recipe_id"), ("tailoring_source_id", "source_id"),
            ("tailoring_source_kind", "source_kind"), ("location_id", "location_id"),
            ("left_source_id", "left_source_id"), ("left_state_json", "left_state_json"),
            ("right_source_id", "right_source_id"), ("right_state_json", "right_state_json"),
            ("tailoring_output_contract_kind", "output_contract_kind"),
            ("expected_output_state_json", "expected_output_state_json"),
            ("random_outcome_contract_json", "random_outcome_contract_json"),
            ("tailoring_tailored_counts_before_json", "tailored_counts_before_json"),
            ("tailoring_native_contract", "native_contract")
        };
        if (ReadString(row.Value, "tailoring_candidate_status") != "ready_for_native_tailoring_menu" ||
            pairs.Any(pair => ReadParameter(action, pair.Item1) != ReadString(row.Value, pair.Item2)) ||
            x != ReadInt(row.Value, "interaction_tile_x") || y != ReadInt(row.Value, "interaction_tile_y") ||
            TailoringInt(action, "tailoring_spend_left_count") != ReadInt(row.Value, "spend_left_count") ||
            TailoringInt(action, "tailoring_spend_right_count") != ReadInt(row.Value, "spend_right_count") ||
            TailoringBool(action, "tailoring_marks_tailored_item") != ReadBool(row.Value, "marks_tailored_item"))
            reasons.Add("tailoring_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? TailoringRow(SnapshotEnvelope snapshot, string candidateId)
    {
        var context = ReadStateFieldValue(snapshot, "player", "tailoring");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            ReadString(context.Value, "projection_status") != "complete_live_native_tailoring_recipe_input_endpoint_and_output_domain_projection" ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var row in rows.EnumerateArray())
            if (row.ValueKind == JsonValueKind.Object && ReadString(row, "tailoring_candidate_id") == candidateId)
                return row.Clone();
        return null;
    }

    private static int? TailoringInt(SmallModelAction action, string name) =>
        int.TryParse(ReadParameter(action, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static bool? TailoringBool(SmallModelAction action, string name) =>
        bool.TryParse(ReadParameter(action, name), out var value) ? value : null;
}
