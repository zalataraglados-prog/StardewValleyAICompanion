using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.OptionRegistry
{
    internal static class QuestContextTagMatcher
    {
        public static bool Matches(JsonElement item, string[] contextTagSets)
        {
            if (contextTagSets is null || contextTagSets.Length == 0)
            {
                return false;
            }

            return Matches(ReadTags(item, "context_tags"), contextTagSets);
        }

        public static bool Matches(IEnumerable<string> itemContextTags, string[] contextTagSets)
        {
            if (contextTagSets is null || contextTagSets.Length == 0)
            {
                return false;
            }

            var tags = itemContextTags
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var set in contextTagSets.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var allGroupsMatch = true;
                foreach (var group in set.Split(','))
                {
                    if (!DoAnyTagsMatch(group.Split('/'), tags))
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

        public static bool MatchesDonateObjective(JsonElement item, string[] contextTagSets)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                contextTagSets is null || contextTagSets.Length == 0)
            {
                return false;
            }

            var itemTags = ReadTags(item, "context_tags");
            var donateColor = ReadDonateColorContext(item);
            foreach (var set in contextTagSets.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var setFailed = false;
                foreach (var group in set.Split(','))
                {
                    if (group.StartsWith("color", StringComparison.Ordinal) &&
                        donateColor.IsColoredObject)
                    {
                        if (donateColor.HasPreservedParent)
                        {
                            if (!donateColor.IsExact)
                            {
                                setFailed = true;
                                break;
                            }
                            if (DoAnyTagsMatch(group.Split('/'), donateColor.ParentBaseTags))
                            {
                                return true;
                            }

                            setFailed = true;
                            break;
                        }
                        if (!donateColor.PreservedParentKnownAbsent)
                        {
                            setFailed = true;
                            break;
                        }
                    }

                    if (!DoAnyTagsMatch(group.Split('/'), itemTags))
                    {
                        setFailed = true;
                        break;
                    }
                }

                if (!setFailed)
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> ReadTags(JsonElement item, string propertyName)
        {
            return item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(propertyName, out var tags) &&
                tags.ValueKind == JsonValueKind.Array
                    ? tags.EnumerateArray()
                        .Where(value => value.ValueKind == JsonValueKind.String)
                        .Select(value => value.GetString() ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private static DonateColorContext ReadDonateColorContext(JsonElement item)
        {
            var runtimeType = item.TryGetProperty("runtime_type", out var runtimeTypeValue) &&
                runtimeTypeValue.ValueKind == JsonValueKind.String
                    ? runtimeTypeValue.GetString() ?? string.Empty
                    : string.Empty;
            var runtimeTypeSaysColored = runtimeType.EndsWith(".ColoredObject", StringComparison.Ordinal) ||
                string.Equals(runtimeType, "ColoredObject", StringComparison.Ordinal);
            if (!item.TryGetProperty("donate_color_context", out var context) ||
                context.ValueKind != JsonValueKind.Object)
            {
                return new DonateColorContext(runtimeTypeSaysColored, false, false, false, EmptyTags());
            }

            var projectedColored = context.TryGetProperty("is_colored_object", out var isColoredValue) &&
                isColoredValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? isColoredValue.GetBoolean()
                    : runtimeTypeSaysColored;
            var isColored = runtimeTypeSaysColored || projectedColored;
            var parentId = context.TryGetProperty("preserved_parent_item_id", out var parentIdValue) &&
                parentIdValue.ValueKind == JsonValueKind.String
                    ? parentIdValue.GetString() ?? string.Empty
                    : string.Empty;
            var status = context.TryGetProperty("projection_status", out var statusValue) &&
                statusValue.ValueKind == JsonValueKind.String
                    ? statusValue.GetString() ?? string.Empty
                    : string.Empty;
            var parentTags = ReadTags(context, "preserved_parent_base_context_tags");
            return new DonateColorContext(
                isColored,
                !string.IsNullOrWhiteSpace(parentId),
                string.Equals(
                    status,
                    "exact_native_preserved_parent_base_context_tags",
                    StringComparison.Ordinal),
                string.Equals(
                    status,
                    "not_applicable_no_preserved_parent",
                    StringComparison.Ordinal),
                parentTags);
        }

        private static HashSet<string> EmptyTags() => new(StringComparer.OrdinalIgnoreCase);

        private static bool DoAnyTagsMatch(IEnumerable<string> requiredTags, HashSet<string> actualTags)
        {
            foreach (var requiredTag in requiredTags)
            {
                if (DoesTagMatch(requiredTag, actualTags))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DoesTagMatch(string? tag, HashSet<string> actualTags)
        {
            if (tag is null)
            {
                return false;
            }

            tag = tag.Trim();
            var expected = true;
            if (tag.StartsWith("!", StringComparison.Ordinal))
            {
                tag = tag[1..].TrimStart();
                expected = false;
            }

            return tag.Length > 0 && actualTags.Contains(tag) == expected;
        }

        private readonly record struct DonateColorContext(
            bool IsColoredObject,
            bool HasPreservedParent,
            bool IsExact,
            bool PreservedParentKnownAbsent,
            HashSet<string> ParentBaseTags);
    }
}
