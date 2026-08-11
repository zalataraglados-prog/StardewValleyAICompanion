using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Mail;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] MailProcessingCandidates(SnapshotEnvelope snapshot)
    {
        var activeMenuType = ActiveMenuTypeForCandidate(snapshot);
        if (string.Equals(activeMenuType, "LetterViewerMenu", StringComparison.Ordinal))
            return OpenLetterCandidates(snapshot);
        if (ActiveMenuOpenForCandidate(snapshot))
            return new[] { BlockedMailCandidate("mail_processing_other_menu_open") };

        var state = ReadStateFieldValue(snapshot, "quests", "mailbox_processing");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object)
            return new[] { BlockedMailCandidate("mailbox_processing_transparent_state_missing") };

        var mailbox = state.Value;
        if (ReadInt(mailbox, "queue_count") == 0)
            return Array.Empty<EventCandidate>();

        var reasons = ReadMailStringArray(mailbox, "blocked_diagnostics").ToList();
        var mailId = ReadString(mailbox, "pending_mail_id");
        var mailHash = ReadString(mailbox, "mail_data_sha256");
        var targetLocation = ReadString(mailbox, "mailbox_location_id");
        var actionX = ReadNullableInt(mailbox, "mailbox_action_tile_x");
        var actionY = ReadNullableInt(mailbox, "mailbox_action_tile_y");
        var standX = ReadNullableInt(mailbox, "stand_tile_x");
        var standY = ReadNullableInt(mailbox, "stand_tile_y");
        if (string.IsNullOrWhiteSpace(mailId)) reasons.Add("pending_mail_identity_missing");
        if (string.IsNullOrWhiteSpace(targetLocation) || !actionX.HasValue || !actionY.HasValue || !standX.HasValue || !standY.HasValue)
            reasons.Add("owned_mailbox_endpoint_incomplete");

        var identity = new[]
        {
            Parameter("target_runtime_type", "Mailbox"),
            Parameter("target_runtime_identity", mailId),
            Parameter("mail_data_sha256", mailHash),
            Parameter("mail_attachment_slot_upper_bound", ReadInt(mailbox, "attachment_slot_upper_bound").ToString()),
            Parameter("mail_constructor_effect_classes_json", mailbox.TryGetProperty("constructor_effect_classes", out var effects) ? effects.GetRawText() : "[]")
        };
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
        {
            var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation, RouteConnectorCandidates(snapshot))?.FirstConnectorCandidate;
            if (route is null)
            {
                reasons.Add("owned_mailbox_route_unavailable");
                return new[] { BlockedMailCandidate(reasons.ToArray()) };
            }
            reasons.AddRange(route.BlockReasons);
            var routeReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = $"mail.process_letter:route:{mailId}:{currentLocation}:{route.TileX},{route.TileY}",
                    Kind = "route_connector_tile",
                    Available = route.Available && routeReasons.Length == 0,
                    LocationId = currentLocation,
                    TileX = route.TileX,
                    TileY = route.TileY,
                    ExpectedEffect = $"mailbox_route_target={targetLocation};one_connector_then_fresh_snapshot=true",
                    EstimatedTicks = route.EstimatedTicks,
                    EnergyCost = 0,
                    AvailabilityClass = "mailbox_cross_map_route_step",
                    AllowedNow = route.AllowedNow,
                    AllowedToday = route.AllowedToday,
                    NextOpenTime = route.NextOpenTime,
                    EffectiveOpenTime = route.EffectiveOpenTime,
                    ClosesAt = route.ClosesAt,
                    WaitCost = route.WaitCost,
                    GateReasons = route.GateReasons,
                    BlockReasons = routeReasons,
                    Parameters = route.Parameters
                        .Concat(new[]
                        {
                            Parameter("continuation.option_id", "mail.process_letter"),
                            Parameter("continuation.target_location", targetLocation)
                        })
                        .Concat(identity)
                        .ToArray()
                }
            };
        }

        var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
        if (playerX != standX || playerY != standY)
        {
            var approachReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = $"mail.process_letter:approach:{mailId}",
                    Kind = "mailbox_approach",
                    Available = approachReasons.Length == 0,
                    LocationId = targetLocation,
                    TileX = standX,
                    TileY = standY,
                    DisplayName = mailId,
                    ExpectedEffect = "player_at_owned_mailbox_stand_tile=true;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 180,
                    EnergyCost = 0,
                    AvailabilityClass = "mailbox_approach",
                    BlockReasons = approachReasons,
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", standX?.ToString() ?? string.Empty),
                        Parameter("target_tile_y", standY?.ToString() ?? string.Empty),
                        Parameter("mailbox_action_tile_x", actionX?.ToString() ?? string.Empty),
                        Parameter("mailbox_action_tile_y", actionY?.ToString() ?? string.Empty),
                        Parameter("max_movement_tiles", "128")
                    }.Concat(identity).ToArray()
                }
            };
        }

        var terminalReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = $"mail.process_letter:open:{mailId}",
                Kind = "open_mailbox_letter",
                Available = terminalReasons.Length == 0,
                LocationId = targetLocation,
                TileX = actionX,
                TileY = actionY,
                DisplayName = mailId,
                ExpectedEffect = $"mailbox_first_removed={mailId};native_letter_constructor_effects_applied=true;fresh_snapshot_replan_required=true",
                EstimatedTicks = 60,
                EnergyCost = 0,
                AvailabilityClass = "native_mailbox_letter_ready",
                BlockReasons = terminalReasons,
                Parameters = new[]
                {
                    Parameter("target_tile_x", actionX?.ToString() ?? string.Empty),
                    Parameter("target_tile_y", actionY?.ToString() ?? string.Empty),
                    Parameter("stand_tile_x", standX?.ToString() ?? string.Empty),
                    Parameter("stand_tile_y", standY?.ToString() ?? string.Empty),
                    Parameter("expected_action_type", "Mailbox"),
                    Parameter("interaction_kind", "map_action")
                }.Concat(identity).ToArray()
            }
        };
    }

    private static EventCandidate[] OpenLetterCandidates(SnapshotEnvelope snapshot)
    {
        var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object)
            return new[] { BlockedMailCandidate("letter_viewer_transparent_state_missing") };

        var letter = state.Value;
        var reasons = new List<string>();
        if (!string.Equals(ReadString(letter, "kind"), "letter_viewer", StringComparison.Ordinal)) reasons.Add("letter_viewer_state_kind_mismatch");
        if (ReadBool(letter, "is_mail") != true) reasons.Add("letter_viewer_is_not_mail");
        if (ReadBool(letter, "is_from_collection") == true) reasons.Add("mail_collection_view_not_processable");
        if (ReadBool(letter, "can_receive_input") != true) reasons.Add("letter_viewer_input_not_ready");
        if (ReadBool(letter, "ready_to_close") != true) reasons.Add("letter_viewer_not_ready_to_close");
        var title = ReadString(letter, "mail_title");
        var identity = ReadString(letter, "menu_identity_sha256");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(identity)) reasons.Add("letter_viewer_identity_incomplete");
        var attachments = letter.TryGetProperty("attachments", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.GetRawText()
            : "[]";
        var attachmentCount = ReadInt(letter, "attachment_count");
        var attachmentSlotsRequired = letter.TryGetProperty("attachments", out var attachmentRows) && attachmentRows.ValueKind == JsonValueKind.Array
            ? attachmentRows.EnumerateArray().Count(row =>
                ReadBool(row, "present") == true &&
                MailDirectiveParser.AttachmentRequiresInventorySlot(ReadString(row, "qualified_item_id")))
            : attachmentCount;
        var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
        var emptySlots = capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object
            ? ReadInt(capacity.Value, "empty_slots")
            : -1;
        if (emptySlots < 0) reasons.Add("player_inventory_capacity_missing");
        if (attachmentSlotsRequired > emptySlots) reasons.Add("mail_attachment_capacity_insufficient_after_open");
        var page = ReadInt(letter, "page", -1);
        var pageCount = ReadInt(letter, "page_count", -1);
        if (page < 0 || pageCount < 1 || page >= pageCount) reasons.Add("letter_viewer_page_state_invalid");

        return new[]
        {
            new EventCandidate
            {
                CandidateId = $"mail.process_letter:menu:{title}:{identity}",
                Kind = "process_open_letter",
                Available = reasons.Count == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                DisplayName = title,
                ExpectedEffect = $"letter_completed={title};attachments_collected={attachmentCount};quest_or_special_order_accepted_if_present=true;menus.active_menu.is_open=false",
                EstimatedTicks = Math.Max(20, pageCount * 10 + attachmentCount * 10),
                EnergyCost = 0,
                AvailabilityClass = reasons.Count == 0 ? "native_letter_viewer_ready" : "native_letter_viewer_blocked",
                BlockReasons = reasons.ToArray(),
                Parameters = new[]
                {
                    Parameter("target_runtime_type", "LetterViewerMenu"),
                    Parameter("target_runtime_identity", title),
                    Parameter("mail_menu_identity_sha256", identity),
                    Parameter("mail_expected_page", page.ToString()),
                    Parameter("mail_expected_page_count", pageCount.ToString()),
                    Parameter("mail_expected_attachment_count", attachmentCount.ToString()),
                    Parameter("mail_attachment_slots_required", attachmentSlotsRequired.ToString()),
                    Parameter("expected_output_items_json", attachments),
                    Parameter("quest_id", ReadString(letter, "quest_id")),
                    Parameter("quest_key", ReadString(letter, "special_order_id"))
                }
            }
        };
    }

    private static EventCandidate BlockedMailCandidate(params string[] reasons) => new()
    {
        CandidateId = "mail.process_letter:blocked",
        Kind = "process_open_letter",
        Available = false,
        ExpectedEffect = "mail_not_processed",
        AvailabilityClass = "mail_processing_blocked",
        BlockReasons = reasons.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray()
    };

    private static string[] ReadMailStringArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.String).Select(row => row.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

}
