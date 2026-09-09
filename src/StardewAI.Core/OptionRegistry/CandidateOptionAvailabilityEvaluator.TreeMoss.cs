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
    private const string TreeMossHarvestNativeContract =
        "MeleeWeapon(scythe) native tool lifecycle -> Tree.performToolAction -> Tree.CreateMossItem -> Game1.createMultipleItemDebris(Item.Stack=1 side effect) -> Tree.shake -> growthStage=11, seedless exact base Tree, no direct tree, RNG, debris, inventory, stat, or skill mutation";

    private EventCandidate[] TreeMossHarvestCandidates(SnapshotEnvelope snapshot)
    {
        var features = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
        if (!features.HasValue || features.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return features.Value.EnumerateArray()
            .Where(feature => feature.ValueKind == JsonValueKind.Object &&
                ReadString(feature, "runtime_type") == "StardewValley.TerrainFeatures.Tree" &&
                ReadBool(feature, "has_moss") == true)
            .Select(feature => BuildTreeMossHarvestCandidate(snapshot, feature, locationId, playerX, playerY))
            .OrderBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildTreeMossHarvestCandidate(
        SnapshotEnvelope snapshot,
        JsonElement feature,
        string locationId,
        int playerX,
        int playerY)
    {
        var x = ReadInt(feature, "tile_x");
        var y = ReadInt(feature, "tile_y");
        var stand = FindBestStandTile(snapshot, x, y);
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        var status = ReadString(feature, "moss_harvest_status");
        var reasons = new List<string>();
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "tree_moss_projection_unavailable" : status);
        }
        if (stand is null)
        {
            reasons.Add("tree_moss_no_adjacent_route_stand_tile");
        }

        var parameters = TreeMossHarvestParameters(feature, locationId, distance);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "executor.clear_obstacle",
            Parameters = parameters
        }));

        var estimatedTicks = Math.Max(60, distance * 60 + 60);
        var currentTime = ReadStateFieldInt(snapshot, "time", "time");
        if (currentTime > 0 && WouldFinishAfterClock(currentTime, estimatedTicks, 2600))
        {
            reasons.Add("tree_moss_harvest_would_exceed_day_time_budget");
        }

        return new EventCandidate
        {
            CandidateId = "harvest-tree-moss:" + locationId + ":" + x + "," + y,
            Kind = "clear_obstacle_tile",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = x,
            TileY = y,
            ItemId = "Moss",
            QualifiedItemId = "(O)Moss",
            Quantity = ReadInt(feature, "moss_harvest_quantity"),
            ExpectedEffect = TreeMossHarvestExpectedEffect(feature, stand),
            EstimatedTicks = estimatedTicks,
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_seedless_tree_moss_scythe",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] TreeMossHarvestParameters(JsonElement feature, string locationId, int distance)
    {
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(feature, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(feature, "tile_y").ToString()),
            Parameter("route_distance_tiles", distance.ToString()),
            Parameter("max_tool_swings", "1"),
            Parameter("clear_completion_mode", ReadString(feature, "moss_harvest_completion_mode")),
            Parameter("target_runtime_type", ReadString(feature, "runtime_type")),
            Parameter("tool_slot_index", ReadInt(feature, "moss_harvest_tool_slot_index").ToString()),
            Parameter("required_tool_kind", ReadString(feature, "moss_harvest_required_tool_kind")),
            Parameter("clear_output_projection_status", "exact"),
            Parameter("clear_output_items_json", JsonSerializer.Serialize(ReadArray(feature, "moss_harvest_output_items"))),
            Parameter("skill_experience_skill_id", "foraging"),
            Parameter("skill_experience_projection_status", "exact"),
            Parameter("skill_experience_on_success_min", ReadInt(feature, "moss_harvest_quantity").ToString()),
            Parameter("skill_experience_on_success_max", ReadInt(feature, "moss_harvest_quantity").ToString()),
            Parameter("expected_tree_has_moss_before", ReadBool(feature, "moss_harvest_has_moss_before").ToString().ToLowerInvariant()),
            Parameter("expected_tree_has_moss_after", ReadBool(feature, "moss_harvest_has_moss_after").ToString().ToLowerInvariant()),
            Parameter("expected_tree_has_seed_before", ReadBool(feature, "moss_harvest_has_seed_before").ToString().ToLowerInvariant()),
            Parameter("expected_tree_has_seed_after", ReadBool(feature, "moss_harvest_has_seed_after").ToString().ToLowerInvariant()),
            Parameter("expected_tree_was_shaken_today_before", ReadBool(feature, "moss_harvest_was_shaken_today_before").ToString().ToLowerInvariant()),
            Parameter("expected_tree_was_shaken_today_after", ReadBool(feature, "moss_harvest_was_shaken_today_after").ToString().ToLowerInvariant()),
            Parameter("expected_tree_growth_stage_before", ReadInt(feature, "moss_harvest_growth_stage_before").ToString()),
            Parameter("expected_tree_growth_stage_after", ReadInt(feature, "moss_harvest_growth_stage_after").ToString()),
            Parameter("expected_tree_health_before", ReadScalar(feature, "moss_harvest_health_before")),
            Parameter("expected_tree_health_after", ReadScalar(feature, "moss_harvest_health_after")),
            Parameter("expected_moss_harvested_before", ReadScalar(feature, "moss_harvest_moss_harvested_before")),
            Parameter("expected_moss_harvested_after", ReadScalar(feature, "moss_harvest_moss_harvested_after")),
            Parameter("expected_foraging_experience_before", ReadInt(feature, "moss_harvest_foraging_experience_before").ToString()),
            Parameter("expected_foraging_experience_delta", ReadInt(feature, "moss_harvest_quantity").ToString()),
            Parameter("expected_foraging_experience_after", ReadInt(feature, "moss_harvest_foraging_experience_after").ToString()),
            Parameter("moss_harvest_projection_status", ReadString(feature, "moss_harvest_output_projection_status")),
            Parameter("moss_harvest_native_contract", ReadString(feature, "moss_harvest_native_contract"))
        };
    }

    private static string TreeMossHarvestExpectedEffect(JsonElement feature, CandidateTile? stand)
    {
        var prefix = stand is null ? string.Empty : "move_to_adjacent=" + stand.X + "," + stand.Y + ";";
        return prefix + string.Join(";", TreeMossHarvestParameters(feature, string.Empty, 0)
            .Where(parameter => parameter.Name is not "target_location" and not "target_tile_x" and not "target_tile_y" and not "route_distance_tiles")
            .Select(parameter => parameter.Name + "=" + parameter.Value));
    }

    private static string ReadScalar(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return string.Empty;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }
}
