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
    private EventCandidate[] FishPondServiceCandidates(SnapshotEnvelope snapshot)
    {
        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        if (!buildings.HasValue || buildings.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var farmIdentity = ReadStateFieldValue(snapshot, "farm", "farm_identity");
        var farmLocation = farmIdentity.HasValue && farmIdentity.Value.ValueKind == JsonValueKind.Object
            ? ReadString(farmIdentity.Value, "location_id")
            : "Farm";
        var playerLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var rows = new List<EventCandidate>();
        foreach (var building in buildings.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
        {
            if (!building.TryGetProperty("fish_pond", out var pond) || pond.ValueKind != JsonValueKind.Object ||
                ReadString(pond, "status") == "not_applicable")
            {
                continue;
            }

            rows.Add(BuildFishPondOutputCandidate(snapshot, building, pond, farmLocation, playerLocation, playerX, playerY));
            rows.Add(BuildFishPondRequestCandidate(snapshot, building, pond, farmLocation, playerLocation, playerX, playerY));
        }
        return rows.ToArray();
    }

    private EventCandidate BuildFishPondOutputCandidate(
        SnapshotEnvelope snapshot,
        JsonElement building,
        JsonElement pond,
        string farmLocation,
        string playerLocation,
        int playerX,
        int playerY)
    {
        var buildingX = ReadInt(building, "tile_x");
        var buildingY = ReadInt(building, "tile_y");
        var targetX = NullableInt(pond, "preferred_target_tile_x");
        var targetY = NullableInt(pond, "preferred_target_tile_y");
        var standX = NullableInt(pond, "preferred_stand_tile_x");
        var standY = NullableInt(pond, "preferred_stand_tile_y");
        var status = ReadString(pond, "output_status");
        var outputId = ReadString(pond, "output_qualified_item_id");
        var outputJson = ReadString(pond, "output_items_json");
        var reasons = FishPondCommonReasons(pond, status, playerLocation, farmLocation, targetX, targetY, standX, standY);
        if (string.IsNullOrWhiteSpace(outputId) || ReadInt(pond, "output_stack") <= 0 ||
            ReadString(pond, "output_unit_state_sha256").Length != 64 || string.IsNullOrWhiteSpace(outputJson))
        {
            reasons.Add("fish_pond_output_identity_incomplete");
        }
        var parameters = targetX.HasValue && targetY.HasValue && standX.HasValue && standY.HasValue
            ? FishPondOutputParameters(building, pond, farmLocation, targetX.Value, targetY.Value, standX.Value, standY.Value)
            : Array.Empty<SmallModelActionParameter>();
        if (parameters.Length > 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.collect_fish_pond_output",
                Parameters = parameters
            }));
        }
        var distance = standX.HasValue && standY.HasValue && string.Equals(playerLocation, farmLocation, StringComparison.OrdinalIgnoreCase)
            ? Math.Abs(playerX - standX.Value) + Math.Abs(playerY - standY.Value)
            : 0;
        return new EventCandidate
        {
            CandidateId = "collect-fish-pond-output:" + farmLocation + ":" + buildingX + "," + buildingY + ":" + outputId,
            Kind = "collect_fish_pond_output",
            Available = reasons.Count == 0,
            LocationId = farmLocation,
            TileX = targetX,
            TileY = targetY,
            ItemId = outputId,
            QualifiedItemId = outputId,
            Quantity = ReadInt(pond, "output_stack"),
            ExpectedEffect = FishPondOutputExpectedEffect(building, pond, standX, standY),
            EstimatedTicks = Math.Max(30, distance * 60 + 30),
            EnergyCost = 0,
            AvailabilityClass = "transparent_fish_pond_native_output_collect",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private EventCandidate BuildFishPondRequestCandidate(
        SnapshotEnvelope snapshot,
        JsonElement building,
        JsonElement pond,
        string farmLocation,
        string playerLocation,
        int playerX,
        int playerY)
    {
        var buildingX = ReadInt(building, "tile_x");
        var buildingY = ReadInt(building, "tile_y");
        var targetX = NullableInt(pond, "preferred_target_tile_x");
        var targetY = NullableInt(pond, "preferred_target_tile_y");
        var standX = NullableInt(pond, "preferred_stand_tile_x");
        var standY = NullableInt(pond, "preferred_stand_tile_y");
        var status = ReadString(pond, "request_status");
        var requestId = ReadString(pond, "request_item_qualified_item_id");
        var slotsJson = ReadString(pond, "request_item_toolbar_slots_json");
        var reasons = FishPondCommonReasons(pond, status, playerLocation, farmLocation, targetX, targetY, standX, standY);
        if (string.IsNullOrWhiteSpace(requestId) || ReadInt(pond, "request_item_count_remaining") <= 0 ||
            string.IsNullOrWhiteSpace(slotsJson) || ReadInt(pond, "request_fishing_experience_delta") <= 0)
        {
            reasons.Add("fish_pond_request_projection_incomplete");
        }
        var parameters = targetX.HasValue && targetY.HasValue && standX.HasValue && standY.HasValue
            ? FishPondRequestParameters(building, pond, farmLocation, targetX.Value, targetY.Value, standX.Value, standY.Value)
            : Array.Empty<SmallModelActionParameter>();
        if (parameters.Length > 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.complete_fish_pond_request",
                Parameters = parameters
            }));
        }
        var distance = standX.HasValue && standY.HasValue && string.Equals(playerLocation, farmLocation, StringComparison.OrdinalIgnoreCase)
            ? Math.Abs(playerX - standX.Value) + Math.Abs(playerY - standY.Value)
            : 0;
        return new EventCandidate
        {
            CandidateId = "complete-fish-pond-request:" + farmLocation + ":" + buildingX + "," + buildingY + ":" + requestId,
            Kind = "complete_fish_pond_request",
            Available = reasons.Count == 0,
            LocationId = farmLocation,
            TileX = targetX,
            TileY = targetY,
            ItemId = requestId,
            QualifiedItemId = requestId,
            Quantity = ReadInt(pond, "request_item_count_remaining"),
            ExpectedEffect = FishPondRequestExpectedEffect(building, pond, standX, standY),
            EstimatedTicks = Math.Max(60, distance * 60 + ReadInt(pond, "request_item_count_remaining") * 30),
            EnergyCost = 0,
            AvailabilityClass = "transparent_fish_pond_native_request_completion",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static List<string> FishPondCommonReasons(
        JsonElement pond,
        string branchStatus,
        string playerLocation,
        string farmLocation,
        int? targetX,
        int? targetY,
        int? standX,
        int? standY)
    {
        var reasons = new List<string>();
        if (ReadString(pond, "status") != "exact")
        {
            reasons.Add("fish_pond_projection_unavailable");
        }
        if (!string.Equals(branchStatus, "ready", StringComparison.Ordinal))
        {
            reasons.Add(string.IsNullOrWhiteSpace(branchStatus) ? "fish_pond_branch_status_unavailable" : branchStatus);
        }
        if (!string.Equals(playerLocation, farmLocation, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("fish_pond_player_not_on_farm");
        }
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            (targetX.HasValue && targetY.HasValue && standX.HasValue && standY.HasValue &&
             Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1))
        {
            reasons.Add("fish_pond_interaction_geometry_unavailable");
        }
        return reasons;
    }

    private static SmallModelActionParameter[] FishPondOutputParameters(
        JsonElement building,
        JsonElement pond,
        string farmLocation,
        int targetX,
        int targetY,
        int standX,
        int standY)
    {
        return FishPondCommonParameters(building, pond, farmLocation, targetX, targetY, standX, standY)
            .Concat(new[]
            {
                Parameter("safe_slot_index", ReadInt(pond, "output_safe_slot_index").ToString()),
                Parameter("qualified_item_id", ReadString(pond, "output_qualified_item_id")),
                Parameter("quantity", ReadInt(pond, "output_stack").ToString()),
                Parameter("expected_output_items_json", ReadString(pond, "output_items_json")),
                Parameter("expected_output_state_context", ReadString(pond, "output_state_context")),
                Parameter("expected_skill_id", "fishing"),
                Parameter("expected_skill_experience_delta", ReadInt(pond, "output_fishing_experience_delta").ToString()),
                Parameter("native_receipt_callbacks_status", ReadString(pond, "output_receipt_callbacks_status"))
            })
            .ToArray();
    }

    private static SmallModelActionParameter[] FishPondRequestParameters(
        JsonElement building,
        JsonElement pond,
        string farmLocation,
        int targetX,
        int targetY,
        int standX,
        int standY)
    {
        return FishPondCommonParameters(building, pond, farmLocation, targetX, targetY, standX, standY)
            .Concat(new[]
            {
                Parameter("qualified_item_id", ReadString(pond, "request_item_qualified_item_id")),
                Parameter("quantity", ReadInt(pond, "request_item_count_remaining").ToString()),
                Parameter("request_item_runtime_type", ReadString(pond, "request_item_runtime_type")),
                Parameter("request_item_toolbar_slots_json", ReadString(pond, "request_item_toolbar_slots_json")),
                Parameter("expected_skill_id", "fishing"),
                Parameter("expected_skill_experience_delta", ReadInt(pond, "request_fishing_experience_delta").ToString()),
                Parameter("expected_maximum_occupants_after", ReadInt(pond, "request_expected_maximum_occupants_after").ToString()),
                Parameter("expected_last_unlocked_population_gate_after", ReadInt(pond, "request_expected_last_unlocked_population_gate_after").ToString()),
                Parameter("expected_days_since_spawn_after", ReadInt(pond, "request_expected_days_since_spawn_after").ToString()),
                Parameter("expected_needed_item_count_after", ReadInt(pond, "request_expected_needed_item_count_after").ToString()),
                Parameter("expected_has_completed_request_after", BoolInt(pond, "request_expected_has_completed_request_after"))
            })
            .ToArray();
    }

    private static IEnumerable<SmallModelActionParameter> FishPondCommonParameters(
        JsonElement building,
        JsonElement pond,
        string farmLocation,
        int targetX,
        int targetY,
        int standX,
        int standY)
    {
        return new[]
        {
            Parameter("target_location", farmLocation),
            Parameter("target_tile_x", targetX.ToString()),
            Parameter("target_tile_y", targetY.ToString()),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("building_tile_x", ReadInt(building, "tile_x").ToString()),
            Parameter("building_tile_y", ReadInt(building, "tile_y").ToString()),
            Parameter("target_runtime_type", ReadString(pond, "runtime_type")),
            Parameter("fish_type_item_id", ReadString(pond, "fish_type_item_id")),
            Parameter("expected_fish_count", ReadInt(pond, "fish_count").ToString()),
            Parameter("expected_maximum_occupants_before", ReadInt(pond, "maximum_occupants").ToString()),
            Parameter("expected_last_unlocked_population_gate_before", ReadInt(pond, "last_unlocked_population_gate").ToString()),
            Parameter("expected_days_since_spawn_before", ReadInt(pond, "days_since_spawn").ToString()),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string FishPondOutputExpectedEffect(JsonElement building, JsonElement pond, int? standX, int? standY)
    {
        return FishPondEffectPrefix(building, standX, standY) +
            ";fish_pond_output=null" +
            ";qualified_item_id=" + ReadString(pond, "output_qualified_item_id") +
            ";quantity=" + ReadInt(pond, "output_stack") +
            ";expected_skill_id=fishing" +
            ";expected_skill_experience_delta=" + ReadInt(pond, "output_fishing_experience_delta");
    }

    private static string FishPondRequestExpectedEffect(JsonElement building, JsonElement pond, int? standX, int? standY)
    {
        return FishPondEffectPrefix(building, standX, standY) +
            ";fish_pond_request_completed=true" +
            ";qualified_item_id=" + ReadString(pond, "request_item_qualified_item_id") +
            ";quantity=" + ReadInt(pond, "request_item_count_remaining") +
            ";maximum_occupants_after=" + ReadInt(pond, "request_expected_maximum_occupants_after") +
            ";expected_skill_id=fishing" +
            ";expected_skill_experience_delta=" + ReadInt(pond, "request_fishing_experience_delta");
    }

    private static string FishPondEffectPrefix(JsonElement building, int? standX, int? standY)
    {
        return "fish_pond_stand_tile=" + standX + "," + standY +
            ";fish_pond_building_tile=" + ReadInt(building, "tile_x") + "," + ReadInt(building, "tile_y");
    }

    private static int? NullableInt(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            return null;
        }
        return parsed;
    }
}
