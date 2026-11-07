using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyShopReplayComparisonBuilder
{
    public static MoneyShopReplayComparisonReceipt Build(string manifestPath)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        var manifest = CurrentTeacherFrontierSupport.Read<MoneyShopReplayComparisonManifest>(manifestPath, "Money replay comparison manifest");
        ValidateManifest(manifest);
        var bindingPaths = manifest.SourceBindingReceiptPaths.Select(Path.GetFullPath).ToArray();
        var first = MoneyShopReplaySourceBindingBuilder.VerifyWithContext(bindingPaths[0]);
        var reference = first.Reference;
        var catalog = reference.Catalog;
        Require(bindingPaths.Length == catalog.RequestCount - 1, "Money replay comparison does not cover its complete original request denominator.");
        var alternatives = new List<MoneyShopReplayComparisonAlternative>
        {
            Project(reference.Catalog.Requests.Single(row => row.RequestId == reference.Witness.RequestId), reference,
                "exact_reference_native_witness", string.Empty)
        };
        var bindings = new List<MoneyRevenueArtifactBinding>();
        foreach (var (path, index) in bindingPaths.Select((path, index) => (path, index)))
        {
            var verified = index == 0 ? first : MoneyShopReplaySourceBindingBuilder.VerifyWithContext(path, reference);
            Require(EqualJson(verified.Reference.Catalog, catalog) && verified.Reference.Evidence.EvidenceSha256 == reference.Evidence.EvidenceSha256,
                "Money replay comparison changed its exact reference request lineage or native witness.");
            var receipt = verified.Receipt;
            Require(receipt.SourceReplayBindingVerified && !receipt.OriginalReferenceFragmentCompleted &&
                !receipt.TeacherPreferenceLabelEligible && !receipt.FormalProductTrainingAuthorized,
                "Money replay comparison input does not have bounded source-binding authority.");
            var request = catalog.Requests.Single(row => row.RequestId == receipt.ReferenceRequestId);
            alternatives.Add(Project(request, verified.Replay, "verified_shop_replay_source", path));
            bindings.Add(new("source_binding:" + index, path, CurrentTeacherFrontierSupport.HashFile(path)));
        }
        var rows = alternatives.OrderBy(row => row.ReferenceRequestId, StringComparer.Ordinal).ToArray();
        var comparisons = CompareVerifiedDenominator(catalog, rows);
        var result = new MoneyShopReplayComparisonReceipt
        {
            ManifestPath = manifestPath, ManifestSha256 = CurrentTeacherFrontierSupport.HashFile(manifestPath),
            ReferenceRequestManifestPath = Path.GetFullPath(reference.Source.RequestManifestPath),
            ReferenceRequestCatalogSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(catalog),
            ReferenceSourceStateHash = catalog.SourceStateHash, ReferenceSnapshotSha256 = catalog.SourceSnapshotSha256,
            ReferenceStrategyLedgerSha256 = catalog.SourceStrategyLedgerSha256, ReferenceLedgerRevision = catalog.SourceStrategyLedgerRevision,
            RequestIds = rows.Select(row => row.ReferenceRequestId).ToArray(),
            ConcreteAlternativeIds = rows.Select(row => row.ReferenceConcreteAlternativeId).ToArray(),
            SourceBindings = bindings.ToArray(), Alternatives = rows, Comparisons = comparisons,
            CandidateDenominatorAuthoritative = true, AllActualNativeOutcomesVerified = true,
            TeacherEligibleCriterionIds = comparisons.Where(row => row.TeacherPreferenceLabelEligible).Select(row => row.CriterionId).ToArray()
        };
        result.ReceiptSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(result);
        return result;
    }

    public static MoneyShopReplayComparisonReceipt Verify(string receiptPath)
    {
        var actual = CurrentTeacherFrontierSupport.Read<MoneyShopReplayComparisonReceipt>(Path.GetFullPath(receiptPath), "Money replay comparison receipt");
        Require(actual.SchemaVersion == "money_shop_replay_comparison_receipt.v1", "Money replay comparison receipt schema differs.");
        var expected = Build(actual.ManifestPath);
        Require(EqualJson(actual, expected), "Money replay comparison differs from its complete source rebuild.");
        return expected;
    }

    internal static void ValidateManifest(MoneyShopReplayComparisonManifest manifest)
    {
        var paths = manifest.SourceBindingReceiptPaths;
        Require(manifest.SchemaVersion == "money_shop_replay_comparison_manifest.v1" && !manifest.FormalProductTrainingAuthorized &&
            paths is { Length: > 0 } && paths.All(path => !string.IsNullOrWhiteSpace(path)) &&
            paths.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() == paths.Length,
            "Money replay comparison manifest is invalid or repeats a source binding.");
    }

    private static MoneyShopReplayComparisonAlternative Project(MoneyRevenueCounterfactualProofRequest reference,
        MoneyShopReplaySourceBindingBuilder.BoundSource source, string kind, string bindingPath)
    {
        var actual = source.Catalog.Requests.Single(row => row.RequestId == source.Witness.RequestId);
        Require(EqualJson(reference.TerminalCandidate, actual.TerminalCandidate), "Money replay comparison remapped a different native candidate.");
        var verified = MoneyRevenueComparisonAdmissionBuilder.VerifyAlternativeWithContext(actual, source.Witness, source.Evidence);
        var episode = source.Witness.ShopSaleEpisodePath;
        return new(reference.RequestId, reference.ConcreteAlternativeId, actual.RequestId, actual.RequestSha256, actual.SourceStateHash,
            source.Native.Request.RunId, source.Native.Request.RequestNonce, kind, bindingPath,
            bindingPath.Length == 0 ? string.Empty : CurrentTeacherFrontierSupport.HashFile(bindingPath),
            Path.GetFullPath(episode), CurrentTeacherFrontierSupport.HashFile(episode), verified.Alternative);
    }

    internal static MoneyRevenueAlternativeComparison[] CompareVerifiedDenominator(MoneyRevenueCounterfactualProofRequestCatalog catalog,
        MoneyShopReplayComparisonAlternative[] rows)
    {
        Require(catalog.RequestDenominatorAuthoritative && catalog.TerminalStrategyLedgerAnchorsVerified && catalog.CounterfactualProofCollectionReady &&
            !catalog.ProductionTeacherPreferenceLabelEligible && !catalog.FormalProductTrainingAuthorized && catalog.RequestCount >= 2 &&
            rows.Length == catalog.RequestCount && rows.Select(row => row.ReferenceRequestId).Distinct(StringComparer.Ordinal).Count() == rows.Length &&
            rows.Select(row => row.ReferenceRequestId).Order(StringComparer.Ordinal).SequenceEqual(catalog.RequestIds.Order(StringComparer.Ordinal), StringComparer.Ordinal) &&
            rows.Select(row => row.ActualRequestId).Distinct(StringComparer.Ordinal).Count() == rows.Length &&
            rows.Select(row => row.ActualRunId).Distinct(StringComparer.Ordinal).Count() == rows.Length &&
            rows.Select(row => row.ActualRequestNonce).Distinct(StringComparer.Ordinal).Count() == rows.Length &&
            rows.All(row => !string.IsNullOrWhiteSpace(row.ActualRunId) && !string.IsNullOrWhiteSpace(row.ActualRequestNonce)),
            "Money replay comparison actual/reference native denominator is incomplete or reused.");
        foreach (var row in rows)
        {
            var reference = catalog.Requests.Single(request => request.RequestId == row.ReferenceRequestId);
            Require(row.ReferenceConcreteAlternativeId == reference.ConcreteAlternativeId &&
                row.ActualComparisonEvidence.RequestId == row.ActualRequestId &&
                row.ActualComparisonEvidence.RevenuePrefix.InitialStateHash == row.ActualSourceStateHash,
                "Money replay comparison lost its explicit reference/actual identity mapping.");
        }
        var ids = rows.Select(row => row.ReferenceConcreteAlternativeId).ToArray();
        var mapped = rows.Select(row => new MoneyRevenueVerifiedArtifactAlternative(row.ReferenceConcreteAlternativeId,
            row.ActualComparisonEvidence.RevenuePrefix, row.ActualComparisonEvidence.AggregateOpportunityCost,
            row.ActualComparisonEvidence.ReservationNotRequired, row.ActualComparisonEvidence.ReservationIds)).ToArray();
        return GrandpaEvaluationGoalEvaluator.MoneyThresholds.Select(threshold => MoneyRevenueAlternativeComparer.CompareVerifiedArtifacts(
            threshold.CriterionId, catalog.SourceStateHash, catalog.SourceStrategyLedgerRevision, ids, mapped)).ToArray();
    }
}
