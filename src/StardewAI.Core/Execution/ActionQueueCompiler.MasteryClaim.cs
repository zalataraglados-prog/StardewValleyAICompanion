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
    private const string MasteryClaimCompilerNativeContract =
        "Forest.MasteryRoom(all_five_base_skills_10)->MasteryCave;MasteryCave_skill_action->MasteryTrackerMenu(skill)->mainButton->claimReward(recipes,direct_inventory_else_debris,mastery_stat,masteryLevelsSpent,combat_trinket_slot,all_plaque_finale)";

    private static readonly string[] MasteryClaimBoundNames =
    {
        "mastery_skill_id", "mastery_skill_key", "mastery_projection_fingerprint", "mastery_option_fingerprint",
        "mastery_experience_before", "mastery_level_before", "mastery_levels_spent_before", "mastery_skill_stat_before",
        "mastery_all_skill_stats_before_csv", "mastery_recipe_rewards_json", "mastery_direct_rewards_json",
        "mastery_grants_trinket_slot", "mastery_trinket_slots_before", "target_location", "target_tile_x", "target_tile_y",
        "stand_tile_x", "stand_tile_y", "mastery_action_raw", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildMasteryClaimParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !MasteryClaimBoundNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        var projection = ReadStateFieldValue(snapshot, "player", "mastery_claim");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("claimable_options", out var options) || options.ValueKind != JsonValueKind.Array)
            return parameters.ToArray();
        var requestedSkill = ReadParameter(action, "mastery_skill_id");
        if (string.IsNullOrWhiteSpace(requestedSkill)) requestedSkill = ReadParameter(action, "continuation.mastery_skill_id");
        var requestedFingerprint = ReadParameter(action, "mastery_option_fingerprint");
        if (string.IsNullOrWhiteSpace(requestedFingerprint)) requestedFingerprint = ReadParameter(action, "continuation.mastery_option_fingerprint");
        var option = options.EnumerateArray().FirstOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "skill_id").ToString(CultureInfo.InvariantCulture) == requestedSkill &&
            (string.IsNullOrWhiteSpace(requestedFingerprint) || ReadString(row, "option_fingerprint") == requestedFingerprint));
        if (option.ValueKind != JsonValueKind.Object || !option.TryGetProperty("action_tile", out var tile) || tile.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var target = ResolveMasteryClaimTarget(tile, action, snapshot);
        if (target is null) return parameters.ToArray();
        var statsCsv = string.Join(",", projection.Value.GetProperty("skills").EnumerateArray()
            .OrderBy(row => ReadInt(row, "skill_id"))
            .Select(row => ReadInt(row, "mastery_stat_value").ToString(CultureInfo.InvariantCulture)));
        parameters.AddRange(new[]
        {
            Parameter("mastery_skill_id", requestedSkill),
            Parameter("mastery_skill_key", ReadString(option, "skill_key")),
            Parameter("mastery_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("mastery_option_fingerprint", ReadString(option, "option_fingerprint")),
            Parameter("mastery_experience_before", ReadInt(projection.Value, "mastery_experience").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_level_before", ReadInt(projection.Value, "current_mastery_level").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_levels_spent_before", ReadInt(projection.Value, "mastery_levels_spent").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_skill_stat_before", ReadInt(option, "mastery_stat_value").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_all_skill_stats_before_csv", statsCsv),
            Parameter("mastery_recipe_rewards_json", option.GetProperty("recipe_rewards").GetRawText()),
            Parameter("mastery_direct_rewards_json", option.GetProperty("direct_rewards").GetRawText()),
            Parameter("mastery_grants_trinket_slot", (ReadBool(option, "grants_trinket_slot") == true).ToString().ToLowerInvariant()),
            Parameter("mastery_trinket_slots_before", ReadInt(projection.Value, "trinket_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(tile, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_action_raw", ReadString(tile, "action_raw")),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileMasteryClaimStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundMasteryClaimAction(action, snapshot);
        var skillId = ReadIntParameter(bound, "mastery_skill_id");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!skillId.HasValue || skillId is < 0 or > 4 || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("claim_mastery", "MasteryCave(" + x + "," + y + "):skill=" + skillId,
                "mastery_" + skillId + "+=1;masteryLevelsSpent+=1;native_rewards_settled=true", 1200)
        };
    }

    private static string[] ValidateMasteryClaimPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("skills.claim_mastery" or "executor.claim_mastery")) return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("mastery_claim_requires_clear_menu");
        var projection = ReadStateFieldValue(snapshot, "player", "mastery_claim");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "mastery_claim_projection_unavailable" };
        if (ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "autonomous_strategic_choice" ||
            ReadString(projection.Value, "native_contract") != MasteryClaimCompilerNativeContract ||
            ReadString(projection.Value, "service_status") != "ready" ||
            ReadBool(projection.Value, "all_base_skills_level_ten") != true ||
            ReadInt(projection.Value, "unspent_mastery_levels") <= 0)
            reasons.Add("mastery_claim_complete_ready_projection_required");
        var bound = BoundMasteryClaimAction(action, snapshot);
        var skillId = ReadIntParameter(bound, "mastery_skill_id");
        var option = skillId.HasValue && projection.Value.TryGetProperty("claimable_options", out var options) && options.ValueKind == JsonValueKind.Array
            ? options.EnumerateArray().FirstOrDefault(row => ReadInt(row, "skill_id") == skillId.Value)
            : default;
        var tile = default(JsonElement);
        var hasTile = option.ValueKind == JsonValueKind.Object &&
            option.TryGetProperty("action_tile", out tile) && tile.ValueKind == JsonValueKind.Object;
        var target = hasTile ? ResolveMasteryClaimTarget(tile, action, snapshot) : null;
        var exact = option.ValueKind == JsonValueKind.Object && target is not null &&
            ReadParameter(bound, "mastery_skill_key") == ReadString(option, "skill_key") &&
            ReadParameter(bound, "mastery_projection_fingerprint") == ReadString(projection.Value, "projection_fingerprint") &&
            ReadParameter(bound, "mastery_option_fingerprint") == ReadString(option, "option_fingerprint") &&
            ReadIntParameter(bound, "mastery_experience_before") == ReadInt(projection.Value, "mastery_experience") &&
            ReadIntParameter(bound, "mastery_level_before") == ReadInt(projection.Value, "current_mastery_level") &&
            ReadIntParameter(bound, "mastery_levels_spent_before") == ReadInt(projection.Value, "mastery_levels_spent") &&
            ReadIntParameter(bound, "mastery_skill_stat_before") == 0 &&
            ReadParameter(bound, "mastery_recipe_rewards_json") == option.GetProperty("recipe_rewards").GetRawText() &&
            ReadParameter(bound, "mastery_direct_rewards_json") == option.GetProperty("direct_rewards").GetRawText() &&
            ReadParameter(bound, "target_location") == "MasteryCave" &&
            ReadIntParameter(bound, "target_tile_x") == target.TargetX &&
            ReadIntParameter(bound, "target_tile_y") == target.TargetY &&
            ReadIntParameter(bound, "stand_tile_x") == target.StandX &&
            ReadIntParameter(bound, "stand_tile_y") == target.StandY &&
            ReadParameter(bound, "mastery_action_raw") == ReadString(tile, "action_raw") &&
            ReadParameter(bound, "native_contract") == MasteryClaimCompilerNativeContract;
        if (!exact) reasons.Add("mastery_claim_complete_fresh_typed_binding_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundMasteryClaimAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildMasteryClaimParameters(action, snapshot)
    };

    private static MasteryClaimCompilerTarget? ResolveMasteryClaimTarget(
        JsonElement tile,
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var targetX = ReadInt(tile, "tile_x");
        var targetY = ReadInt(tile, "tile_y");
        var requestedX = ReadIntParameter(action, "stand_tile_x");
        var requestedY = ReadIntParameter(action, "stand_tile_y");
        var stand = requestedX.HasValue && requestedY.HasValue &&
            Math.Abs(targetX - requestedX.Value) + Math.Abs(targetY - requestedY.Value) == 1 &&
            SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                ? new SleepStandTile(requestedX.Value, requestedY.Value)
                : FindBestSleepStandTile(snapshot, targetX, targetY);
        return stand is null ? null : new MasteryClaimCompilerTarget(targetX, targetY, stand.X, stand.Y);
    }

    private sealed record MasteryClaimCompilerTarget(int TargetX, int TargetY, int StandX, int StandY);
}
