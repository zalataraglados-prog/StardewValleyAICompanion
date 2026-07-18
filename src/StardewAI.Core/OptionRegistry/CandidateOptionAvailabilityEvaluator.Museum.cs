using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] MuseumDonationCandidates(SnapshotEnvelope snapshot)
    {
        var museum = ReadStateFieldValue(snapshot, "world_progress", "museum");
        if (!museum.HasValue || museum.Value.ValueKind != JsonValueKind.Object ||
            !museum.Value.TryGetProperty("donation_candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var museumRow = museum.Value;
        var locationId = ReadString(museumRow, "museum_location_id");
        var actionX = NullableReadInt(museumRow, "gunther_action_tile_x");
        var actionY = NullableReadInt(museumRow, "gunther_action_tile_y");
        var donationX = NullableReadInt(museumRow, "free_donation_tile_x");
        var donationY = NullableReadInt(museumRow, "free_donation_tile_y");
        var stand = actionX.HasValue && actionY.HasValue
            ? FindBestStandTile(snapshot, actionX.Value, actionY.Value)
            : null;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return candidates.EnumerateArray()
            .Where(candidate => candidate.ValueKind == JsonValueKind.Object)
            .Select(candidate =>
            {
                var reasons = new List<string>();
                var status = ReadString(candidate, "action_status");
                if (!string.Equals(status, "ready", StringComparison.Ordinal))
                {
                    reasons.Add(string.IsNullOrWhiteSpace(status) ? "museum_donation_projection_unavailable" : status);
                }
                if (!actionX.HasValue || !actionY.HasValue)
                {
                    reasons.Add("gunther_action_tile_unavailable");
                }
                if (!donationX.HasValue || !donationY.HasValue)
                {
                    reasons.Add("museum_free_donation_tile_unavailable");
                }
                if (stand is null)
                {
                    reasons.Add("museum_no_reachable_counter_stand_tile");
                }

                var slot = ReadInt(candidate, "slot_index");
                var before = ReadInt(candidate, "donated_count_before");
                var after = ReadInt(candidate, "donated_count_after");
                if (slot < 0 || after != before + 1)
                {
                    reasons.Add("museum_donation_candidate_typed_projection_invalid");
                }
                var parameters = stand is null || !actionX.HasValue || !actionY.HasValue || !donationX.HasValue || !donationY.HasValue
                    ? Array.Empty<SmallModelActionParameter>()
                    : MuseumDonationParameters(museumRow, candidate, stand.X, stand.Y, actionX.Value, actionY.Value, donationX.Value, donationY.Value);
                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);

                return new EventCandidate
                {
                    CandidateId = "museum-donate:" + slot + ":" + ReadString(candidate, "qualified_item_id"),
                    Kind = "donate_museum_item",
                    Available = reasons.Count == 0,
                    LocationId = locationId,
                    TileX = actionX,
                    TileY = actionY,
                    ExpectedEffect = MuseumDonationExpectedEffect(museumRow, candidate),
                    ItemId = ReadString(candidate, "item_id"),
                    QualifiedItemId = ReadString(candidate, "qualified_item_id"),
                    SlotIndex = slot,
                    Quantity = 1,
                    EstimatedTicks = Math.Max(240, distance * 60 + 240),
                    AvailabilityClass = "transparent_native_museum_donation",
                    AllowedNow = reasons.Count == 0,
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }

    private static SmallModelActionParameter[] MuseumDonationParameters(
        JsonElement museum,
        JsonElement candidate,
        int standX,
        int standY,
        int actionX,
        int actionY,
        int donationX,
        int donationY)
    {
        return new[]
        {
            Parameter("target_location", ReadString(museum, "museum_location_id")),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("gunther_action_tile_x", actionX.ToString()),
            Parameter("gunther_action_tile_y", actionY.ToString()),
            Parameter("gunther_action_raw", ReadString(museum, "gunther_action_raw")),
            Parameter("donation_tile_x", donationX.ToString()),
            Parameter("donation_tile_y", donationY.ToString()),
            Parameter("inventory_slot_index", ReadInt(candidate, "slot_index").ToString()),
            Parameter("item_id", ReadString(candidate, "item_id")),
            Parameter("qualified_item_id", ReadString(candidate, "qualified_item_id")),
            Parameter("target_runtime_type", ReadString(candidate, "runtime_type")),
            Parameter("expected_stack_before", ReadInt(candidate, "stack_before").ToString()),
            Parameter("expected_stack_after", ReadInt(candidate, "stack_after").ToString()),
            Parameter("expected_donated_count_before", ReadInt(candidate, "donated_count_before").ToString()),
            Parameter("expected_donated_count_after", ReadInt(candidate, "donated_count_after").ToString()),
            Parameter("museum_total_donatable_items", ReadInt(museum, "total_donatable_items").ToString()),
            Parameter("expected_collection_complete_after", ReadBool(candidate, "completes_collection") == true ? "true" : "false"),
            Parameter("rusty_key_donation_threshold", ReadInt(museum, "rusty_key_donation_threshold").ToString()),
            Parameter("reaches_rusty_key_threshold", ReadBool(candidate, "reaches_rusty_key_threshold") == true ? "true" : "false"),
            Parameter("rusty_key_reward_id", ReadString(museum, "rusty_key_reward_id")),
            Parameter("rusty_key_reward_action", ReadString(museum, "rusty_key_reward_action")),
            Parameter("native_contract", "LibraryMuseum.OpenDonationMenu_then_MuseumMenu.receiveLeftClick_inventory_and_display_then_Game1.exitActiveMenu")
        };
    }

    private static string MuseumDonationExpectedEffect(JsonElement museum, JsonElement candidate)
    {
        return "museum_donated_count=" + ReadInt(candidate, "donated_count_after") +
            ";inventory_slot=" + ReadInt(candidate, "slot_index") + ":stack=" + ReadInt(candidate, "stack_after") +
            ";collection_complete=" + (ReadBool(candidate, "completes_collection") == true ? "true" : "false") +
            ";rusty_key_threshold=" + ReadInt(museum, "rusty_key_donation_threshold") +
            ";reaches_rusty_key_threshold=" + (ReadBool(candidate, "reaches_rusty_key_threshold") == true ? "true" : "false");
    }
}
