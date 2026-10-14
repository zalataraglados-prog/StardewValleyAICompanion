using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueComparisonAdmissionBuilder
{
    public static MoneyRevenueComparisonAdmissionReceipt Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money Revenue comparison admission manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueComparisonAdmissionManifest>(
            fullManifestPath,
            "Money Revenue comparison admission manifest");
        Require(manifest.SchemaVersion ==
                    MoneyRevenueComparisonAdmissionVersions.Manifest &&
                !manifest.FormalProductTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(manifest.CriterionId),
            "Money Revenue comparison admission manifest is invalid.");

        var terminalManifestPath = RequiredFile(
            manifest.TerminalExecutionProofManifestPath,
            "Money Revenue terminal execution proof manifest");
        var terminalReceiptPath = RequiredFile(
            manifest.TerminalExecutionProofReceiptPath,
            "Money Revenue terminal execution proof receipt");
        var terminalManifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalExecutionProofManifest>(
            terminalManifestPath,
            "Money Revenue terminal execution proof manifest");
        var expectedTerminalReceipt = MoneyRevenueTerminalExecutionProofBuilder
            .Build(terminalManifestPath);
        var terminalReceipt = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalExecutionProofReceipt>(
            terminalReceiptPath,
            "Money Revenue terminal execution proof receipt");
        Require(EqualJson(terminalReceipt, expectedTerminalReceipt) &&
                terminalReceipt.TerminalOpportunityCostProofsVerified &&
                terminalReceipt.TerminalReservationClaimProofsVerified &&
                terminalReceipt.StrategyLedgerBoundaryArtifactsVerified &&
                terminalReceipt.NativeRevenueRecurrencesVerified &&
                !terminalReceipt.ProductionTeacherPreferenceLabelEligible &&
                !terminalReceipt.FormalProductTrainingAuthorized,
            "Money Revenue terminal execution proof receipt is not exact.");

        var requestManifestPath = RequiredFile(
            terminalManifest.CounterfactualRequestManifestPath,
            "Money Revenue counterfactual request manifest");
        var requestCatalogPath = RequiredFile(
            terminalManifest.CounterfactualRequestCatalogPath,
            "Money Revenue counterfactual request catalog");
        var expectedCatalog = MoneyRevenueCounterfactualProofRequestBuilder
            .Build(requestManifestPath);
        var catalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCounterfactualProofRequestCatalog>(
            requestCatalogPath,
            "Money Revenue counterfactual request catalog");
        Require(EqualJson(catalog, expectedCatalog) &&
                catalog.RequestDenominatorAuthoritative &&
                catalog.CounterfactualProofCollectionReady &&
                terminalReceipt.CounterfactualRequestCatalogSha256 ==
                    Hash(requestCatalogPath),
            "Money Revenue request catalog is not authoritative or terminal-bound.");

        var receipt = CompareVerifiedDenominator(
            manifest.CriterionId,
            catalog,
            terminalManifest,
            terminalReceipt);
        receipt.ManifestSha256 = Hash(fullManifestPath);
        receipt.RequestCatalogSha256 = Hash(requestCatalogPath);
        receipt.TerminalExecutionProofReceiptSha256 =
            Hash(terminalReceiptPath);
        receipt.ReceiptSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                receipt);
        return receipt;
    }

    internal static MoneyRevenueComparisonAdmissionReceipt
        CompareVerifiedDenominator(
            string criterionId,
            MoneyRevenueCounterfactualProofRequestCatalog catalog,
            MoneyRevenueTerminalExecutionProofManifest terminalManifest,
            MoneyRevenueTerminalExecutionProofReceipt terminalReceipt)
    {
        Require(!string.IsNullOrWhiteSpace(criterionId) &&
                catalog.RequestDenominatorAuthoritative &&
                catalog.TerminalStrategyLedgerAnchorsVerified &&
                catalog.CounterfactualProofCollectionReady &&
                !catalog.ProductionTeacherPreferenceLabelEligible &&
                !catalog.FormalProductTrainingAuthorized &&
                !terminalManifest.FormalProductTrainingAuthorized &&
                terminalReceipt.TerminalOpportunityCostProofsVerified &&
                terminalReceipt.TerminalReservationClaimProofsVerified &&
                terminalReceipt.StrategyLedgerBoundaryArtifactsVerified &&
                terminalReceipt.NativeRevenueRecurrencesVerified &&
                !terminalReceipt.ProductionTeacherPreferenceLabelEligible &&
                !terminalReceipt.FormalProductTrainingAuthorized,
            "Money Revenue comparison admission inputs are not authoritative and bounded.");
        var requests = catalog.Requests.ToDictionary(
            row => row.RequestId,
            StringComparer.Ordinal);
        var proofs = terminalManifest.Alternatives.ToDictionary(
            row => row.RequestId,
            StringComparer.Ordinal);
        var terminalEvidence = terminalReceipt.Alternatives.ToDictionary(
            row => row.RequestId,
            StringComparer.Ordinal);
        Require(catalog.RequestCount >= 2 &&
                requests.Count == catalog.RequestCount &&
                proofs.Count == catalog.RequestCount &&
                terminalEvidence.Count == catalog.RequestCount &&
                catalog.RequestIds.Order(StringComparer.Ordinal).SequenceEqual(
                    requests.Keys.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                catalog.RequestIds.All(proofs.ContainsKey) &&
                catalog.RequestIds.All(terminalEvidence.ContainsKey) &&
                terminalReceipt.RequestCount == catalog.RequestCount &&
                terminalReceipt.VerifiedAlternativeCount ==
                    catalog.RequestCount,
            "Money Revenue comparison request/evidence denominator is not exact.");

        var alternatives = catalog.RequestIds.Select(requestId =>
                VerifyAlternative(
                    requests[requestId],
                    proofs[requestId],
                    terminalEvidence[requestId]))
            .ToArray();
        var concreteIds = catalog.RequestIds
            .Select(requestId => requests[requestId].ConcreteAlternativeId)
            .ToArray();
        Require(concreteIds.Distinct(StringComparer.Ordinal).Count() ==
                    concreteIds.Length &&
                alternatives.Select(row => row.ConcreteAlternativeId)
                    .SequenceEqual(concreteIds, StringComparer.Ordinal),
            "Money Revenue concrete alternative identity drifted from requests.");

        var comparison = MoneyRevenueAlternativeComparer
            .CompareVerifiedArtifacts(
                criterionId,
                catalog.SourceStateHash,
                catalog.SourceStrategyLedgerRevision,
                concreteIds,
                alternatives.Select(row =>
                    new MoneyRevenueVerifiedArtifactAlternative(
                        row.ConcreteAlternativeId,
                        row.RevenuePrefix,
                        row.AggregateOpportunityCost,
                        row.ReservationNotRequired,
                        row.ReservationIds))
                    .ToArray());
        var labelEligible = comparison.TeacherPreferenceLabelEligible;
        return new MoneyRevenueComparisonAdmissionReceipt
        {
            Status = labelEligible
                ? "verified_authoritative_unique_revenue_preference"
                : "verified_authoritative_incomparable_revenue_frontier",
            SourceStateHash = catalog.SourceStateHash,
            SourceSnapshotSha256 = catalog.SourceSnapshotSha256,
            SourceStrategyLedgerSha256 =
                catalog.SourceStrategyLedgerSha256,
            SourceLedgerRevision = catalog.SourceStrategyLedgerRevision,
            RequestDenominatorCount = catalog.RequestCount,
            RequestIds = catalog.RequestIds.ToArray(),
            ConcreteAlternativeIds = concreteIds,
            CandidateDenominatorAuthoritative = true,
            TerminalExecutionProofsVerified = true,
            Comparison = comparison,
            Alternatives = alternatives,
            ProductionTeacherPreferenceLabelEligible = labelEligible,
            BlockingReasons = labelEligible
                ? new[]
                {
                    "criterion_partition_native_outcome_admission_not_built"
                }
                : new[]
                {
                    "authoritative_revenue_frontier_incomparable"
                },
            FormalProductTrainingAuthorized = false
        };
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));
}
