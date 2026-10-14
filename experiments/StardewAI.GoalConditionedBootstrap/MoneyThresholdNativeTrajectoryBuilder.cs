using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeTrajectoryBuilder
{
    public static MoneyThresholdNativeTrajectoryReceipt Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money threshold native trajectory manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdNativeTrajectoryManifest>(
                fullManifestPath,
                "Money threshold native trajectory manifest");
        Require(manifest.SchemaVersion ==
                    MoneyThresholdNativeTrajectoryVersions.Manifest &&
                !string.IsNullOrWhiteSpace(manifest.RequestId) &&
                !manifest.FormalProductTrainingAuthorized,
            "Money threshold native trajectory manifest is invalid.");

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
            "Money threshold native trajectory terminal proof is not exact.");

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
                catalog.TerminalStrategyLedgerAnchorsVerified &&
                catalog.CounterfactualProofCollectionReady &&
                terminalReceipt.CounterfactualRequestCatalogSha256 ==
                    Hash(requestCatalogPath),
            "Money threshold native trajectory request catalog is not exact.");

        return ProjectVerified(
            fullManifestPath,
            terminalManifestPath,
            terminalReceiptPath,
            requestCatalogPath,
            manifest.RequestId,
            catalog,
            terminalManifest,
            terminalReceipt);
    }

    public static MoneyThresholdNativeTrajectoryReceipt Verify(
        string receiptPath)
    {
        var fullReceiptPath = RequiredFile(
            receiptPath,
            "Money threshold native trajectory receipt");
        var receipt = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdNativeTrajectoryReceipt>(
                fullReceiptPath,
                "Money threshold native trajectory receipt");
        Require(receipt.SchemaVersion ==
                    MoneyThresholdNativeTrajectoryVersions.Receipt &&
                receipt.NativeOutcomeVerified &&
                !receipt.TeacherPreferenceLabelVerified &&
                !receipt.FormalProductTrainingAuthorized &&
                receipt.ReceiptSha256.Length == 64,
            "Money threshold native trajectory receipt is invalid.");
        var expected = Build(receipt.ManifestPath);
        Require(EqualJson(receipt, expected),
            "Money threshold native trajectory receipt drifted from its exact rebuild.");
        return receipt;
    }

    internal static MoneyThresholdNativeTrajectoryReceipt ProjectVerified(
        string manifestPath,
        string terminalManifestPath,
        string terminalReceiptPath,
        string requestCatalogPath,
        string requestId,
        MoneyRevenueCounterfactualProofRequestCatalog catalog,
        MoneyRevenueTerminalExecutionProofManifest terminalManifest,
        MoneyRevenueTerminalExecutionProofReceipt terminalReceipt)
    {
        Require(catalog.RequestDenominatorAuthoritative &&
                catalog.TerminalStrategyLedgerAnchorsVerified &&
                catalog.CounterfactualProofCollectionReady &&
                !catalog.ProductionTeacherPreferenceLabelEligible &&
                !catalog.FormalProductTrainingAuthorized &&
                terminalReceipt.TerminalOpportunityCostProofsVerified &&
                terminalReceipt.TerminalReservationClaimProofsVerified &&
                terminalReceipt.StrategyLedgerBoundaryArtifactsVerified &&
                terminalReceipt.NativeRevenueRecurrencesVerified &&
                !terminalReceipt.ProductionTeacherPreferenceLabelEligible &&
                !terminalReceipt.FormalProductTrainingAuthorized,
            "Money threshold native trajectory inputs are not verified native evidence.");

        var requests = catalog.Requests.ToDictionary(
            value => value.RequestId,
            StringComparer.Ordinal);
        var proofs = terminalManifest.Alternatives.ToDictionary(
            value => value.RequestId,
            StringComparer.Ordinal);
        var terminalEvidence = terminalReceipt.Alternatives.ToDictionary(
            value => value.RequestId,
            StringComparer.Ordinal);
        Require(catalog.RequestCount >= 1 &&
                requests.Count == catalog.RequestCount &&
                proofs.Count == catalog.RequestCount &&
                terminalEvidence.Count == catalog.RequestCount &&
                terminalReceipt.RequestCount == catalog.RequestCount &&
                terminalReceipt.VerifiedAlternativeCount ==
                    catalog.RequestCount &&
                catalog.RequestIds.Order(StringComparer.Ordinal).SequenceEqual(
                    requests.Keys.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                catalog.RequestIds.All(proofs.ContainsKey) &&
                catalog.RequestIds.All(terminalEvidence.ContainsKey),
            "Money threshold native trajectory denominator is incomplete.");
        Require(requests.TryGetValue(requestId, out var request),
            "Money threshold native trajectory request is missing.");
        Require(proofs.TryGetValue(requestId, out var proof),
            "Money threshold native trajectory terminal proof is missing.");
        Require(terminalEvidence.TryGetValue(requestId, out var terminal),
            "Money threshold native trajectory terminal evidence is missing.");

        var alternative = MoneyRevenueComparisonAdmissionBuilder
            .VerifyAlternative(request, proof, terminal);
        var sourceSnapshotPath = RequiredFile(
            request!.SourceSnapshotPath,
            "Money threshold native trajectory source snapshot");
        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<
            SnapshotEnvelope>(
                sourceSnapshotPath,
                "Money threshold native trajectory source snapshot");
        var source = MoneyRevenueProgressionVerifier.Project(sourceSnapshot);
        var prefix = alternative.RevenuePrefix;
        var final = prefix.Transitions[^1].After;
        Require(Hash(sourceSnapshotPath) == request.SourceSnapshotSha256 &&
                source.StateHash == request.SourceStateHash &&
                source.SaveId == prefix.SaveId &&
                source.PlayerId == prefix.PlayerId &&
                source.TotalMoneyEarned == prefix.InitialTotalMoneyEarned &&
                final.StateHash == prefix.FinalStateHash &&
                final.SaveId == source.SaveId &&
                final.PlayerId == source.PlayerId &&
                final.TotalMoneyEarned > source.TotalMoneyEarned,
            "Money threshold native trajectory does not preserve source-to-outcome identity.");

        var artifacts = BuildArtifacts(
            sourceSnapshotPath,
            terminalManifest.CounterfactualRequestManifestPath,
            requestCatalogPath,
            terminalManifestPath,
            terminalReceiptPath,
            request,
            proof,
            alternative)
            .OrderBy(value => value.Role, StringComparer.Ordinal)
            .ToArray();
        Require(artifacts.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() == artifacts.Length,
            "Money threshold native trajectory artifact roles are not unique.");

        var receipt = new MoneyThresholdNativeTrajectoryReceipt
        {
            Status = "verified_native_money_threshold_trajectory",
            ManifestPath = Path.GetFullPath(manifestPath),
            ManifestSha256 = Hash(manifestPath),
            RequestId = request.RequestId,
            ConcreteAlternativeId = request.ConcreteAlternativeId,
            TrajectoryKind = TrajectoryKind(request),
            SourceStateHash = source.StateHash,
            SourceSnapshotPath = sourceSnapshotPath,
            SourceSnapshotSha256 = request.SourceSnapshotSha256,
            SourceCheckpoint = source,
            FinalCheckpoint = final,
            RevenuePrefix = prefix,
            AcquisitionRolloutRequired =
                request.AcquisitionRolloutProofRequired,
            TerminalExecutionEvidenceSha256 = terminal.EvidenceSha256,
            Artifacts = artifacts,
            NativeOutcomeVerified = true,
            TeacherPreferenceLabelVerified = false,
            FormalProductTrainingAuthorized = false
        };
        receipt.ReceiptSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                receipt);
        return receipt;
    }

    private static IEnumerable<MoneyRevenueArtifactBinding> BuildArtifacts(
        string sourceSnapshotPath,
        string requestManifestPath,
        string requestCatalogPath,
        string terminalManifestPath,
        string terminalReceiptPath,
        MoneyRevenueCounterfactualProofRequest request,
        MoneyRevenueTerminalAlternativeProof proof,
        MoneyRevenueComparisonAdmissionAlternative alternative)
    {
        yield return Artifact("source_snapshot", sourceSnapshotPath);
        yield return Artifact(
            "counterfactual_request_manifest",
            requestManifestPath);
        yield return Artifact(
            "counterfactual_request_catalog",
            requestCatalogPath);
        yield return Artifact(
            "terminal_execution_proof_manifest",
            terminalManifestPath);
        yield return Artifact(
            "terminal_execution_proof_receipt",
            terminalReceiptPath);
        yield return Artifact(
            "revenue_recurrence_manifest",
            proof.RevenueRecurrenceManifestPath);
        yield return Artifact(
            "revenue_recurrence_receipt",
            proof.RevenueRecurrenceReceiptPath);
        if (request.AcquisitionRolloutProofRequired)
        {
            yield return Artifact(
                "acquisition_rollout_manifest",
                request.AcquisitionRolloutManifestPath);
            yield return Artifact(
                "acquisition_rollout_receipt",
                request.AcquisitionRolloutReceiptPath);
        }
        foreach (var artifact in alternative.NativeTerminalArtifacts)
        {
            yield return new MoneyRevenueArtifactBinding(
                "terminal_" + artifact.Role,
                artifact.Path,
                artifact.Sha256);
        }
    }

    internal static string TrajectoryKind(
        MoneyRevenueCounterfactualProofRequest request)
    {
        Require(request.NativeShopSaleProofRequired !=
                    request.NativeShippingDepositProofRequired &&
                request.NativeShippingDepositProofRequired ==
                    request.NativeShippingSettlementProofRequired,
            "Money threshold native trajectory terminal kind is invalid.");
        return request.AcquisitionRolloutProofRequired
            ? request.NativeShopSaleProofRequired
                ? MoneyThresholdNativeTrajectoryKinds
                    .AcquisitionThenShopSale
                : MoneyThresholdNativeTrajectoryKinds
                    .AcquisitionThenShipping
            : request.NativeShopSaleProofRequired
                ? MoneyThresholdNativeTrajectoryKinds.DirectShopSale
                : MoneyThresholdNativeTrajectoryKinds.DirectShipping;
    }

    private static MoneyRevenueArtifactBinding Artifact(
        string role,
        string path)
    {
        var fullPath = RequiredFile(path, role);
        return new MoneyRevenueArtifactBinding(role, fullPath, Hash(fullPath));
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
