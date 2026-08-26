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
    private static readonly string[] PotOfGoldBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "qualified_item_id", "quantity", "expected_output_items_json",
        "reward_branch", "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildPotOfGoldParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !PotOfGoldBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var reward = ReadStateFieldValue(snapshot, "current_location", "pot_of_gold_reward");
        if (!reward.HasValue || reward.Value.ValueKind != JsonValueKind.Object)
        {
            return parameters.ToArray();
        }
        var stand = PotOfGoldStand(reward.Value, snapshot);
        if (stand is null)
        {
            return parameters.ToArray();
        }
        parameters.AddRange(new[]
        {
            Parameter("target_location", "Forest"),
            Parameter("target_tile_x", ReadInt(reward.Value, "target_tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(reward.Value, "target_tile_y").ToString()),
            Parameter("stand_tile_x", stand.Value.X.ToString()),
            Parameter("stand_tile_y", stand.Value.Y.ToString()),
            Parameter("target_runtime_type", ReadString(reward.Value, "target_runtime_type")),
            Parameter("qualified_item_id", ReadString(reward.Value, "qualified_item_id")),
            Parameter("quantity", ReadInt(reward.Value, "expected_coin_quantity").ToString()),
            Parameter("expected_output_items_json", ReadString(reward.Value, "expected_output_items_json")),
            Parameter("reward_branch", ReadString(reward.Value, "reward_branch")),
            Parameter("interaction_kind", ReadString(reward.Value, "interaction_kind")),
            Parameter("expected_action_type", ReadString(reward.Value, "expected_action_type")),
            Parameter("native_contract", ReadString(reward.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileClaimPotOfGoldStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundPotOfGoldAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var quantity = ReadIntParameter(bound, "quantity");
        if (!x.HasValue || !y.HasValue || !quantity.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "claim_pot_of_gold",
                "Forest(" + x.Value + "," + y.Value + "):(O)PotOfGold",
                "current_location.pot_of_gold_reward.exact_object_present=false;reward_conserved_in_inventory_plus_debris=(O)GoldCoin*" + quantity.Value + "+(H)LeprechuanHat*1;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidatePotOfGoldPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "rewards.claim_pot_of_gold")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var reward = ReadStateFieldValue(snapshot, "current_location", "pot_of_gold_reward");
        if (!reward.HasValue || reward.Value.ValueKind != JsonValueKind.Object)
        {
            return new[] { "pot_of_gold_projection_unavailable" };
        }
        if (!string.Equals(ReadString(reward.Value, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("pot_of_gold_not_ready_by_transparent_state:" + ReadString(reward.Value, "status"));
        }
        var bound = BoundPotOfGoldAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var standX = ReadIntParameter(bound, "stand_tile_x");
        var standY = ReadIntParameter(bound, "stand_tile_y");
        var quantity = ReadIntParameter(bound, "quantity");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue || !quantity.HasValue)
        {
            reasons.Add("pot_of_gold_typed_target_fields_required");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1)
        {
            reasons.Add("pot_of_gold_stand_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("pot_of_gold_menu_must_be_clear");
        }
        if (x.Value != ReadInt(reward.Value, "target_tile_x") ||
            y.Value != ReadInt(reward.Value, "target_tile_y") ||
            quantity.Value != ReadInt(reward.Value, "expected_coin_quantity") ||
            !string.Equals(ReadParameter(bound, "target_location"), "Forest", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), ReadString(reward.Value, "target_runtime_type"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), ReadString(reward.Value, "qualified_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "reward_branch"), ReadString(reward.Value, "reward_branch"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), ReadString(reward.Value, "interaction_kind"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), ReadString(reward.Value, "expected_action_type"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), ReadString(reward.Value, "native_contract"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_output_items_json"), ReadString(reward.Value, "expected_output_items_json"), StringComparison.Ordinal))
        {
            reasons.Add("pot_of_gold_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundPotOfGoldAction(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        return new SmallModelAction
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildPotOfGoldParameters(action, snapshot)
        };
    }

    private static (int X, int Y)? PotOfGoldStand(JsonElement reward, SnapshotEnvelope snapshot)
    {
        if (!reward.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
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
