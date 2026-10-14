using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCounterfactualProofRequestBuilder
{
    public static MoneyRevenueCounterfactualProofRequestCatalog Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money Revenue counterfactual proof request manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCounterfactualProofRequestManifest>(
            fullManifestPath,
            "Money Revenue counterfactual proof request manifest");
        ValidateManifest(manifest);
        var concretePath = RequiredFile(
            manifest.ConcreteAlternativeCatalogPath,
            "Money Revenue concrete alternative catalog");
        var concrete = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueConcreteAlternativeDenominatorCatalog>(
            concretePath,
            "Money Revenue concrete alternative catalog");
        var rebuilt = MoneyRevenueConcreteAlternativeDenominatorBuilder.Build(
            concrete.ManifestPath);
        Require(EqualJson(concrete, rebuilt),
            "Money Revenue concrete alternative catalog did not equal its source rebuild.");
        var sourceSnapshotPath = RequiredFile(
            concrete.SourceSnapshotPath,
            "Money Revenue source snapshot");
        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            sourceSnapshotPath,
            "Money Revenue source snapshot");
        Require(Hash(sourceSnapshotPath) == concrete.SourceSnapshotSha256 &&
                sourceSnapshot.StateHash == concrete.SourceStateHash,
            "Money Revenue concrete catalog source snapshot drifted.");
        var ledgerPath = RequiredFile(
            manifest.SourceStrategyLedgerPath,
            "Money Revenue source strategy ledger");
        var ledger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            ledgerPath,
            "Money Revenue source strategy ledger");
        ValidateSourceLedger(ledger, sourceSnapshot, concrete.SourceStateHash);
        return BuildCatalog(
            Hash(fullManifestPath),
            concretePath,
            Hash(concretePath),
            concrete,
            ledgerPath,
            Hash(ledgerPath),
            ledger);
    }

    internal static MoneyRevenueCounterfactualProofRequestCatalog BuildCatalog(
        string manifestSha256,
        string concreteCatalogPath,
        string concreteCatalogFileSha256,
        MoneyRevenueConcreteAlternativeDenominatorCatalog concrete,
        string ledgerPath,
        string ledgerSha256,
        StrategyCommitmentLedger ledger)
    {
        Require(concrete.CurrentConcreteDenominatorAuthoritative &&
                concrete.SkeletonDenominatorComplete &&
                concrete.TerminalRebindDenominatorComplete &&
                !concrete.ProductionTeacherPreferenceLabelEligible &&
                !concrete.FormalProductTrainingAuthorized &&
                concrete.Alternatives.Length ==
                    concrete.ConcreteAlternativeCount &&
                concrete.Alternatives.Length > 0,
            "Money Revenue concrete alternative denominator is not authoritative and bounded.");
        Require(ledger.SchemaVersion == "strategy_commitment_ledger.v1" &&
                !string.IsNullOrWhiteSpace(ledger.LedgerId) &&
                ledger.Revision >= 0 &&
                ledger.SourceStateHash == concrete.SourceStateHash,
            "Money Revenue source strategy ledger does not bind the concrete denominator.");
        var requests = concrete.Alternatives
            .OrderBy(row => row.ConcreteAlternativeId, StringComparer.Ordinal)
            .Select(row => BuildRequest(
                row,
                concrete.SourceStateHash,
                concrete.SourceSnapshotPath,
                concrete.SourceSnapshotSha256,
                ledgerSha256,
                ledger.Revision))
            .ToArray();
        Require(requests.Length == concrete.ConcreteAlternativeCount &&
                requests.Select(row => row.ConcreteAlternativeId)
                    .SequenceEqual(
                        concrete.ConcreteAlternativeIds,
                        StringComparer.Ordinal) &&
                requests.Select(row => row.RequestId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    requests.Length,
            "Money Revenue counterfactual proof request denominator drifted.");
        var catalog = new MoneyRevenueCounterfactualProofRequestCatalog
        {
            Status = "ready_for_counterfactual_proof_collection",
            ManifestSha256 = manifestSha256,
            ConcreteAlternativeCatalogPath = concreteCatalogPath,
            ConcreteAlternativeCatalogFileSha256 =
                concreteCatalogFileSha256,
            ConcreteAlternativeCatalogSha256 = concrete.CatalogSha256,
            SourceStateHash = concrete.SourceStateHash,
            SourceSnapshotPath = concrete.SourceSnapshotPath,
            SourceSnapshotSha256 = concrete.SourceSnapshotSha256,
            SourceStrategyLedgerPath = ledgerPath,
            SourceStrategyLedgerSha256 = ledgerSha256,
            SourceStrategyLedgerId = ledger.LedgerId,
            SourceStrategyLedgerRevision = ledger.Revision,
            RequestDenominatorAuthoritative = true,
            CounterfactualProofCollectionReady = true,
            ProductionTeacherPreferenceLabelEligible = false,
            RequestCount = requests.Length,
            RequestIds = requests.Select(row => row.RequestId).ToArray(),
            Requests = requests,
            BlockingReasons = new[]
            {
                "terminal_opportunity_cost_proofs_not_supplied",
                "verified_counterfactual_revenue_recurrences_not_supplied"
            },
            FormalProductTrainingAuthorized = false
        };
        catalog.CatalogSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                catalog);
        return catalog;
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));
}
