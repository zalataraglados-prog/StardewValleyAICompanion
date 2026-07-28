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
        private EventCandidate[] BushHarvestCandidates(SnapshotEnvelope snapshot)
        {
            var features = ReadStateFieldValue(snapshot, "current_location", "large_terrain_features");
            if (!features.HasValue || features.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return features.Value.EnumerateArray()
                .Where(feature => feature.ValueKind == JsonValueKind.Object && ReadBool(feature, "is_bush") == true)
                .Select(feature =>
                {
                    var x = ReadInt(feature, "tile_x");
                    var y = ReadInt(feature, "tile_y");
                    var width = Math.Max(1, ReadInt(feature, "bounding_tile_width"));
                    var interaction = FindBestBushInteraction(snapshot, x, y, width);
                    var outputId = ReadString(feature, "bush_output_qualified_item_id");
                    var quantity = ReadInt(feature, "bush_output_quantity_min");
                    var status = ReadString(feature, "bush_harvest_status");
                    var blockReasons = new List<string>();
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "bush_projection_unavailable" : status);
                    }
                    if (!string.Equals(ReadString(feature, "bush_projection_status"), "exact_from_native_bush_shake", StringComparison.Ordinal))
                    {
                        blockReasons.Add("bush_projection_incomplete");
                    }
                    if (string.IsNullOrWhiteSpace(outputId) || quantity <= 0 ||
                        ReadInt(feature, "bush_output_quantity_max") != quantity)
                    {
                        blockReasons.Add("bush_output_projection_incomplete");
                    }
                    if (interaction is null)
                    {
                        blockReasons.Add("bush_no_reachable_perimeter_interaction");
                    }

                    var parameters = interaction is null
                        ? Array.Empty<SmallModelActionParameter>()
                        : BushParameters(feature, locationId, interaction);
                    if (parameters.Length > 0)
                    {
                        blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "executor.harvest_bush",
                            Parameters = parameters
                        }));
                    }

                    var distance = interaction is null
                        ? 0
                        : Math.Abs(playerX - interaction.Stand.X) + Math.Abs(playerY - interaction.Stand.Y);
                    return new EventCandidate
                    {
                        CandidateId = "harvest-bush:" + locationId + ":" + x + "," + y + ":" + ReadString(feature, "bush_kind"),
                        Kind = "harvest_bush",
                        Available = blockReasons.Count == 0,
                        LocationId = locationId,
                        TileX = x,
                        TileY = y,
                        ItemId = UnqualifiedObjectId(outputId),
                        QualifiedItemId = outputId,
                        Quantity = quantity,
                        ExpectedEffect = BushExpectedEffect(feature, interaction),
                        EstimatedTicks = Math.Max(45, distance * 60 + 45),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_native_bush_shake_harvest",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = parameters
                    };
                })
                .OrderBy(candidate => candidate.EstimatedTicks)
                .ThenBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ToArray();
        }

        private static BushInteraction? FindBestBushInteraction(SnapshotEnvelope snapshot, int anchorX, int anchorY, int width)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var candidates = new List<BushInteraction>();
            for (var actionX = anchorX; actionX < anchorX + width; actionX++)
            {
                var action = new CandidateTile(actionX, anchorY);
                foreach (var stand in new[]
                {
                    new CandidateTile(actionX, anchorY - 1),
                    new CandidateTile(actionX, anchorY + 1),
                    new CandidateTile(actionX - 1, anchorY),
                    new CandidateTile(actionX + 1, anchorY)
                })
                {
                    var insideFootprint = stand.Y == anchorY && stand.X >= anchorX && stand.X < anchorX + width;
                    if (!insideFootprint && !CollisionGridBlocksTile(snapshot, stand.X, stand.Y))
                    {
                        candidates.Add(new BushInteraction(action, stand));
                    }
                }
            }

            return candidates
                .GroupBy(candidate => candidate.Stand.X + "," + candidate.Stand.Y + ":" + candidate.Action.X + "," + candidate.Action.Y, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => Math.Abs(playerX - candidate.Stand.X) + Math.Abs(playerY - candidate.Stand.Y))
                .FirstOrDefault();
        }

        private static SmallModelActionParameter[] BushParameters(JsonElement feature, string locationId, BushInteraction interaction)
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
                Parameter("bush_size", ReadInt(feature, "bush_size").ToString()),
                Parameter("bush_kind", ReadString(feature, "bush_kind")),
                Parameter("qualified_item_id", ReadString(feature, "bush_output_qualified_item_id")),
                Parameter(
                    "bush_output_context_tags_json",
                    JsonSerializer.Serialize(ReadStringArray(feature, "bush_output_context_tags"))),
                Parameter("quantity", ReadInt(feature, "bush_output_quantity_min").ToString()),
                Parameter("expected_output_quality", ReadInt(feature, "bush_output_quality").ToString()),
                Parameter("expected_foraging_experience_delta", ReadInt(feature, "bush_foraging_experience_on_success_min").ToString()),
                Parameter("expected_tile_sheet_offset_after", ReadInt(feature, "tile_sheet_offset_expected_after").ToString()),
                Parameter("bush_nut_key", ReadString(feature, "bush_nut_key")),
                Parameter("bush_nut_collected_before", (ReadBool(feature, "bush_nut_collected_before") == true).ToString().ToLowerInvariant()),
                Parameter("bush_nut_collected_expected_after", (ReadBool(feature, "bush_nut_collected_expected_after") == true).ToString().ToLowerInvariant()),
                Parameter("bush_projection_status", ReadString(feature, "bush_projection_status")),
                Parameter("max_movement_tiles", "512")
            };
        }

        private static string BushExpectedEffect(JsonElement feature, BushInteraction? interaction)
        {
            var x = ReadInt(feature, "tile_x");
            var y = ReadInt(feature, "tile_y");
            return (interaction is null
                    ? string.Empty
                    : "bush_stand_tile=" + interaction.Stand.X + "," + interaction.Stand.Y +
                      ";bush_interaction_tile=" + interaction.Action.X + "," + interaction.Action.Y + ";") +
                "current_location.large_terrain_features[" + x + "," + y + "].tile_sheet_offset=0" +
                ";bush_kind=" + ReadString(feature, "bush_kind") +
                ";qualified_item_id=" + ReadString(feature, "bush_output_qualified_item_id") +
                ";quantity=" + ReadInt(feature, "bush_output_quantity_min") +
                ";expected_output_quality=" + ReadInt(feature, "bush_output_quality") +
                ";expected_foraging_experience_delta=" + ReadInt(feature, "bush_foraging_experience_on_success_min") +
                ";bush_nut_key=" + ReadString(feature, "bush_nut_key") +
                ";bush_nut_collected_expected_after=" + (ReadBool(feature, "bush_nut_collected_expected_after") == true).ToString().ToLowerInvariant() +
                ";bush_projection_status=" + ReadString(feature, "bush_projection_status") +
                ";max_movement_tiles=512";
        }

        private static string UnqualifiedObjectId(string qualifiedItemId)
        {
            return qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) ? qualifiedItemId[3..] : qualifiedItemId;
        }

        private sealed class BushInteraction
        {
            public BushInteraction(CandidateTile action, CandidateTile stand)
            {
                Action = action;
                Stand = stand;
            }

            public CandidateTile Action { get; }
            public CandidateTile Stand { get; }
        }
    }
}
