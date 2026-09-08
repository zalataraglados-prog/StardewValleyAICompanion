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
    private const int StageOneGrandpaDeadlineTotalDayExclusive = 224;
    private const double CrabPotCapacityTargetSuccessProbability = 0.95d;

    private static HashSet<string> CrabPotNetworkCapacitySatisfiedSpecies(
        SnapshotEnvelope snapshot,
        JsonElement[] rows,
        IEnumerable<string> species)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var qualifiedItemId in species.Distinct(StringComparer.Ordinal))
        {
            var assessment = AssessCrabPotCapacity(
                snapshot,
                rows,
                source: null,
                qualifiedItemId);
            if (assessment.ExistingSuccessProbability >=
                CrabPotCapacityTargetSuccessProbability)
            {
                result.Add(qualifiedItemId);
            }
        }
        return result;
    }

    private static bool CrabPotAdditionalCapacityCanAdvance(
        SnapshotEnvelope snapshot,
        JsonElement[] networkRows,
        JsonElement productionSource,
        string qualifiedItemId)
    {
        var assessment = AssessCrabPotCapacity(
            snapshot,
            networkRows,
            productionSource,
            qualifiedItemId);
        return assessment.RemainingServicedCycles > 0 &&
            assessment.AdditionalPotSingleCycleProbability > 0d &&
            assessment.ExistingSuccessProbability <
                CrabPotCapacityTargetSuccessProbability;
    }

    private static string CrabPotCapacityEvidenceJson(
        SnapshotEnvelope snapshot,
        JsonElement[] networkRows,
        JsonElement productionSource,
        IEnumerable<string> qualifiedItemIds)
    {
        return JsonSerializer.Serialize(qualifiedItemIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value =>
            {
                var assessment = AssessCrabPotCapacity(
                    snapshot,
                    networkRows,
                    productionSource,
                    value);
                return new
                {
                    qualified_item_id = value,
                    remaining_serviced_cycles =
                        assessment.RemainingServicedCycles,
                    target_success_probability =
                        CrabPotCapacityTargetSuccessProbability,
                    existing_success_probability =
                        assessment.ExistingSuccessProbability,
                    additional_pot_single_cycle_probability =
                        assessment.AdditionalPotSingleCycleProbability,
                    success_probability_after_one_additional_pot =
                        assessment.SuccessProbabilityAfterOneAdditionalPot
                };
            })
            .ToArray());
    }

    private static CrabPotCapacityAssessment AssessCrabPotCapacity(
        SnapshotEnvelope snapshot,
        IEnumerable<JsonElement> networkRows,
        JsonElement? source,
        string qualifiedItemId)
    {
        var remainingCycles = RemainingStageOneCrabPotCycles(snapshot);
        var missProbability = 1d;
        foreach (var row in networkRows.Where(row =>
                     ReadBool(row, "exact_base_crab_pot") == true &&
                     ReadBool(row, "production_domain_complete") == true &&
                     ReadString(row, "service_status") is
                         "ready_for_collection" or
                         "bait_required" or
                         "producing_or_waiting"))
        {
            if (ReadBool(row, "current_output_collection_eligible") == true &&
                string.Equals(
                    ReadString(row, "current_output_qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.Ordinal))
            {
                missProbability = 0d;
                break;
            }
            var probability = ReadCrabPotSingleCycleProbability(
                row,
                qualifiedItemId);
            if (probability.HasValue && remainingCycles > 0)
            {
                missProbability *= Math.Pow(
                    1d - probability.Value,
                    remainingCycles);
            }
        }

        var additionalProbability = source.HasValue
            ? ReadCrabPotSingleCycleProbability(
                source.Value,
                qualifiedItemId) ?? 0d
            : 0d;
        var existingSuccess = 1d - missProbability;
        var successAfterOne = remainingCycles > 0 &&
            additionalProbability > 0d
                ? 1d - missProbability * Math.Pow(
                    1d - additionalProbability,
                    remainingCycles)
                : existingSuccess;
        return new CrabPotCapacityAssessment(
            remainingCycles,
            existingSuccess,
            additionalProbability,
            successAfterOne);
    }

    private static double? ReadCrabPotSingleCycleProbability(
        JsonElement source,
        string qualifiedItemId)
    {
        if (!string.Equals(
                ReadString(
                    source,
                    "conservative_serviced_probability_status"),
                "complete_native_supported_bait_lower_bound",
                StringComparison.Ordinal) ||
            !source.TryGetProperty(
                "conservative_serviced_outcome_rows",
                out var outcomes) ||
            outcomes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var matches = outcomes.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.Ordinal))
            .Select(row => ReadDouble(
                row,
                "single_cycle_probability",
                double.NaN))
            .Where(value => !double.IsNaN(value) &&
                value > 0d && value <= 1d)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static int RemainingStageOneCrabPotCycles(
        SnapshotEnvelope snapshot)
    {
        var totalDays = ReadStateFieldValue(snapshot, "time", "total_days");
        return totalDays.HasValue &&
            totalDays.Value.ValueKind == JsonValueKind.Number &&
            totalDays.Value.TryGetInt32(out var currentTotalDays)
                ? Math.Max(
                    0,
                    StageOneGrandpaDeadlineTotalDayExclusive -
                    currentTotalDays - 1)
                : 0;
    }

    private sealed record CrabPotCapacityAssessment(
        int RemainingServicedCycles,
        double ExistingSuccessProbability,
        double AdditionalPotSingleCycleProbability,
        double SuccessProbabilityAfterOneAdditionalPot);
}
