using System.Security.Cryptography;
using System.Text;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRoutePortfolioContinuationBuilder
{
    public static AcquisitionRoutePortfolioContinuationTeacherRequest
        BuildInitialRequest(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath,
            AcquisitionRoutePortfolioInputs currentInputs) => BuildRequest(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    checkpointPath),
                currentInputs);

    public static AcquisitionRoutePortfolioContinuationTeacherRequest
        BuildRequest(
            string rolloutProofManifestPath,
            AcquisitionRoutePortfolioInputs currentInputs) => BuildRequest(
                AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                    rolloutProofManifestPath),
                currentInputs);

    internal static AcquisitionRoutePortfolioContinuationTeacherRequest
        BuildRequest(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
            AcquisitionRoutePortfolioInputs currentInputs)
    {
        var checkpointFullPath = verifiedPrior.CheckpointPath;
        var checkpoint = verifiedPrior.Checkpoint;
        Require(!checkpoint.PortfolioCompletionVerified &&
                checkpoint.FreshReplanRequired,
            "A completed rollout checkpoint cannot emit a continuation request.");

        var context = AcquisitionRoutePortfolioBuilder.Prepare(currentInputs);
        Require(context.Inventory.GoalId == checkpoint.GoalId &&
                context.Opportunity.GoalId == checkpoint.GoalId,
            "Continuation goal identity drifted.");
        Require(context.Snapshot.StateHash == checkpoint.LatestStateHash &&
                context.Opportunity.SnapshotStateHash ==
                    checkpoint.LatestStateHash,
            "Continuation state is not the checkpoint terminal state.");
        Require(context.LedgerState.Ledger.Revision ==
                    checkpoint.LatestLedgerRevision &&
                context.StrategyLedgerSha256 ==
                    checkpoint.LatestLedgerSha256,
            "Continuation ledger is not the checkpoint settled ledger.");
        ValidateProgress(checkpoint.ScopedProgress);

        var incomplete = checkpoint.ScopedProgress
            .Where(scope => !scope.ScopeComplete)
            .OrderBy(ScopeKey, StringComparer.Ordinal)
            .Select(Clone)
            .ToArray();
        Require(incomplete.Length > 0,
            "Continuation checkpoint has no incomplete requirement scope.");
        var checkpointSha = CurrentTeacherFrontierSupport.HashFile(
            checkpointFullPath);
        return new AcquisitionRoutePortfolioContinuationTeacherRequest
        {
            RequestId = RequestId(
                checkpoint.RolloutId,
                checkpoint.TransitionCount + 1,
                checkpointSha,
                context.Snapshot.StateHash,
                context.StrategyLedgerSha256),
            RolloutId = checkpoint.RolloutId,
            GoalId = checkpoint.GoalId,
            SnapshotStateHash = context.Snapshot.StateHash,
            ExpectedLedgerRevision = context.LedgerState.Ledger.Revision,
            StrategyLedgerSha256 = context.StrategyLedgerSha256,
            PriorCheckpointSha256 = checkpointSha,
            RootPreferenceRequestSha256 =
                checkpoint.RootPreferenceRequestSha256,
            TransitionCount = checkpoint.TransitionCount + 1,
            ScopedProgress = incomplete,
            CompletedRouteOccurrenceIds =
                checkpoint.CompletedRouteOccurrenceIds
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
            FormalTrainingAuthorized = false
        };
    }

    internal static void ValidateProgress(
        IEnumerable<AcquisitionRoutePortfolioScopeProgress> progress)
    {
        var rows = progress.ToArray();
        Require(rows.Length > 0 &&
                rows.Select(ScopeKey).Distinct(StringComparer.Ordinal)
                    .Count() == rows.Length,
            "Continuation requirement progress is invalid.");
        foreach (var row in rows)
        {
            var completed = row.CompletedAlternativeIndices ??
                Array.Empty<int>();
            Require(!string.IsNullOrWhiteSpace(row.RequirementSetId) &&
                    !string.IsNullOrWhiteSpace(row.RequirementId) &&
                    row.AlternativeCount > 0 &&
                    row.RequiredAlternativeCount > 0 &&
                    row.RequiredAlternativeCount <= row.AlternativeCount &&
                    completed.Distinct().Count() == completed.Length &&
                    completed.All(index => index >= 0 &&
                        index < row.AlternativeCount),
                "Continuation requirement progress row is invalid: " +
                ScopeKey(row));
            var remaining = Math.Max(
                0,
                row.RequiredAlternativeCount - completed.Length);
            Require(row.RemainingRequiredSlots == remaining &&
                    row.ScopeComplete == (remaining == 0),
                "Continuation requirement progress count drifted: " +
                ScopeKey(row));
        }
    }

    private static AcquisitionRoutePortfolioScopeProgress Clone(
        AcquisitionRoutePortfolioScopeProgress value) => new(
        value.RequirementSetId,
        value.RequirementId,
        value.SelectionRule,
        value.RequiredAlternativeCount,
        value.AlternativeCount,
        value.CompletedAlternativeIndices.ToArray(),
        value.RemainingRequiredSlots,
        value.ScopeComplete);

    private static string RequestId(
        string rolloutId,
        int transitionCount,
        string checkpointSha,
        string stateHash,
        string ledgerSha)
    {
        var identity = string.Join(
            "|",
            rolloutId,
            transitionCount,
            checkpointSha,
            stateHash,
            ledgerSha);
        var digest = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return rolloutId + ":transition:" + transitionCount + ":" + digest;
    }

    private static string ScopeKey(
        AcquisitionRoutePortfolioScopeProgress row) =>
        Uri.EscapeDataString(row.RequirementSetId) + "/" +
        Uri.EscapeDataString(row.RequirementId);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
