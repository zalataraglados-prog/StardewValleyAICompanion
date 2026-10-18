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
        private EventCandidate[] TillSoilCandidates(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(
                snapshot,
                "current_location",
                "site_preparation_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(context.Value, "projection_status"),
                    "complete_bounded_current_farm_region",
                    StringComparison.Ordinal) ||
                !context.Value.TryGetProperty("tiles", out var tiles) ||
                tiles.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadString(context.Value, "location_id");
            var hoeSlot = NullableReadInt(context.Value, "hoe_tool_slot_index");
            var hoeQualifiedItemId = ReadString(
                context.Value,
                "hoe_tool_qualified_item_id");
            var energyCost = ReadSitePreparationDouble(
                context.Value,
                "hoe_energy_cost_per_single_tile_action");
            var currentEnergy = ReadStateFieldDoubleOptional(
                snapshot,
                "player",
                "energy");
            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            var candidates = new List<EventCandidate>();
            foreach (var tile in tiles.EnumerateArray().Where(row =>
                         row.ValueKind == JsonValueKind.Object &&
                         string.Equals(
                             ReadString(row, "status"),
                             "ready_to_till",
                             StringComparison.Ordinal)))
            {
                var x = ReadInt(tile, "tile_x");
                var y = ReadInt(tile, "tile_y");
                var stand = FindBestStandTile(snapshot, x, y);
                var parameters = new List<SmallModelActionParameter>
                {
                    Parameter("target_location", locationId),
                    Parameter("target_tile_x", x.ToString()),
                    Parameter("target_tile_y", y.ToString()),
                    Parameter("required_tool_kind", "hoe"),
                    Parameter("tool_slot_index", hoeSlot?.ToString() ?? string.Empty),
                    Parameter("site_preparation_status", "ready_to_till"),
                    Parameter("fresh_snapshot_rebind_required", "true")
                };
                if (stand is not null)
                {
                    parameters.Add(Parameter("stand_tile_x", stand.X.ToString()));
                    parameters.Add(Parameter("stand_tile_y", stand.Y.ToString()));
                    parameters.Add(Parameter("route_distance_tiles", stand.Distance.ToString()));
                }

                var blockReasons = CompilerProbeBlockingReasons(
                    snapshot,
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.till_soil",
                        Parameters = parameters.ToArray()
                    }).ToList();
                if (!hoeSlot.HasValue || string.IsNullOrWhiteSpace(hoeQualifiedItemId))
                    blockReasons.Add("till_soil_required_hoe_missing");
                if (stand is null)
                    blockReasons.Add("till_soil_no_adjacent_route_stand_tile");
                if (!currentEnergy.HasValue)
                    blockReasons.Add("till_soil_player_energy_unavailable");
                else if (currentEnergy.Value <= 0d)
                    blockReasons.Add("till_soil_insufficient_stamina");

                var estimatedTicks = Math.Max(
                    60,
                    (stand?.Distance ?? 0) * 60 + 60);
                if (currentTime > 0 &&
                    WouldFinishAfterClock(currentTime, estimatedTicks, 2600))
                {
                    blockReasons.Add("till_soil_would_exceed_day_time_budget");
                }

                candidates.Add(new EventCandidate
                {
                    CandidateId = "till:" + locationId + ":" + x + "," + y,
                    Kind = "till_soil_tile",
                    Available = blockReasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect =
                        "current_location.terrain_features[" + x + "," + y + "].type=HoeDirt;" +
                        "native_tool=Hoe;fresh_snapshot_rebind_required=true",
                    EstimatedTicks = estimatedTicks,
                    EnergyCost = Math.Max(0, (int)Math.Ceiling(energyCost ?? 0d)),
                    AvailabilityClass = "bounded_farm_site_preparation",
                    BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters.ToArray()
                });
            }

            return candidates
                .OrderBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .Take(1024)
                .ToArray();
        }

        private static double? ReadSitePreparationDouble(
            JsonElement source,
            string property)
        {
            return source.ValueKind == JsonValueKind.Object &&
                source.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var parsed)
                    ? parsed
                    : null;
        }
    }
}
