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
        private EventCandidate[] FruitTreeHarvestCandidates(SnapshotEnvelope snapshot)
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
                .Where(feature => feature.ValueKind == JsonValueKind.Object && ReadBool(feature, "is_fruit_tree") == true)
                .Select(feature =>
                {
                    var x = ReadInt(feature, "tile_x");
                    var y = ReadInt(feature, "tile_y");
                    var interaction = FindBestTerrainInteraction(snapshot, x, y, 1);
                    var status = ReadString(feature, "fruit_tree_harvest_status");
                    var outputs = ReadArray(feature, "fruit_tree_expected_outputs");
                    var total = ReadInt(feature, "fruit_tree_expected_output_quantity_total");
                    var blockReasons = new List<string>();
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "fruit_tree_projection_unavailable" : status);
                    }
                    if (!string.Equals(
                            ReadString(feature, "fruit_tree_projection_status"),
                            "exact_from_native_fruit_tree_performUseAction_and_shake",
                            StringComparison.Ordinal))
                    {
                        blockReasons.Add("fruit_tree_projection_incomplete");
                    }
                    if (outputs.Length == 0 || total <= 0 || outputs.Any(output =>
                            string.IsNullOrWhiteSpace(ReadString(output, "qualified_item_id")) ||
                            ReadInt(output, "quantity") <= 0))
                    {
                        blockReasons.Add("fruit_tree_output_projection_incomplete");
                    }
                    if (ReadInt(feature, "fruit_count") <= 0 ||
                        ReadInt(feature, "fruit_tree_expected_fruit_count_after") != 0 ||
                        ReadInt(feature, "fruit_tree_expected_foraging_experience_delta") != 0)
                    {
                        blockReasons.Add("fruit_tree_native_postcondition_projection_incomplete");
                    }
                    if (interaction is null)
                    {
                        blockReasons.Add("fruit_tree_no_reachable_adjacent_interaction");
                    }

                    var parameters = interaction is null
                        ? Array.Empty<SmallModelActionParameter>()
                        : FruitTreeParameters(feature, locationId, interaction);
                    if (parameters.Length > 0)
                    {
                        blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "executor.harvest_fruit_tree",
                            Parameters = parameters
                        }));
                    }

                    var distance = interaction is null
                        ? 0
                        : Math.Abs(playerX - interaction.Stand.X) + Math.Abs(playerY - interaction.Stand.Y);
                    var firstOutputId = outputs.Length == 0 ? string.Empty : ReadString(outputs[0], "qualified_item_id");
                    return new EventCandidate
                    {
                        CandidateId = "harvest-fruit-tree:" + locationId + ":" + x + "," + y + ":" + ReadString(feature, "fruit_tree_id"),
                        Kind = "harvest_fruit_tree",
                        Available = blockReasons.Count == 0,
                        LocationId = locationId,
                        TileX = x,
                        TileY = y,
                        ItemId = UnqualifiedObjectId(firstOutputId),
                        QualifiedItemId = firstOutputId,
                        Quantity = total,
                        ExpectedEffect = FruitTreeExpectedEffect(feature, interaction),
                        EstimatedTicks = Math.Max(45, distance * 60 + 45),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_native_fruit_tree_shake_harvest",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = parameters
                    };
                })
                .OrderBy(candidate => candidate.EstimatedTicks)
                .ThenBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ToArray();
        }

        private static SmallModelActionParameter[] FruitTreeParameters(
            JsonElement feature,
            string locationId,
            TerrainInteraction interaction)
        {
            return new[]
            {
                Parameter("target_location", locationId),
                Parameter("target_tile_x", ReadInt(feature, "tile_x").ToString()),
                Parameter("target_tile_y", ReadInt(feature, "tile_y").ToString()),
                Parameter("interaction_tile_x", interaction.Action.X.ToString()),
                Parameter("interaction_tile_y", interaction.Action.Y.ToString()),
                Parameter("stand_tile_x", interaction.Stand.X.ToString()),
                Parameter("stand_tile_y", interaction.Stand.Y.ToString()),
                Parameter("target_runtime_type", ReadString(feature, "runtime_type")),
                Parameter("fruit_tree_id", ReadString(feature, "fruit_tree_id")),
                Parameter("expected_fruit_count_before", ReadInt(feature, "fruit_count").ToString()),
                Parameter("expected_fruit_count_after", ReadInt(feature, "fruit_tree_expected_fruit_count_after").ToString()),
                Parameter("expected_output_items_json", JsonSerializer.Serialize(ReadArray(feature, "fruit_tree_expected_outputs"))),
                Parameter("expected_foraging_experience_delta", ReadInt(feature, "fruit_tree_expected_foraging_experience_delta").ToString()),
                Parameter("fruit_tree_projection_status", ReadString(feature, "fruit_tree_projection_status")),
                Parameter("fruit_tree_native_contract", ReadString(feature, "fruit_tree_native_contract")),
                Parameter("max_movement_tiles", "512")
            };
        }

        private static string FruitTreeExpectedEffect(JsonElement feature, TerrainInteraction? interaction)
        {
            var x = ReadInt(feature, "tile_x");
            var y = ReadInt(feature, "tile_y");
            return (interaction is null
                    ? string.Empty
                    : "fruit_tree_stand_tile=" + interaction.Stand.X + "," + interaction.Stand.Y +
                      ";fruit_tree_interaction_tile=" + interaction.Action.X + "," + interaction.Action.Y + ";") +
                "current_location.terrain_features[" + x + "," + y + "].fruit_count=0" +
                ";fruit_tree_id=" + ReadString(feature, "fruit_tree_id") +
                ";expected_fruit_count_before=" + ReadInt(feature, "fruit_count") +
                ";expected_fruit_count_after=0" +
                ";expected_output_items_json=" + JsonSerializer.Serialize(ReadArray(feature, "fruit_tree_expected_outputs")) +
                ";expected_foraging_experience_delta=0" +
                ";fruit_tree_projection_status=" + ReadString(feature, "fruit_tree_projection_status") +
                ";max_movement_tiles=512";
        }

        private static JsonElement[] ReadArray(JsonElement parent, string propertyName)
        {
            return parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
        }
    }
}
