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
    private EventCandidate[] SlimeBallCollectionCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var safeContext = ReadNativeObjectSafeItemContext(snapshot);
        var safeSlot = safeContext.AllowsEmpty ? safeContext.SafeSlotIndex : -1;
        var restoreSlot = safeContext.RestoreSlotIndex;
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("slime_ball_collection", out var collection) &&
                collection.ValueKind == JsonValueKind.Object)
            .Select(row => BuildSlimeBallCandidate(
                snapshot, row, row.GetProperty("slime_ball_collection"), locationId,
                playerX, playerY, safeSlot, restoreSlot))
            .OrderBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildSlimeBallCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement collection,
        string locationId,
        int playerX,
        int playerY,
        int safeSlot,
        int restoreSlot)
    {
        var reasons = new List<string>();
        if (!string.Equals(ReadString(collection, "status"), "ready", StringComparison.Ordinal))
            reasons.Add("slime_ball_not_ready:" + ReadString(collection, "status"));
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("slime_ball_menu_must_be_clear");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11)
            reasons.Add("slime_ball_empty_toolbar_slot_required");

        var targetX = ReadInt(row, "tile_x");
        var targetY = ReadInt(row, "tile_y");
        var stand = SelectNearestAvailableNativeObjectStand(collection, playerX, playerY);
        if (stand is null)
            reasons.Add("slime_ball_no_reachable_adjacent_stand");
        var parameters = SlimeBallCandidateParameters(
            row, collection, locationId, stand, safeSlot, restoreSlot);
        if (stand is not null && safeSlot >= 0 && restoreSlot >= 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "farming.collect_slime_ball",
                Parameters = parameters
            }));
        }

        var slimeQuantity = ReadInt(collection, "expected_slime_quantity");
        var petrifiedQuantity = ReadInt(collection, "expected_petrified_slime_quantity");
        return new EventCandidate
        {
            CandidateId = "slime-ball:" + locationId + ":" + targetX + "," + targetY +
                ":slime=" + slimeQuantity + ":petrified=" + petrifiedQuantity,
            Kind = "collect_slime_ball",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Collect Slime Ball",
            ExpectedEffect = "current_location.objects[" + targetX + "," + targetY + "].present=false" +
                ";conserved_output[(O)766]+=" + slimeQuantity +
                ";conserved_output[(O)557]+=" + petrifiedQuantity +
                ";fresh_snapshot_replan_required=true",
            EstimatedTicks = stand is null ? 90 : Math.Max(90, stand.Distance * 60 + 90),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_slime_ball_collection",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] SlimeBallCandidateParameters(
        JsonElement row,
        JsonElement collection,
        string locationId,
        NativeObjectStand? stand,
        int safeSlot,
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
            Parameter("target_runtime_type", ReadString(collection, "target_runtime_type")),
            Parameter("item_id", ReadString(collection, "canonical_item_id")),
            Parameter("qualified_item_id", ReadString(collection, "canonical_qualified_item_id")),
            Parameter("required_fragility", ReadInt(collection, "required_fragility").ToString()),
            Parameter("slime_ball_seed_days_played", ReadInt(collection, "day_seed_days_played").ToString()),
            Parameter("slime_ball_seed_unique_game_id", ReadInt64(collection, "day_seed_unique_game_id").ToString()),
            Parameter("slime_ball_expected_slime_quantity", ReadInt(collection, "expected_slime_quantity").ToString()),
            Parameter("slime_ball_expected_petrified_slime_quantity", ReadInt(collection, "expected_petrified_slime_quantity").ToString()),
            Parameter("slime_ball_expected_location_action_return", ReadBool(collection, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", ReadString(collection, "interaction_kind")),
            Parameter("expected_action_type", ReadString(collection, "expected_action_type")),
            Parameter("native_contract", ReadString(collection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static long ReadInt64(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var result)
            ? result
            : 0L;

}
