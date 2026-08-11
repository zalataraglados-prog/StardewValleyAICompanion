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
    private static CompiledActionStep[] CompileClaimQuestRewardStep(SmallModelAction action)
    {
        var fingerprint = ReadParameter(action, "quest_reward_fingerprint");
        var reward = ReadIntParameter(action, "quest_money_reward_expected");
        if (string.IsNullOrWhiteSpace(fingerprint) || !reward.HasValue || reward.Value <= 0)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "claim_quest_reward",
                "QuestLog:reward=" + fingerprint,
                "money_increased_by=" + reward.Value + ";quest_reward_consumed=true",
                300)
        };
    }

    private static string[] ValidateClaimQuestRewardPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.claim_quest_reward") return Array.Empty<string>();

        var reasons = new List<string>();
        var fingerprint = ReadParameter(action, "quest_reward_fingerprint");
        var questId = ReadParameter(action, "quest_id");
        var runtimeType = ReadParameter(action, "quest_runtime_type");
        var reward = ReadIntParameter(action, "quest_money_reward_expected");
        var expectedMoney = ReadIntParameter(action, "expected_money_before");
        if (string.IsNullOrWhiteSpace(fingerprint)) reasons.Add("quest_reward_fingerprint_required");
        if (string.IsNullOrWhiteSpace(runtimeType)) reasons.Add("quest_reward_runtime_type_required");
        if (!reward.HasValue || reward.Value <= 0) reasons.Add("quest_reward_amount_invalid");
        if (!expectedMoney.HasValue) reasons.Add("quest_reward_expected_money_before_required");
        var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
        if (activeMenu.HasValue && ReadBool(activeMenu.Value, "is_open") == true)
            reasons.Add("quest_reward_requires_clear_menu");
        if (expectedMoney.HasValue && ReadStateFieldInt(snapshot, "player", "money") != expectedMoney.Value)
            reasons.Add("quest_reward_money_before_drifted");

        var claims = ReadStateFieldValue(snapshot, "quests", "claimable_rewards");
        JsonElement? matching = null;
        if (claims.HasValue && claims.Value.ValueKind == JsonValueKind.Array)
        {
            matching = claims.Value.EnumerateArray().FirstOrDefault(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "reward_fingerprint"), fingerprint, StringComparison.Ordinal));
        }
        if (!matching.HasValue || matching.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("quest_reward_claimable_identity_missing");
        }
        else if (!matching.Value.TryGetProperty("quest", out var quest) || quest.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("quest_reward_payload_missing");
        }
        else
        {
            if (!string.Equals(ReadString(quest, "id"), questId, StringComparison.Ordinal))
                reasons.Add("quest_reward_id_drifted");
            if (!string.Equals(ReadString(quest, "runtime_type"), runtimeType, StringComparison.Ordinal))
                reasons.Add("quest_reward_runtime_type_drifted");
            if (ReadInt(quest, "money_reward") != reward)
                reasons.Add("quest_reward_amount_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
