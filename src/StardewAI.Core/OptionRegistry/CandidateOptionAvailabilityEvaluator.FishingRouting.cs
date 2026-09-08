using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static readonly string[] MasterAnglerIntentParameterNames =
        {
            "master_angler_target_qualified_item_id",
            "master_angler_target_location",
            "master_angler_source_kind",
            "master_angler_source_key",
            "master_angler_window_first_total_day",
            "master_angler_window_last_total_day",
            "master_angler_target_total_day",
            "master_angler_effective_start_time",
            "master_angler_last_cast_time_exclusive",
            "master_angler_stage_one_deadline_total_day_exclusive",
            "master_angler_window_index_path",
            "master_angler_window_index_sha256",
            "master_angler_validation_status",
            "master_angler_runtime_terminal_validation_required",
            MasterAnglerRouteTimingValidator.CalibrationPathParameter,
            MasterAnglerRouteTimingValidator.CalibrationSha256Parameter,
            MasterAnglerRouteTimingValidator.ValidationRequiredParameter
        };

        private EventCandidate[] FishingCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] boundParameters)
        {
            if (!HasMasterAnglerIntentParameters(boundParameters))
            {
                return FishingEventCandidateBuilder.Build(snapshot);
            }

            if (!TryNormalizeMasterAnglerIntent(
                    snapshot,
                    boundParameters,
                    out var intentParameters,
                    out var normalizationReason))
            {
                return new[]
                {
                    BlockedMasterAnglerCandidate(
                        normalizationReason,
                        intentParameters)
                };
            }
            if (!MasterAnglerWindowIntentValidator.TryValidate(
                    snapshot,
                    intentParameters,
                    out var intent,
                    out var validationReason))
            {
                return new[]
                {
                    BlockedMasterAnglerCandidate(
                        validationReason,
                        intentParameters)
                };
            }

            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            var continuation = MasterAnglerContinuationParameters(
                "fishing.catch_fish",
                intentParameters);
            if (!string.Equals(
                    currentLocation,
                    intent.TargetLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                var routePlan = FindResolvedRoutePlan(
                    snapshot,
                    currentLocation,
                    intent.TargetLocation,
                    RouteConnectorCandidates(snapshot, int.MaxValue));
                var route = routePlan?.FirstActionCandidate;
                if (route is null)
                {
                    return new[]
                    {
                        BlockedMasterAnglerCandidate(
                            "master_angler_cross_location_route_unavailable",
                            intentParameters)
                    };
                }

                var routed = CloneCandidate(
                        route,
                        candidateId: "master-angler:route:" +
                            intent.TargetQualifiedItemId + ":" +
                            currentLocation + ":" + route.CandidateId,
                        expectedEffect: route.ExpectedEffect +
                            ";master_angler_target_qualified_item_id=" +
                            intent.TargetQualifiedItemId +
                            ";master_angler_target_location=" +
                            intent.TargetLocation +
                            ";master_angler_runtime_terminal_validation_required=true",
                        parameters: route.Parameters
                            .Concat(intentParameters)
                            .Concat(continuation)
                            .ToArray(),
                        availabilityClass: "master_angler_rolling_route",
                        allowedNow: route.AllowedNow ?? route.Available,
                        allowedToday: route.AllowedToday ?? route.Available);
                return new[]
                {
                    ApplyMasterAnglerTimeBudget(
                        snapshot,
                        routed,
                        intent,
                        FishingEventCandidateBuilder.EstimatedCatchTicks)
                };
            }

            var exactAttempts = FishingEventCandidateBuilder.Build(snapshot)
                .Where(candidate =>
                    candidate.Available &&
                    string.Equals(
                        candidate.Kind,
                        "catch_fish",
                        StringComparison.Ordinal) &&
                    RuntimeFishingCandidateMatchesMasterAngler(
                        snapshot,
                        candidate,
                        intent))
                .Select(candidate => ApplyMasterAnglerTimeBudget(
                    snapshot,
                    CloneCandidate(
                        candidate,
                        candidateId: candidate.CandidateId +
                            ":master-angler:" + intent.TargetQualifiedItemId,
                        expectedEffect: candidate.ExpectedEffect +
                            ";master_angler_target_qualified_item_id=" +
                            intent.TargetQualifiedItemId +
                            ";master_angler_runtime_terminal_validation_required=true",
                        parameters: candidate.Parameters
                            .Concat(intentParameters)
                            .Concat(continuation)
                            .ToArray(),
                        availabilityClass: "master_angler_exact_runtime_attempt"),
                    intent,
                    terminalReserveTicks: 0))
                .ToArray();
            return exactAttempts.Length > 0
                ? exactAttempts
                : new[]
                {
                    BlockedMasterAnglerCandidate(
                        "master_angler_no_exact_runtime_source_attempt",
                        intentParameters)
                };
        }

        private static bool HasMasterAnglerIntentParameters(
            IEnumerable<SmallModelActionParameter> parameters)
        {
            return parameters.Any(parameter =>
                string.Equals(
                    parameter.Name,
                    "master_angler_target_qualified_item_id",
                    StringComparison.Ordinal) ||
                string.Equals(
                    parameter.Name,
                    "continuation.master_angler_target_qualified_item_id",
                    StringComparison.Ordinal));
        }

        private static EventCandidate ApplyMasterAnglerTimeBudget(
            SnapshotEnvelope snapshot,
            EventCandidate candidate,
            MasterAnglerWindowIntentValidation intent,
            int terminalReserveTicks)
        {
            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            var availableMinutes = GameClockBudgetPolicy.ClockMinutesBetween(
                currentTime,
                intent.LastCastTimeExclusive);
            var candidateMinutes = GameClockBudgetPolicy.TicksToGameMinutes(
                Math.Max(1, candidate.EstimatedTicks));
            var terminalReserveMinutes = terminalReserveTicks > 0
                ? GameClockBudgetPolicy.TicksToGameMinutes(
                    terminalReserveTicks)
                : 0;
            var requiredMinutes = Math.Max(
                    candidateMinutes,
                    intent.RouteTiming.RemainingRouteGameMinutes) +
                terminalReserveMinutes;
            var parameters = candidate.Parameters.Concat(new[]
            {
                Parameter(
                    "master_angler_time_budget_status",
                    "conservative_full_remaining_connector_path_and_terminal_reserve"),
                Parameter(
                    "master_angler_time_budget_available_minutes",
                    Math.Max(0, availableMinutes).ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "master_angler_time_budget_required_minutes",
                    requiredMinutes.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "master_angler_terminal_reserve_ticks",
                    Math.Max(0, terminalReserveTicks).ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "master_angler_full_route_timing_status",
                    intent.RouteTiming.Status),
                Parameter(
                    "master_angler_full_route_guaranteed_arrival_time",
                    intent.RouteTiming.GuaranteedArrivalByTime.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "master_angler_full_route_remaining_minutes",
                    intent.RouteTiming.RemainingRouteGameMinutes.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "master_angler_full_route_edge_count",
                    intent.RouteTiming.PathEdgeCount.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "master_angler_full_route_timing_evidence_id",
                    intent.RouteTiming.TimingEvidenceId)
            }).ToArray();
            if (currentTime < 600 ||
                currentTime > 2600 ||
                currentTime % 100 >= 60 ||
                availableMinutes < requiredMinutes)
            {
                return CloneCandidate(
                    candidate,
                    available: false,
                    availabilityClass: "master_angler_time_budget_blocked",
                    blockReasons: candidate.BlockReasons
                        .Append(
                            "master_angler_current_step_would_miss_window")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    parameters: parameters,
                    allowedNow: false,
                    allowedToday: false);
            }
            return CloneCandidate(candidate, parameters: parameters);
        }

        private static bool TryNormalizeMasterAnglerIntent(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] parameters,
            out SmallModelActionParameter[] normalized,
            out string rejectionReason)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in MasterAnglerIntentParameterNames)
            {
                var matches = parameters
                    .Where(parameter =>
                        string.Equals(parameter.Name, name, StringComparison.Ordinal) ||
                        string.Equals(
                            parameter.Name,
                            "continuation." + name,
                            StringComparison.Ordinal))
                    .Select(parameter => parameter.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (matches.Length != 1)
                {
                    normalized = Array.Empty<SmallModelActionParameter>();
                    rejectionReason =
                        "master_angler_window_intent_contract_incomplete_or_duplicated";
                    return false;
                }
                values[name] = matches[0];
            }

            if (!int.TryParse(
                    values["master_angler_effective_start_time"],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var effectiveStart))
            {
                normalized = Array.Empty<SmallModelActionParameter>();
                rejectionReason = "master_angler_effective_start_time_invalid";
                return false;
            }
            values["master_angler_effective_start_time"] = Math.Max(
                    effectiveStart,
                    ReadStateFieldInt(snapshot, "time", "time"))
                .ToString(CultureInfo.InvariantCulture);

            normalized = MasterAnglerIntentParameterNames
                .Select(name => Parameter(name, values[name]))
                .ToArray();
            rejectionReason = string.Empty;
            return true;
        }

        private static SmallModelActionParameter[] MasterAnglerContinuationParameters(
            string optionId,
            IEnumerable<SmallModelActionParameter> intentParameters)
        {
            return new[]
                {
                    Parameter("continuation.option_id", optionId)
                }
                .Concat(intentParameters.Select(parameter => Parameter(
                    "continuation." + parameter.Name,
                    parameter.Value)))
                .ToArray();
        }

    }
}
