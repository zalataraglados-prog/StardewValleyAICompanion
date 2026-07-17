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
    }
}
