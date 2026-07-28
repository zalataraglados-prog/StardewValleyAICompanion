using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static string[] MatchingMonsterDropQualifiedItemIds(
            SnapshotEnvelope snapshot,
            string[] acceptableContextTagSets)
        {
            var monsters = ReadStateFieldValue(snapshot, "mining", "monsters");
            if (!monsters.HasValue ||
                monsters.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var monster in monsters.Value.EnumerateArray())
            {
                AddMatchingMonsterDropItems(
                    monster,
                    "possible_drop_items",
                    acceptableContextTagSets,
                    matches);
            }
            return matches.OrderBy(itemId => itemId, StringComparer.Ordinal).ToArray();
        }

        private static void AddMatchingMonsterDropItems(
            JsonElement owner,
            string propertyName,
            string[] acceptableContextTagSets,
            HashSet<string> matches)
        {
            if (owner.TryGetProperty(propertyName, out var items))
            {
                AddMatchingMonsterDropItems(items, acceptableContextTagSets, matches);
            }
        }

        private static void AddMatchingMonsterDropItems(
            JsonElement items,
            string[] acceptableContextTagSets,
            HashSet<string> matches)
        {
            if (items.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            foreach (var item in items.EnumerateArray())
            {
                if (!string.Equals(
                        ReadString(item, "context_tag_status"),
                        "exact_item_get_context_tags",
                        StringComparison.Ordinal) ||
                    !QuestContextTagMatcher.Matches(
                        ReadMonsterDropStringArray(item, "context_tags"),
                        acceptableContextTagSets))
                {
                    continue;
                }
                var qualifiedItemId = ReadString(item, "qualified_item_id");
                if (!string.IsNullOrWhiteSpace(qualifiedItemId))
                {
                    matches.Add(qualifiedItemId);
                }
            }
        }

        private static string[] ReadMonsterDropStringArray(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToArray()
                    : Array.Empty<string>();
        }
    }
}
