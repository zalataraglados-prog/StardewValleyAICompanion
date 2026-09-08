using System;
using System.Linq;
using System.Text.Json;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class SocialGiftInventoryBindingResolver
    {
        private static JsonElement? FindRow(
            JsonElement? rows,
            string identityProperty,
            string identity)
        {
            if (!rows.HasValue || rows.Value.ValueKind != JsonValueKind.Array)
                return null;
            var matches = rows.Value.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, identityProperty),
                    identity,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return matches.Length == 1 ? matches[0] : (JsonElement?)null;
        }

        private static JsonElement? FindTaste(
            JsonElement? tastes,
            string npcName,
            int slotIndex,
            string qualifiedItemId,
            int quality)
        {
            if (!tastes.HasValue || tastes.Value.ValueKind != JsonValueKind.Array)
                return null;
            var matches = tastes.Value.EnumerateArray().Where(taste =>
                taste.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(taste, "npc_name"),
                    npcName,
                    StringComparison.OrdinalIgnoreCase) &&
                ReadInt(taste, "slot_index") == slotIndex &&
                string.Equals(
                    ReadString(taste, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                ReadInt(taste, "quality") == quality &&
                ReadBool(taste, "complete"))
                .ToArray();
            return matches.Length == 1 ? matches[0] : (JsonElement?)null;
        }

        private static string GiftSideEffectRisk(
            string spouse,
            JsonElement npc,
            string npcName,
            bool isStardropTea)
        {
            if (isStardropTea ||
                string.IsNullOrWhiteSpace(spouse) ||
                string.Equals(spouse, npcName, StringComparison.Ordinal) ||
                !ReadBool(npc, "is_datably_flagged"))
            {
                return "none_identified_from_transparent_branch";
            }
            return "spouse_jealousy_stochastic_side_effect_possible_not_in_target_delta";
        }

        private static bool IsNativeSpecialSwitchItem(string id) => id is
            "(O)233" or "(O)897" or "(O)71" or "(O)864" or "(O)865" or
            "(O)866" or "(O)867" or "(O)868" or "(O)869" or "(O)870" or
            "(O)809" or "(O)458" or "(O)277" or "(O)460";

        private static bool HasContextTagPrefix(
            JsonElement item,
            string prefix)
        {
            if (!item.TryGetProperty("context_tags", out var tags) ||
                tags.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            return tags.EnumerateArray().Any(tag =>
                tag.ValueKind == JsonValueKind.String &&
                (tag.GetString() ?? string.Empty).StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static JsonElement? ReadRawField(
            JsonElement snapshot,
            string section,
            string field)
        {
            if (snapshot.ValueKind != JsonValueKind.Object ||
                !snapshot.TryGetProperty("state", out var state) ||
                state.ValueKind != JsonValueKind.Object ||
                !state.TryGetProperty(section, out var sectionNode) ||
                sectionNode.ValueKind != JsonValueKind.Object ||
                !sectionNode.TryGetProperty(field, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String ||
                status.GetString() is not ("available" or "derived") ||
                !envelope.TryGetProperty("value", out var value))
            {
                return null;
            }
            return value;
        }

        private static bool TryReadBool(
            JsonElement source,
            string name,
            out bool value)
        {
            value = false;
            if (!source.TryGetProperty(name, out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }
            value = property.GetBoolean();
            return true;
        }

        private static bool TryReadInt(
            JsonElement source,
            string name,
            out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static SocialGiftInventoryBindingResolution Blocked(
            string npcName,
            string reason) => new()
        {
            NpcName = npcName,
            BlockingReasons = new[] { reason }
        };

    }
}
