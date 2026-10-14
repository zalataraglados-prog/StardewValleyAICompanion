using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCounterfactualProofRequestBuilder
{
    private const string SourceLedgerAnchorKind =
        "source_strategy_ledger";
    private const string RolloutLedgerAnchorKind =
        "acquisition_rollout_latest_settlement";

    private static IReadOnlyDictionary<
        string,
        MoneyRevenueTerminalStrategyLedgerAnchor>
        BuildTerminalLedgerAnchors(
            MoneyRevenueConcreteAlternativeDenominatorCatalog concrete,
            string sourceLedgerPath,
            string sourceLedgerSha256,
            StrategyCommitmentLedger sourceLedger)
    {
        var anchors = new Dictionary<
            string,
            MoneyRevenueTerminalStrategyLedgerAnchor>(
                StringComparer.Ordinal);
        var rolloutCache = new Dictionary<
            string,
            MoneyRevenueTerminalStrategyLedgerAnchor>(
                StringComparer.Ordinal);
        foreach (var alternative in concrete.Alternatives)
        {
            var anchor = alternative.RequiresAcquisitionOrProcessingRollout
                ? ResolveRolloutTerminalLedger(
                    alternative,
                    sourceLedger,
                    rolloutCache)
                : new MoneyRevenueTerminalStrategyLedgerAnchor(
                    SourceLedgerAnchorKind,
                    sourceLedgerPath,
                    sourceLedgerSha256,
                    sourceLedger.LedgerId,
                    sourceLedger.Revision,
                    sourceLedger.SourceStateHash);
            Require(anchors.TryAdd(
                    alternative.ConcreteAlternativeId,
                    anchor),
                "Money Revenue terminal strategy-ledger anchor identity duplicated.");
        }
        return anchors;
    }

    private static MoneyRevenueTerminalStrategyLedgerAnchor
        ResolveRolloutTerminalLedger(
            MoneyRevenueConcreteAlternative alternative,
            StrategyCommitmentLedger sourceLedger,
            IDictionary<string, MoneyRevenueTerminalStrategyLedgerAnchor>
                cache)
    {
        var manifestPath = RequiredFile(
            alternative.AcquisitionRolloutManifestPath,
            "Money Revenue acquisition rollout proof manifest");
        var receiptPath = RequiredFile(
            alternative.AcquisitionRolloutReceiptPath,
            "Money Revenue acquisition rollout proof receipt");
        var cacheKey = manifestPath + "\n" + receiptPath;
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        Require(Hash(manifestPath) ==
                    alternative.AcquisitionRolloutManifestSha256 &&
                Hash(receiptPath) ==
                    alternative.AcquisitionRolloutReceiptSha256,
            "Money Revenue acquisition rollout proof identity drifted while resolving its terminal ledger.");
        var manifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            manifestPath,
            "Money Revenue acquisition rollout proof manifest");
        var rebuilt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildVerifiedReceipt(manifestPath);
        var stored = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            receiptPath,
            "Money Revenue acquisition rollout proof receipt");
        Require(EqualJson(stored, rebuilt.Receipt) &&
                stored.ProofChainVerified &&
                stored.PortfolioCompletionVerified &&
                !stored.FormalTrainingAuthorized,
            "Money Revenue acquisition rollout proof is not complete and exact while resolving its terminal ledger.");

        var transitions = manifest.ContinuationTransitions ??
            throw new InvalidDataException(
                "Money Revenue acquisition rollout transition list is missing.");
        var settledLedgerPath = RequiredFile(
            transitions.Length == 0
                ? manifest.InitialCheckpointProof.SettledLedgerPath
                : transitions[^1].SettledLedgerPath,
            "Money Revenue acquisition rollout terminal settled ledger");
        var terminalSnapshotPath = RequiredFile(
            alternative.TerminalSnapshotPath,
            "Money Revenue terminal snapshot");
        Require(Hash(terminalSnapshotPath) ==
                    alternative.TerminalSnapshotSha256,
            "Money Revenue terminal snapshot drifted while resolving its strategy ledger.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            terminalSnapshotPath,
            "Money Revenue terminal snapshot");
        using var snapshotDocument = JsonDocument.Parse(
            CurrentTeacherFrontierSupport.ReadArtifactText(
                terminalSnapshotPath));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            settledLedgerPath,
            snapshotDocument.RootElement).Ledger;
        var ledgerSha256 = Hash(settledLedgerPath);
        Require(snapshot.StateHash == alternative.TerminalStateHash &&
                ledger.LedgerId == sourceLedger.LedgerId &&
                ledger.SourceStateHash == alternative.TerminalStateHash &&
                ledger.Revision >= sourceLedger.Revision &&
                ledger.Revision == stored.LatestLedgerRevision &&
                ledgerSha256 == stored.LatestLedgerSha256,
            "Money Revenue rollout terminal strategy ledger does not bind its verified terminal state.");
        var anchor = new MoneyRevenueTerminalStrategyLedgerAnchor(
            RolloutLedgerAnchorKind,
            settledLedgerPath,
            ledgerSha256,
            ledger.LedgerId,
            ledger.Revision,
            ledger.SourceStateHash);
        cache.Add(cacheKey, anchor);
        return anchor;
    }
}
