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
    private EventCandidate[] FeedHopperWithdrawalCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var safeContext = ReadNativeObjectSafeItemContext(snapshot);
        var safeKind = safeContext.SafeSlotKind;
        var safeSlot = safeContext.AllowsEmptyOrTool ? safeContext.SafeSlotIndex : -1;
        var restoreSlot = safeContext.RestoreSlotIndex;
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("feed_hopper_withdrawal", out var projection) &&
                projection.ValueKind == JsonValueKind.Object)
            .Select(row => BuildFeedHopperCandidate(
                snapshot, row, row.GetProperty("feed_hopper_withdrawal"), locationId,
                playerX, playerY, safeSlot, safeKind, restoreSlot))
            .OrderBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildFeedHopperCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement projection,
        string locationId,
        int playerX,
        int playerY,
        int safeSlot,
        string safeKind,
        int restoreSlot)
    {
        var reasons = new List<string>();
        if (!string.Equals(ReadString(projection, "status"), "ready", StringComparison.Ordinal))
            reasons.Add("feed_hopper_not_ready:" + ReadString(projection, "status"));
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("feed_hopper_menu_must_be_clear");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11 ||
            (safeKind != "empty" && safeKind != "tool"))
        {
            reasons.Add("feed_hopper_safe_toolbar_slot_required");
        }

        var targetX = ReadInt(row, "tile_x");
        var targetY = ReadInt(row, "tile_y");
        var stand = SelectNearestAvailableNativeObjectStand(projection, playerX, playerY);
        if (stand is null)
            reasons.Add("feed_hopper_no_reachable_adjacent_stand");
        var parameters = FeedHopperCandidateParameters(
            row, projection, locationId, stand, safeSlot, safeKind, restoreSlot);
        if (stand is not null && safeSlot >= 0 && restoreSlot >= 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "animals.withdraw_feed_hopper_hay",
                Parameters = parameters
            }));
        }

        var quantity = ReadInt(projection, "expected_withdrawal_quantity");
        return new EventCandidate
        {
            CandidateId = "feed-hopper:" + locationId + ":" + targetX + "," + targetY + ":hay=" + quantity,
            Kind = "withdraw_feed_hopper_hay",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Withdraw Feed Hopper Hay",
            ExpectedEffect = "root_location.pieces_of_hay-=" + quantity +
                ";player.inventory[(O)178]+=" + quantity +
                ";feed_hopper_identity_unchanged=true;fresh_snapshot_replan_required=true",
            EstimatedTicks = stand is null ? 90 : Math.Max(90, stand.Distance * 60 + 90),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_feed_hopper_withdrawal",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] FeedHopperCandidateParameters(
        JsonElement row,
        JsonElement projection,
        string locationId,
        NativeObjectStand? stand,
        int safeSlot,
        string safeKind,
        int restoreSlot)
    {
        if (stand is null || safeSlot < 0 || restoreSlot < 0)
            return Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(row, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(projection, "target_runtime_type")),
            Parameter("item_id", ReadString(projection, "canonical_item_id")),
            Parameter("qualified_item_id", ReadString(projection, "canonical_qualified_item_id")),
            Parameter("feed_hopper_hay_qualified_item_id", ReadString(projection, "hay_qualified_item_id")),
            Parameter("feed_hopper_root_location_id", ReadString(projection, "root_location_id")),
            Parameter("feed_hopper_silo_hay_before", ReadInt(projection, "silo_hay_before").ToString()),
            Parameter("feed_hopper_animal_count", ReadInt(projection, "animal_count").ToString()),
            Parameter("feed_hopper_animal_limit", ReadInt(projection, "animal_limit").ToString()),
            Parameter("feed_hopper_placed_hay_count", ReadInt(projection, "placed_hay_count").ToString()),
            Parameter("feed_hopper_unfed_animal_count", ReadInt(projection, "unfed_animal_count").ToString()),
            Parameter("feed_hopper_expected_withdrawal_quantity", ReadInt(projection, "expected_withdrawal_quantity").ToString()),
            Parameter("feed_hopper_expected_silo_hay_after", ReadInt(projection, "expected_silo_hay_after").ToString()),
            Parameter("feed_hopper_expected_location_action_return", ReadBool(projection, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", ReadString(projection, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection, "expected_action_type")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

}
