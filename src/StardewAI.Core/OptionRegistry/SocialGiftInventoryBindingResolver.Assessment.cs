using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class SocialGiftInventoryBindingResolver
    {
        private static SocialGiftInventoryBinding Assess(
            JsonElement item,
            JsonElement npc,
            string npcName,
            JsonElement? friendship,
            JsonElement? tastes,
            JsonElement? dialogueEvents,
            string spouse)
        {
            var reasons = new List<string>();
            var incomplete = new List<string>();
            var slotIndex = ReadInt(item, "slot_index");
            var itemId = ReadString(item, "item_id");
            var qualifiedItemId = ReadString(item, "qualified_item_id");
            var quality = ReadInt(item, "quality");
            var stack = ReadInt(item, "stack");

            if (!TryReadBool(item, "is_object", out var isObject))
                incomplete.Add("social_gift_item_object_shape_incomplete");
            else if (!isObject)
                reasons.Add("social_gift_item_not_object");
            if (!TryReadInt(item, "stack", out stack))
                incomplete.Add("social_gift_item_stack_incomplete");
            else if (stack <= 0)
                reasons.Add("social_gift_item_stack_empty");
            if (ReadBool(item, "protected_from_auto_sell"))
                reasons.Add("social_gift_protected_item");
            if (ReadBool(item, "object_quest_item") ||
                string.Equals(
                    ReadString(item, "object_type"),
                    "Quest",
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("social_gift_quest_delivery_ambiguous");
            }
            if (ReadBool(item, "object_big_craftable") ||
                ReadBool(item, "is_furniture") ||
                ReadBool(item, "is_wallpaper"))
            {
                reasons.Add("social_gift_item_not_giftable_shape");
            }

            // Once the native item shape is conclusively ineligible, taste data
            // cannot change the result and should not turn an exclusion into a block.
            var shapeRejected = reasons.Count > 0;
            if (!shapeRejected)
            {
                if (!TryReadBool(
                        item,
                        "can_be_given_as_gift",
                        out var canBeGiven))
                {
                    incomplete.Add(
                        "social_gift_item_can_be_given_missing_or_malformed");
                }
                else if (!canBeGiven)
                {
                    reasons.Add("social_gift_item_can_be_given_false");
                }
                if (!TryReadBool(
                        item,
                        "base_tag_not_giftable",
                        out var baseNotGiftable))
                {
                    incomplete.Add(
                        "social_gift_item_base_tag_not_giftable_missing_or_malformed");
                }
                else if (baseNotGiftable)
                {
                    reasons.Add("social_gift_item_base_tag_not_giftable");
                }
                if (ReadBool(item, "special_item"))
                    reasons.Add("social_gift_special_item_branch_unsupported");
                if (!item.TryGetProperty("context_tags", out var tags) ||
                    tags.ValueKind != JsonValueKind.Array)
                {
                    incomplete.Add("social_gift_context_tags_incomplete");
                }
                if (IsNativeSpecialSwitchItem(qualifiedItemId))
                {
                    reasons.Add(
                        "social_gift_special_switch_item_branch_unsupported");
                }
                if (HasContextTagPrefix(item, "propose_roommate_"))
                {
                    reasons.Add(
                        "social_gift_roommate_proposal_context_branch_unsupported");
                }
            }

            var isStardropTea = string.Equals(
                qualifiedItemId,
                "(O)StardropTea",
                StringComparison.OrdinalIgnoreCase);
            var tasteLabel = string.Empty;
            int? expectedDelta = null;
            if (!shapeRejected && incomplete.Count == 0 && reasons.Count == 0)
            {
                if (friendship.HasValue &&
                    ReadBool(friendship.Value, "is_divorced"))
                {
                    reasons.Add("social_gift_divorced_rejected");
                }
                var limitExempt =
                    string.Equals(spouse, npcName, StringComparison.Ordinal) ||
                    ReadBool(npc, "is_child") ||
                    ReadBool(npc, "is_birthday");
                if (friendship.HasValue)
                {
                    if (ReadInt(friendship.Value, "gifts_today") >= 1 &&
                        !isStardropTea)
                    {
                        reasons.Add("social_gift_daily_limit_exhausted");
                    }
                    if (ReadInt(friendship.Value, "gifts_this_week") >= 2 &&
                        !limitExempt &&
                        !isStardropTea)
                    {
                        reasons.Add("social_gift_weekly_limit_exhausted");
                    }
                }
                if (!dialogueEvents.HasValue ||
                    dialogueEvents.Value.ValueKind != JsonValueKind.Array)
                {
                    incomplete.Add("social_gift_dialogue_event_state_incomplete");
                }
                else if (dialogueEvents.Value.EnumerateArray().Any(value =>
                    value.ValueKind == JsonValueKind.String &&
                    (value.GetString() ?? string.Empty).Contains(
                        "dumped",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    reasons.Add("social_gift_dumped_dialogue_rejection");
                }

                var taste = FindTaste(
                    tastes,
                    npcName,
                    slotIndex,
                    qualifiedItemId,
                    quality);
                if (!taste.HasValue)
                {
                    incomplete.Add("social_gift_taste_incomplete");
                }
                else if (!ReadBool(
                    taste.Value,
                    "expected_friendship_delta_complete"))
                {
                    incomplete.Add("social_gift_delta_incomplete");
                }
                else if (!int.TryParse(
                    ReadString(taste.Value, "expected_friendship_delta"),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedDelta))
                {
                    incomplete.Add("social_gift_delta_malformed");
                }
                else
                {
                    expectedDelta = parsedDelta;
                    tasteLabel = ReadString(taste.Value, "taste");
                    if (parsedDelta <= 0)
                    {
                        reasons.Add(
                            "social_gift_no_positive_friendship_delta");
                    }
                }
            }

            var sideEffectRisk = GiftSideEffectRisk(
                spouse,
                npc,
                npcName,
                isStardropTea);
            if (!shapeRejected &&
                !string.Equals(
                    sideEffectRisk,
                    "none_identified_from_transparent_branch",
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "social_gift_stochastic_spouse_jealousy_projection_incomplete");
            }

            var allReasons = reasons
                .Concat(incomplete)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var pointsBefore = friendship.HasValue
                ? ReadInt(friendship.Value, "points")
                : 0;
            return new SocialGiftInventoryBinding
            {
                SlotIndex = slotIndex,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                Quality = quality,
                StackBefore = stack,
                GiftTaste = tasteLabel,
                ExpectedFriendshipDelta = expectedDelta,
                FriendshipPointsBefore = pointsBefore,
                ExpectedFriendshipPointsAfter = expectedDelta.HasValue
                    ? pointsBefore + expectedDelta.Value
                    : (int?)null,
                GiftUpdatesNormalLimits = !isStardropTea,
                GiftSideEffectRisk = sideEffectRisk,
                EvidenceComplete = incomplete.Count == 0,
                Available = allReasons.Length == 0,
                BlockReasons = allReasons
            };
        }


    }
}
