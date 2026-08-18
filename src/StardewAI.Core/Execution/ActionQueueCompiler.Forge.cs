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
    private static CompiledActionStep[] CompileForgeItemStep(SmallModelAction action)
    {
        var operation = ReadParameter(action, "forge_operation");
        var source = ReadParameter(action, "forge_source_id");
        return string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(source) ? Array.Empty<CompiledActionStep>() :
            new[] { Step("forge_item", source + ":" + operation, "native_forge_input_shard_stat_output_and_random_domain_receipt_verified", 240) };
    }

    private static string[] ValidateForgeItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.forge_item") return Array.Empty<string>();
        var reasons = new List<string>();
        var candidateId = ReadParameter(action, "forge_candidate_id");
        var operation = ReadParameter(action, "forge_operation");
        var reason = ReadParameter(action, "forge_reason");
        var location = ReadParameter(action, "location_id");
        var x = ForgeInt(action, "interaction_tile_x"); var y = ForgeInt(action, "interaction_tile_y");
        var sx = ForgeInt(action, "stand_tile_x"); var sy = ForgeInt(action, "stand_tile_y");
        if (string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(reason) ||
            string.IsNullOrWhiteSpace(location) || !x.HasValue || !y.HasValue || !sx.HasValue || !sy.HasValue)
            return new[] { "forge_item_typed_projection_required" };
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), location, StringComparison.OrdinalIgnoreCase)) reasons.Add("forge_item_location_drifted");
        if (Math.Abs(sx.Value - x.Value) + Math.Abs(sy.Value - y.Value) != 1) reasons.Add("forge_item_stand_tile_not_adjacent");
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("forge_item_menu_must_be_clear");
        var row = ForgeRow(snapshot, candidateId);
        if (!row.HasValue) { reasons.Add("forge_item_not_verified_by_transparent_state"); return reasons.ToArray(); }
        var pairs = new[]
        {
            ("forge_operation", "forge_operation"), ("forge_source_id", "forge_source_id"), ("forge_source_kind", "forge_source_kind"),
            ("location_id", "location_id"), ("left_source_id", "left_source_id"), ("left_state_json", "left_state_json"),
            ("right_source_id", "right_source_id"), ("right_state_json", "right_state_json"),
            ("forge_output_contract_kind", "output_contract_kind"), ("expected_output_state_json", "expected_output_state_json"),
            ("random_outcome_contract_json", "random_outcome_contract_json")
        };
        if (ReadString(row.Value, "forge_candidate_status") != "ready_for_native_forge_menu" ||
            pairs.Any(pair => ReadParameter(action, pair.Item1) != ReadString(row.Value, pair.Item2)) ||
            x != ReadInt(row.Value, "interaction_tile_x") || y != ReadInt(row.Value, "interaction_tile_y") ||
            ForgeInt(action, "forge_shard_cost") != ReadInt(row.Value, "shard_cost") ||
            ForgeInt(action, "forge_shard_refund") != ReadInt(row.Value, "shard_refund") ||
            ForgeInt(action, "forge_shard_count_before") != ReadInt(row.Value, "shard_count_before") ||
            ForgeLong(action, "times_enchanted_before") != ReadInt(row.Value, "times_enchanted_before") ||
            ForgeLong(action, "times_enchanted_after") != ReadInt(row.Value, "times_enchanted_after"))
            reasons.Add("forge_item_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? ForgeRow(SnapshotEnvelope snapshot, string candidateId)
    {
        var context = ReadStateFieldValue(snapshot, "player", "forge");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            ReadString(context.Value, "projection_status") != "complete_loaded_native_forge_source_and_live_input_projection" ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array) return null;
        foreach (var row in rows.EnumerateArray())
            if (row.ValueKind == JsonValueKind.Object && ReadString(row, "forge_candidate_id") == candidateId) return row.Clone();
        return null;
    }

    private static int? ForgeInt(SmallModelAction action, string name) =>
        int.TryParse(ReadParameter(action, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static long? ForgeLong(SmallModelAction action, string name) =>
        long.TryParse(ReadParameter(action, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
