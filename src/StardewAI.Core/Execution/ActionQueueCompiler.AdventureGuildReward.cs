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
    private const string AdventureGuildRewardCompilerNativeContract =
        "AdventureGuild.checkAction_gil_tile->gil_all_complete_unclaimed_goals->DialogueBox_optional->ItemGrabMenu->receiveLeftClick_each_reward->OnRewardCollected_Gil_goalId";

    private static readonly string[] AdventureGuildRewardBoundNames =
    {
        "adventure_guild_reward_batch_fingerprint", "adventure_guild_reward_goals_json",
        "adventure_guild_reward_pending_goal_count", "adventure_guild_reward_item_count",
        "adventure_guild_reward_dialogue_count", "adventure_guild_reward_inventory_max_items",
        "adventure_guild_reward_inventory_occupied_slots", "adventure_guild_reward_inventory_capacity_sufficient",
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "adventure_guild_reward_action_tile_index", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildAdventureGuildRewardParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var requestedFingerprint = ReadParameter(action, "adventure_guild_reward_batch_fingerprint");
        var parameters = action.Parameters
            .Where(parameter => !AdventureGuildRewardBoundNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        var projection = ReadStateFieldValue(snapshot, "quests", "adventure_guild_reward");
        var liveFingerprint = projection.HasValue && projection.Value.ValueKind == JsonValueKind.Object
            ? ReadString(projection.Value, "batch_fingerprint")
            : string.Empty;
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(liveFingerprint) ||
            (!string.IsNullOrWhiteSpace(requestedFingerprint) && liveFingerprint != requestedFingerprint) ||
            !projection.Value.TryGetProperty("goals", out var goals) || goals.ValueKind != JsonValueKind.Array)
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("adventure_guild_reward_batch_fingerprint", liveFingerprint),
            Parameter("adventure_guild_reward_goals_json", goals.GetRawText()),
            Parameter("adventure_guild_reward_pending_goal_count", ReadInt(projection.Value, "pending_goal_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("adventure_guild_reward_item_count", ReadInt(projection.Value, "reward_item_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("adventure_guild_reward_dialogue_count", ReadInt(projection.Value, "reward_dialogue_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("adventure_guild_reward_inventory_max_items", ReadInt(projection.Value, "inventory_max_items").ToString(CultureInfo.InvariantCulture)),
            Parameter("adventure_guild_reward_inventory_occupied_slots", ReadInt(projection.Value, "inventory_occupied_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("adventure_guild_reward_inventory_capacity_sufficient", (ReadBool(projection.Value, "inventory_capacity_sufficient") == true).ToString().ToLowerInvariant()),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", ReadInt(projection.Value, "action_tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", ReadInt(projection.Value, "action_tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", ReadInt(projection.Value, "stand_tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", ReadInt(projection.Value, "stand_tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("adventure_guild_reward_action_tile_index", ReadInt(projection.Value, "action_tile_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileAdventureGuildRewardStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundAdventureGuildRewardAction(action, snapshot);
        var fingerprint = ReadParameter(bound, "adventure_guild_reward_batch_fingerprint");
        if (string.IsNullOrWhiteSpace(fingerprint)) return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("claim_adventure_guild_reward", "AdventureGuild:Gil:batch=" + fingerprint,
                "all_pending_Gil_goal_flags=true;reward_items=" + ReadParameter(bound, "adventure_guild_reward_item_count"), 3600)
        };
    }

    private static string[] ValidateAdventureGuildRewardPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("rewards.claim_adventure_guild_reward" or "executor.claim_adventure_guild_reward"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("adventure_guild_reward_requires_clear_menu");
        var projection = ReadStateFieldValue(snapshot, "quests", "adventure_guild_reward");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "adventure_guild_reward_projection_unavailable" };
        if (ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "autonomous_positive_reward" ||
            ReadString(projection.Value, "native_contract") != AdventureGuildRewardCompilerNativeContract ||
            ReadString(projection.Value, "status") != "ready")
            reasons.Add("adventure_guild_reward_complete_ready_projection_required");
        var bound = BoundAdventureGuildRewardAction(action, snapshot);
        var fingerprint = ReadParameter(bound, "adventure_guild_reward_batch_fingerprint");
        var goalsJson = projection.Value.TryGetProperty("goals", out var goals) && goals.ValueKind == JsonValueKind.Array
            ? goals.GetRawText()
            : string.Empty;
        var exact = !string.IsNullOrWhiteSpace(fingerprint) &&
            fingerprint == ReadString(projection.Value, "batch_fingerprint") &&
            ReadParameter(bound, "adventure_guild_reward_goals_json") == goalsJson &&
            ReadIntParameter(bound, "adventure_guild_reward_pending_goal_count") == ReadInt(projection.Value, "pending_goal_count") &&
            ReadIntParameter(bound, "adventure_guild_reward_item_count") == ReadInt(projection.Value, "reward_item_count") &&
            ReadIntParameter(bound, "adventure_guild_reward_dialogue_count") == ReadInt(projection.Value, "reward_dialogue_count") &&
            ReadIntParameter(bound, "adventure_guild_reward_inventory_max_items") == ReadInt(projection.Value, "inventory_max_items") &&
            ReadIntParameter(bound, "adventure_guild_reward_inventory_occupied_slots") == ReadInt(projection.Value, "inventory_occupied_slots") &&
            ReadBoolParameter(bound, "adventure_guild_reward_inventory_capacity_sufficient") == true &&
            ReadParameter(bound, "target_location") == "AdventureGuild" &&
            ReadIntParameter(bound, "target_tile_x") == ReadInt(projection.Value, "action_tile_x") &&
            ReadIntParameter(bound, "target_tile_y") == ReadInt(projection.Value, "action_tile_y") &&
            ReadIntParameter(bound, "stand_tile_x") == ReadInt(projection.Value, "stand_tile_x") &&
            ReadIntParameter(bound, "stand_tile_y") == ReadInt(projection.Value, "stand_tile_y") &&
            ReadIntParameter(bound, "adventure_guild_reward_action_tile_index") == ReadInt(projection.Value, "action_tile_index") &&
            ReadParameter(bound, "native_contract") == AdventureGuildRewardCompilerNativeContract;
        if (!exact) reasons.Add("adventure_guild_reward_complete_fresh_typed_binding_required");
        if (ReadInt(projection.Value, "pending_goal_count") <= 0 ||
            ReadInt(projection.Value, "pending_goal_count") != ReadInt(projection.Value, "reward_item_count"))
            reasons.Add("adventure_guild_reward_complete_item_backed_batch_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundAdventureGuildRewardAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildAdventureGuildRewardParameters(action, snapshot)
    };
}
