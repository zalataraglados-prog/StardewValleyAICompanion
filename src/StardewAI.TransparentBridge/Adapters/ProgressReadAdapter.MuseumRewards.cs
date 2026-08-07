using StardewValley;
using StardewValley.GameData.Museum;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static bool TryProjectMuseumRewards(
        LibraryMuseum museum,
        Farmer player,
        Dictionary<string, MuseumRewards> rewards,
        Item? candidateItem,
        out MuseumRewardProjection projection)
    {
        projection = new MuseumRewardProjection();
        try
        {
            var beforeCounts = museum.GetDonatedByContextTag(rewards);
            var afterCounts = beforeCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            if (candidateItem is not null)
            {
                foreach (var tag in afterCounts.Keys.ToArray())
                {
                    if (tag.Length == 0 || ItemContextTagManager.HasBaseTag(candidateItem.ItemId, tag))
                    {
                        afterCounts[tag]++;
                    }
                }
            }

            var pendingBefore = PendingMuseumRewardIds(museum, player, rewards, beforeCounts);
            var pendingAfter = PendingMuseumRewardIds(museum, player, rewards, afterCounts);
            var autoRewards = rewards
                .Where(pair => pair.Value.RewardItemId is null)
                .Where(pair => museum.CanCollectReward(pair.Value, pair.Key, player, afterCounts))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            projection = new MuseumRewardProjection
            {
                PendingRewardIdsBefore = pendingBefore,
                PendingRewardIdsAfter = pendingAfter,
                NewlyPendingRewardIds = pendingAfter.Except(pendingBefore, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                AutoAppliedRewardIds = autoRewards.Select(pair => pair.Key).ToArray(),
                AutoAppliedRewardActions = autoRewards
                    .SelectMany(pair => pair.Value.RewardActions ?? new List<string>())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string[] PendingMuseumRewardIds(
        LibraryMuseum museum,
        Farmer player,
        Dictionary<string, MuseumRewards> rewards,
        Dictionary<string, int> counts)
    {
        return rewards
            .Where(pair => pair.Value.RewardItemId is not null)
            .Where(pair => museum.CanCollectReward(pair.Value, pair.Key, player, counts))
            .Where(pair =>
            {
                var item = ItemRegistry.Create(pair.Value.RewardItemId!, pair.Value.RewardItemCount);
                return !player.mailReceived.Contains(museum.getRewardItemKey(item));
            })
            .Select(pair => pair.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class MuseumRewardProjection
    {
        public string[] PendingRewardIdsBefore { get; init; } = Array.Empty<string>();
        public string[] PendingRewardIdsAfter { get; init; } = Array.Empty<string>();
        public string[] NewlyPendingRewardIds { get; init; } = Array.Empty<string>();
        public string[] AutoAppliedRewardIds { get; init; } = Array.Empty<string>();
        public string[] AutoAppliedRewardActions { get; init; } = Array.Empty<string>();
    }
}
