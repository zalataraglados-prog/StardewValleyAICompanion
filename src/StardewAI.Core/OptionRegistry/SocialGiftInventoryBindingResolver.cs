using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed class SocialGiftInventoryBinding
    {
        public int SlotIndex { get; set; }

        public string ItemId { get; set; } = string.Empty;

        public string QualifiedItemId { get; set; } = string.Empty;

        public int Quality { get; set; }

        public int StackBefore { get; set; }

        public string GiftTaste { get; set; } = string.Empty;

        public int? ExpectedFriendshipDelta { get; set; }

        public int FriendshipPointsBefore { get; set; }

        public int? ExpectedFriendshipPointsAfter { get; set; }

        public bool GiftUpdatesNormalLimits { get; set; }

        public string GiftSideEffectRisk { get; set; } = string.Empty;

        public bool EvidenceComplete { get; set; }

        public bool Available { get; set; }

        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class SocialGiftInventoryBindingResolution
    {
        public string Status { get; set; } = "blocked";

        public string NpcName { get; set; } = string.Empty;

        public SocialGiftInventoryBinding[] Bindings { get; set; } =
            Array.Empty<SocialGiftInventoryBinding>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed partial class SocialGiftInventoryBindingResolver
    {
        public SocialGiftInventoryBindingResolution Resolve(
            SnapshotEnvelope snapshot,
            string npcName) => ResolveCore(
                (section, field) => ReadableStatus(
                        ReadStateFieldStatus(snapshot, section, field))
                    ? ReadStateFieldValue(snapshot, section, field)
                    : null,
                npcName);

        public SocialGiftInventoryBindingResolution Resolve(
            JsonElement snapshot,
            string npcName) => ResolveCore(
                (section, field) => ReadRawField(snapshot, section, field),
                npcName);

        private static SocialGiftInventoryBindingResolution ResolveCore(
            Func<string, string, JsonElement?> readField,
            string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return Blocked(npcName, "social_gift_npc_identity_missing");

            var inventory = readField("player", "inventory");
            if (!inventory.HasValue ||
                inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return Blocked(
                    npcName,
                    "social_gift_inventory_missing_or_incomplete");
            }

            var socialRows = readField("npcs", "social_interaction");
            var npcRows = socialRows.HasValue &&
                socialRows.Value.ValueKind == JsonValueKind.Array
                    ? socialRows.Value.EnumerateArray().Where(row =>
                        row.ValueKind == JsonValueKind.Object &&
                        string.Equals(
                            ReadString(row, "name"),
                            npcName,
                            StringComparison.OrdinalIgnoreCase))
                        .ToArray()
                    : Array.Empty<JsonElement>();
            if (npcRows.Length != 1)
            {
                return Blocked(
                    npcName,
                    npcRows.Length == 0
                        ? "social_gift_social_interaction_row_missing"
                        : "social_gift_social_interaction_row_ambiguous");
            }

            var friendshipRows = readField("npcs", "friendships");
            var friendship = FindRow(
                friendshipRows,
                "npc_name",
                npcName);
            var tastes = readField("npcs", "gift_tastes");
            var dialogueEvents = readField(
                "player",
                "active_dialogue_events");
            var spouseNode = readField("player", "spouse");
            var spouse = spouseNode.HasValue &&
                spouseNode.Value.ValueKind == JsonValueKind.String
                    ? spouseNode.Value.GetString() ?? string.Empty
                    : string.Empty;
            var npc = npcRows[0];
            var bindings = inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object &&
                    !ReadBool(item, "is_empty"))
                .Select(item => Assess(
                    item,
                    npc,
                    npcName,
                    friendship,
                    tastes,
                    dialogueEvents,
                    spouse))
                .OrderBy(binding => binding.SlotIndex)
                .ToArray();
            var incomplete = bindings
                .Where(binding => !binding.EvidenceComplete)
                .SelectMany(binding => binding.BlockReasons.Select(reason =>
                    reason + ":slot:" + binding.SlotIndex))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new SocialGiftInventoryBindingResolution
            {
                Status = incomplete.Length == 0 ? "exact" : "blocked",
                NpcName = npcName,
                Bindings = bindings,
                BlockingReasons = incomplete
            };
        }

    }
}
