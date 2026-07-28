using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static bool MonsterDropSourceMatches(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string requiredQualifiedItemId)
        {
            if (!string.Equals(
                    ReadParameter(action, "combat_terminal_state"),
                    "defeat",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadParameter(action, "qualified_item_id"),
                    requiredQualifiedItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var monster = ReadAttachedMonster(snapshot, action);
            return monster.HasValue &&
                ReadExpandedMonsterDropIds(snapshot, monster.Value)
                    .Contains(requiredQualifiedItemId, StringComparer.OrdinalIgnoreCase);
        }

        private static bool MonsterDropSourceMatchesContextTags(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string[] acceptableContextTagSets)
        {
            if (!string.Equals(
                    ReadParameter(action, "combat_terminal_state"),
                    "defeat",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var targetItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var monster = ReadAttachedMonster(snapshot, action);
            return !string.IsNullOrWhiteSpace(targetItemId) &&
                monster.HasValue &&
                ReadExpandedMonsterDropItems(snapshot, monster.Value)
                    .Any(item =>
                        string.Equals(
                            ReadString(item, "qualified_item_id"),
                            targetItemId,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            ReadString(item, "context_tag_status"),
                            "exact_item_get_context_tags",
                            StringComparison.Ordinal) &&
                        QuestContextTagMatcher.Matches(
                            ReadQuestStringArray(item, "context_tags"),
                            acceptableContextTagSets));
        }

        private static JsonElement? ReadAttachedMonster(
            SnapshotEnvelope snapshot,
            SmallModelAction action)
        {
            var monsters = ReadStateFieldValue(snapshot, "mining", "monsters");
            if (!monsters.HasValue || monsters.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var runtimeIdentity = ReadParameter(action, "target_runtime_identity") ?? string.Empty;
            var runtimeType = ReadParameter(action, "target_runtime_type") ?? string.Empty;
            var targetName = ReadParameter(action, "target_name") ?? string.Empty;
            var matches = monsters.Value.EnumerateArray()
                .Where(monster =>
                    string.Equals(ReadString(monster, "runtime_identity"), runtimeIdentity, StringComparison.Ordinal) &&
                    string.Equals(ReadString(monster, "runtime_type"), runtimeType, StringComparison.Ordinal) &&
                    string.Equals(ReadString(monster, "name"), targetName, StringComparison.Ordinal))
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static string[] ReadExpandedMonsterDropIds(
            SnapshotEnvelope snapshot,
            JsonElement monster)
        {
            var ids = new HashSet<string>(
                ReadQuestStringArray(monster, "possible_drop_qualified_item_ids"),
                StringComparer.OrdinalIgnoreCase);
            var catalogs = ReadStateFieldValue(snapshot, "mining", "monster_drop_catalogs");
            if (catalogs.HasValue && catalogs.Value.ValueKind == JsonValueKind.Array)
            {
                var keys = new HashSet<string>(
                    ReadQuestStringArray(monster, "conditional_drop_catalog_keys"),
                    StringComparer.Ordinal);
                foreach (var catalog in catalogs.Value.EnumerateArray())
                {
                    if (!keys.Contains(ReadString(catalog, "key")) ||
                        ReadBool(catalog, "active") != true ||
                        !ReadString(catalog, "item_identity_completeness")
                            .StartsWith("complete", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    ids.UnionWith(ReadQuestStringArray(
                        catalog,
                        "possible_qualified_item_ids"));
                }
            }
            return ids.OrderBy(itemId => itemId, StringComparer.Ordinal).ToArray();
        }

        private static IEnumerable<JsonElement> ReadExpandedMonsterDropItems(
            SnapshotEnvelope snapshot,
            JsonElement monster)
        {
            if (monster.TryGetProperty("possible_drop_items", out var directItems) &&
                directItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in directItems.EnumerateArray())
                {
                    yield return item;
                }
            }

        }
    }
}
