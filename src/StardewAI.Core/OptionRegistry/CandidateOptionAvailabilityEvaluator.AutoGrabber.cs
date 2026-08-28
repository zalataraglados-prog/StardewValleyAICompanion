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
    private EventCandidate[] AutoGrabberCollectionCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var safeContext = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        var safeKind = safeContext.HasValue && safeContext.Value.ValueKind == JsonValueKind.Object
            ? ReadString(safeContext.Value, "safe_slot_kind")
            : "unavailable";
        var safeSlot = safeContext.HasValue && safeContext.Value.ValueKind == JsonValueKind.Object
            ? ReadInt(safeContext.Value, "safe_slot_index")
            : -1;
        var restoreSlot = safeContext.HasValue && safeContext.Value.ValueKind == JsonValueKind.Object
            ? ReadInt(safeContext.Value, "current_tool_index")
            : -1;
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("auto_grabber_collection", out var projection) &&
                projection.ValueKind == JsonValueKind.Object)
            .Select(row => BuildAutoGrabberCandidate(
                snapshot, row, row.GetProperty("auto_grabber_collection"), locationId,
                playerX, playerY, safeSlot, safeKind, restoreSlot))
            .OrderBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildAutoGrabberCandidate(
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
            reasons.Add("auto_grabber_not_ready:" + status);
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("auto_grabber_menu_must_be_clear");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11 ||
            (safeKind != "empty" && safeKind != "tool"))
        {
            reasons.Add("auto_grabber_safe_toolbar_slot_required");
        }

        var targetX = ReadInt(row, "tile_x");
        var targetY = ReadInt(row, "tile_y");
        var stand = SelectAutoGrabberStand(projection, playerX, playerY);
        if (stand is null)
            reasons.Add("auto_grabber_no_reachable_adjacent_stand");
        var parameters = AutoGrabberCandidateParameters(
            row, projection, locationId, stand, safeSlot, safeKind, restoreSlot);
        if (stand is not null && safeSlot >= 0 && restoreSlot >= 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "animals.collect_auto_grabber_contents",
                Parameters = parameters
            }));
        }

        var quantity = ReadInt(projection, "expected_transfer_quantity");
        var stacks = ReadInt(projection, "transferable_stack_count");
        return new EventCandidate
        {
            CandidateId = "auto-grabber:" + locationId + ":" + targetX + "," + targetY +
                ":stacks=" + stacks + ":quantity=" + quantity,
            Kind = "collect_auto_grabber_contents",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Collect Auto-Grabber Contents",
            ExpectedEffect = "auto_grabber.contents_stacks-=" + stacks +
                ";player.inventory_quantity+=" + quantity +
                ";unfittable_stacks_unchanged=true;fresh_snapshot_replan_required=true",
            EstimatedTicks = stand is null ? 120 : Math.Max(120, stand.Distance * 60 + stacks * 15 + 120),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_auto_grabber_collection",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] AutoGrabberCandidateParameters(
        JsonElement row,
        JsonElement projection,
        string locationId,
        AutoGrabberStand? stand,
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
            Parameter("auto_grabber_held_container_runtime_type", ReadString(projection, "held_container_runtime_type")),
            Parameter("auto_grabber_contents_before_json", ReadString(projection, "contents_before_json")),
            Parameter("auto_grabber_transferable_contents_json", ReadString(projection, "transferable_contents_json")),
            Parameter("auto_grabber_remaining_contents_json", ReadString(projection, "remaining_contents_json")),
            Parameter("auto_grabber_content_stack_count_before", ReadInt(projection, "content_stack_count_before").ToString()),
            Parameter("auto_grabber_transferable_stack_count", ReadInt(projection, "transferable_stack_count").ToString()),
            Parameter("auto_grabber_expected_stack_count_after", ReadInt(projection, "expected_stack_count_after").ToString()),
            Parameter("auto_grabber_content_quantity_before", ReadInt(projection, "content_quantity_before").ToString()),
            Parameter("auto_grabber_expected_transfer_quantity", ReadInt(projection, "expected_transfer_quantity").ToString()),
            Parameter("auto_grabber_expected_quantity_after", ReadInt(projection, "expected_quantity_after").ToString()),
            Parameter("auto_grabber_expected_location_action_return", ReadBool(projection, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", ReadString(projection, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection, "expected_action_type")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static AutoGrabberStand? SelectAutoGrabberStand(JsonElement projection, int playerX, int playerY)
    {
        if (!projection.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
            return null;
        return stands.EnumerateArray()
            .Where(stand => ReadBool(stand, "available") == true)
            .Select(stand => new AutoGrabberStand(
                ReadInt(stand, "tile_x"),
                ReadInt(stand, "tile_y"),
                Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
            .OrderBy(stand => stand.Distance)
            .ThenBy(stand => stand.Y)
            .ThenBy(stand => stand.X)
            .FirstOrDefault();
    }

    private sealed record AutoGrabberStand(int X, int Y, int Distance);
}
