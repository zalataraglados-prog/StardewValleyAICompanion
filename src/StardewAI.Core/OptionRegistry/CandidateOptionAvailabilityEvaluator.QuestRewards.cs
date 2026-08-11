using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] QuestRewardClaimCandidates(SnapshotEnvelope snapshot)
    {
        var state = ReadStateFieldValue(snapshot, "quests", "claimable_rewards");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Array)
        {
            return new[] { BlockedQuestRewardClaimCandidate("claimable_quest_rewards_transparent_state_missing") };
        }

        var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
        var menuOpen = activeMenu.HasValue && ReadBool(activeMenu.Value, "is_open") == true;
        var rows = state.Value.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => QuestRewardClaimCandidate(snapshot, row, menuOpen))
            .ToArray();
        return rows.Length == 0
            ? new[] { BlockedQuestRewardClaimCandidate("no_claimable_quest_money_rewards") }
            : rows;
    }

    private static EventCandidate QuestRewardClaimCandidate(
        SnapshotEnvelope snapshot,
        JsonElement claim,
        bool menuOpen)
    {
        var fingerprint = ReadString(claim, "reward_fingerprint");
        var claimable = ReadBool(claim, "claimable") == true;
        var quest = claim.TryGetProperty("quest", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        var questId = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "id") : string.Empty;
        var runtimeType = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "runtime_type") : string.Empty;
        var title = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "title") : string.Empty;
        var reward = quest.ValueKind == JsonValueKind.Object ? ReadInt(quest, "money_reward") : 0;
        var reasons = ReadDailyQuestStringArray(claim, "blocked_diagnostics").ToList();
        if (!claimable) reasons.Add("quest_reward_not_claimable");
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(runtimeType))
            reasons.Add("quest_reward_identity_incomplete");
        if (reward <= 0) reasons.Add("quest_reward_amount_not_positive");
        if (menuOpen) reasons.Add("quest_reward_requires_clear_menu");
        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new EventCandidate
        {
            CandidateId = "quest.claim_reward:" + fingerprint,
            Kind = "claim_quest_reward",
            Available = blocking.Length == 0,
            DisplayName = title,
            ExpectedEffect = "money_increased_by=" + reward + ";quest_reward_consumed=true",
            EstimatedTicks = 180,
            EnergyCost = 0,
            AvailabilityClass = "native_quest_log_money_reward",
            AllowedNow = blocking.Length == 0,
            AllowedToday = true,
            BlockReasons = blocking,
            Parameters = new[]
            {
                Parameter("quest_candidate_id", "quest_reward:" + fingerprint),
                Parameter("quest_family", "ordinary"),
                Parameter("quest_id", questId),
                Parameter("quest_runtime_type", runtimeType),
                Parameter("quest_reward_fingerprint", fingerprint),
                Parameter("quest_reward_title", title),
                Parameter("quest_money_reward_expected", reward.ToString()),
                Parameter("expected_money_before", ReadStateFieldInt(snapshot, "player", "money").ToString())
            }
        };
    }

    private static EventCandidate BlockedQuestRewardClaimCandidate(params string[] reasons) => new()
    {
        CandidateId = "quest.claim_reward:blocked",
        Kind = "claim_quest_reward",
        Available = false,
        ExpectedEffect = "quest_reward_not_claimed",
        AvailabilityClass = "quest_reward_claim_blocked",
        BlockReasons = reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
    };
}
