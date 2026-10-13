using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueRecurrenceProofBuilder
{
    public static MoneyRevenueRecurrenceProofReceipt Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money revenue recurrence proof manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofManifest>(
            fullManifestPath,
            "Money revenue recurrence proof manifest");
        ValidateManifest(manifest);

        var evidence = manifest.Transitions.Select(
                (proof, index) => VerifyTransition(proof, index))
            .ToArray();
        var prefix = MoneyRevenueRecurrenceVerifier.VerifyPrefix(
            evidence.Select(row => row.Progression).ToArray());
        return new MoneyRevenueRecurrenceProofReceipt
        {
            Status = "verified_revenue_recurrence_proof",
            ManifestPath = fullManifestPath,
            ManifestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullManifestPath),
            Prefix = prefix,
            Transitions = evidence,
            ProofVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueRecurrenceTransitionProofEvidence
        VerifyTransition(
            MoneyRevenueRecurrenceTransitionProof proof,
            int index) => proof.ProofKind switch
        {
            MoneyRevenueRecurrenceProofKinds.NativeShopSale =>
                VerifyShopSale(proof, index),
            MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement =>
                VerifyShippingSettlement(proof, index),
            _ => throw new InvalidDataException(
                "Money revenue recurrence proof kind is unsupported: " +
                proof.ProofKind)
        };

    private static MoneyRevenueRecurrenceTransitionProofEvidence
        VerifyShopSale(
            MoneyRevenueRecurrenceTransitionProof proof,
            int index)
    {
        Require(ShippingFieldsEmpty(proof),
            "Shop-sale revenue proof contains shipping fields.");
        var sale = NativeShopSaleEpisodeVerifier.Verify(
            proof.ShopSaleEpisodePath);
        return new MoneyRevenueRecurrenceTransitionProofEvidence(
            index,
            proof.TransitionId,
            proof.ProofKind,
            sale.Progression,
            new[]
            {
                Artifact("shop_sale_episode", sale.ExecutionEpisodePath,
                    sale.ExecutionEpisodeSha256),
                Artifact("before_snapshot", sale.BeforeSnapshotPath,
                    sale.BeforeSnapshotSha256),
                Artifact("execution_result", sale.ExecutionResultPath,
                    sale.ExecutionResultSha256),
                Artifact("after_snapshot", sale.AfterSnapshotPath,
                    sale.AfterSnapshotSha256)
            });
    }

    private static MoneyRevenueRecurrenceTransitionProofEvidence
        VerifyShippingSettlement(
            MoneyRevenueRecurrenceTransitionProof proof,
            int index)
    {
        Require(string.IsNullOrWhiteSpace(proof.ShopSaleEpisodePath) &&
                ShippingFieldsComplete(proof),
            "Shipping revenue proof identity is incomplete or mixed.");
        var queuePath = RequiredFile(proof.QueuePath, "Revenue sleep queue");
        var beforePath = RequiredFile(
            proof.BeforeSnapshotPath,
            "Revenue shipping before snapshot");
        var receiptPath = RequiredFile(
            proof.ExecutionReceiptPath,
            "Revenue shipping execution receipt");
        var afterPath = RequiredFile(
            proof.AfterSnapshotPath,
            "Revenue shipping after snapshot");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queuePath,
            "Revenue sleep queue");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Revenue shipping before snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
            receiptPath,
            "Revenue shipping execution receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Revenue shipping after snapshot");
        var settlement = NativeShippingSettlementEvidenceVerifier.Verify(
            queue,
            before,
            receipt,
            after,
            proof.RunId,
            proof.ExecutorVersion,
            proof.SelectedCandidateId);
        if (!settlement.Verified || settlement.Progression is null)
        {
            throw new InvalidDataException(
                "Native shipping revenue proof was rejected: " +
                string.Join(",", settlement.BlockingReasons));
        }
        var progression = settlement.Progression;
        return new MoneyRevenueRecurrenceTransitionProofEvidence(
            index,
            proof.TransitionId,
            proof.ProofKind,
            progression,
            new[]
            {
                Artifact("queue", queuePath),
                Artifact("before_snapshot", beforePath),
                Artifact("execution_receipt", receiptPath),
                Artifact("after_snapshot", afterPath)
            });
    }

    private static MoneyRevenueArtifactBinding Artifact(
        string role,
        string path,
        string? sha256 = null) => new(
            role,
            Path.GetFullPath(path),
            sha256 ?? CurrentTeacherFrontierSupport.HashFile(path));
}
