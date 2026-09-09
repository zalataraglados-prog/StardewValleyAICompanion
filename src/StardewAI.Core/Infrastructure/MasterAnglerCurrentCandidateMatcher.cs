using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure
{
    public sealed class MasterAnglerCurrentCandidateMatch
    {
        public MasterAnglerWindowIntentValidation Intent { get; init; } = new();

        public string IdentityEvidence { get; init; } = string.Empty;
    }

    public static class MasterAnglerCurrentCandidateMatcher
    {
        public static bool TryMatch(
            SnapshotEnvelope snapshot,
            PolicyEventCandidatePrediction candidate,
            out MasterAnglerCurrentCandidateMatch match,
            out string rejectionReason)
        {
            match = new MasterAnglerCurrentCandidateMatch();
            if (!MasterAnglerWindowIntentValidator.TryValidate(
                    snapshot,
                    candidate.Parameters,
                    out var intent,
                    out rejectionReason))
            {
                return false;
            }

            string evidence;
            switch (candidate.Kind)
            {
                case "route_connector_tile":
                case "clear_obstacle_tile":
                    if (!HasRuntimeBindingProvenance(
                            candidate,
                            "master_angler_rolling_route",
                            "fishing.catch_fish"))
                    {
                        rejectionReason =
                            "master_angler_rolling_route_candidate_provenance_invalid";
                        return false;
                    }
                    evidence = "validated_master_angler_window_intent+rolling_route_step";
                    break;
                case "catch_fish":
                    if (!HasRuntimeBindingProvenance(
                            candidate,
                            "master_angler_exact_runtime_attempt",
                            "fishing.catch_fish"))
                    {
                        rejectionReason =
                            "master_angler_runtime_attempt_candidate_provenance_invalid";
                        return false;
                    }
                    if (!TryReadCompletePossibleFish(candidate, out var possible) ||
                        !possible.Contains(intent.TargetQualifiedItemId))
                    {
                        rejectionReason =
                            "master_angler_runtime_attempt_does_not_include_intent_target";
                        return false;
                    }
                    evidence =
                        "validated_master_angler_window_intent+complete_runtime_fish_outcome_domain";
                    break;
                case "collect_crab_pot":
                    if (!HasRuntimeBindingProvenance(
                            candidate,
                            "master_angler_exact_ready_crab_pot",
                            "fishing.collect_crab_pots"))
                    {
                        rejectionReason =
                            "master_angler_crab_pot_candidate_provenance_invalid";
                        return false;
                    }
                    if (!string.Equals(
                            candidate.QualifiedItemId,
                            intent.TargetQualifiedItemId,
                            StringComparison.Ordinal) ||
                        !TryReadUniqueParameter(
                            candidate,
                            "expected_fish_collection_eligible",
                            out var eligible) ||
                        eligible != "1")
                    {
                        rejectionReason =
                            "master_angler_crab_pot_candidate_is_not_exact_collection_eligible_target";
                        return false;
                    }
                    evidence =
                        "validated_master_angler_window_intent+exact_collection_eligible_crab_pot_output";
                    break;
                default:
                    rejectionReason =
                        "master_angler_window_intent_candidate_kind_invalid";
                    return false;
            }

            match = new MasterAnglerCurrentCandidateMatch
            {
                Intent = intent,
                IdentityEvidence = evidence
            };
            rejectionReason = string.Empty;
            return true;
        }

        public static bool TryReadCompletePossibleFish(
            PolicyEventCandidatePrediction candidate,
            out HashSet<string> possible)
        {
            possible = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                possible.Add(candidate.QualifiedItemId);
                return true;
            }
            if (!string.IsNullOrWhiteSpace(candidate.ItemId))
            {
                possible.Add(candidate.ItemId.StartsWith("(O)", StringComparison.Ordinal)
                    ? candidate.ItemId
                    : "(O)" + candidate.ItemId);
            }
            if (TryReadUniqueParameter(
                    candidate,
                    "expected_qualified_item_id",
                    out var expected))
            {
                possible.Add(expected);
            }

            var hasCompleteDistribution =
                TryReadUniqueParameter(
                    candidate,
                    "outcome_distribution_complete",
                    out var completeText) &&
                bool.TryParse(completeText, out var complete) && complete;
            if (TryReadUniqueParameter(
                    candidate,
                    "possible_qualified_item_ids_json",
                    out var json))
            {
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind != JsonValueKind.Array)
                        return false;
                    foreach (var value in document.RootElement.EnumerateArray())
                    {
                        var qualifiedItemId = value.ValueKind == JsonValueKind.String
                            ? value.GetString()
                            : null;
                        if (string.IsNullOrWhiteSpace(qualifiedItemId))
                            return false;
                        possible.Add(qualifiedItemId);
                    }
                }
                catch (JsonException)
                {
                    return false;
                }
            }
            return possible.Count > 0 && hasCompleteDistribution;
        }

        private static bool TryReadUniqueParameter(
            PolicyEventCandidatePrediction candidate,
            string name,
            out string value)
        {
            var matches = (candidate.Parameters ?? Array.Empty<SmallModelActionParameter>())
                .Where(parameter => string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.Ordinal))
                .Select(parameter => parameter.Value)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            value = matches.Length == 1 ? matches[0] : string.Empty;
            return matches.Length == 1;
        }

        private static bool HasRuntimeBindingProvenance(
            PolicyEventCandidatePrediction candidate,
            string expectedAvailabilityClass,
            string expectedContinuationOptionId) =>
            string.Equals(
                candidate.AvailabilityClass,
                expectedAvailabilityClass,
                StringComparison.Ordinal) &&
            TryReadUniqueParameter(
                candidate,
                "continuation.option_id",
                out var continuationOptionId) &&
            string.Equals(
                continuationOptionId,
                expectedContinuationOptionId,
                StringComparison.Ordinal) &&
            TryReadUniqueParameter(
                candidate,
                "master_angler_time_budget_status",
                out var timeBudgetStatus) &&
            string.Equals(
                timeBudgetStatus,
                "conservative_full_remaining_connector_path_and_terminal_reserve",
                StringComparison.Ordinal);
    }
}
