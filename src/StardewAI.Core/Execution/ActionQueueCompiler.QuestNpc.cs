using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static string[] ValidateQuestNpcInteractPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.quest_npc_interact")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var interactionKind = ReadParameter(action, "quest_interaction_kind") ?? string.Empty;
            var family = ReadParameter(action, "quest_family") ?? string.Empty;
            var candidateId = ReadParameter(action, "quest_candidate_id") ?? string.Empty;
            var questId = ReadParameter(action, "quest_id") ?? string.Empty;
            var questKey = ReadParameter(action, "quest_key") ?? string.Empty;
            var runtimeType = ReadParameter(action, "quest_runtime_type") ?? string.Empty;
            var objectiveIndex = ReadIntParameter(action, "quest_objective_index");
            var expectedCurrent = ReadIntParameter(action, "quest_expected_current_count");
            var expectedTarget = ReadIntParameter(action, "quest_expected_target_count");

            if (string.IsNullOrWhiteSpace(npcName)) reasons.Add("quest_target_npc_required");
            if (interactionKind is not ("report" or "offer_item" or "gift")) reasons.Add("quest_interaction_kind_report_offer_item_or_gift_required");
            if (string.IsNullOrWhiteSpace(candidateId)) reasons.Add("quest_candidate_id_required");
            if (family is not ("ordinary_quest" or "special_order")) reasons.Add("quest_family_invalid");
            if (string.IsNullOrWhiteSpace(runtimeType)) reasons.Add("quest_runtime_type_required");
            if (!expectedCurrent.HasValue || !expectedTarget.HasValue) reasons.Add("quest_expected_progress_required");

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue)
            {
                reasons.Add("quest_npc_and_stand_tiles_required");
            }
            else if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1)
            {
                reasons.Add("quest_stand_not_adjacent_to_npc");
            }

            ValidateQuestIdentityAgainstSnapshot(
                snapshot,
                family,
                candidateId,
                questId,
                questKey,
                runtimeType,
                objectiveIndex,
                expectedCurrent,
                expectedTarget,
                reasons);
            ValidateQuestNpcGeometry(snapshot, npcName, targetX, targetY, standX, standY, reasons);

            if (interactionKind is "offer_item" or "gift")
            {
                var slot = ReadIntParameter(action, "slot_index");
                var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
                if (!slot.HasValue || string.IsNullOrWhiteSpace(qualifiedItemId))
                {
                    reasons.Add("quest_offer_inventory_identity_required");
                }
                else
                {
                    var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
                    var matching = inventory.HasValue && inventory.Value.ValueKind == JsonValueKind.Array &&
                        inventory.Value.EnumerateArray().Any(item =>
                            ReadInt(item, "slot_index") == slot.Value &&
                            ReadBool(item, "is_empty") != true &&
                            ReadInt(item, "stack") > 0 &&
                            string.Equals(ReadString(item, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase));
                    if (!matching)
                    {
                        reasons.Add("quest_offer_inventory_identity_drifted");
                    }
                }
            }
            if (interactionKind == "gift")
            {
                ValidateQuestGiftObjective(
                    action,
                    snapshot,
                    family,
                    questKey,
                    objectiveIndex,
                    reasons);
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("quest_npc_interact_menu_must_be_clear");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static void ValidateQuestGiftObjective(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string family,
            string questKey,
            int? objectiveIndex,
            ICollection<string> reasons)
        {
            if (family != "special_order")
            {
                reasons.Add("quest_gift_requires_special_order_family");
                return;
            }

            var state = ReadStateFieldValue(snapshot, "quests", "special_orders");
            var orders = state.HasValue && state.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(state.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();
            var order = orders.SingleOrDefault(row =>
                string.Equals(row.QuestKey, questKey, StringComparison.Ordinal));
            if (order is null ||
                !objectiveIndex.HasValue ||
                objectiveIndex.Value < 0 ||
                objectiveIndex.Value >= order.Objectives.Length)
            {
                reasons.Add("special_order_gift_objective_not_found");
                return;
            }

            var objective = order.Objectives[objectiveIndex.Value];
            var fields = objective.PerTypeFields;
            if (!string.Equals(objective.RuntimeType, "GiftObjective", StringComparison.Ordinal) ||
                fields is null ||
                !fields.Available)
            {
                reasons.Add("special_order_gift_objective_projection_unavailable");
                return;
            }

            var minimumLikeLevel = ReadParameter(action, "quest_gift_minimum_like_level") ?? string.Empty;
            if (!string.Equals(minimumLikeLevel, fields.MinimumLikeLevel, StringComparison.Ordinal))
            {
                reasons.Add("special_order_gift_minimum_like_level_drifted");
            }
            var contextTagSetsJson = ReadParameter(action, "quest_gift_acceptable_context_tag_sets_json") ?? string.Empty;
            string[] projectedTagSets;
            try
            {
                projectedTagSets = JsonSerializer.Deserialize<string[]>(contextTagSetsJson) ?? Array.Empty<string>();
            }
            catch (JsonException)
            {
                projectedTagSets = Array.Empty<string>();
            }
            if (!projectedTagSets.SequenceEqual(fields.AcceptableContextTagSets, StringComparer.Ordinal))
            {
                reasons.Add("special_order_gift_context_tag_sets_drifted");
            }
            if (QuestContextTagMatcher.ContainsUnprojectedColorTag(fields.AcceptableContextTagSets))
            {
                reasons.Add("special_order_gift_has_unprojected_color_tags");
            }

            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var slotIndex = ReadIntParameter(action, "slot_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var giftCandidate = SocialCandidateBuilder.Build(snapshot, "social.gift_npc", int.MaxValue)
                .FirstOrDefault(candidate =>
                    candidate.Available &&
                    string.Equals(
                        SocialCandidateBuilder.CandidateParameter(candidate, "npc_name"),
                        npcName,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        SocialCandidateBuilder.CandidateParameter(candidate, "slot_index"),
                        slotIndex?.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        SocialCandidateBuilder.CandidateParameter(candidate, "qualified_item_id"),
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase));
            if (giftCandidate is null)
            {
                reasons.Add("special_order_gift_social_candidate_drifted");
                return;
            }
            if (!QuestGiftObjectiveMatcher.MeetsMinimumLikeLevel(
                    ReadParameter(giftCandidate.Parameters, "gift_taste"),
                    fields.MinimumLikeLevel))
            {
                reasons.Add("special_order_gift_minimum_like_level_not_met");
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            var matchingItem = inventory.HasValue &&
                inventory.Value.ValueKind == JsonValueKind.Array &&
                slotIndex.HasValue
                    ? inventory.Value.EnumerateArray().FirstOrDefault(item =>
                        item.ValueKind == JsonValueKind.Object &&
                        ReadInt(item, "slot_index") == slotIndex.Value &&
                        ReadBool(item, "is_empty") != true &&
                        string.Equals(ReadString(item, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase))
                    : default;
            if (matchingItem.ValueKind != JsonValueKind.Object ||
                !QuestContextTagMatcher.Matches(matchingItem, fields.AcceptableContextTagSets))
            {
                reasons.Add("special_order_gift_item_context_tags_drifted");
            }
        }

        private static void ValidateQuestIdentityAgainstSnapshot(
            SnapshotEnvelope snapshot,
            string family,
            string candidateId,
            string questId,
            string questKey,
            string runtimeType,
            int? objectiveIndex,
            int? expectedCurrent,
            int? expectedTarget,
            ICollection<string> reasons)
        {
            if (family == "ordinary_quest")
            {
                var state = ReadStateFieldValue(snapshot, "quests", "active_quests");
                var rows = state.HasValue && state.Value.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<QuestProgressRef[]>(state.Value.GetRawText()) ?? Array.Empty<QuestProgressRef>()
                    : Array.Empty<QuestProgressRef>();
                var candidate = QuestCandidateBuilder.BuildOrdinaryCandidates(rows)
                    .SingleOrDefault(row =>
                        string.Equals(row.CandidateId, candidateId, StringComparison.Ordinal) &&
                        string.Equals(row.QuestId, questId, StringComparison.Ordinal) &&
                        string.Equals(row.RuntimeType, runtimeType, StringComparison.Ordinal));
                if (candidate is null)
                {
                    reasons.Add("quest_live_identity_not_found");
                    return;
                }
                if (candidate.CurrentProgressCount != expectedCurrent) reasons.Add("quest_current_progress_drifted");
                if (candidate.RequiredTargetCount != expectedTarget) reasons.Add("quest_target_progress_drifted");
                return;
            }

            if (family == "special_order")
            {
                var state = ReadStateFieldValue(snapshot, "quests", "special_orders");
                var rows = state.HasValue && state.Value.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(state.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                    : Array.Empty<SpecialOrderProgressRef>();
                var candidate = QuestCandidateBuilder.BuildSpecialOrderCandidates(rows)
                    .SingleOrDefault(row =>
                        string.Equals(row.CandidateId, candidateId, StringComparison.Ordinal) &&
                        string.Equals(row.QuestKey, questKey, StringComparison.Ordinal) &&
                        string.Equals(row.RuntimeType, runtimeType, StringComparison.Ordinal));
                if (candidate is null)
                {
                    reasons.Add("special_order_live_identity_not_found");
                    return;
                }
                if (candidate.SelectedObjectiveIndex != objectiveIndex) reasons.Add("special_order_objective_index_drifted");
                if (candidate.CurrentProgressCount != expectedCurrent) reasons.Add("special_order_current_progress_drifted");
                if (candidate.RequiredTargetCount != expectedTarget) reasons.Add("special_order_target_progress_drifted");
            }
        }

        private static void ValidateQuestNpcGeometry(
            SnapshotEnvelope snapshot,
            string npcName,
            int? targetX,
            int? targetY,
            int? standX,
            int? standY,
            ICollection<string> reasons)
        {
            if (string.IsNullOrWhiteSpace(npcName) ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                !standX.HasValue ||
                !standY.HasValue)
            {
                return;
            }
            var candidate = SocialCandidateBuilder.Build(snapshot, "social.talk_npc", int.MaxValue)
                .FirstOrDefault(row =>
                    string.Equals(SocialCandidateBuilder.CandidateParameter(row, "npc_name"), npcName, StringComparison.OrdinalIgnoreCase));
            if (candidate is null ||
                !int.TryParse(SocialCandidateBuilder.CandidateParameter(candidate, "npc_tile_x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var liveNpcX) ||
                !int.TryParse(SocialCandidateBuilder.CandidateParameter(candidate, "npc_tile_y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var liveNpcY) ||
                !int.TryParse(SocialCandidateBuilder.CandidateParameter(candidate, "stand_tile_x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var liveStandX) ||
                !int.TryParse(SocialCandidateBuilder.CandidateParameter(candidate, "stand_tile_y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var liveStandY) ||
                liveNpcX != targetX.Value ||
                liveNpcY != targetY.Value ||
                liveStandX != standX.Value ||
                liveStandY != standY.Value)
            {
                reasons.Add("quest_npc_geometry_drifted");
            }
        }
    }
}
