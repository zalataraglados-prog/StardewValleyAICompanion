using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.OptionRegistry
{
    internal static class QuestContextTagMatcher
    {
        public static bool ContainsUnprojectedColorTag(string[] contextTagSets)
        {
            return contextTagSets is not null &&
                contextTagSets
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .SelectMany(value => value.Split(
                        new[] { ',' },
                        StringSplitOptions.RemoveEmptyEntries))
                    .Any(group => group.Trim().StartsWith("color", StringComparison.Ordinal));
        }

        public static bool Matches(JsonElement item, string[] contextTagSets)
        {
            if (contextTagSets is null || contextTagSets.Length == 0)
            {
                return false;
            }

            var tags = item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("context_tags", out var contextTags) &&
                contextTags.ValueKind == JsonValueKind.Array
                    ? contextTags.EnumerateArray()
                        .Where(value => value.ValueKind == JsonValueKind.String)
                        .Select(value => value.GetString() ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
            return Matches(tags, contextTagSets);
        }

        public static bool Matches(IEnumerable<string> itemContextTags, string[] contextTagSets)
        {
            if (contextTagSets is null || contextTagSets.Length == 0)
            {
                return false;
            }

            var tags = itemContextTags
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var set in contextTagSets.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var allGroupsMatch = true;
                foreach (var rawGroup in set.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var group = rawGroup.Trim();
                    // Native preserved-object color checks use base-parent tags which aren't
                    // present in the inventory projection yet.
                    if (group.StartsWith("color", StringComparison.Ordinal))
                    {
                        allGroupsMatch = false;
                        break;
                    }

                    var alternatives = group.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => value.Trim());
                    if (!alternatives.Any(tags.Contains))
                    {
                        allGroupsMatch = false;
                        break;
                    }
                }

                if (allGroupsMatch)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
