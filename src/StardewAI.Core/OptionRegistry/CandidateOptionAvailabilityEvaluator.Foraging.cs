using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] SpawnedObjectForagingCandidates(SnapshotEnvelope snapshot)
        {
            var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
            if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return objects.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && ReadBool(item, "is_spawned_object") == true)
                .Select(item =>
                {
                    var x = ReadInt(item, "tile_x");
                    var y = ReadInt(item, "tile_y");
                    var qualifiedItemId = ReadString(item, "qualified_item_id");
                    var totalQuantity = Math.Max(1, ReadInt(item, "projected_total_quantity"));
                    var projectedQuality = ReadInt(item, "projected_harvest_quality");
                    var status = ReadString(item, "spawned_object_pickup_status");
                    var stand = FindBestStandTile(snapshot, x, y);
                    var blockReasons = new List<string>();
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "spawned_object_projection_unavailable" : status);
                    }
                    if (string.IsNullOrWhiteSpace(qualifiedItemId))
                    {
                        blockReasons.Add("spawned_object_item_identity_unavailable");
                    }
                    if (stand is null)
                    {
                        blockReasons.Add("spawned_object_no_adjacent_stand_tile");
                    }
                    if (stand is not null)
                    {
                        blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "executor.collect_spawned_object",
                            Parameters = new[]
                            {
                                Parameter("target_tile_x", x.ToString()),
                                Parameter("target_tile_y", y.ToString()),
                                Parameter("stand_tile_x", stand.X.ToString()),
                                Parameter("stand_tile_y", stand.Y.ToString()),
                                Parameter("qualified_item_id", qualifiedItemId),
                                Parameter("quantity", totalQuantity.ToString()),
                                Parameter("projected_harvest_quality", projectedQuality.ToString()),
                                Parameter("foraging_experience_on_success_min", ReadInt(item, "foraging_experience_on_success_min").ToString()),
                                Parameter("foraging_experience_on_success_max", ReadInt(item, "foraging_experience_on_success_max").ToString()),
                                Parameter("farming_experience_on_success_min", ReadInt(item, "farming_experience_on_success_min").ToString()),
                                Parameter("farming_experience_on_success_max", ReadInt(item, "farming_experience_on_success_max").ToString()),
                                Parameter("harvest_experience_status", ReadString(item, "harvest_experience_status")),
                                Parameter("max_movement_tiles", "512")
                            }
                        }));
                    }

                    var distance = stand is null
                        ? 0
                        : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                    var effect = (stand is null ? string.Empty : "spawned_object_stand_tile=" + stand.X + "," + stand.Y + ";") +
                        "current_location.objects[" + x + "," + y + "].present=false" +
                        ";qualified_item_id=" + qualifiedItemId +
                        ";projected_harvest_quality=" + projectedQuality +
                        ";projected_total_quantity=" + totalQuantity +
                        ";projected_gatherer_duplicate=" + ReadBool(item, "projected_gatherer_duplicate").ToString().ToLowerInvariant() +
                        ";foraging_experience_on_success_min=" + ReadInt(item, "foraging_experience_on_success_min") +
                        ";foraging_experience_on_success_max=" + ReadInt(item, "foraging_experience_on_success_max") +
                        ";farming_experience_on_success_min=" + ReadInt(item, "farming_experience_on_success_min") +
                        ";farming_experience_on_success_max=" + ReadInt(item, "farming_experience_on_success_max") +
                        ";harvest_experience_status=" + ReadString(item, "harvest_experience_status") +
                        ";harvest_experience_basis=" + ReadString(item, "harvest_experience_basis") +
                        ";max_movement_tiles=512";
                    return new EventCandidate
                    {
                        CandidateId = "collect-spawned-object:" + locationId + ":" + x + "," + y + ":" + qualifiedItemId,
                        Kind = "collect_spawned_object",
                        Available = blockReasons.Count == 0,
                        LocationId = locationId,
                        TileX = x,
                        TileY = y,
                        ItemId = ReadString(item, "item_id"),
                        QualifiedItemId = qualifiedItemId,
                        Quantity = totalQuantity,
                        ExpectedEffect = effect,
                        EstimatedTicks = Math.Max(30, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_spawned_object_native_pickup",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = new[]
                        {
                            Parameter("foraging_experience_on_success_min", ReadInt(item, "foraging_experience_on_success_min").ToString()),
                            Parameter("foraging_experience_on_success_max", ReadInt(item, "foraging_experience_on_success_max").ToString()),
                            Parameter("farming_experience_on_success_min", ReadInt(item, "farming_experience_on_success_min").ToString()),
                            Parameter("farming_experience_on_success_max", ReadInt(item, "farming_experience_on_success_max").ToString()),
                            Parameter("skill_experience_projection_status", ReadString(item, "harvest_experience_status")),
                            Parameter("skill_experience_condition", ReadString(item, "harvest_experience_basis"))
                        }
                    };
                })
                .ToArray();
        }

        private EventCandidate[] GingerHarvestCandidates(SnapshotEnvelope snapshot)
        {
            var terrainFeatures = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            if (!terrainFeatures.HasValue || terrainFeatures.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return terrainFeatures.Value.EnumerateArray()
                .Where(feature => feature.ValueKind == JsonValueKind.Object && ReadBool(feature, "is_ginger") == true)
                .Select(feature =>
                {
                    var x = ReadInt(feature, "tile_x");
                    var y = ReadInt(feature, "tile_y");
                    var stand = FindBestStandTile(snapshot, x, y);
                    var status = ReadString(feature, "ginger_harvest_status");
                    var energyCost = Math.Max(0d, ReadDouble(feature, "ginger_energy_cost"));
                    var blockReasons = new List<string>();
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "ginger_projection_unavailable" : status);
                    }
                    if (!string.Equals(ReadString(feature, "ginger_projection_status"), "exact_from_native_crop_hit_with_hoe", StringComparison.Ordinal))
                    {
                        blockReasons.Add("ginger_projection_incomplete");
                    }
                    if (stand is null)
                    {
                        blockReasons.Add("ginger_no_adjacent_stand_tile");
                    }
                    if (stand is not null)
                    {
                        blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "executor.harvest_ginger",
                            Parameters = GingerParameters(feature, x, y, stand)
                        }));
                    }

                    var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                    return new EventCandidate
                    {
                        CandidateId = "harvest-ginger:" + locationId + ":" + x + "," + y,
                        Kind = "harvest_ginger",
                        Available = blockReasons.Count == 0,
                        LocationId = locationId,
                        TileX = x,
                        TileY = y,
                        ItemId = "829",
                        QualifiedItemId = "(O)829",
                        SlotIndex = ReadInt(feature, "ginger_tool_slot_index"),
                        Quantity = 1,
                        ExpectedEffect = GingerExpectedEffect(feature, stand),
                        EstimatedTicks = Math.Max(85, distance * 60 + 85),
                        EnergyCost = (int)Math.Ceiling(energyCost),
                        AvailabilityClass = "transparent_native_ginger_hoe_harvest",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = stand is null ? Array.Empty<SmallModelActionParameter>() : GingerParameters(feature, x, y, stand)
                    };
                })
                .OrderBy(candidate => candidate.EstimatedTicks)
                .ThenBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ToArray();
        }

        private static SmallModelActionParameter[] GingerParameters(JsonElement feature, int x, int y, CandidateTile stand)
        {
            return new[]
            {
                Parameter("target_tile_x", x.ToString()),
                Parameter("target_tile_y", y.ToString()),
                Parameter("stand_tile_x", stand.X.ToString()),
                Parameter("stand_tile_y", stand.Y.ToString()),
                Parameter("required_tool_kind", "Hoe"),
                Parameter("tool_slot_index", ReadInt(feature, "ginger_tool_slot_index").ToString()),
                Parameter("qualified_item_id", "(O)829"),
                Parameter(
                    "ginger_output_context_tags_json",
                    JsonSerializer.Serialize(ReadStringArray(feature, "ginger_output_context_tags"))),
                Parameter("quantity", "1"),
                Parameter("expected_output_quality", "0"),
                Parameter("expected_energy_cost", ReadDouble(feature, "ginger_energy_cost").ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                Parameter("expected_foraging_experience_delta", "7"),
                Parameter("skill_experience_skill_id", "foraging"),
                Parameter("skill_experience_on_success_min", "7"),
                Parameter("skill_experience_on_success_max", "7"),
                Parameter("skill_experience_projection_status", "exact_from_native_ginger_hoe_branch"),
                Parameter("skill_experience_condition", "native_hoe_hit_removes_exact_ginger_crop"),
                Parameter("expected_hoe_dirt_state_after", ReadInt(feature, "ginger_hoe_dirt_state_expected_after").ToString()),
                Parameter("ginger_projection_status", ReadString(feature, "ginger_projection_status")),
                Parameter("max_movement_tiles", "512")
            };
        }

        private static string GingerExpectedEffect(JsonElement feature, CandidateTile? stand)
        {
            var x = ReadInt(feature, "tile_x");
            var y = ReadInt(feature, "tile_y");
            return (stand is not null ? "ginger_stand_tile=" + stand.X + "," + stand.Y + ";" : string.Empty) +
                "current_location.terrain_features[" + x + "," + y + "].crop=none" +
                ";current_location.terrain_features[" + x + "," + y + "].type=HoeDirt" +
                ";qualified_item_id=(O)829;quantity=1" +
                ";expected_output_quality=0" +
                ";tool_slot_index=" + ReadInt(feature, "ginger_tool_slot_index") +
                ";expected_energy_cost=" + ReadDouble(feature, "ginger_energy_cost").ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ";expected_foraging_experience_delta=7" +
                ";expected_hoe_dirt_state_after=" + ReadInt(feature, "ginger_hoe_dirt_state_expected_after") +
                ";ginger_projection_status=" + ReadString(feature, "ginger_projection_status") +
                ";max_movement_tiles=512";
        }
    }
}
