using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

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
