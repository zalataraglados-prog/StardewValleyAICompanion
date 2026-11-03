using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed partial class GrandpaDirectionDailyCandidateBinding
{
    private static bool HasRustyKeyEvidence(
        PolicyEventCandidatePrediction candidate,
        SnapshotEnvelope snapshot,
        out string rejectionReason)
    {
        if (candidate.OptionId == "museum.donate_items" &&
            candidate.Kind is "donate_museum_item" or "route_connector_tile")
        {
            if (!HasRustyKeyDonationEvidence(candidate, out rejectionReason))
                return false;
            if (candidate.Kind == "donate_museum_item")
                return true;

            var museum = ReadStateFieldValue(snapshot, "world_progress", "museum");
            if (!museum.HasValue || museum.Value.ValueKind != JsonValueKind.Object ||
                !ReadableStatus(ReadStateFieldStatus(snapshot, "world_progress", "museum")) ||
                !TryReadUniqueIntParameter(candidate, "expected_donated_count_before", out var before) ||
                before != ReadInt(museum.Value, "donated_count", -1) ||
                !TryReadUniqueIntParameter(candidate, "rusty_key_donation_threshold", out var donationThreshold) ||
                donationThreshold != ReadInt(museum.Value, "rusty_key_donation_threshold", -1) ||
                !MatchesRustyKeyParameter(candidate, "continuation.option_id", "museum.donate_items") ||
                !MatchesRustyKeyParameter(candidate, "continuation.target_location",
                    ReadString(museum.Value, "museum_location_id")) ||
                !TryReadUniqueIntParameter(candidate, "continuation.inventory_slot_index", out var slot) ||
                slot != candidate.SlotIndex ||
                !MatchesRustyKeyParameter(candidate, "continuation.qualified_item_id", candidate.QualifiedItemId) ||
                !museum.Value.TryGetProperty("donation_candidates", out var donations) ||
                donations.ValueKind != JsonValueKind.Array ||
                !donations.EnumerateArray().Any(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "slot_index", -1) == slot &&
                    ReadString(row, "qualified_item_id") == candidate.QualifiedItemId &&
                    ReadString(row, "action_status") == "museum_not_current_location" &&
                    ReadString(row, "reward_projection_status") == "ready"))
            {
                rejectionReason = "rusty_key_donation_route_identity_mismatch";
                return false;
            }

            return true;
        }

        rejectionReason = "rusty_key_native_key_event_not_attributable";
        if (candidate.OptionId != "story.advance_event" ||
            candidate.Kind != "advance_story_event_automatic")
            return false;

        var museumProgress = ReadStateFieldValue(snapshot, "world_progress", "museum");
        var story = ReadStateFieldValue(snapshot, "player", "story_event");
        if (!museumProgress.HasValue || museumProgress.Value.ValueKind != JsonValueKind.Object ||
            !story.HasValue || story.Value.ValueKind != JsonValueKind.Object ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "world_progress", "museum")) ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "player", "story_event")) ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "player", "has_rusty_key")))
            return false;

        var progress = museumProgress.Value;
        var rowEvent = story.Value;
        var threshold = ReadInt(progress, "rusty_key_donation_threshold");
        // museum60 marks the prerequisite; only native Farm event 66 delivers the key.
        if (threshold != 60 || ReadInt(progress, "donated_count", -1) < threshold ||
            ReadString(progress, "rusty_key_reward_id") != "museum60" ||
            ReadString(progress, "rusty_key_reward_action") != "MarkEventSeen Host 295672" ||
            ReadBool(progress, "rusty_key_reward_claimed") != true ||
            ReadBool(progress, "rusty_key_prerequisite_event_seen") != true ||
            !RustyKeyExplicitFalse(progress, "rusty_key_event_seen") ||
            !RustyKeyExplicitFalse(progress, "has_rusty_key") ||
            ReadStateFieldValue(snapshot, "player", "has_rusty_key")?.ValueKind != JsonValueKind.False ||
            ReadString(rowEvent, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadBool(rowEvent, "active") != true || ReadBool(rowEvent, "event_up") != true ||
            !RustyKeyExplicitFalse(rowEvent, "is_festival") ||
            !RustyKeyExplicitFalse(rowEvent, "skipped") ||
            !RustyKeyExplicitFalse(rowEvent, "player_control_sequence") ||
            !string.IsNullOrEmpty(ReadString(rowEvent, "active_minigame_type")) ||
            ReadString(rowEvent, "event_id") != "66" ||
            ReadString(rowEvent, "from_asset_name").Replace('\\', '/') != "Data/Events/Farm" ||
            ReadString(rowEvent, "location_id") != "Farm" || candidate.LocationId != "Farm" ||
            ReadString(rowEvent, "boundary_kind") != "automatic_progress" ||
            !MatchesRustyKeyParameter(candidate, "story_event_id", "66") ||
            !MatchesRustyKeyParameter(candidate, "story_event_location_id", "Farm") ||
            !MatchesRustyKeyParameter(candidate, "story_event_projection_fingerprint",
                ReadString(rowEvent, "projection_fingerprint")))
            return false;

        rejectionReason = string.Empty;
        return true;
    }

    private static bool MatchesRustyKeyParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        string expected) => !string.IsNullOrWhiteSpace(expected) &&
        TryReadUniqueParameter(candidate, name, out var value) && value == expected;

    private static bool RustyKeyExplicitFalse(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.False;
}
