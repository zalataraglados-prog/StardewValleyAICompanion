using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] RainTotemHardwoodReserveCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters,
        MasterAnglerRainTotemReserveIntentValidation intent)
    {
        EventCandidate[] candidates = intent.ReserveStage switch
        {
            "clear_loaded_farm_resource_clump" =>
                ClearFarmResourceClumpCandidates(snapshot)
                    .Where(candidate => HardwoodSourceMatches(candidate, intent))
                    .ToArray(),
            "chop_loaded_wild_tree" =>
                WildTreeChopAcquisitionCandidates(snapshot)
                    .Where(candidate => HardwoodSourceMatches(candidate, intent))
                    .ToArray(),
            "route_to_farm_resource_clump" =>
                RouteToHardwoodFarmSourceCandidates(snapshot, intent),
            "blocked_required_axe_upgrade_missing" or
                "blocked_no_exact_hardwood_source" =>
            [
                BlockedHardwoodReserveCandidate(
                    "master_angler_hardwood_reserve_" + intent.ReserveStage,
                    boundParameters,
                    intent.ReserveStage)
            ],
            _ => Array.Empty<EventCandidate>()
        };

        var bound = candidates
            .Select(candidate => BindHardwoodReserveCandidate(
                candidate,
                boundParameters,
                intent))
            .ToArray();
        return bound.Length > 0
            ? bound
            :
            [
                BlockedHardwoodReserveCandidate(
                    "master_angler_hardwood_reserve_no_exact_stage_candidate:" +
                        intent.ReserveStage,
                    boundParameters,
                    intent.ReserveStage)
            ];
    }

    private EventCandidate[] RouteToHardwoodFarmSourceCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerRainTotemReserveIntentValidation intent)
    {
        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var route = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                intent.HardwoodSourceLocationId,
                RouteConnectorCandidates(snapshot, int.MaxValue))
            ?.FirstActionCandidate;
        return route is null
            ? Array.Empty<EventCandidate>()
            :
            [
                CloneCandidate(
                    route,
                    candidateId: "rain-totem-hardwood-route:" +
                        currentLocation + ":" +
                        intent.HardwoodSourceLocationId + ":" +
                        route.CandidateId,
                    expectedEffect: route.ExpectedEffect +
                        ";hardwood_source_location=" +
                        intent.HardwoodSourceLocationId +
                        ";hardwood_source_tile=" +
                        intent.HardwoodSourceTileX.ToString(CultureInfo.InvariantCulture) +
                        "," +
                        intent.HardwoodSourceTileY.ToString(CultureInfo.InvariantCulture) +
                        ";fresh_snapshot_replan_required=true",
                    availabilityClass:
                        "master_angler_hardwood_source_route")
            ];
    }

    private static bool HardwoodSourceMatches(
        EventCandidate candidate,
        MasterAnglerRainTotemReserveIntentValidation intent) =>
        string.Equals(
            candidate.LocationId,
            intent.HardwoodSourceLocationId,
            StringComparison.OrdinalIgnoreCase) &&
        candidate.TileX == intent.HardwoodSourceTileX &&
        candidate.TileY == intent.HardwoodSourceTileY &&
        CandidateGuaranteesItem(candidate, "(O)709") &&
        CandidateGuaranteedItemQuantity(candidate, "(O)709") ==
            intent.HardwoodSourceGuaranteedQuantity;

    private static EventCandidate BindHardwoodReserveCandidate(
        EventCandidate candidate,
        SmallModelActionParameter[] boundParameters,
        MasterAnglerRainTotemReserveIntentValidation intent) =>
        CloneCandidate(
            candidate,
            candidateId: "rain-totem-hardwood-reserve:" +
                intent.ReserveStage + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";master_angler_weather_reserve_material=(O)709" +
                ";master_angler_hardwood_reserve_stage=" +
                intent.ReserveStage +
                ";master_angler_hardwood_guaranteed_quantity=" +
                intent.HardwoodSourceGuaranteedQuantity.ToString(
                    CultureInfo.InvariantCulture) +
                ";fresh_snapshot_and_weather_reserve_replan_required=true",
            parameters: candidate.Parameters
                .Concat(boundParameters)
                .ToArray(),
            availabilityClass:
                "master_angler_future_rain_window_hardwood_" +
                intent.ReserveStage);

    private static EventCandidate BlockedHardwoodReserveCandidate(
        string reason,
        SmallModelActionParameter[] parameters,
        string stage) => new()
        {
            CandidateId = "master-angler:rain-totem-hardwood-reserve:" +
                stage + ":blocked",
            Kind = "rain_totem_hardwood_reserve_wait",
            Available = false,
            QualifiedItemId = "(O)709",
            Quantity = 1,
            EstimatedTicks = -1,
            AvailabilityClass =
                "master_angler_hardwood_reserve_fail_closed",
            ExpectedEffect =
                "master_angler_hardwood_reserve_not_compiled;fresh_snapshot_required=true",
            BlockReasons = [reason],
            Parameters = parameters
        };
}
