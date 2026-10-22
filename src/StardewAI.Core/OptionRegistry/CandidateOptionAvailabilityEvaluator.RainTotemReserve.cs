using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] RainTotemReserveCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (!MasterAnglerWindowIntentValidator
                .TryValidateRainTotemReserveIntent(
                    snapshot,
                    boundParameters,
                    out var intent,
                    out var rejectionReason))
        {
            return
            [
                BlockedRainTotemReserveCandidate(
                    rejectionReason,
                    boundParameters)
            ];
        }

        if (string.Equals(
                intent.ReserveKind,
                "truffle_oil_capacity",
                StringComparison.Ordinal))
        {
            return RainTotemTruffleOilReserveCandidates(
                snapshot,
                boundParameters,
                commitmentLedger,
                intent);
        }
        if (string.Equals(
                intent.ReserveKind,
                "hardwood_reserve",
                StringComparison.Ordinal))
        {
            return RainTotemHardwoodReserveCandidates(
                snapshot,
                boundParameters,
                intent);
        }

        var request = new AcquisitionMachineCapacityRequest(
            "goal.fishing.complete_master_angler",
            "rain_totem_pine_tar:" +
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture),
            "native_wild_tree_tapper_output",
            "wild_tree:3:0",
            "(O)726",
            "(BC)105",
            "machine-support:master-angler:pine-tar:" +
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture) +
                ":remaining=" +
                intent.RequiredAdditionalTapperCount.ToString(
                    CultureInfo.InvariantCulture),
            string.Empty);
        var candidates = AcquisitionMachineCapacityCandidates(
                snapshot,
                commitmentLedger,
                request)
            .Select(candidate => CloneCandidate(
                candidate,
                candidateId: "rain-totem-pine-tar-reserve:" +
                    intent.TargetTotalDay.ToString(
                        CultureInfo.InvariantCulture) + ":" +
                    candidate.CandidateId,
                expectedEffect: candidate.ExpectedEffect +
                    ";master_angler_weather_reserve_material=(O)726" +
                    ";master_angler_weather_reserve_required_additional_tapper_count=" +
                    intent.RequiredAdditionalTapperCount.ToString(
                        CultureInfo.InvariantCulture) +
                    ";fresh_snapshot_and_weather_reserve_replan_required=true",
                parameters: candidate.Parameters
                    .Concat(boundParameters)
                    .ToArray(),
                availabilityClass:
                    "master_angler_future_rain_window_pine_tar_capacity_reserve"))
            .ToArray();
        return candidates.Length > 0
            ? candidates
            :
            [
                BlockedRainTotemReserveCandidate(
                    "master_angler_weather_reserve_no_exact_tapper_capacity_candidate",
                    boundParameters)
            ];
    }

    private EventCandidate[] RainTotemTruffleOilReserveCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters,
        StrategyCommitmentLedger? commitmentLedger,
        MasterAnglerRainTotemReserveIntentValidation intent)
    {
        EventCandidate[] candidates = intent.ReserveStage switch
        {
            "construct_barn" or "upgrade_big_barn" or
                "upgrade_deluxe_barn" => BuildingConstructionCandidates(
                    snapshot,
                    [
                        Parameter("building_type", intent.TargetBuildingType),
                        Parameter(
                            "placement_location_id",
                            intent.TargetBuildingLocationId),
                        Parameter(
                            "construction_reason",
                            "master_angler_truffle_oil_reserve")
                    ],
                    commitmentLedger),
            "purchase_pig" => AnimalPurchaseStageCandidates(
                snapshot,
                [
                    Parameter("animal_type_id", "Pig"),
                    Parameter(
                        "target_location_id",
                        intent.TargetBuildingLocationId),
                    Parameter(
                        "home_building_tile_x",
                        intent.TargetBuildingTileX.ToString(
                            CultureInfo.InvariantCulture)),
                    Parameter(
                        "home_building_tile_y",
                        intent.TargetBuildingTileY.ToString(
                            CultureInfo.InvariantCulture))
                ]),
            "open_pig_home_animal_door" => OpenAnimalDoorCandidates(
                snapshot,
                intent.TargetBuildingLocationId,
                intent.TargetBuildingType,
                intent.TargetBuildingTileX,
                intent.TargetBuildingTileY),
            "establish_oil_maker" => AcquisitionMachineCapacityCandidates(
                snapshot,
                commitmentLedger,
                new AcquisitionMachineCapacityRequest(
                    "goal.fishing.complete_master_angler",
                    "rain_totem_truffle_oil:" +
                        intent.TargetTotalDay.ToString(
                            CultureInfo.InvariantCulture),
                    "native_oil_maker_output",
                    "animal:Pig:truffle",
                    "(O)432",
                    "(BC)19",
                    "machine-support:master-angler:truffle-oil:" +
                        intent.TargetTotalDay.ToString(
                            CultureInfo.InvariantCulture),
                    string.Empty)),
            "collect_ready_truffle_oil" => MachineServiceCandidates(
                    snapshot,
                    commitmentLedger)
                .Where(candidate =>
                    candidate.Available &&
                    (candidate.Kind is "collect_machine_output_tile" or
                        "route_connector_tile") &&
                    string.Equals(
                        candidate.QualifiedItemId,
                        "(O)432",
                        StringComparison.Ordinal))
                .ToArray(),
            "load_truffle_into_oil_maker" => MachineServiceCandidates(
                    snapshot,
                    commitmentLedger)
                .Where(candidate =>
                    candidate.Available &&
                    string.Equals(
                        candidate.Kind,
                        "load_machine_input_tile",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.QualifiedItemId,
                        "(O)430",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "machine_qualified_item_id"),
                        "(BC)19",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "predicted_output_qualified_item_id"),
                        "(O)432",
                        StringComparison.Ordinal))
                .ToArray(),
            "collect_spawned_truffle" =>
                TruffleCollectionCandidates(snapshot),
            "wait_for_barn_upgrade" or "wait_for_pig_maturity" or
                "await_native_pig_truffle" =>
            [
                BlockedTruffleOilReserveCandidate(
                    "master_angler_truffle_oil_reserve_waiting:" +
                        intent.ReserveStage,
                    boundParameters,
                    intent.ReserveStage)
            ],
            _ => Array.Empty<EventCandidate>()
        };

        var bound = candidates
            .Select(candidate => BindTruffleOilReserveCandidate(
                candidate,
                boundParameters,
                intent))
            .ToArray();
        return bound.Length > 0
            ? bound
            :
            [
                BlockedTruffleOilReserveCandidate(
                    "master_angler_truffle_oil_reserve_no_exact_stage_candidate:" +
                        intent.ReserveStage,
                    boundParameters,
                    intent.ReserveStage)
            ];
    }

    private EventCandidate[] TruffleCollectionCandidates(
        SnapshotEnvelope snapshot)
    {
        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        if (string.Equals(
                currentLocation,
                "Farm",
                StringComparison.OrdinalIgnoreCase))
        {
            return SpawnedObjectForagingCandidates(snapshot)
                .Where(candidate =>
                    candidate.Available &&
                    string.Equals(
                        candidate.QualifiedItemId,
                        "(O)430",
                        StringComparison.Ordinal))
                .ToArray();
        }

        var route = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                "Farm",
                RouteConnectorCandidates(snapshot, int.MaxValue))
            ?.FirstActionCandidate;
        return route is null
            ? Array.Empty<EventCandidate>()
            :
            [
                CloneCandidate(
                    route,
                    candidateId: "rain-totem-truffle-route:" +
                        currentLocation + ":Farm:" + route.CandidateId,
                    expectedEffect: route.ExpectedEffect +
                        ";truffle_collection_location=Farm" +
                        ";fresh_snapshot_replan_required=true",
                    availabilityClass:
                        "master_angler_truffle_collection_route")
            ];
    }

    private static EventCandidate BindTruffleOilReserveCandidate(
        EventCandidate candidate,
        SmallModelActionParameter[] boundParameters,
        MasterAnglerRainTotemReserveIntentValidation intent) =>
        CloneCandidate(
            candidate,
            candidateId: "rain-totem-truffle-oil-reserve:" +
                intent.ReserveStage + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";master_angler_weather_reserve_material=(O)432" +
                ";master_angler_truffle_oil_reserve_stage=" +
                intent.ReserveStage +
                ";fresh_snapshot_and_weather_reserve_replan_required=true",
            parameters: candidate.Parameters
                .Concat(boundParameters)
                .ToArray(),
            availabilityClass:
                "master_angler_future_rain_window_truffle_oil_" +
                intent.ReserveStage);

    private static EventCandidate BlockedTruffleOilReserveCandidate(
        string reason,
        SmallModelActionParameter[] parameters,
        string stage) => new()
        {
            CandidateId = "master-angler:rain-totem-truffle-oil-reserve:" +
                stage + ":blocked",
            Kind = "rain_totem_truffle_oil_reserve_wait",
            Available = false,
            QualifiedItemId = "(O)432",
            Quantity = 1,
            EstimatedTicks = -1,
            AvailabilityClass =
                "master_angler_truffle_oil_reserve_fail_closed",
            ExpectedEffect =
                "master_angler_truffle_oil_reserve_not_compiled;fresh_snapshot_required=true",
            BlockReasons = [reason],
            Parameters = parameters
        };

    private static EventCandidate BlockedRainTotemReserveCandidate(
        string reason,
        SmallModelActionParameter[] parameters) => new()
        {
            CandidateId = "master-angler:rain-totem-pine-tar-reserve:blocked",
            Kind = "craft_machine_item",
            Available = false,
            QualifiedItemId = "(BC)105",
            Quantity = 1,
            EstimatedTicks = -1,
            AvailabilityClass =
                "master_angler_weather_reserve_fail_closed",
            ExpectedEffect =
                "master_angler_weather_reserve_not_compiled;fresh_snapshot_required=true",
            BlockReasons =
            [
                string.IsNullOrWhiteSpace(reason)
                    ? "master_angler_weather_reserve_validation_failed"
                    : reason
            ],
            Parameters = parameters
        };
}
