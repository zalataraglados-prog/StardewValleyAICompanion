using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public interface IPolicyTeacherSupervisionReplayVerifier
{
    string TeacherId { get; }
    string TeacherVersion { get; }
    PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed);
}

public sealed class PolicyPairwiseSupervisionAdmission
{
    private readonly IPolicyTeacherSupervisionReplayVerifier? replayVerifier;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PolicyPairwiseSupervisionAdmission(IPolicyTeacherSupervisionReplayVerifier? replayVerifier = null)
    {
        this.replayVerifier = replayVerifier;
    }

    public string? Validate(PolicyDecisionTrajectoryEnvelope row, out PolicyTeacherPreferencePair[] pairs)
    {
        pairs = Array.Empty<PolicyTeacherPreferencePair>();
        var claim = row.Audit?.TeacherSupervision;
        if (claim is null || claim.SchemaVersion != "policy_teacher_supervision.v2")
            return "independent_teacher_preference_required";
        if (replayVerifier is null || string.IsNullOrWhiteSpace(replayVerifier.TeacherId) ||
            string.IsNullOrWhiteSpace(replayVerifier.TeacherVersion))
            return "teacher_replay_verifier_unavailable";
        if (claim.TeacherId != replayVerifier.TeacherId ||
            claim.TeacherVersion != replayVerifier.TeacherVersion ||
            claim.SourceStateHash != row.SourceStateHash ||
            claim.CandidateDenominatorSha256 != CandidateDenominatorSha256(row) ||
            claim.DatasetPartition != PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey) ||
            row.Context.DatasetPartition != claim.DatasetPartition ||
            claim.Sources is null)
            return "teacher_supervision_identity_mismatch";
        if (claim.PairwisePreferences is null || claim.PairwisePreferences.Length == 0)
            return "teacher_strict_comparison_missing";
        PolicyDecisionTrajectoryEnvelope expected;
        try
        {
            expected = replayVerifier.Rebuild(row);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or
                                         InvalidOperationException or JsonException or UnauthorizedAccessException)
        {
            return "teacher_supervision_replay_failed";
        }
        // Compare native observation and full candidate context, never the backfilled return.
        if (expected is null || expected.Audit?.TeacherSupervision is null ||
            !Same(Binding(row), Binding(expected)))
            return "teacher_supervision_replay_mismatch";
        var candidates = row.Candidates.ToDictionary(value => value.CandidateId, StringComparer.Ordinal);
        var preferences = expected.Audit.TeacherSupervision.PairwisePreferences;
        if (preferences is null || preferences.Length == 0)
            return "teacher_strict_comparison_missing";
        if (preferences.Any(value => value is null))
            return "teacher_strict_comparison_invalid";
        var keys = new System.Collections.Generic.HashSet<string>(preferences.Select(value =>
            value.PreferredCandidateId + "\n" + value.AlternativeCandidateId), StringComparer.Ordinal);
        if (keys.Count != preferences.Length ||
            preferences.Any(value =>
                value.PreferredCandidateId == value.AlternativeCandidateId ||
                string.IsNullOrWhiteSpace(value.FirstDifferingAuthoritativeCriterion) ||
                value.LabelSemantics != "preferred_over_current_eligible_counterfactual" ||
                !Trainable(value.PreferredCandidateId) || !Trainable(value.AlternativeCandidateId) ||
                keys.Contains(value.AlternativeCandidateId + "\n" + value.PreferredCandidateId)))
            return "teacher_strict_comparison_invalid";
        pairs = preferences.Select(value => new PolicyTeacherPreferencePair
        {
            PreferredCandidateId = value.PreferredCandidateId,
            AlternativeCandidateId = value.AlternativeCandidateId,
            FirstDifferingAuthoritativeCriterion = value.FirstDifferingAuthoritativeCriterion,
            LabelSemantics = value.LabelSemantics
        }).ToArray();
        return null;

        bool Trainable(string candidateId) => candidates.TryGetValue(candidateId, out var value) &&
            value.Available && value.AdmittedForPolicy;
    }

    public static string CandidateDenominatorSha256(PolicyDecisionTrajectoryEnvelope row)
    {
        var value = JsonSerializer.Serialize(row.Candidates.OrderBy(candidate => candidate.CandidateId,
            StringComparer.Ordinal).Select(candidate => candidate.SourceCandidate).ToArray(), JsonOptions);
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    private static object Binding(PolicyDecisionTrajectoryEnvelope row) => new
    {
        row.TrajectoryId, row.RunId, row.SourceStateHash, row.Context, row.Versions,
        row.StateFeatures, row.Candidates, row.Selection, row.Outcome,
        TeacherSupervision = row.Audit?.TeacherSupervision
    };

    private static bool Same(object left, object right) =>
        JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions);
}
