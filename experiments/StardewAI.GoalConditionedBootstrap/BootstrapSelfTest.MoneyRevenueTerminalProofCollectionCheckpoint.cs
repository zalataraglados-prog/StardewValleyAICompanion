namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueTerminalProofCollectionCheckpoint()
    {
        const string fixtureId = "terminal-proof-checkpoint";
        var before = MoneyRevenueTerminalSnapshot(
            gameTick: 200,
            money: 100,
            totalMoneyEarned: 49_900,
            inventoryStack: 1);
        var after = MoneyRevenueTerminalSnapshot(
            gameTick: 210,
            money: 120,
            totalMoneyEarned: 49_920,
            inventoryStack: 0);
        var episodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
            before,
            after,
            fixtureId);
        var root = Path.GetDirectoryName(episodePath) ??
            throw new InvalidDataException(
                "Money Revenue checkpoint fixture root is missing.");
        var beforeSnapshotPath = Path.Combine(root, "before.json");
        var afterSnapshotPath = Path.Combine(root, "after.json");
        var beforeLedgerPath = Path.Combine(root, "checkpoint-ledger-before.json");
        var afterLedgerPath = Path.Combine(root, "checkpoint-ledger-after.json");
        var beforeCapturePath = Path.Combine(root, "checkpoint-capture-before.json");
        var afterCapturePath = Path.Combine(root, "checkpoint-capture-after.json");
        var recurrenceManifestPath = Path.Combine(
            root,
            "shop-revenue-recurrence-manifest.json");
        var recurrenceReceiptPath = Path.Combine(
            root,
            "shop-revenue-recurrence-receipt.json");
        var ledger = MoneyRevenueTerminalLedger(before.StateHash);
        Write(beforeLedgerPath, ledger);
        Write(afterLedgerPath, ledger);
        var request = MoneyRevenueTerminalRequest(
            before,
            beforeSnapshotPath,
            beforeLedgerPath,
            ledger,
            "checkpoint");
        WriteMoneyRevenueBoundaryCaptures(
            request,
            beforeSnapshotPath,
            afterSnapshotPath,
            beforeLedgerPath,
            afterLedgerPath,
            beforeCapturePath,
            afterCapturePath);
        var proof = new MoneyRevenueTerminalAlternativeProof
        {
            RequestId = request.RequestId,
            BeforeStrategyLedgerCapturePath = beforeCapturePath,
            AfterStrategyLedgerCapturePath = afterCapturePath,
            RouteTransitions =
                Array.Empty<MoneyRevenueTerminalQueueTransitionProof>(),
            ShopSaleEpisodePath = episodePath,
            RevenueRecurrenceManifestPath = recurrenceManifestPath,
            RevenueRecurrenceReceiptPath = recurrenceReceiptPath
        };
        var plan = new MoneyRevenueTerminalProofCollectionPlan
        {
            RequestCount = 1,
            RequestIds = new[] { request.RequestId },
            TerminalProofIndexReady = false,
            FormalProductTrainingAuthorized = false
        };
        var catalog = new MoneyRevenueCounterfactualProofRequestCatalog
        {
            RequestCount = 1,
            RequestIds = new[] { request.RequestId },
            Requests = new[] { request }
        };
        var fragmentIndex = new MoneyRevenueTerminalProofFragmentIndex
        {
            CollectionPlanPath = "collection-plan.json",
            Alternatives = new[] { proof },
            FormalProductTrainingAuthorized = false
        };

        var checkpoint = MoneyRevenueTerminalProofCollectionCheckpointBuilder
            .BuildCheckpoint(
                "checkpoint-request.json",
                new string('1', 64),
                "collection-plan.json",
                new string('2', 64),
                plan,
                "fragment-index.json",
                new string('3', 64),
                catalog,
                fragmentIndex);
        Require(checkpoint.Status == "terminal_proof_denominator_complete" &&
                checkpoint.VerifiedFragmentCount == 1 &&
                checkpoint.PendingRequestCount == 0 &&
                checkpoint.TerminalProofIndexReady &&
                checkpoint.VerifiedEvidence.Length == 1 &&
                checkpoint.VerifiedEvidence[0].RequestId == request.RequestId &&
                !checkpoint.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof checkpoint was not complete.");
        var finalIndex = MoneyRevenueTerminalProofCollectionCheckpointBuilder
            .BuildFinalIndex(
                "checkpoint.json",
                new string('4', 64),
                checkpoint,
                plan,
                fragmentIndex);
        Require(finalIndex.Alternatives.Length == 1 &&
                finalIndex.Alternatives[0].RequestId == request.RequestId &&
                !finalIndex.FormalProductTrainingAuthorized,
            "Money Revenue final terminal-proof index was not exact.");

        fragmentIndex.Alternatives =
            Array.Empty<MoneyRevenueTerminalAlternativeProof>();
        var partial = MoneyRevenueTerminalProofCollectionCheckpointBuilder
            .BuildCheckpoint(
                "checkpoint-request.json",
                new string('1', 64),
                "collection-plan.json",
                new string('2', 64),
                plan,
                "fragment-index.json",
                new string('3', 64),
                catalog,
                fragmentIndex);
        Require(partial.Status == "terminal_proof_collection_in_progress" &&
                partial.VerifiedFragmentCount == 0 &&
                partial.PendingRequestCount == 1 &&
                partial.PendingRequestIds.SequenceEqual(
                    new[] { request.RequestId },
                    StringComparer.Ordinal) &&
                !partial.TerminalProofIndexReady,
            "Money Revenue partial terminal-proof checkpoint was not resumable.");

        fragmentIndex.Alternatives = new[] { proof, proof };
        var duplicateRejected = false;
        try
        {
            _ = MoneyRevenueTerminalProofCollectionCheckpointBuilder
                .BuildCheckpoint(
                    "checkpoint-request.json",
                    new string('1', 64),
                    "collection-plan.json",
                    new string('2', 64),
                    plan,
                    "fragment-index.json",
                    new string('3', 64),
                    catalog,
                    fragmentIndex);
        }
        catch (InvalidDataException)
        {
            duplicateRejected = true;
        }
        Require(duplicateRejected,
            "Duplicate Money Revenue terminal-proof fragments were admitted.");
    }
}
