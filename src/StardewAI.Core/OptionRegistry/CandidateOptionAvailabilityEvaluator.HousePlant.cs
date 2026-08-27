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
    private EventCandidate[] HousePlantRotationCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var safeContext = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        var safeEmptySlot = safeContext.HasValue && safeContext.Value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(safeContext.Value, "safe_slot_kind"), "empty", StringComparison.Ordinal)
                ? ReadInt(safeContext.Value, "safe_slot_index")
                : -1;
        var restoreSlot = safeContext.HasValue && safeContext.Value.ValueKind == JsonValueKind.Object
            ? ReadInt(safeContext.Value, "current_tool_index")
            : -1;
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("house_plant_rotation", out var rotation) &&
                rotation.ValueKind == JsonValueKind.Object)
            .Select(row => BuildHousePlantCandidate(
                snapshot, row, row.GetProperty("house_plant_rotation"), locationId,
                playerX, playerY, safeEmptySlot, restoreSlot))
            .OrderBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildHousePlantCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement rotation,
        string locationId,
        int playerX,
        int playerY,
        int safeEmptySlot,
        int restoreSlot)
    {
        var reasons = new List<string>();
        if (!string.Equals(ReadString(rotation, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("house_plant_not_ready:" + ReadString(rotation, "status"));
        }
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("house_plant_menu_must_be_clear");
        }
        if (safeEmptySlot is < 0 or > 11 || restoreSlot is < 0 or > 11)
        {
            reasons.Add("house_plant_empty_toolbar_slot_required");
        }

        var targetX = ReadInt(row, "tile_x");
        var targetY = ReadInt(row, "tile_y");
        var stand = SelectHousePlantStand(rotation, playerX, playerY);
        if (stand is null)
        {
            reasons.Add("house_plant_no_reachable_adjacent_stand");
        }
        var parameters = HousePlantCandidateParameters(
            row, rotation, locationId, stand, safeEmptySlot, restoreSlot);
        if (stand is not null && safeEmptySlot >= 0 && restoreSlot >= 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "world.rotate_house_plant",
                Parameters = parameters
            }));
        }

        var current = ReadInt(rotation, "current_sprite_index");
        var expected = ReadInt(rotation, "expected_sprite_index_after_native_location_action");
        return new EventCandidate
        {
            CandidateId = "house-plant:" + locationId + ":" + targetX + "," + targetY + ":" + current + "->" + expected,
            Kind = "rotate_house_plant",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Rotate House Plant " + current + " to " + expected,
            ExpectedEffect = "current_location.objects[" + targetX + "," + targetY + "].parent_sheet_index=" + expected +
                ";item_id_unchanged=true;qualified_item_id_unchanged=true;fresh_snapshot_replan_required=true",
            EstimatedTicks = stand is null ? 90 : Math.Max(90, stand.Distance * 60 + 90),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_house_plant_single_rotation",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] HousePlantCandidateParameters(
        JsonElement row,
        JsonElement rotation,
        string locationId,
        HousePlantStand? stand,
        int safeEmptySlot,
        int restoreSlot)
    {
        if (stand is null || safeEmptySlot < 0 || restoreSlot < 0)
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(row, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(rotation, "target_runtime_type")),
            Parameter("item_id", ReadString(rotation, "canonical_item_id")),
            Parameter("qualified_item_id", ReadString(rotation, "canonical_qualified_item_id")),
            Parameter("house_plant_current_sprite_index", ReadInt(rotation, "current_sprite_index").ToString()),
            Parameter("house_plant_expected_sprite_index", ReadInt(rotation, "expected_sprite_index_after_native_location_action").ToString()),
            Parameter("house_plant_expected_object_action_calls", ReadInt(rotation, "expected_object_check_for_action_call_count").ToString()),
            Parameter("house_plant_expected_location_action_return", ReadBool(rotation, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("safe_slot_index", safeEmptySlot.ToString()),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", ReadString(rotation, "interaction_kind")),
            Parameter("expected_action_type", ReadString(rotation, "expected_action_type")),
            Parameter("native_contract", ReadString(rotation, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static HousePlantStand? SelectHousePlantStand(JsonElement rotation, int playerX, int playerY)
    {
        if (!rotation.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        return stands.EnumerateArray()
            .Where(stand => ReadBool(stand, "available") == true)
            .Select(stand => new HousePlantStand(
                ReadInt(stand, "tile_x"),
                ReadInt(stand, "tile_y"),
                Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
            .OrderBy(stand => stand.Distance)
            .ThenBy(stand => stand.Y)
            .ThenBy(stand => stand.X)
            .FirstOrDefault();
    }

    private sealed record HousePlantStand(int X, int Y, int Distance);
}
