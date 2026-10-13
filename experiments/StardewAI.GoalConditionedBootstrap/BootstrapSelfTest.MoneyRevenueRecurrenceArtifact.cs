using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueShippingRecurrenceArtifact(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope executionReceipt,
        SnapshotEnvelope after,
        string executorVersion,
        string selectedCandidateId,
        string saleEpisodePath)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-money-revenue-recurrence-artifact");
        Directory.CreateDirectory(root);
        var queuePath = Path.Combine(root, "queue.json");
        var beforePath = Path.Combine(root, "before.json");
        var executionReceiptPath = Path.Combine(
            root,
            "execution-receipt.json");
        var afterPath = Path.Combine(root, "after.json");
        Write(queuePath, queue);
        Write(beforePath, before);
        Write(executionReceiptPath, executionReceipt);
        Write(afterPath, after);

        var proof = new MoneyRevenueRecurrenceTransitionProof
        {
            TransitionId = "transition.shipping",
            ProofKind =
                MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement,
            QueuePath = queuePath,
            BeforeSnapshotPath = beforePath,
            ExecutionReceiptPath = executionReceiptPath,
            AfterSnapshotPath = afterPath,
            RunId = executionReceipt.RunId,
            ExecutorVersion = executorVersion,
            SelectedCandidateId = selectedCandidateId
        };
        var manifestPath = Path.Combine(root, "manifest.json");
        var saleProof = new MoneyRevenueRecurrenceTransitionProof
        {
            TransitionId = "transition.shop-sale",
            ProofKind = MoneyRevenueRecurrenceProofKinds.NativeShopSale,
            ShopSaleEpisodePath = saleEpisodePath
        };
        Write(manifestPath, RevenueRecurrenceManifest(saleProof, proof));
        var receipt = MoneyRevenueRecurrenceProofBuilder.Build(manifestPath);
        Require(
            receipt.Status == "verified_revenue_recurrence_proof" &&
            receipt.ProofVerified &&
            !receipt.FormalProductTrainingAuthorized &&
            receipt.Prefix?.TotalRevenue == 320 &&
            receipt.Transitions.Length == 2 &&
            receipt.Transitions[0].ProofKind ==
                MoneyRevenueRecurrenceProofKinds.NativeShopSale &&
            receipt.Transitions[1].ProofKind ==
                MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement &&
            receipt.Transitions.All(transition =>
                transition.Artifacts.Length == 4 &&
                transition.Artifacts.All(artifact =>
                    artifact.Sha256.Length == 64)),
            "Shipping Revenue recurrence artifact binding was rejected.");

        var tamperedPath = Path.Combine(root, "tampered-manifest.json");
        proof.SelectedCandidateId += ".forged";
        Write(tamperedPath, RevenueRecurrenceManifest(saleProof, proof));
        RequireMoneyRevenueRecurrenceArtifactRejected(
            tamperedPath,
            "A shipping proof with a forged candidate binding was admitted.");
    }

    private static void VerifyMoneyRevenueShopRecurrenceArtifact(
        string root,
        string episodePath)
    {
        var manifestPath = Path.Combine(
            root,
            "shop-revenue-recurrence-manifest.json");
        Write(manifestPath, RevenueRecurrenceManifest(
            new MoneyRevenueRecurrenceTransitionProof
            {
                TransitionId = "transition.shop-sale",
                ProofKind = MoneyRevenueRecurrenceProofKinds.NativeShopSale,
                ShopSaleEpisodePath = episodePath
            }));
        var receipt = MoneyRevenueRecurrenceProofBuilder.Build(manifestPath);
        Require(
            receipt.ProofVerified &&
            receipt.Prefix?.TotalRevenue == 20 &&
            receipt.Transitions.Length == 1 &&
            receipt.Transitions[0].ProofKind ==
                MoneyRevenueRecurrenceProofKinds.NativeShopSale &&
            receipt.Transitions[0].Artifacts.Length == 4,
            "Shop-sale Revenue recurrence artifact binding was rejected.");
    }

    private static MoneyRevenueRecurrenceProofManifest RevenueRecurrenceManifest(
        params MoneyRevenueRecurrenceTransitionProof[] transitions) => new()
        {
            Transitions = transitions,
            FormalProductTrainingAuthorized = false
        };

    private static void RequireMoneyRevenueRecurrenceArtifactRejected(
        string manifestPath,
        string message)
    {
        var rejected = false;
        try
        {
            MoneyRevenueRecurrenceProofBuilder.Build(manifestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }
}
