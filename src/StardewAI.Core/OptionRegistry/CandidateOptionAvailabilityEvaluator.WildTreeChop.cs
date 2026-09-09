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
    private const string WildTreeChopNativeContract =
        "Axe native tool lifecycle -> Tree.performToolAction -> Tree.performTreeFall(trunk,stump) -> Tree.tickUpdate falling settlement; locked Data/WildTrees DropWoodOnChop, DropHardwoodOnLumberChop, ChopItems, SeedItemId, and SeedOnChopChance; complete stochastic output domain; no direct tree, RNG, debris, inventory, stats, or skill mutation";

    private EventCandidate[] WildTreeChopAcquisitionCandidates(SnapshotEnvelope snapshot)
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
                ReadInt(feature, "growth_stage") >= 5 &&
                ReadBool(feature, "stump") != true)
            .Select(feature => BuildWildTreeChopAcquisitionCandidate(snapshot, feature, locationId, playerX, playerY))
            .OrderBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildWildTreeChopAcquisitionCandidate(
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
        var status = ReadString(feature, "tree_chop_acquisition_status");
        var reasons = new List<string>();
        if (status != "ready")
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "wild_tree_chop_projection_unavailable" : status);
        }
        if (stand is null)
        {
            reasons.Add("wild_tree_chop_no_adjacent_route_stand_tile");
        }

        var parameters = WildTreeChopParameters(feature, locationId, distance);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "executor.clear_obstacle",
            Parameters = parameters
        }));

        var expectedSwings = Math.Max(1, ReadInt(feature, "tree_chop_expected_tool_swings"));
        var estimatedTicks = Math.Max(60, distance * 60 + expectedSwings * 60 + 240);
        var energyCost = (int)Math.Ceiling(Math.Max(0d, ReadDouble(feature, "tree_chop_energy_cost")));
        var playerEnergy = ReadStateFieldValue(snapshot, "player", "energy");
        if (playerEnergy.HasValue && playerEnergy.Value.ValueKind == JsonValueKind.Number &&
            playerEnergy.Value.TryGetDouble(out var availableEnergy) && availableEnergy < energyCost)
        {
            reasons.Add("insufficient_energy_for_wild_tree_chop");
        }
        var currentTime = ReadStateFieldInt(snapshot, "time", "time");
        if (currentTime > 0 && WouldFinishAfterClock(currentTime, estimatedTicks, 2600))
        {
            reasons.Add("wild_tree_chop_would_exceed_day_time_budget");
        }

        return new EventCandidate
        {
            CandidateId = "chop-wild-tree:" + locationId + ":" + x + "," + y,
            Kind = "clear_obstacle_tile",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = x,
            TileY = y,
            ExpectedEffect = WildTreeChopExpectedEffect(feature, stand),
            EstimatedTicks = estimatedTicks,
            EnergyCost = energyCost,
            AvailabilityClass = "transparent_native_wild_tree_chop_acquisition",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] WildTreeChopParameters(
        JsonElement feature,
        string locationId,
        int distance)
    {
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(feature, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(feature, "tile_y").ToString()),
            Parameter("route_distance_tiles", distance.ToString()),
            Parameter("max_tool_swings", ReadInt(feature, "tree_chop_expected_tool_swings").ToString()),
            Parameter("clear_completion_mode", ReadString(feature, "tree_chop_completion_mode")),
            Parameter("target_runtime_type", ReadString(feature, "runtime_type")),
            Parameter("tool_slot_index", ReadInt(feature, "tree_chop_tool_slot_index").ToString()),
            Parameter("required_tool_kind", ReadString(feature, "tree_chop_required_tool_kind")),
            Parameter("tree_chop_tree_type", ReadString(feature, "tree_type")),
            Parameter("tree_chop_data_contract_status", ReadString(feature, "tree_chop_data_contract_status")),
            Parameter("tree_chop_protection_status", ReadString(feature, "tree_chop_protection_status")),
            Parameter("tree_chop_projection_status", ReadString(feature, "tree_chop_projection_status")),
            Parameter("tree_chop_output_domain_contract", ReadString(feature, "tree_chop_output_distribution_status")),
            Parameter("tree_chop_guaranteed_minimum_outputs_json", JsonSerializer.Serialize(ReadArray(feature, "tree_chop_guaranteed_minimum_outputs"))),
            Parameter("tree_chop_output_domain_json", JsonSerializer.Serialize(ReadArray(feature, "tree_chop_optional_output_domain"))),
            Parameter("tree_chop_native_contract", ReadString(feature, "tree_chop_native_contract")),
            Parameter("skill_experience_skill_id", "foraging"),
            Parameter("skill_experience_projection_status", "exact"),
            Parameter("skill_experience_on_success_min", ReadInt(feature, "tree_chop_foraging_experience_delta").ToString()),
            Parameter("skill_experience_on_success_max", ReadInt(feature, "tree_chop_foraging_experience_delta").ToString()),
            Parameter("expected_foraging_experience_before", ReadInt(feature, "tree_chop_foraging_experience_before").ToString()),
            Parameter("expected_foraging_experience_delta", ReadInt(feature, "tree_chop_foraging_experience_delta").ToString()),
            Parameter("expected_foraging_experience_after", ReadInt(feature, "tree_chop_foraging_experience_after").ToString()),
            Parameter("expected_tree_has_moss_before", ReadBool(feature, "has_moss").ToString().ToLowerInvariant()),
            Parameter("expected_tree_has_seed_before", ReadBool(feature, "has_seed").ToString().ToLowerInvariant()),
            Parameter("expected_tree_growth_stage_before", ReadInt(feature, "growth_stage").ToString()),
            Parameter("expected_tree_health_before", ReadScalar(feature, "health")),
            Parameter("expected_tree_present_after", ReadBool(feature, "tree_chop_expected_tree_present_after").ToString().ToLowerInvariant()),
            Parameter("expected_trees_chopped_before", ReadScalar(feature, "tree_chop_trees_chopped_before")),
            Parameter("expected_trees_chopped_delta", ReadInt(feature, "tree_chop_trees_chopped_delta").ToString()),
            Parameter("expected_trees_chopped_after", ReadScalar(feature, "tree_chop_trees_chopped_after"))
        };
    }

    private static string WildTreeChopExpectedEffect(JsonElement feature, CandidateTile? stand)
    {
        var prefix = stand is null ? string.Empty : "move_to_adjacent=" + stand.X + "," + stand.Y + ";";
        return prefix + string.Join(";", WildTreeChopParameters(feature, string.Empty, 0)
            .Where(parameter => parameter.Name is not "target_location" and not "target_tile_x" and not "target_tile_y" and not "route_distance_tiles")
            .Select(parameter => parameter.Name + "=" + parameter.Value));
    }
}
