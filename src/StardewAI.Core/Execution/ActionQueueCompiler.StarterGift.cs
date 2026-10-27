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
    private static readonly string[] StarterGiftBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "qualified_item_id", "quantity", "expected_output_items_json",
        "quest_id", "reward_branch", "interaction_kind", "expected_action_type", "native_contract",
        "projection_fingerprint", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildStarterGiftParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !StarterGiftBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var gift = ReadStateFieldValue(snapshot, "current_location", "starter_gift_claim");
        if (!gift.HasValue || gift.Value.ValueKind != JsonValueKind.Object)
        {
            return parameters.ToArray();
        }
        var stand = StarterGiftStand(gift.Value, snapshot);
        if (stand is null)
        {
            return parameters.ToArray();
        }
        parameters.AddRange(new[]
        {
            Parameter("target_location", ReadString(gift.Value, "location_id")),
            Parameter("target_tile_x", ReadInt(gift.Value, "target_tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(gift.Value, "target_tile_y").ToString()),
            Parameter("stand_tile_x", stand.Value.X.ToString()),
            Parameter("stand_tile_y", stand.Value.Y.ToString()),
            Parameter("target_runtime_type", ReadString(gift.Value, "target_runtime_type")),
            Parameter("qualified_item_id", ReadString(gift.Value, "qualified_item_id")),
            Parameter("quantity", Math.Max(1, ReadInt(gift.Value, "content_slot_count")).ToString()),
            Parameter("expected_output_items_json", ReadString(gift.Value, "expected_output_items_json")),
            Parameter("quest_id", ReadString(gift.Value, "expected_quest_id")),
            Parameter("reward_branch", "starter_gift_farm_type_" + ReadInt(gift.Value, "farm_type")),
            Parameter("interaction_kind", ReadString(gift.Value, "interaction_kind")),
            Parameter("expected_action_type", ReadString(gift.Value, "expected_action_type")),
            Parameter("native_contract", ReadString(gift.Value, "native_contract")),
            Parameter("projection_fingerprint", ReadString(gift.Value, "projection_fingerprint")),
            Parameter("max_movement_tiles", "64")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileClaimStarterGiftStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundStarterGiftAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!x.HasValue || !y.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "claim_starter_gift",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + ")",
                "current_location.starter_gift_claim.exact_starter_gift_present=false;quest_id=" + ReadParameter(bound, "quest_id") + ";starter_gift_outputs_added_to_inventory=true;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidateStarterGiftPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "rewards.claim_starter_gift")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var gift = ReadStateFieldValue(snapshot, "current_location", "starter_gift_claim");
        if (!gift.HasValue || gift.Value.ValueKind != JsonValueKind.Object)
        {
            return new[] { "starter_gift_projection_unavailable" };
        }
        if (!string.Equals(ReadString(gift.Value, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("starter_gift_not_ready_by_transparent_state:" + ReadString(gift.Value, "status"));
        }
        var bound = BoundStarterGiftAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var standX = ReadIntParameter(bound, "stand_tile_x");
        var standY = ReadIntParameter(bound, "stand_tile_y");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue)
        {
            reasons.Add("starter_gift_typed_target_fields_required");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1)
        {
            reasons.Add("starter_gift_stand_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("starter_gift_menu_must_be_clear");
        }
        if (x.Value != ReadInt(gift.Value, "target_tile_x") ||
            y.Value != ReadInt(gift.Value, "target_tile_y") ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadString(gift.Value, "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), ReadString(gift.Value, "target_runtime_type"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), ReadString(gift.Value, "qualified_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_output_items_json"), ReadString(gift.Value, "expected_output_items_json"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "quest_id"), ReadString(gift.Value, "expected_quest_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "reward_branch"), "starter_gift_farm_type_" + ReadInt(gift.Value, "farm_type"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), ReadString(gift.Value, "interaction_kind"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), ReadString(gift.Value, "expected_action_type"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), ReadString(gift.Value, "native_contract"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "projection_fingerprint"), ReadString(gift.Value, "projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("starter_gift_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundStarterGiftAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildStarterGiftParameters(action, snapshot)
        };

    private static (int X, int Y)? StarterGiftStand(JsonElement gift, SnapshotEnvelope snapshot)
    {
        if (!gift.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return stands.EnumerateArray()
            .Where(row => ReadBool(row, "available") == true)
            .Select(row => (X: ReadInt(row, "tile_x"), Y: ReadInt(row, "tile_y")))
            .OrderBy(row => Math.Abs(row.X - playerX) + Math.Abs(row.Y - playerY))
            .ThenBy(row => row.Y)
            .ThenBy(row => row.X)
            .Select(row => ((int X, int Y)?)row)
            .FirstOrDefault();
    }
}
