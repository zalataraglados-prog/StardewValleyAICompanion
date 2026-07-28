using System;

namespace StardewAI.Core.OptionRegistry
{
    internal static class QuestGiftObjectiveMatcher
    {
        public static bool IsKnownMinimumLikeLevel(string? minimumLikeLevel)
        {
            return MinimumRank(minimumLikeLevel).HasValue;
        }

        public static bool MeetsMinimumLikeLevel(string? giftTaste, string? minimumLikeLevel)
        {
            var minimum = MinimumRank(minimumLikeLevel);
            var actual = GiftTasteRank(giftTaste);
            return minimum.HasValue && actual.HasValue && actual.Value >= minimum.Value;
        }

        private static int? MinimumRank(string? value)
        {
            return value?.Trim() switch
            {
                var text when string.Equals(text, "None", StringComparison.OrdinalIgnoreCase) => 0,
                var text when string.Equals(text, "Hated", StringComparison.OrdinalIgnoreCase) => 1,
                var text when string.Equals(text, "Disliked", StringComparison.OrdinalIgnoreCase) => 2,
                var text when string.Equals(text, "Neutral", StringComparison.OrdinalIgnoreCase) => 3,
                var text when string.Equals(text, "Liked", StringComparison.OrdinalIgnoreCase) => 4,
                var text when string.Equals(text, "Loved", StringComparison.OrdinalIgnoreCase) => 5,
                _ => null
            };
        }

        private static int? GiftTasteRank(string? value)
        {
            return value?.Trim() switch
            {
                var text when string.Equals(text, "hate", StringComparison.OrdinalIgnoreCase) => 1,
                var text when string.Equals(text, "dislike", StringComparison.OrdinalIgnoreCase) => 2,
                var text when string.Equals(text, "neutral", StringComparison.OrdinalIgnoreCase) => 3,
                var text when string.Equals(text, "like", StringComparison.OrdinalIgnoreCase) => 4,
                var text when string.Equals(text, "love", StringComparison.OrdinalIgnoreCase) => 5,
                // GiftObjective's native switch doesn't assign a LikeLevel for Stardrop Tea.
                var text when string.Equals(text, "stardrop_tea", StringComparison.OrdinalIgnoreCase) => 0,
                _ => null
            };
        }
    }
}
