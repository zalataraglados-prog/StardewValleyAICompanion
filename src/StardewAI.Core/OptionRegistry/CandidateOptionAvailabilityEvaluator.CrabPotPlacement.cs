using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] CrabPotPlacementCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) || missing.Count == 0)
        {
            return Array.Empty<EventCandidate>();
        }
        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_placement");
        if (!placement.HasValue || placement.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(placement.Value, "projection_status"),
                "complete_inventory_crab_pots_across_loaded_persistent_locations",
                StringComparison.Ordinal) ||
            !placement.Value.TryGetProperty("rows", out var inventoryRows) ||
            inventoryRows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        if (!TryReadCompleteCrabPotNetwork(snapshot, out var networkRows))
            return Array.Empty<EventCandidate>();
        var alreadyCovered = CrabPotNetworkCapacitySatisfiedSpecies(
            snapshot,
            networkRows,
            missing);
        var uncovered = missing
            .Where(value => !alreadyCovered.Contains(value))
            .ToHashSet(StringComparer.Ordinal);
        if (uncovered.Count == 0)
            return Array.Empty<EventCandidate>();

        var candidates = new List<EventCandidate>();
        foreach (var inventory in inventoryRows.EnumerateArray())
        {
            if (inventory.ValueKind != JsonValueKind.Object ||
                ReadString(inventory, "qualified_item_id") != "(O)710" ||
                ReadInt(inventory, "stack") <= 0 ||
                !inventory.TryGetProperty("locations", out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var location in locations.EnumerateArray().Where(value =>
                         value.ValueKind == JsonValueKind.Object &&
                         string.Equals(
                             ReadString(value, "location_id"),
                             currentLocation,
                             StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(
                             ReadString(value, "placement_probe_status"),
                             "native_legal_water_tiles_available",
                             StringComparison.Ordinal)))
            {
                if (!location.TryGetProperty(
                        "static_legal_tile_ranges",
                        out var ranges) || ranges.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var range in ranges.EnumerateArray())
                {
                    var possible = CrabPotRangePossibleSpecies(range);
                    var targetSpecies = possible
                        .Where(uncovered.Contains)
                        .Where(value => CrabPotAdditionalCapacityCanAdvance(
                            snapshot,
                            networkRows,
                            range,
                            value))
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray();
                    if (targetSpecies.Length == 0)
                        continue;

                    var y = ReadInt(range, "y", -1);
                    var startX = ReadInt(range, "start_x", -1);
                    var endX = ReadInt(range, "end_x", -1);
                    if (y < 0 || startX < 0 || endX < startX)
                        continue;
                    var tile = Enumerable.Range(
                            startX,
                            Math.Max(0, endX - startX + 1))
                        .Select(x => new
                        {
                            X = x,
                            Y = y,
                            Stand = FindBestStandTile(snapshot, x, y)
                        })
                        .Where(value => value.Stand is not null)
                        .OrderBy(value =>
                            Math.Abs(playerX - value.Stand!.X) +
                            Math.Abs(playerY - value.Stand.Y))
                        .FirstOrDefault();
                    if (tile?.Stand is null)
                        continue;

                    var parameters = new[]
                    {
                        Parameter("target_location", currentLocation),
                        Parameter("target_tile_x", tile.X.ToString()),
                        Parameter("target_tile_y", tile.Y.ToString()),
                        Parameter("stand_tile_x", tile.Stand.X.ToString()),
                        Parameter("stand_tile_y", tile.Stand.Y.ToString()),
                        Parameter(
                            "inventory_slot_index",
                            ReadInt(inventory, "inventory_slot_index").ToString()),
                        Parameter(
                            "inventory_stack_before",
                            ReadInt(inventory, "stack").ToString()),
                        Parameter("qualified_item_id", "(O)710"),
                        Parameter(
                            "expected_owner_player_id",
                            ReadInt64(placement.Value, "owner_player_id").ToString()),
                        Parameter(
                            "placement_projection_fingerprint",
                            ReadString(
                                placement.Value,
                                "static_projection_fingerprint")),
                        Parameter(
                            "production_signature",
                            ReadString(range, "production_signature")),
                        Parameter(
                            "native_contract",
                            ReadString(
                                placement.Value,
                                "native_runtime_contract")),
                        Parameter(
                            "crab_pot_placement_reason",
                            "master_angler_missing_trap_species_capacity"),
                        Parameter(
                            "possible_qualified_item_ids_json",
                            JsonSerializer.Serialize(possible)),
                        Parameter("outcome_distribution_complete", "true"),
                        Parameter(
                            "master_angler_target_qualified_item_ids_json",
                            JsonSerializer.Serialize(targetSpecies)),
                        Parameter(
                            "crab_pot_capacity_assessment_json",
                            CrabPotCapacityEvidenceJson(
                                snapshot,
                                networkRows,
                                range,
                                targetSpecies))
                    };
                    var blockReasons = CompilerProbeBlockingReasons(
                            snapshot,
                            new OptionAvailabilityCandidate
                            {
                                OptionId = "executor.place_crab_pot",
                                Parameters = parameters
                            })
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    var distance = Math.Abs(playerX - tile.Stand.X) +
                        Math.Abs(playerY - tile.Stand.Y);
                    candidates.Add(new EventCandidate
                    {
                        CandidateId = "place-crab-pot:" + currentLocation +
                            ":" + tile.X + "," + tile.Y + ":" +
                            ReadString(range, "production_signature"),
                        Kind = "place_crab_pot",
                        Available = blockReasons.Length == 0,
                        LocationId = currentLocation,
                        TileX = tile.X,
                        TileY = tile.Y,
                        ItemId = "710",
                        QualifiedItemId = string.Empty,
                        Quantity = 1,
                        ExpectedEffect =
                            "crab_pot_production_capacity_added=true;" +
                            "production_signature=" +
                            ReadString(range, "production_signature") +
                            ";master_angler_target_qualified_item_ids_json=" +
                            JsonSerializer.Serialize(targetSpecies),
                        EstimatedTicks = Math.Max(60, distance * 60 + 60),
                        EnergyCost = 0,
                        AvailabilityClass =
                            "transparent_crab_pot_native_placement",
                        BlockReasons = blockReasons,
                        Parameters = parameters
                    });
                }
            }
        }

        return candidates
            .GroupBy(candidate => ReadParameter(
                candidate.Parameters,
                "production_signature"), StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(candidate => candidate.EstimatedTicks)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .First())
            .ToArray();
    }
}
