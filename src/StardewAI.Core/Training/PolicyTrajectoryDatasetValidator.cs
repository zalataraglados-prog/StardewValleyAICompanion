using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

internal sealed class PolicyTrajectoryDatasetValidator
{
    private readonly PolicyTrainingAdmissionFilter admissionFilter = new();

    public string? Validate(PolicyDecisionTrajectoryEnvelope row)
    {
        if (!string.Equals(row.SchemaVersion, PolicyTrajectoryVersionPins.TrajectorySchema, StringComparison.Ordinal))
            return "unsupported_schema";
        if (string.IsNullOrWhiteSpace(row.TrajectoryId) ||
            string.IsNullOrWhiteSpace(row.RunId) ||
            string.IsNullOrWhiteSpace(row.SourceStateHash))
            return "identity_missing";
        if (row.Context is null ||
            row.Versions is null ||
            row.StateFeatures is null ||
            row.Selection is null ||
            row.Outcome is null ||
            row.Returns is null)
            return "envelope_section_missing";
        if (!ValidContext(row.Context))
            return "context_invalid";
        if (!ValidStateFeatures(row.StateFeatures))
            return "state_features_invalid";
        if (!ValidVersions(row.Versions))
            return "version_binding_missing";
        if (row.Candidates is null || row.Candidates.Length == 0)
            return "candidate_set_empty";
        if (row.Candidates.Any(candidate => candidate is null ||
            candidate.SourceCandidate is null ||
            string.IsNullOrWhiteSpace(candidate.CandidateId) ||
            string.IsNullOrWhiteSpace(candidate.OptionId) ||
            candidate.Rank <= 0 ||
            candidate.EstimatedTicks < 0 ||
            candidate.EnergyCost < 0 ||
            !Finite(candidate.Score) ||
            !Finite(candidate.ExpectedReward)))
            return "candidate_invalid";
        if (row.Candidates.Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count() != row.Candidates.Length)
            return "candidate_id_duplicate";
        if (row.Candidates.Select(candidate => candidate.Rank).Distinct().Count() != row.Candidates.Length)
            return "candidate_rank_duplicate";
        if (!row.Candidates.Select(candidate => candidate.Rank).OrderBy(rank => rank).SequenceEqual(Enumerable.Range(1, row.Candidates.Length)))
            return "candidate_rank_not_contiguous";
        if (row.Candidates.Any(candidate => !SourceCandidateMatches(candidate)))
            return "source_candidate_binding_mismatch";

        foreach (var candidate in row.Candidates)
        {
            var admitted = admissionFilter.FilterOptionIds(new[] { candidate.OptionId }).Length == 1;
            if (candidate.AdmittedForPolicy != admitted)
                return "candidate_admission_mismatch";
        }

        var selectedRows = row.Candidates.Where(candidate => candidate.Selected).ToArray();
        if (selectedRows.Length != 1)
            return "selection_count_invalid";
        var selected = selectedRows[0];
        if (!string.Equals(selected.CandidateId, row.Selection.CandidateId, StringComparison.Ordinal) ||
            !string.Equals(selected.OptionId, row.Selection.OptionId, StringComparison.Ordinal))
            return "selection_binding_mismatch";
        if (!string.Equals(
                JsonSerializer.Serialize(selected.Parameters ?? Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>()),
                JsonSerializer.Serialize(row.Selection.Parameters ?? Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>()),
                StringComparison.Ordinal))
            return "selection_parameter_mismatch";
        if (!selected.Available || !selected.AdmittedForPolicy)
            return "selection_not_trainable";
        if (string.IsNullOrWhiteSpace(row.Outcome.EpisodeId) ||
            string.IsNullOrWhiteSpace(row.Outcome.QueueId) ||
            string.IsNullOrWhiteSpace(row.Outcome.PrimitiveOptionId))
            return "outcome_identity_missing";
        if (!string.Equals(row.Outcome.Status, "applied", StringComparison.Ordinal) ||
            !row.Outcome.Success ||
            !row.Outcome.AfterSnapshotFresh ||
            row.Outcome.ActualTicks < 0)
            return "outcome_not_verified_success";
        if (row.Outcome.ChangedFacts.ValueKind != JsonValueKind.Array)
            return "changed_facts_invalid";
        if (!Finite(row.Returns.Immediate))
            return "immediate_return_invalid";
        if (!NullableFinite(row.Returns.Day) ||
            !NullableFinite(row.Returns.Season) ||
            !NullableFinite(row.Returns.Year) ||
            !NullableFinite(row.Returns.Grandpa21))
            return "long_return_invalid";

        return null;
    }

