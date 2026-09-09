using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed partial class GrandpaDirectionDailyCandidateBinding
    {
        private static bool TryBuildMasterAnglerEvidence(
            SnapshotEnvelope snapshot,
            PolicyEventCandidatePrediction candidate,
            out SmallModelActionParameter[] evidence,
            out string rejectionReason)
        {
            evidence = Array.Empty<SmallModelActionParameter>();
            rejectionReason = string.Empty;
            var progress = ReadStateFieldValue(snapshot, "world_progress", "fish_collection_progress");
            if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
                !TryReadConsistentFishDenominator(progress.Value, out var denominator, out var missing))
            {
                rejectionReason = "master_angler_transparent_denominator_missing_or_inconsistent";
                return false;
            }
            if (missing.Count == 0)
            {
                rejectionReason = "master_angler_has_no_missing_species";
                return false;
            }

            var hasWindowIntent = candidate.Parameters.Any(parameter =>
                parameter.Name == "master_angler_target_qualified_item_id" ||
                parameter.Name ==
                    "continuation.master_angler_target_qualified_item_id");
            if (hasWindowIntent)
            {
                if (denominator != 72 ||
                    !MasterAnglerCurrentCandidateMatcher.TryMatch(
                        snapshot,
                        candidate,
                        out var candidateMatch,
                        out rejectionReason))
                {
                    rejectionReason = denominator != 72
                        ? "master_angler_native_denominator_is_not_72"
                        : rejectionReason;
                    return false;
                }
                evidence = new[]
                {
                    Parameter(
                        "master_angler_denominator_count",
                        denominator.ToString()),
                    Parameter(
                        "master_angler_missing_species_count_before",
                        missing.Count.ToString()),
                    Parameter(
                        "master_angler_target_qualified_item_ids_json",
                        JsonSerializer.Serialize(new[]
                        {
                            candidateMatch.Intent.TargetQualifiedItemId
                        })),
                    Parameter(
                        "master_angler_progress_evidence_status",
                        "exact_native_denominator_authoritative_window_intent"),
                    Parameter(
                        "master_angler_acquisition_kind",
                        candidate.Kind)
                };
                return true;
            }

            if (!MasterAnglerCurrentCandidateMatcher.TryReadCompletePossibleFish(
                    candidate,
                    out var possible))
            {
                rejectionReason = "master_angler_candidate_outcome_projection_incomplete";
                return false;
            }
            var targets = possible
                .Where(missing.Contains)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (targets.Length == 0)
            {
                if (string.Equals(
                        candidate.Kind,
                        "collect_crab_pot",
                        StringComparison.Ordinal) &&
                    TryBuildCrabPotCycleClearEvidence(
                        candidate,
                        denominator,
                        missing,
                        out evidence))
                {
                    return true;
                }
                rejectionReason = "master_angler_candidate_contains_no_missing_species";
                return false;
            }
            if (string.Equals(
                    candidate.Kind,
                    "collect_crab_pot",
                    StringComparison.Ordinal) &&
                (!TryReadUniqueParameter(
                    candidate,
                    "expected_fish_collection_eligible",
                    out var collectionEligible) ||
                collectionEligible != "1"))
            {
                rejectionReason =
                    "master_angler_crab_pot_output_not_collection_eligible";
                return false;
            }

            evidence = new[]
            {
                Parameter("master_angler_denominator_count", denominator.ToString()),
                Parameter("master_angler_missing_species_count_before", missing.Count.ToString()),
                Parameter("master_angler_target_qualified_item_ids_json", JsonSerializer.Serialize(targets)),
                Parameter("master_angler_progress_evidence_status", "exact_native_denominator_intersection"),
                Parameter("master_angler_acquisition_kind", candidate.Kind)
            };
            return true;
        }

        private static bool TryBuildCrabPotCycleClearEvidence(
            PolicyEventCandidatePrediction candidate,
            int denominator,
            ISet<string> missing,
            out SmallModelActionParameter[] evidence)
        {
            evidence = Array.Empty<SmallModelActionParameter>();
            if (!TryReadUniqueParameter(
                    candidate,
                    "crab_pot_production_domain_complete",
                    out var completeText) ||
                !bool.TryParse(completeText, out var complete) || !complete ||
                !TryReadUniqueParameter(
                    candidate,
                    "crab_pot_production_possible_qualified_item_ids_json",
                    out var json))
            {
                return false;
            }

            string[] targets;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return false;
                targets = document.RootElement.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? string.Empty)
                    .Where(missing.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (JsonException)
            {
                return false;
            }
            if (targets.Length == 0)
                return false;

            evidence = new[]
            {
                Parameter(
                    "master_angler_denominator_count",
                    denominator.ToString()),
                Parameter(
                    "master_angler_missing_species_count_before",
                    missing.Count.ToString()),
                Parameter(
                    "master_angler_target_qualified_item_ids_json",
                    JsonSerializer.Serialize(targets)),
                Parameter(
                    "master_angler_progress_evidence_status",
                    "crab_pot_cycle_clear_for_missing_species_domain"),
                Parameter(
                    "master_angler_acquisition_kind",
                    "collect_crab_pot_cycle_clear")
            };
            return true;
        }

        private static bool TryReadConsistentFishDenominator(
            JsonElement progress,
            out int denominator,
            out HashSet<string> missingQualifiedItemIds)
        {
            denominator = 0;
            missingQualifiedItemIds = new HashSet<string>(StringComparer.Ordinal);
            if (!progress.TryGetProperty("eligible_species_count", out var denominatorValue) ||
                !denominatorValue.TryGetInt32(out denominator) || denominator <= 0 ||
                !progress.TryGetProperty("caught_eligible_species_count", out var caughtValue) ||
                !caughtValue.TryGetInt32(out var caught) || caught < 0 ||
                !progress.TryGetProperty("missing_species_count", out var missingValue) ||
                !missingValue.TryGetInt32(out var missingCount) || missingCount < 0 ||
                caught + missingCount != denominator ||
                !progress.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() != denominator ||
                !progress.TryGetProperty("missing_item_ids", out var missingIds) || missingIds.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
            var caughtRows = 0;
            var missingItemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in missingIds.EnumerateArray())
            {
                var itemId = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (string.IsNullOrWhiteSpace(itemId) || !missingItemIds.Add(itemId))
                    return false;
            }
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("item_id", out var itemIdValue) || itemIdValue.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("qualified_item_id", out var qualifiedValue) || qualifiedValue.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("caught", out var caughtFlag) || caughtFlag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return false;
                }
                var itemId = itemIdValue.GetString() ?? string.Empty;
                var qualifiedItemId = qualifiedValue.GetString() ?? string.Empty;
                if (itemId.Length == 0 || qualifiedItemId.Length == 0 || !seenItemIds.Add(itemId))
                    return false;
                if (caughtFlag.GetBoolean())
                {
                    caughtRows++;
                    if (missingItemIds.Contains(itemId))
                        return false;
                }
                else
                {
                    if (!missingItemIds.Contains(itemId) || !missingQualifiedItemIds.Add(qualifiedItemId))
                        return false;
                }
            }
            return caughtRows == caught &&
                missingItemIds.Count == missingCount &&
                missingQualifiedItemIds.Count == missingCount;
        }

    }
}
