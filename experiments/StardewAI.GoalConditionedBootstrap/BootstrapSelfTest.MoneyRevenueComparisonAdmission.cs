using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueComparisonAdmission()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-money-revenue-comparison-admission");
        Directory.CreateDirectory(root);
        var before = MoneyRevenueTerminalSnapshot(
            gameTick: 100,
            money: 100,
            totalMoneyEarned: 49_990,
            inventoryStack: 1);
        var sourceSnapshotPath = Path.Combine(root, "source-snapshot.json");
        Write(sourceSnapshotPath, before);
        var ledger = MoneyRevenueTerminalLedger(before.StateHash);
        var sourceLedgerPath = Path.Combine(root, "source-ledger.json");
        Write(sourceLedgerPath, ledger);

        var fast = MoneyRevenueComparisonAdmissionAlternative(
            root,
            "fast",
            before,
            afterTick: 142,
            sourceSnapshotPath,
            sourceLedgerPath,
            ledger);
        var slow = MoneyRevenueComparisonAdmissionAlternative(
            root,
            "slow",
            before,
            afterTick: 184,
            sourceSnapshotPath,
            sourceLedgerPath,
            ledger);
        var requests = new[] { fast.Request, slow.Request };
        var catalog = new MoneyRevenueCounterfactualProofRequestCatalog
        {
            Status = "ready_for_counterfactual_proof_collection",
            SourceStateHash = before.StateHash,
            SourceSnapshotPath = sourceSnapshotPath,
            SourceSnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourceSnapshotPath),
            SourceStrategyLedgerPath = sourceLedgerPath,
            SourceStrategyLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourceLedgerPath),
            SourceStrategyLedgerId = ledger.LedgerId,
            SourceStrategyLedgerRevision = ledger.Revision,
            RequestDenominatorAuthoritative = true,
            TerminalStrategyLedgerAnchorsVerified = true,
            TerminalStrategyLedgerAnchorCount = requests.Length,
            CounterfactualProofCollectionReady = true,
            RequestCount = requests.Length,
            RequestIds = requests.Select(row => row.RequestId).ToArray(),
            Requests = requests
        };
        var terminalManifest = new
            MoneyRevenueTerminalExecutionProofManifest
        {
            Alternatives = new[] { fast.Proof, slow.Proof }
        };
        var terminalReceipt = new MoneyRevenueTerminalExecutionProofReceipt
        {
            Status = "verified_terminal_execution_proof_denominator",
            RequestCount = requests.Length,
            VerifiedAlternativeCount = requests.Length,
            TerminalOpportunityCostProofsVerified = true,
            TerminalReservationClaimProofsVerified = true,
            NativeRevenueRecurrencesVerified = true,
            Alternatives = new[] { fast.Evidence, slow.Evidence }
        };
        var receipt = MoneyRevenueComparisonAdmissionBuilder
            .CompareVerifiedDenominator(
                "money_50000",
                catalog,
                terminalManifest,
                terminalReceipt);
        Require(receipt.SchemaVersion ==
                    MoneyRevenueComparisonAdmissionVersions.Receipt &&
                receipt.CandidateDenominatorAuthoritative &&
                receipt.TerminalExecutionProofsVerified &&
                receipt.ProductionTeacherPreferenceLabelEligible &&
                !receipt.FormalProductTrainingAuthorized &&
                receipt.RequestDenominatorCount == 2 &&
                receipt.Alternatives.All(row =>
                    !row.AcquisitionRolloutRequired &&
                    row.AcquisitionEvidence is null &&
                    row.AcquisitionOpportunityCost is null &&
                    row.ReservationNotRequired &&
                    row.ReservationIds.Length == 0) &&
                receipt.Comparison.SelectedAlternativeId ==
                    fast.Request.ConcreteAlternativeId &&
                receipt.Comparison.Evaluations.Single(row =>
                    row.AlternativeId == fast.Request.ConcreteAlternativeId)
                    .OpportunityCost.GuaranteedElapsedGameMinutes == 1 &&
                receipt.Comparison.Evaluations.Single(row =>
                    row.AlternativeId == slow.Request.ConcreteAlternativeId)
                    .OpportunityCost.GuaranteedElapsedGameMinutes == 2,
            "Money Revenue exact-ID comparison admission drifted.");

        var join = MoneyThresholdTeacherCorpusBuilder
            .VerifyComparisonJoinForTest(
                "money_50000",
                fast.EpisodePath,
                receipt);
        Require(join.PreferredAlternative ==
                    fast.Request.ConcreteAlternativeId &&
                join.RejectedAlternatives.SequenceEqual(
                    new[] { slow.Request.ConcreteAlternativeId },
                    StringComparer.Ordinal),
            "Money threshold outcome did not join its exact comparison preference.");

        var selectedIndex = Array.FindIndex(
            receipt.Alternatives,
            value => value.ConcreteAlternativeId ==
                receipt.Comparison.SelectedAlternativeId);
        var selected = receipt.Alternatives[selectedIndex];
        receipt.Alternatives[selectedIndex] = selected with
        {
            NativeTerminalArtifacts = selected.NativeTerminalArtifacts
                .Where(value => value.Role != "shop_sale_episode")
                .ToArray()
        };
        var mismatchedEpisodeRejected = false;
        try
        {
            MoneyThresholdTeacherCorpusBuilder.VerifyComparisonJoinForTest(
                "money_50000",
                fast.EpisodePath,
                receipt);
        }
        catch (InvalidDataException)
        {
            mismatchedEpisodeRejected = true;
        }
        Require(mismatchedEpisodeRejected,
            "A comparison without the selected native sale episode was admitted.");
        receipt.Alternatives[selectedIndex] = selected;

        terminalReceipt.Alternatives = new[] { fast.Evidence };
        var rejected = false;
        try
        {
            MoneyRevenueComparisonAdmissionBuilder.CompareVerifiedDenominator(
                "money_50000",
                catalog,
                terminalManifest,
                terminalReceipt);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "An incomplete Money Revenue terminal evidence denominator was admitted.");
    }

    private static MoneyRevenueComparisonAdmissionFixture
        MoneyRevenueComparisonAdmissionAlternative(
            string root,
            string identity,
            SnapshotEnvelope before,
            long afterTick,
            string sourceSnapshotPath,
            string sourceLedgerPath,
            StrategyCommitmentLedger ledger)
    {
        var after = MoneyRevenueTerminalSnapshot(
            gameTick: afterTick,
            money: 120,
            totalMoneyEarned: 50_010,
            inventoryStack: 0);
        var episodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
            before,
            after,
            "comparison-admission-" + identity);
        var episodeRoot = Path.GetDirectoryName(episodePath) ??
            throw new InvalidDataException(
                "Money Revenue comparison episode root is missing.");
        var terminalSnapshotPath = Path.Combine(episodeRoot, "before.json");
        var afterLedgerPath = Path.Combine(
            root,
            "after-ledger-" + identity + ".json");
        Write(afterLedgerPath, ledger);
        var request = MoneyRevenueTerminalRequest(
            before,
            terminalSnapshotPath,
            sourceLedgerPath,
            ledger,
            identity);
        request.SourceSnapshotPath = sourceSnapshotPath;
        request.SourceSnapshotSha256 =
            CurrentTeacherFrontierSupport.HashFile(sourceSnapshotPath);
        var proof = new MoneyRevenueTerminalAlternativeProof
        {
            RequestId = request.RequestId,
            BeforeStrategyLedgerPath = sourceLedgerPath,
            AfterStrategyLedgerPath = afterLedgerPath,
            RouteTransitions =
                Array.Empty<MoneyRevenueTerminalQueueTransitionProof>(),
            ShopSaleEpisodePath = episodePath,
            RevenueRecurrenceManifestPath = Path.Combine(
                episodeRoot,
                "shop-revenue-recurrence-manifest.json"),
            RevenueRecurrenceReceiptPath = Path.Combine(
                episodeRoot,
                "shop-revenue-recurrence-receipt.json")
        };
        var evidence = MoneyRevenueTerminalExecutionProofBuilder
            .VerifyAlternative(request, proof);
        return new MoneyRevenueComparisonAdmissionFixture(
            request,
            proof,
            evidence,
            episodePath);
    }

    private sealed record MoneyRevenueComparisonAdmissionFixture(
        MoneyRevenueCounterfactualProofRequest Request,
        MoneyRevenueTerminalAlternativeProof Proof,
        MoneyRevenueTerminalExecutionEvidence Evidence,
        string EpisodePath);
}