    public static string CanonicalSplitKey(PolicyTrajectoryContext context) =>
        context.SaveId + ":" + context.Year + ":" + context.Season + ":" + context.Day;

    public static int SeasonOrdinal(string season) => season switch
    {
        "spring" => 0,
        "summer" => 1,
        "fall" => 2,
        "winter" => 3,
        _ => -1
    };

    public static long DateOrdinal(PolicyTrajectoryContext context) =>
        ((long)context.Year * 4L + SeasonOrdinal(context.Season)) * 28L + context.Day;

    public static long DateOrdinal(PolicyHorizonObservationEnvelope observation) =>
        ((long)observation.Year * 4L + SeasonOrdinal(observation.Season)) * 28L + observation.Day;

    private static bool ValidContext(PolicyTrajectoryContext context)
    {
        if (string.IsNullOrWhiteSpace(context.SaveId) ||
            context.Year < 1 ||
            SeasonOrdinal(context.Season) < 0 ||
            context.Day is < 1 or > 28 ||
            !ValidGameTime(context.Time))
            return false;
        return string.Equals(context.SplitKey, CanonicalSplitKey(context), StringComparison.Ordinal);
    }

    private static bool ValidVersions(PolicyTrajectoryVersions versions) =>
        string.Equals(versions.FeatureSchema, PolicyTrajectoryVersionPins.FeatureSchema, StringComparison.Ordinal) &&
        string.Equals(versions.CandidateVocabulary, OptionCapabilityRegistrySource.SchemaVersion, StringComparison.Ordinal) &&
        string.Equals(versions.CapabilityRegistry, OptionCapabilityRegistrySource.SchemaVersion, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(versions.KnowledgeDictionary) &&
        string.Equals(versions.Compiler, PolicyTrajectoryVersionPins.Compiler, StringComparison.Ordinal) &&
        PolicyTrajectoryVersionPins.IsKnownExecutor(versions.Executor);

    private static bool ValidGameTime(int time)
    {
        var hour = time / 100;
        var minute = time % 100;
        return hour is >= 6 and <= 26 && minute is >= 0 and < 60;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool NullableFinite(double? value) => !value.HasValue || Finite(value.Value);

    private static bool ValidStateFeatures(FeatureVector features)
    {
        if (features.Numeric is null || features.Categorical is null || features.Boolean is null)
            return false;
        var names = features.Numeric.Select(feature => feature?.Name ?? string.Empty)
            .Concat(features.Categorical.Select(feature => feature?.Name ?? string.Empty))
            .Concat(features.Boolean.Select(feature => feature?.Name ?? string.Empty))
            .ToArray();
        return names.Length > 0 &&
            names.All(name => !string.IsNullOrWhiteSpace(name)) &&
            names.Distinct(StringComparer.Ordinal).Count() == names.Length &&
            features.Numeric.All(feature => feature is not null && Finite(feature.Value)) &&
            features.Categorical.All(feature => feature is not null && !string.IsNullOrWhiteSpace(feature.Value)) &&
            features.Boolean.All(feature => feature is not null);
    }

    private static bool SourceCandidateMatches(PolicyTrajectoryCandidate candidate)
    {
        var source = candidate.SourceCandidate;
        return string.Equals(source.CandidateId, candidate.CandidateId, StringComparison.Ordinal) &&
            string.Equals(source.OptionId, candidate.OptionId, StringComparison.Ordinal) &&
            string.Equals(source.Kind, candidate.Kind, StringComparison.Ordinal) &&
            source.Rank == candidate.Rank &&
            source.Score.Equals(candidate.Score) &&
            source.ExpectedReward.Equals(candidate.ExpectedReward) &&
            source.Available == candidate.Available &&
            source.EstimatedTicks == candidate.EstimatedTicks &&
            source.EnergyCost == candidate.EnergyCost &&
            string.Equals(
                JsonSerializer.Serialize(source.Parameters ?? Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>()),
                JsonSerializer.Serialize(candidate.Parameters ?? Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>()),
                StringComparison.Ordinal);
    }
}
