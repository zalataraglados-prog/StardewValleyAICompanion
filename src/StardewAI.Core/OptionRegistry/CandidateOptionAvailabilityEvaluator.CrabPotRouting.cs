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
    private EventCandidate[] CrabPotRemoteServiceCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!TryReadCompleteCrabPotNetwork(snapshot, out var networkRows))
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var hasExactMissing =
            MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) &&
            missing.Count > 0;
        var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue);
        return networkRows
            .Where(row =>
                ReadBool(row, "exact_base_crab_pot") == true &&
                ReadBool(row, "production_domain_complete") == true &&
                !string.Equals(
                    ReadString(row, "location_id"),
                    currentLocation,
                    StringComparison.OrdinalIgnoreCase) &&
                ReadString(row, "service_status") is
                    "ready_for_collection" or "bait_required")
            .Select(row =>
            {
                var possible = ReadCrabPotStringArray(
                    row,
                    "possible_qualified_item_ids");
                var directTarget =
                    ReadBool(
                        row,
                        "current_output_collection_eligible") == true &&
                    missing.Contains(ReadString(
                        row,
                        "current_output_qualified_item_id"));
                var targets = hasExactMissing
                    ? possible
                        .Where(missing.Contains)
                        .Append(directTarget
                            ? ReadString(
                                row,
                                "current_output_qualified_item_id")
                            : string.Empty)
                        .Where(value => value.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray()
                    : possible;
                return (Row: row, Possible: possible, Targets: targets);
            })
            .Where(entry => !hasExactMissing || entry.Targets.Length > 0)
            .Select(entry => BuildCrabPotRouteCandidate(
                snapshot,
                routeCandidates,
                purpose: "service_existing_crab_pot",
                targetLocation: ReadString(entry.Row, "location_id"),
                targetX: ReadInt(entry.Row, "tile_x"),
                targetY: ReadInt(entry.Row, "tile_y"),
                productionSignature: ReadString(
                    entry.Row,
                    "production_signature"),
                possibleSpecies: entry.Possible,
                targetSpecies: entry.Targets,
                serviceStatus: ReadString(
                    entry.Row,
                    "service_status")))
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private EventCandidate[] CrabPotRemotePlacementRouteCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) ||
            missing.Count == 0 ||
            !TryReadCompleteCrabPotNetwork(snapshot, out var networkRows))
        {
            return Array.Empty<EventCandidate>();
        }
        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(placement.Value, "projection_status"),
                "complete_inventory_crab_pots_across_loaded_persistent_locations",
                StringComparison.Ordinal) ||
            !placement.Value.TryGetProperty("rows", out var inventoryRows) ||
            inventoryRows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var capacitySatisfied = CrabPotNetworkCapacitySatisfiedSpecies(
            snapshot,
            networkRows,
            missing);
        var uncovered = missing
            .Where(value => !capacitySatisfied.Contains(value))
            .ToHashSet(StringComparer.Ordinal);
        if (uncovered.Count == 0)
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue);
        var routes = new List<EventCandidate>();
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

            foreach (var location in locations.EnumerateArray().Where(row =>
                         row.ValueKind == JsonValueKind.Object &&
                         !string.Equals(
                             ReadString(row, "location_id"),
                             currentLocation,
                             StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(
                             ReadString(row, "placement_probe_status"),
                             "native_legal_water_tiles_available",
                             StringComparison.Ordinal) &&
                         row.TryGetProperty(
                             "static_legal_tile_ranges",
                             out var ranges) &&
                         ranges.ValueKind == JsonValueKind.Array))
            {
                foreach (var range in location
                             .GetProperty("static_legal_tile_ranges")
                             .EnumerateArray())
                {
                    var possible = CrabPotRangePossibleSpecies(range);
                    var targets = possible
                        .Where(uncovered.Contains)
                        .Where(value => CrabPotAdditionalCapacityCanAdvance(
                            snapshot,
                            networkRows,
                            range,
                            value))
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray();
                    if (targets.Length == 0)
                        continue;

                    routes.Add(BuildCrabPotRouteCandidate(
                        snapshot,
                        routeCandidates,
                        purpose: "place_for_missing_species",
                        targetLocation: ReadString(location, "location_id"),
                        targetX: null,
                        targetY: null,
                        productionSignature: ReadString(
                            range,
                            "production_signature"),
                        possibleSpecies: possible,
                        targetSpecies: targets,
                        serviceStatus: "placement_required"));
                }
            }
        }

        return routes
            .GroupBy(candidate =>
                ReadParameter(candidate.Parameters, "crab_pot_route_identity"),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Available)
                .ThenBy(candidate => candidate.EstimatedTicks)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .First())
            .ToArray();
    }

    private EventCandidate BuildCrabPotRouteCandidate(
        SnapshotEnvelope snapshot,
        EventCandidate[] routeCandidates,
        string purpose,
        string targetLocation,
        int? targetX,
        int? targetY,
        string productionSignature,
        string[] possibleSpecies,
        string[] targetSpecies,
        string serviceStatus)
    {
        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var identity = purpose + "|" + targetLocation + "|" +
            productionSignature;
        var routePlan = FindResolvedRoutePlan(
            snapshot,
            currentLocation,
            targetLocation,
            routeCandidates);
        var route = routePlan?.FirstActionCandidate;
        var parameters = new[]
        {
            Parameter("continuation.option_id", "fishing.collect_crab_pots"),
            Parameter("continuation.crab_pot_route_purpose", purpose),
            Parameter(
                "continuation.crab_pot_target_location",
                targetLocation),
            Parameter(
                "continuation.crab_pot_production_signature",
                productionSignature),
            Parameter("crab_pot_route_identity", identity),
            Parameter("crab_pot_route_purpose", purpose),
            Parameter("crab_pot_target_location", targetLocation),
            Parameter(
                "crab_pot_target_tile_x",
                targetX?.ToString() ?? string.Empty),
            Parameter(
                "crab_pot_target_tile_y",
                targetY?.ToString() ?? string.Empty),
            Parameter(
                "crab_pot_production_signature",
                productionSignature),
            Parameter("crab_pot_remote_service_status", serviceStatus),
            Parameter(
                "possible_qualified_item_ids_json",
                JsonSerializer.Serialize(possibleSpecies)),
            Parameter("outcome_distribution_complete", "true"),
            Parameter(
                "master_angler_target_qualified_item_ids_json",
                JsonSerializer.Serialize(targetSpecies))
        };
        if (route is null)
        {
            return new EventCandidate
            {
                CandidateId = "crab-pot-route-blocked:" + identity,
                Kind = "route_connector_tile",
                Available = false,
                LocationId = targetLocation,
                TileX = targetX,
                TileY = targetY,
                ExpectedEffect = "crab_pot_route_unavailable=true",
                AvailabilityClass =
                    "transparent_crab_pot_remote_route_blocked",
                BlockReasons = new[]
                {
                    "crab_pot_cross_location_route_unavailable"
                },
                Parameters = parameters
            };
        }

        return CloneCandidate(
            route,
            candidateId: "crab-pot-route:" + identity + ":" +
                route.CandidateId,
            expectedEffect: route.ExpectedEffect +
                ";crab_pot_route_purpose=" + purpose +
                ";crab_pot_target_location=" + targetLocation +
                ";crab_pot_rebind_after_arrival=true",
            parameters: route.Parameters.Concat(parameters).ToArray(),
            availabilityClass: "transparent_crab_pot_remote_route",
            allowedNow: route.AllowedNow ?? route.Available,
            allowedToday: route.AllowedToday ?? route.Available);
    }

    private static bool TryReadCompleteCrabPotNetwork(
        SnapshotEnvelope snapshot,
        out JsonElement[] rows)
    {
        rows = Array.Empty<JsonElement>();
        var network = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_network");
        if (!network.HasValue ||
            network.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(network.Value, "projection_status"),
                "complete_crab_pots_across_loaded_persistent_locations",
                StringComparison.Ordinal) ||
            !network.Value.TryGetProperty("rows", out var rowsElement) ||
            rowsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        rows = rowsElement.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .ToArray();
        return true;
    }
}
