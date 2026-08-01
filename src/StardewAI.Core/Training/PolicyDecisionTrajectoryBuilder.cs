using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class PolicyDecisionTrajectoryBuilder
{
    private readonly PolicyTrainingAdmissionFilter admissionFilter;

    public PolicyDecisionTrajectoryBuilder()
        : this(new PolicyTrainingAdmissionFilter())
    {
    }

    public PolicyDecisionTrajectoryBuilder(PolicyTrainingAdmissionFilter admissionFilter)
    {
        this.admissionFilter = admissionFilter;
    }

    public PolicyDecisionTrajectoryEnvelope Build(
        string trajectoryId,
        string runId,
        PolicyTrajectoryContext context,
        FeatureVector stateFeatures,
        PolicyTrajectoryVersions versions,
        string sourceStateHash,
        AvailabilityAwarePolicyPredictionEnvelope decision,
        string selectedCandidateId,
        PlanExecutionEpisodeEnvelope execution,
        PolicyTrajectoryReturns? returns = null)
    {
        Require(trajectoryId, nameof(trajectoryId));
        Require(runId, nameof(runId));
        Require(context.SaveId, "context.save_id");
        Require(context.Season, "context.season");
        ValidateStateFeatures(stateFeatures);
        Require(sourceStateHash, nameof(sourceStateHash));
        ValidateVersions(versions);
        if (!string.Equals(sourceStateHash, execution.SourceStateHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Policy decision and execution source hashes differ.");

        var sourceCandidates = decision.RankedEventCandidates ?? Array.Empty<PolicyEventCandidatePrediction>();
        if (sourceCandidates.Length == 0)
            throw new InvalidOperationException("Policy trajectory requires the complete non-empty candidate set.");
        if (sourceCandidates.Select(row => row.CandidateId).Distinct(StringComparer.Ordinal).Count() != sourceCandidates.Length)
            throw new InvalidOperationException("Policy trajectory candidate IDs must be unique.");

        var selected = sourceCandidates.SingleOrDefault(row =>
            string.Equals(row.CandidateId, selectedCandidateId, StringComparison.Ordinal));
        if (selected is null)
            throw new InvalidOperationException("Selected candidate is absent from the decision candidate set.");
        if (!selected.Available)
            throw new InvalidOperationException("Selected candidate is not available.");
        if (admissionFilter.FilterOptionIds(new[] { selected.OptionId }).Length != 1)
            throw new InvalidOperationException("Selected candidate option is not admitted for policy training.");

        var candidates = sourceCandidates
            .Select(row => BuildCandidate(row, selectedCandidateId))
            .OrderBy(row => row.Rank)
            .ThenBy(row => row.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var contextCopy = new PolicyTrajectoryContext
        {
            SaveId = context.SaveId,
            Year = context.Year,
            Season = context.Season,
            Day = context.Day,
            Time = context.Time,
            SplitKey = string.IsNullOrWhiteSpace(context.SplitKey)
                ? context.SaveId + ":" + context.Year + ":" + context.Season + ":" + context.Day
                : context.SplitKey,
            DatasetPartition = string.IsNullOrWhiteSpace(context.DatasetPartition)
                ? "unassigned"
                : context.DatasetPartition
        };

        return new PolicyDecisionTrajectoryEnvelope
        {
            TrajectoryId = trajectoryId,
            RunId = runId,
            SourceStateHash = sourceStateHash,
            Context = contextCopy,
            StateFeatures = Copy(stateFeatures),
            Versions = versions,
            Candidates = candidates,
            Selection = new PolicyTrajectorySelection
            {
                CandidateId = selected.CandidateId,
                OptionId = selected.OptionId,
                Parameters = selected.Parameters ?? Array.Empty<SmallModelActionParameter>()
            },
            Outcome = new PolicyTrajectoryOutcome
            {
                EpisodeId = execution.EpisodeId,
                QueueId = execution.QueueId,
                PrimitiveOptionId = execution.OptionId,
                Status = execution.Status,
                Success = execution.Success,
                ActualTicks = Math.Max(0, execution.AfterGameTick - execution.BeforeGameTick),
                StateHashChanged = execution.StateHashChanged,
                AfterSnapshotFresh = execution.AfterSnapshotFresh,
                FailureAttribution = execution.FailureAttribution,
                BlockReasons = execution.BlockReasons ?? Array.Empty<string>(),
                ChangedFacts = execution.ChangedFacts.ValueKind == JsonValueKind.Undefined
                    ? JsonDocument.Parse("[]").RootElement.Clone()
                    : execution.ChangedFacts.Clone()
            },
            Returns = returns ?? new PolicyTrajectoryReturns { Immediate = execution.Reward }
        };
    }

    private PolicyTrajectoryCandidate BuildCandidate(
        PolicyEventCandidatePrediction source,
        string selectedCandidateId)
    {
        var admitted = admissionFilter.FilterOptionIds(new[] { source.OptionId }).Length == 1;
        var reasons = new List<string>();
        if (!admitted)
            reasons.Add(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason);
        reasons.AddRange(source.GateReasons ?? Array.Empty<string>());
        reasons.AddRange(source.TimelineReasons ?? Array.Empty<string>());
        reasons.AddRange(source.BlockReasons ?? Array.Empty<string>());

        return new PolicyTrajectoryCandidate
        {
            SourceCandidate = CloneCandidate(source),
            CandidateId = source.CandidateId,
            OptionId = source.OptionId,
            Kind = source.Kind,
            Rank = source.Rank,
            Score = source.Score,
            ExpectedReward = source.ExpectedReward,
            Available = source.Available,
            AdmittedForPolicy = admitted,
            Selected = string.Equals(source.CandidateId, selectedCandidateId, StringComparison.Ordinal),
            EstimatedTicks = source.EstimatedTicks,
            EnergyCost = source.EnergyCost,
            ExclusionReasons = reasons
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reason => reason, StringComparer.Ordinal)
                .ToArray(),
            Parameters = source.Parameters ?? Array.Empty<SmallModelActionParameter>()
        };
    }

    private static void ValidateVersions(PolicyTrajectoryVersions versions)
    {
        Require(versions.FeatureSchema, "versions.feature_schema");
        Require(versions.CandidateVocabulary, "versions.candidate_vocabulary");
        Require(versions.CapabilityRegistry, "versions.capability_registry");
        Require(versions.KnowledgeDictionary, "versions.knowledge_dictionary");
        Require(versions.Compiler, "versions.compiler");
        Require(versions.Executor, "versions.executor");
    }

    private static void ValidateStateFeatures(FeatureVector features)
    {
        if (features is null ||
            features.Numeric is null ||
            features.Categorical is null ||
            features.Boolean is null ||
            features.Numeric.Length + features.Categorical.Length + features.Boolean.Length == 0)
            throw new ArgumentException("Policy trajectory state features are required.", nameof(features));
        if (features.Numeric.Any(feature =>
                feature is null ||
                string.IsNullOrWhiteSpace(feature.Name) ||
                double.IsNaN(feature.Value) ||
                double.IsInfinity(feature.Value)) ||
            features.Categorical.Any(feature =>
                feature is null ||
                string.IsNullOrWhiteSpace(feature.Name) ||
                string.IsNullOrWhiteSpace(feature.Value)) ||
            features.Boolean.Any(feature => feature is null || string.IsNullOrWhiteSpace(feature.Name)))
            throw new ArgumentException("Policy trajectory state features are invalid.", nameof(features));
        var names = features.Numeric.Select(feature => feature.Name)
            .Concat(features.Categorical.Select(feature => feature.Name))
            .Concat(features.Boolean.Select(feature => feature.Name))
            .ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new ArgumentException("Policy trajectory state feature names must be unique.", nameof(features));
    }

    private static FeatureVector Copy(FeatureVector source) => new()
    {
        Numeric = source.Numeric
            .OrderBy(feature => feature.Name, StringComparer.Ordinal)
            .Select(feature => new NumericFeature { Name = feature.Name, Value = feature.Value })
            .ToArray(),
        Categorical = source.Categorical
            .OrderBy(feature => feature.Name, StringComparer.Ordinal)
            .Select(feature => new CategoricalFeature { Name = feature.Name, Value = feature.Value })
            .ToArray(),
        Boolean = source.Boolean
            .OrderBy(feature => feature.Name, StringComparer.Ordinal)
            .Select(feature => new BooleanFeature { Name = feature.Name, Value = feature.Value })
            .ToArray()
    };

    private static PolicyEventCandidatePrediction CloneCandidate(PolicyEventCandidatePrediction source) =>
        JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(source, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Policy source candidate clone failed.");

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Required policy trajectory field is missing: " + name, name);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
