using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class StructuredPolicyRanker
{
    private readonly StructuredPolicyCheckpointStore checkpointStore;
    private readonly PolicyTrainingAdmissionFilter admissionFilter;

    public StructuredPolicyRanker()
        : this(new StructuredPolicyCheckpointStore(), new PolicyTrainingAdmissionFilter())
    {
    }

    public StructuredPolicyRanker(
        StructuredPolicyCheckpointStore checkpointStore,
        PolicyTrainingAdmissionFilter admissionFilter)
    {
        this.checkpointStore = checkpointStore;
        this.admissionFilter = admissionFilter;
    }

    public PolicyEventCandidatePrediction[] Rank(
        StructuredPolicyCheckpointEnvelope checkpoint,
        FeatureVector stateFeatures,
        IReadOnlyList<PolicyEventCandidatePrediction> candidates)
    {
        checkpointStore.Validate(checkpoint);
        if (stateFeatures is null)
            throw new ArgumentNullException(nameof(stateFeatures));
        var admitted = new HashSet<string>(admissionFilter.Allowlist, StringComparer.Ordinal);
        var result = candidates.Select(Clone).ToArray();
        foreach (var candidate in result)
        {
            if (!admitted.Contains(candidate.OptionId))
            {
                if (IsNativeSaveBoundary(candidate))
                {
                    candidate.ModelScore = null;
                    candidate.PolicyModelSource = "control_plane.native_save_boundary";
                    continue;
                }

                candidate.Available = false;
                candidate.BlockReasons = (candidate.BlockReasons ?? Array.Empty<string>())
                    .Append(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                candidate.ModelScore = null;
                candidate.PolicyModelSource = checkpoint.CheckpointId;
                continue;
            }
            if (!candidate.Available)
            {
                candidate.ModelScore = null;
                candidate.PolicyModelSource = checkpoint.CheckpointId;
                continue;
            }
            var encoded = StructuredPolicyFeatureEncoder.Encode(stateFeatures, candidate, checkpoint.Model);
            var score = Dot(checkpoint.Model.Weights, encoded);
            if (double.IsNaN(score) || double.IsInfinity(score))
                throw new InvalidOperationException("Structured policy produced a non-finite score.");
            candidate.ModelScore = Math.Round(score, 8);
            candidate.Score = candidate.ModelScore.Value;
            candidate.PolicyModelSource = checkpoint.CheckpointId;
        }

        var ordered = result
            .OrderByDescending(candidate => candidate.Available && candidate.ModelScore.HasValue)
            .ThenByDescending(candidate => candidate.ModelScore ?? double.MinValue)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
            ordered[index].Rank = index + 1;
        return ordered;
    }

    private static bool IsNativeSaveBoundary(PolicyEventCandidatePrediction candidate) =>
        candidate.Available &&
        string.Equals(candidate.OptionId, "recovery.stabilize_day", StringComparison.Ordinal) &&
        (candidate.Parameters ?? Array.Empty<SmallModelActionParameter>()).Any(parameter =>
            string.Equals(parameter.Name, "control_plane.native_save_boundary", StringComparison.Ordinal) &&
            string.Equals(parameter.Value, "true", StringComparison.OrdinalIgnoreCase));

    public PolicyPredictionEnvelope Summarize(
        StructuredPolicyCheckpointEnvelope checkpoint,
        IReadOnlyList<PolicyEventCandidatePrediction> candidates)
    {
        var options = candidates
            .Where(candidate => candidate.Available && candidate.ModelScore.HasValue)
            .GroupBy(candidate => candidate.OptionId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(candidate => candidate.ModelScore)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal).First())
            .OrderByDescending(candidate => candidate.ModelScore)
            .ThenBy(candidate => candidate.OptionId, StringComparer.Ordinal)
            .Select((candidate, index) => new PolicyOptionPrediction
            {
                OptionId = candidate.OptionId,
                Rank = index + 1,
                Score = candidate.ModelScore!.Value,
                ExpectedReward = candidate.ExpectedReward,
                Evidence = "structured_policy_checkpoint"
            })
            .ToArray();
        return new PolicyPredictionEnvelope
        {
            Source = checkpoint.CheckpointId,
            RankedOptions = options,
            Audit = new PolicyPredictionAudit
            {
                Predictor = "StardewAI.Core.Training.StructuredPolicyRanker",
                Policy = "The checkpoint only reranks complete evidence-admitted candidates; deterministic authorities retain candidate generation, constraints, compilation and execution."
            }
        };
    }

    private static PolicyEventCandidatePrediction Clone(PolicyEventCandidatePrediction candidate) =>
        JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(candidate, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Policy event candidate clone failed.");

    private static double Dot(double[] left, double[] right)
    {
        var result = 0d;
        for (var index = 0; index < left.Length; index++)
            result += left[index] * right[index];
        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
