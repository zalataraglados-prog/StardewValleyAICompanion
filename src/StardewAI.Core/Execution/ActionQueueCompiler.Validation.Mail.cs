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
        private static string[] ValidateLetterViewerMenuPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
            if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(state.Value, "kind"), "letter_viewer", StringComparison.Ordinal))
            {
                return new[] { "letter_viewer_transparent_state_missing" };
            }

            var letter = state.Value;
            var reasons = new List<string>();
            if (ReadBool(letter, "is_mail") != true) reasons.Add("letter_viewer_is_not_mail");
            if (ReadBool(letter, "is_from_collection") == true) reasons.Add("mail_collection_view_not_processable");
            if (ReadBool(letter, "can_receive_input") != true) reasons.Add("letter_viewer_input_not_ready");
            if (ReadBool(letter, "ready_to_close") != true) reasons.Add("letter_viewer_not_ready_to_close");

            var title = ReadString(letter, "mail_title");
            if (string.IsNullOrWhiteSpace(title) ||
                !string.Equals(ReadParameter(action, "target_runtime_identity"), title, StringComparison.Ordinal))
            {
                reasons.Add("mail_runtime_identity_mismatch");
            }

            var identity = ReadString(letter, "menu_identity_sha256");
            if (string.IsNullOrWhiteSpace(identity) ||
                !string.Equals(ReadParameter(action, "mail_menu_identity_sha256"), identity, StringComparison.Ordinal))
            {
                reasons.Add("mail_menu_identity_mismatch");
            }

            var page = ReadInt(letter, "page", -1);
            var pageCount = ReadInt(letter, "page_count", -1);
            if (ReadIntParameter(action, "mail_expected_page") != page ||
                ReadIntParameter(action, "mail_expected_page_count") != pageCount ||
                page < 0 || pageCount < 1 || page >= pageCount)
            {
                reasons.Add("mail_page_projection_mismatch");
            }

            var attachments = letter.TryGetProperty("attachments", out var rows) && rows.ValueKind == JsonValueKind.Array
                ? rows.GetRawText()
                : "[]";
            if (!JsonEquivalent(ReadParameter(action, "expected_output_items_json"), attachments) ||
                ReadIntParameter(action, "mail_expected_attachment_count") != ReadInt(letter, "attachment_count"))
            {
                reasons.Add("mail_attachment_projection_mismatch");
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            var emptySlots = capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object
                ? ReadInt(capacity.Value, "empty_slots", -1)
                : -1;
            var requiredSlots = ReadIntParameter(action, "mail_attachment_slots_required");
            if (!requiredSlots.HasValue || emptySlots < requiredSlots.Value)
            {
                reasons.Add("mail_attachment_capacity_insufficient");
            }

            if (!string.Equals(ReadParameter(action, "quest_id") ?? string.Empty, ReadString(letter, "quest_id"), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "quest_key") ?? string.Empty, ReadString(letter, "special_order_id"), StringComparison.Ordinal))
            {
                reasons.Add("mail_quest_or_special_order_identity_mismatch");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool JsonEquivalent(string? left, string right)
        {
            try
            {
                using var leftDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(left) ? "[]" : left);
                using var rightDocument = JsonDocument.Parse(right);
                return string.Equals(leftDocument.RootElement.GetRawText(), rightDocument.RootElement.GetRawText(), StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
