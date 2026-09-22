using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    internal const int MaxCandidateCount = 4096;

    public static AcquisitionRoutePortfolioTeacherPreference Build(
        AcquisitionRoutePortfolioInputs inputs,
        string requestPath) => BuildScoringSet(inputs, requestPath).Preference;

    internal static AcquisitionRoutePortfolioTeacherScoringSet
        BuildScoringSet(
            AcquisitionRoutePortfolioInputs inputs,
            string requestPath)
    {
        var requestFullPath = Path.GetFullPath(requestPath);
        var context = AcquisitionRoutePortfolioBuilder.Prepare(inputs);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreferenceRequest>(
            requestFullPath,
            "Acquisition route portfolio Teacher preference request");
        var result = BaseResult(
            context,
            request,
            CurrentTeacherFrontierSupport.HashFile(requestFullPath));
        var reasons = ValidateRequest(context, request);
        if (reasons.Count > 0)
        {
            return new AcquisitionRoutePortfolioTeacherScoringSet(
                Block(result, reasons),
                context.Snapshot,
                Array.Empty<PortfolioCandidate>());
        }
        return BuildResolvedScoringSet(
            context,
            result,
            request.RequestId,
            request.GoalId,
            request.SnapshotStateHash,
            request.ExpectedLedgerRevision,
            ResolveScopes(context, request),
            string.Empty,
            Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>());
    }

    public static AcquisitionRoutePortfolioTeacherPreference
        BuildInitialContinuation(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath,
            AcquisitionRoutePortfolioInputs currentInputs,
            string continuationRequestPath) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    checkpointPath),
                currentInputs,
                continuationRequestPath);

    public static AcquisitionRoutePortfolioTeacherPreference
        BuildContinuation(
            string rolloutProofManifestPath,
            AcquisitionRoutePortfolioInputs currentInputs,
            string continuationRequestPath) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                    rolloutProofManifestPath),
                currentInputs,
                continuationRequestPath);

    internal static AcquisitionRoutePortfolioTeacherPreference
        BuildContinuation(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
            AcquisitionRoutePortfolioInputs currentInputs,
            string continuationRequestPath) => BuildContinuationScoringSet(
                verifiedPrior,
                currentInputs,
                continuationRequestPath).Preference;

    internal static AcquisitionRoutePortfolioTeacherScoringSet
        BuildContinuationScoringSet(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
            AcquisitionRoutePortfolioInputs currentInputs,
            string continuationRequestPath)
    {
        var expected = AcquisitionRoutePortfolioContinuationBuilder
            .BuildRequest(verifiedPrior, currentInputs);
        var requestFullPath = Path.GetFullPath(continuationRequestPath);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioContinuationTeacherRequest>(
            requestFullPath,
            "Acquisition route portfolio continuation Teacher request");
        Require(EqualJson(request, expected),
            "Continuation Teacher request drifted from verified checkpoint.");
        var context = AcquisitionRoutePortfolioBuilder.Prepare(currentInputs);
        var surrogate = new AcquisitionRoutePortfolioTeacherPreferenceRequest
        {
            RequestId = request.RequestId,
            GoalId = request.GoalId,
            SnapshotStateHash = request.SnapshotStateHash,
            ExpectedLedgerRevision = request.ExpectedLedgerRevision,
            ScopedRequirements = request.ScopedProgress.Select(row =>
                    new AcquisitionRoutePortfolioRequirementScope(
                        row.RequirementSetId,
                        row.RequirementId))
                .ToArray()
        };
        var result = BaseResult(
            context,
            surrogate,
            CurrentTeacherFrontierSupport.HashFile(requestFullPath));
        result.SelectionPolicyId =
            "verified_checkpoint_continuation_unique_strict_pareto.v1";
        var reasons = ValidateRequest(context, surrogate);
        AcquisitionRoutePortfolioContinuationBuilder.ValidateProgress(
            request.ScopedProgress);
        if (request.SchemaVersion !=
                "acquisition_route_portfolio_continuation_teacher_request.v1" ||
            request.FormalTrainingAuthorized ||
            request.StrategyLedgerSha256 != context.StrategyLedgerSha256)
        {
            reasons.Add("portfolio_teacher_continuation_request_invalid");
        }
        if (reasons.Count > 0)
        {
            return new AcquisitionRoutePortfolioTeacherScoringSet(
                Block(result, reasons),
                context.Snapshot,
                Array.Empty<PortfolioCandidate>());
        }
        var completed = request.ScopedProgress
            .Where(row => row.CompletedAlternativeIndices.Length > 0)
            .OrderBy(row => ScopeKey(
                row.RequirementSetId,
                row.RequirementId), StringComparer.Ordinal)
            .Select(row =>
                new AcquisitionRoutePortfolioCompletedAlternatives(
                    row.RequirementSetId,
                    row.RequirementId,
                    row.CompletedAlternativeIndices.Order().ToArray()))
            .ToArray();
        return BuildResolvedScoringSet(
            context,
            result,
            request.RequestId,
            request.GoalId,
            request.SnapshotStateHash,
            request.ExpectedLedgerRevision,
            ResolveScopes(context, request),
            request.PriorCheckpointSha256,
            completed);
    }

    internal static string ArtifactSha256(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Options) +
            Environment.NewLine;
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private static AcquisitionRoutePortfolioTeacherPreference BaseResult(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request,
        string requestSha256) => new()
        {
            RequestId = request.RequestId,
            GoalId = request.GoalId,
            SnapshotStateHash = request.SnapshotStateHash,
            ExpectedLedgerRevision = request.ExpectedLedgerRevision,
            PreferenceRequestSha256 = requestSha256,
            RequirementInventorySha256 = context.RequirementInventorySha256,
            OpportunityCostSha256 = context.OpportunityCostSha256,
            StrategyLedgerSha256 = context.StrategyLedgerSha256,
            SnapshotSha256 = context.SnapshotSha256,
            CandidateLimit = MaxCandidateCount,
            FormalTrainingAuthorized = false,
            UsesLearnerRankOrScore = false,
            EmitsNegativeLabelsForUnavailablePortfolios = false
        };

    private static AcquisitionRoutePortfolioTeacherPreference Block(
        AcquisitionRoutePortfolioTeacherPreference result,
        IEnumerable<string> reasons)
    {
        result.Status = "blocked_portfolio_teacher_preference";
        result.TeacherPreferenceLabelEligible = false;
        result.FormalTrainingAuthorized = false;
        result.BlockingReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return result;
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    internal sealed record AcquisitionRoutePortfolioTeacherScoringSet(
        AcquisitionRoutePortfolioTeacherPreference Preference,
        StardewAI.Contracts.State.SnapshotEnvelope Snapshot,
        PortfolioCandidate[] Candidates);
}
