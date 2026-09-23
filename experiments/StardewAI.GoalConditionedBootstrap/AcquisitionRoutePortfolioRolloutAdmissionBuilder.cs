using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRoutePortfolioRolloutAdmissionBuilder
{
    public static AcquisitionRoutePortfolioRolloutAdmissionReceipt Build(
        string proofManifestPath,
        string proofReceiptPath)
    {
        var manifestFullPath = Path.GetFullPath(proofManifestPath);
        var receiptFullPath = Path.GetFullPath(proofReceiptPath);
        var actual = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            receiptFullPath,
            "Acquisition route portfolio rollout proof receipt");
        var expected = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestFullPath);
        if (!EqualJson(actual, expected))
        {
            throw new InvalidDataException(
                "Rollout proof receipt does not equal controller recomputation.");
        }

        var result = new AcquisitionRoutePortfolioRolloutAdmissionReceipt
        {
            RolloutId = expected.RolloutId,
            GoalId = expected.GoalId,
            CommunityCenterProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.Clone(
                    expected.CommunityCenterProvenance),
            ProofManifestSha256 = CurrentTeacherFrontierSupport.HashFile(
                manifestFullPath),
            ProofReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                receiptFullPath),
            LatestCheckpointSha256 = expected.LatestCheckpointSha256,
            TransitionCount = expected.TransitionCount,
            LatestStateHash = expected.LatestStateHash,
            LatestLedgerRevision = expected.LatestLedgerRevision,
            LatestLedgerSha256 = expected.LatestLedgerSha256
        };
        var reasons = Validate(expected, result.ProofManifestSha256);
        if (reasons.Count > 0)
        {
            result.Status = "blocked_rollout_not_terminal";
            result.BlockingReasons = reasons.ToArray();
            return result;
        }

        result.Status = "ready_verified_terminal_portfolio_teacher_evidence";
        result.ControllerAdmissionGranted = true;
        result.TeacherTrainingEvidenceEligible = true;
        return result;
    }

    private static List<string> Validate(
        AcquisitionRoutePortfolioRolloutProofReceipt proof,
        string manifestSha256)
    {
        var reasons = new List<string>();
        if (!string.Equals(
                proof.SchemaVersion,
                "acquisition_route_portfolio_rollout_proof_receipt.v1",
                StringComparison.Ordinal))
            reasons.Add("rollout_proof_schema_mismatch");
        if (!proof.ProofChainVerified)
            reasons.Add("rollout_proof_chain_not_verified");
        if (!proof.PortfolioCompletionVerified)
            reasons.Add("rollout_portfolio_not_complete");
        if (proof.FormalTrainingAuthorized)
            reasons.Add("source_rollout_proof_must_not_self_authorize");
        if (!string.Equals(
                proof.ManifestSha256,
                manifestSha256,
                StringComparison.OrdinalIgnoreCase))
            reasons.Add("rollout_proof_manifest_digest_mismatch");
        if (string.IsNullOrWhiteSpace(proof.RolloutId) ||
            string.IsNullOrWhiteSpace(proof.GoalId) ||
            string.IsNullOrWhiteSpace(proof.LatestStateHash) ||
            !IsSha256(proof.LatestCheckpointSha256) ||
            !IsSha256(proof.LatestLedgerSha256) ||
            proof.TransitionCount < 1 ||
            proof.ContinuationTransitionCount != proof.TransitionCount - 1 ||
            proof.LatestLedgerRevision < 0)
        {
            reasons.Add("rollout_proof_terminal_identity_invalid");
        }
        if (!string.Equals(
                proof.Status,
                "verified_complete_rollout_proof_chain",
                StringComparison.Ordinal))
            reasons.Add("rollout_proof_status_not_complete");
        return reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
