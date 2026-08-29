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
        private static CompiledActionStep[] CompileReadSecretNoteStep(SmallModelAction action)
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
                    "read_secret_note",
                    "player.inventory[" + slot.Value + "]:" + qualifiedItemId + ":native_performUseAction",
                    "player.inventory[" + slot.Value + "].stack=" + ReadParameter(action, "secret_note_stack_after") +
                        ";secret_note_id=" + ReadParameter(action, "secret_note_selected_id") +
                        ";quest_id=" + ReadParameter(action, "secret_note_expected_quest_id") +
                        ";menu=StardewValley.Menus.LetterViewerMenu",
                    30)
            };
        }

        private static string[] ValidateReadSecretNotePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.read_secret_note")
            {
                return Array.Empty<string>();
            }

            var slot = ReadIntParameter(action, "slot_index");
            if (!slot.HasValue || slot.Value < 0)
            {
                return new[] { "read_secret_note_inventory_identity_required" };
            }
            var context = ReadStateFieldValue(snapshot, "player", "secret_note_candidates");
            if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return new[] { "read_secret_note_not_verified_by_transparent_player_state" };
            }
            JsonElement? candidate = null;
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "slot_index", -1) == slot.Value)
                {
                    candidate = row;
                    break;
                }
            }
            if (!candidate.HasValue)
            {
                return new[] { "read_secret_note_not_verified_by_transparent_player_state" };
            }

            var reasons = new List<string>();
            var note = candidate.Value;
            if (ReadBool(note, "available") != true)
            {
                reasons.Add("read_secret_note_native_use_gate_blocked");
            }
            CompareSecretNote(action, note, reasons, "item_id", "item_id");
            CompareSecretNote(action, note, reasons, "qualified_item_id", "qualified_item_id");
            CompareSecretNote(action, note, reasons, "secret_note_runtime_type", "runtime_type");
            CompareSecretNoteInt(action, note, reasons, "secret_note_stack_before", "stack_before");
            CompareSecretNoteInt(action, note, reasons, "secret_note_stack_after", "stack_after");
            CompareSecretNoteBool(action, note, reasons, "secret_note_is_journal", "is_journal");
            CompareSecretNoteInt(action, note, reasons, "secret_note_journal_index", "journal_index");
            CompareSecretNoteInt(action, note, reasons, "secret_note_total_count", "total_note_count");
            CompareSecretNote(action, note, reasons, "secret_note_unseen_ids_native_order_json", "unseen_note_ids_native_order_json");
            CompareSecretNoteInt(action, note, reasons, "secret_note_unseen_count", "unseen_note_count");
            CompareSecretNote(action, note, reasons, "secret_note_selection_kind", "selection_kind");
            CompareSecretNoteInt(action, note, reasons, "secret_note_selected_id", "selected_note_id");
            CompareSecretNote(action, note, reasons, "secret_note_content_sha256", "selected_note_content_sha256");
            CompareSecretNote(action, note, reasons, "secret_note_display_kind", "display_kind");
            CompareSecretNoteInt(action, note, reasons, "secret_note_expected_image", "expected_secret_note_image");
            CompareSecretNoteInt(action, note, reasons, "secret_note_expected_which_bg", "expected_which_bg");
            CompareSecretNote(action, note, reasons, "secret_note_expected_quest_id", "expected_quest_id");
            CompareSecretNoteBool(action, note, reasons, "secret_note_expected_quest_present_before", "expected_quest_present_before");
            CompareSecretNoteBool(action, note, reasons, "secret_note_expected_quest_present_after", "expected_quest_present_after");
            CompareSecretNote(action, context.Value, reasons, "secret_note_projection_fingerprint", "projection_fingerprint");
            CompareSecretNote(action, note, reasons, "secret_note_native_contract", "native_contract");

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static void CompareSecretNote(SmallModelAction action, JsonElement row, List<string> reasons, string parameter, string field)
        {
            if (!string.Equals(ReadParameter(action, parameter), ReadString(row, field), StringComparison.Ordinal))
            {
                reasons.Add("read_secret_note_projection_drifted:" + parameter);
            }
        }

        private static void CompareSecretNoteInt(SmallModelAction action, JsonElement row, List<string> reasons, string parameter, string field)
        {
            if (ReadIntParameter(action, parameter) != ReadInt(row, field))
            {
                reasons.Add("read_secret_note_projection_drifted:" + parameter);
            }
        }

        private static void CompareSecretNoteBool(SmallModelAction action, JsonElement row, List<string> reasons, string parameter, string field)
        {
            var expected = ReadBool(row, field) == true ? "true" : "false";
            if (!string.Equals(ReadParameter(action, parameter), expected, StringComparison.Ordinal))
            {
                reasons.Add("read_secret_note_projection_drifted:" + parameter);
            }
        }
    }
}
