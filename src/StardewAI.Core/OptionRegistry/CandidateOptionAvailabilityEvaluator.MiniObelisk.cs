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
    private EventCandidate[] MiniObeliskCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var safeContext = ReadNativeObjectSafeItemContext(snapshot);
        var safeSlot = safeContext.AllowsEmptyOrTool ? safeContext.SafeSlotIndex : -1;
        var safeKind = safeContext.SafeSlotKind;
        var restoreSlot = safeContext.RestoreSlotIndex;
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("mini_obelisk_use", out var projection) &&
                projection.ValueKind == JsonValueKind.Object)
            .Select(row => BuildMiniObeliskCandidate(
                snapshot,
                row,
                row.GetProperty("mini_obelisk_use"),
                locationId,
                playerX,
                playerY,
                safeSlot,
                safeKind,
                restoreSlot))
            .OrderBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildMiniObeliskCandidate(
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
        var status = ReadString(projection, "status");
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
            reasons.Add("mini_obelisk_not_ready:" + status);
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("mini_obelisk_menu_must_be_clear");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11 ||
            safeKind is not ("empty" or "tool"))
        {
            reasons.Add("mini_obelisk_safe_toolbar_slot_required");
        }

        var sourceX = ReadInt(row, "tile_x");
        var sourceY = ReadInt(row, "tile_y");
        var stand = SelectNearestAvailableNativeObjectStand(projection, playerX, playerY);
        if (stand is null)
            reasons.Add("mini_obelisk_no_safe_source_stand_or_native_landing");
        var parameters = MiniObeliskCandidateParameters(
            row, projection, locationId, stand, safeSlot, safeKind, restoreSlot);
        if (stand is not null && safeSlot >= 0 && restoreSlot >= 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "movement.use_mini_obelisk",
                Parameters = parameters
            }));
        }

        var destinationX = stand is null ? -1 : ReadInt(stand.Projection, "native_destination_tile_x");
        var destinationY = stand is null ? -1 : ReadInt(stand.Projection, "native_destination_tile_y");
        var landingX = stand is null ? -1 : ReadInt(stand.Projection, "native_landing_tile_x");
        var landingY = stand is null ? -1 : ReadInt(stand.Projection, "native_landing_tile_y");
        return new EventCandidate
        {
            CandidateId = "mini-obelisk:" + locationId + ":" + sourceX + "," + sourceY +
                "->" + landingX + "," + landingY,
            Kind = "use_mini_obelisk",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = sourceX,
            TileY = sourceY,
            DisplayName = "Use Mini-Obelisk",
            ExpectedEffect = "player.location_id=" + locationId +
                ";native_destination_obelisk=" + destinationX + "," + destinationY +
                ";player.tile=" + landingX + "," + landingY +
                ";native_pair_identity_unchanged=true;fresh_snapshot_replan_required=true",
            EstimatedTicks = stand is null ? 120 : Math.Max(120, stand.Distance * 60 + 120),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_mini_obelisk_route_primitive",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] MiniObeliskCandidateParameters(
        JsonElement row,
        JsonElement projection,
        string locationId,
        NativeObjectStand? stand,
        int safeSlot,
        string safeKind,
        int restoreSlot)
    {
        if (stand is null || safeSlot < 0 || restoreSlot < 0 || safeKind is not ("empty" or "tool"))
            return Array.Empty<SmallModelActionParameter>();
        var standProjection = stand.Projection;
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
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("mini_obelisk_pair_member_index", ReadInt(projection, "native_pair_member_index").ToString()),
            Parameter("mini_obelisk_pair_first_tile_x", ReadInt(projection, "native_pair_first_tile_x").ToString()),
            Parameter("mini_obelisk_pair_first_tile_y", ReadInt(projection, "native_pair_first_tile_y").ToString()),
            Parameter("mini_obelisk_pair_second_tile_x", ReadInt(projection, "native_pair_second_tile_x").ToString()),
            Parameter("mini_obelisk_pair_second_tile_y", ReadInt(projection, "native_pair_second_tile_y").ToString()),
            Parameter("mini_obelisk_destination_tile_x", ReadInt(standProjection, "native_destination_tile_x").ToString()),
            Parameter("mini_obelisk_destination_tile_y", ReadInt(standProjection, "native_destination_tile_y").ToString()),
            Parameter("mini_obelisk_landing_tile_x", ReadInt(standProjection, "native_landing_tile_x").ToString()),
            Parameter("mini_obelisk_landing_tile_y", ReadInt(standProjection, "native_landing_tile_y").ToString()),
            Parameter("mini_obelisk_expected_delay_milliseconds", ReadInt(projection, "expected_delay_milliseconds").ToString()),
            Parameter("mini_obelisk_expected_location_action_return", ReadBool(projection, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("interaction_kind", ReadString(projection, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection, "expected_action_type")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }
}
