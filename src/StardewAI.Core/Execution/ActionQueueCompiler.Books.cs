using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileReadBookStep(SmallModelAction action)
        {
            var slot = ReadIntParameter(action, "slot_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            if (!slot.HasValue || string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "read_book",
                    "player.inventory[" + slot.Value + "]:" + qualifiedItemId + ":native_performUseAction",
                    "player.inventory[" + slot.Value + "].stack=" + ReadParameter(action, "book_stack_after") +
                        ";skill_experience_deltas_json=" + ReadParameter(action, "expected_skill_experience_deltas_json") +
                        ";mastery_experience_delta=" + ReadParameter(action, "expected_mastery_experience_delta") +
                        ";book_stat_after=" + ReadParameter(action, "book_stat_after") +
                        ";cooking_recipes_added_json=" + ReadParameter(action, "cooking_recipes_added_json"),
                    90)
            };
        }

        private static string[] ValidateReadBookPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.read_book")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var slot = ReadIntParameter(action, "slot_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            if (!slot.HasValue || slot.Value < 0 || string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return new[] { "read_book_inventory_identity_required" };
            }

            var candidate = BookCandidateAt(snapshot, slot.Value);
            if (!candidate.HasValue)
            {
                return new[] { "read_book_not_verified_by_transparent_player_state" };
            }
            var book = candidate.Value;
            if (ReadBool(book, "available") != true)
            {
                reasons.Add("read_book_native_use_gate_blocked");
            }

            CompareBookParameter(action, book, reasons, "qualified_item_id", "qualified_item_id");
            CompareBookParameter(action, book, reasons, "item_id", "item_id");
            CompareBookParameter(action, book, reasons, "book_runtime_type", "runtime_type");
            CompareBookIntParameter(action, book, reasons, "book_category", "category");
            CompareBookIntParameter(action, book, reasons, "book_stack_before", "stack_before");
            CompareBookIntParameter(action, book, reasons, "book_stack_after", "stack_after");
            CompareBookParameter(action, book, reasons, "book_native_branch", "native_branch");
            CompareBookParameter(action, book, reasons, "book_native_branch_status", "native_branch_status");
            CompareBookParameter(action, book, reasons, "book_context_tags_native_order_json", "context_tags_native_order_json");
            CompareBookParameter(action, book, reasons, "book_matched_experience_tag", "matched_book_experience_tag");
            CompareBookParameter(action, book, reasons, "expected_skill_experience_deltas_json", "experience_deltas_json");
            CompareBookParameter(action, book, reasons, "book_skill_level_deltas_json", "skill_level_deltas_json");
            CompareBookParameter(action, book, reasons, "book_new_levels_before_json", "new_levels_before_json");
            CompareBookParameter(action, book, reasons, "book_new_levels_after_json", "new_levels_after_json");
            CompareBookParameter(action, book, reasons, "book_native_feedback_callbacks", "native_feedback_callbacks");
            CompareBookIntParameter(action, book, reasons, "expected_mastery_experience_delta", "mastery_experience_delta");
            CompareBookParameter(action, book, reasons, "skill_experience_projection_status", "experience_projection_status");
            CompareBookParameter(action, book, reasons, "book_stat_key", "book_stat_key");
            CompareBookNullableNumberParameter(action, book, reasons, "book_stat_before", "book_stat_before");
            CompareBookNullableNumberParameter(action, book, reasons, "book_stat_after", "book_stat_after");
            CompareBookBoolParameter(action, book, reasons, "read_a_book_mail_before", "read_a_book_mail_before");
            CompareBookBoolParameter(action, book, reasons, "read_a_book_mail_after", "read_a_book_mail_after");
            CompareBookBoolParameter(action, book, reasons, "well_read_achievement_before", "well_read_achievement_before");
            CompareBookBoolParameter(action, book, reasons, "well_read_achievement_after", "well_read_achievement_after");
            CompareBookBoolParameter(action, book, reasons, "well_read_achievement_will_unlock", "well_read_achievement_will_unlock");
            CompareBookBoolParameter(action, book, reasons, "well_read_hatter_mail_before", "well_read_hatter_mail_before");
            CompareBookBoolParameter(action, book, reasons, "well_read_hatter_mail_after", "well_read_hatter_mail_after");
            CompareBookBoolParameter(action, book, reasons, "well_read_dialogue_event_seen_before", "well_read_dialogue_event_seen_before");
            CompareBookBoolParameter(action, book, reasons, "well_read_dialogue_event_seen_after", "well_read_dialogue_event_seen_after");
            CompareBookParameter(action, book, reasons, "well_read_ui_sound_platform_callbacks", "well_read_ui_sound_platform_callbacks");
            CompareBookParameter(action, book, reasons, "cooking_recipes_added_json", "cooking_recipes_added_json");
            CompareBookIntParameter(action, book, reasons, "cooking_recipes_added_count", "cooking_recipes_added_count");
            if (!string.Equals(ReadParameter(action, "skill_experience_condition"), "native_book_read", StringComparison.Ordinal))
            {
                reasons.Add("read_book_experience_condition_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? BookCandidateAt(SnapshotEnvelope snapshot, int slotIndex)
        {
            var candidates = ReadStateFieldValue(snapshot, "player", "book_candidates");
            if (!candidates.HasValue || candidates.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var candidate in candidates.Value.EnumerateArray())
            {
                if (candidate.ValueKind == JsonValueKind.Object && ReadInt(candidate, "slot_index", -1) == slotIndex)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static void CompareBookParameter(SmallModelAction action, JsonElement book, List<string> reasons, string parameter, string field)
        {
            if (!string.Equals(ReadParameter(action, parameter), ReadString(book, field), StringComparison.Ordinal))
            {
                reasons.Add("read_book_projection_drifted:" + parameter);
            }
        }

        private static void CompareBookIntParameter(SmallModelAction action, JsonElement book, List<string> reasons, string parameter, string field)
        {
            if (ReadIntParameter(action, parameter) != ReadInt(book, field))
            {
                reasons.Add("read_book_projection_drifted:" + parameter);
            }
        }

        private static void CompareBookNullableNumberParameter(SmallModelAction action, JsonElement book, List<string> reasons, string parameter, string field)
        {
            var expected = string.Empty;
            if (book.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number))
            {
                expected = number.ToString();
            }
            var actual = ReadParameter(action, parameter);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                reasons.Add("read_book_projection_drifted:" + parameter);
            }
        }

        private static void CompareBookBoolParameter(SmallModelAction action, JsonElement book, List<string> reasons, string parameter, string field)
        {
            var expected = ReadBool(book, field) == true ? "true" : "false";
            if (!string.Equals(ReadParameter(action, parameter), expected, StringComparison.Ordinal))
            {
                reasons.Add("read_book_projection_drifted:" + parameter);
            }
        }
    }
}
