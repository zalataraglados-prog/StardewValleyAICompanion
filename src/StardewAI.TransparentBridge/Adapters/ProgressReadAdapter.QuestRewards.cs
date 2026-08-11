using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ProgressQuestReadAdapter
{
    private QuestRewardClaimRef[]? ReadClaimableQuestRewards(Farmer? player)
    {
        return player?.questLog
            .Select(quest =>
            {
                var mapped = mapper.MapQuest(quest);
                var reasons = new System.Collections.Generic.List<string>();
                if (quest.IsHidden()) reasons.Add("quest_hidden_from_native_quest_log");
                if (!quest.ShouldDisplayAsComplete()) reasons.Add("quest_not_displayed_as_complete");
                if (!quest.HasMoneyReward()) reasons.Add("quest_has_no_unclaimed_money_reward");
                if (quest.GetMoneyReward() <= 0) reasons.Add("quest_money_reward_not_positive");
                return new QuestRewardClaimRef
                {
                    RewardFingerprint = QuestRewardClaimIdentity.Compute(mapped),
                    Quest = mapped,
                    Claimable = reasons.Count == 0,
                    Status = reasons.Count == 0 ? "ready" : "blocked",
                    BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
                };
            })
            .Where(claim => claim.Claimable)
            .OrderBy(claim => claim.Quest.Id, StringComparer.Ordinal)
            .ThenBy(claim => claim.RewardFingerprint, StringComparer.Ordinal)
            .ToArray();
    }
}
