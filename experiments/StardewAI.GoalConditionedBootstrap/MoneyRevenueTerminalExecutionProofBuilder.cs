namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalExecutionProofBuilder
{
    public static MoneyRevenueTerminalExecutionProofReceipt Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money Revenue terminal execution proof manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalExecutionProofManifest>(
            fullManifestPath,
            "Money Revenue terminal execution proof manifest");
        ValidateManifest(manifest);

        var requestManifestPath = RequiredFile(
            manifest.CounterfactualRequestManifestPath,
            "Money Revenue counterfactual request manifest");
        var requestCatalogPath = RequiredFile(
            manifest.CounterfactualRequestCatalogPath,
            "Money Revenue counterfactual request catalog");
        var expectedCatalog = MoneyRevenueCounterfactualProofRequestBuilder
            .Build(requestManifestPath);
        var requestCatalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCounterfactualProofRequestCatalog>(
            requestCatalogPath,
            "Money Revenue counterfactual request catalog");
        Require(EqualJson(requestCatalog, expectedCatalog) &&
                requestCatalog.RequestDenominatorAuthoritative &&
                requestCatalog.TerminalStrategyLedgerAnchorsVerified &&
                requestCatalog.CounterfactualProofCollectionReady &&
                !requestCatalog.ProductionTeacherPreferenceLabelEligible &&
                !requestCatalog.FormalProductTrainingAuthorized,
            "Money Revenue counterfactual request catalog is not exact and authoritative.");

        var proofs = manifest.Alternatives.ToDictionary(
            row => row.RequestId,
            StringComparer.Ordinal);
        Require(proofs.Count == requestCatalog.RequestCount &&
                requestCatalog.RequestIds.All(proofs.ContainsKey),
            "Money Revenue terminal proof denominator does not equal its request catalog.");
        var alternatives = requestCatalog.Requests
            .OrderBy(row => row.RequestId, StringComparer.Ordinal)
            .Select(request => VerifyAlternative(
                request,
                proofs[request.RequestId]))
            .ToArray();
        Require(alternatives.Length == requestCatalog.RequestCount &&
                alternatives.All(row =>
                    row.ReservationSafetyVerified &&
                    row.StrategyLedgerBoundaryArtifactsVerified &&
                    row.ResourceNeutralRouteVerified &&
                    row.NativeTerminalTransitionVerified &&
                    row.RevenueRecurrenceVerified),
            "Money Revenue terminal proof set is incomplete.");

        var receipt = new MoneyRevenueTerminalExecutionProofReceipt
        {
            Status = "verified_terminal_execution_proof_denominator",
            ManifestSha256 = Hash(fullManifestPath),
            CounterfactualRequestCatalogSha256 = Hash(requestCatalogPath),
            RequestCount = requestCatalog.RequestCount,
            VerifiedAlternativeCount = alternatives.Length,
            TerminalOpportunityCostProofsVerified = true,
            TerminalReservationClaimProofsVerified = true,
            StrategyLedgerBoundaryArtifactsVerified = true,
            NativeRevenueRecurrencesVerified = true,
            ProductionTeacherPreferenceLabelEligible = false,
            Alternatives = alternatives,
            BlockingReasons = new[]
            {
                "exact_request_id_comparison_admission_not_built"
            },
            FormalProductTrainingAuthorized = false
        };
        receipt.ReceiptSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                receipt);
        return receipt;
    }

    private static void ValidateManifest(
        MoneyRevenueTerminalExecutionProofManifest manifest)
    {
        var proofs = manifest.Alternatives ??
            Array.Empty<MoneyRevenueTerminalAlternativeProof>();
        Require(manifest.SchemaVersion ==
                    "money_revenue_terminal_execution_proof_manifest.v2" &&
                !manifest.FormalProductTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(
                    manifest.CounterfactualRequestManifestPath) &&
                !string.IsNullOrWhiteSpace(
                    manifest.CounterfactualRequestCatalogPath) &&
                proofs.Length > 0 &&
                proofs.All(row => row is not null &&
                    !string.IsNullOrWhiteSpace(row.RequestId) &&
                    !string.IsNullOrWhiteSpace(
                        row.BeforeStrategyLedgerCapturePath) &&
                    !string.IsNullOrWhiteSpace(
                        row.AfterStrategyLedgerCapturePath)) &&
                proofs.Select(row => row.RequestId)
                    .Distinct(StringComparer.Ordinal).Count() == proofs.Length,
            "Money Revenue terminal execution proof manifest is invalid.");
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
