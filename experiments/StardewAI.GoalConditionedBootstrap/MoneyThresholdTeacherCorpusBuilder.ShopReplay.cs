using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static MoneyThresholdTeacherComparisonEvidence? BuildShopReplayComparisonEvidence(
        GrandpaMoneyThreshold threshold, MoneyThresholdVerifiedNativeOutcome outcome, VerifiedShopReplayInput input)
    {
        var receipt = input.Receipt;
        Require(receipt.SchemaVersion == "money_shop_replay_comparison_receipt.v1" &&
            receipt.Scope == "criterion_local_declared_held_stock_direct_shop_sale" &&
            receipt.CandidateDenominatorAuthoritative && receipt.AllActualNativeOutcomesVerified &&
            !receipt.OriginalExactFrameIndexCompleted && !receipt.CriterionCorpusAdmissionVerified && !receipt.FormalProductTrainingAuthorized &&
            outcome.Kind == MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode,
            "Money-threshold replay comparison has an invalid authority or native scope.");
        var comparison = receipt.Comparisons.SingleOrDefault(row => row.CriterionId == threshold.CriterionId) ??
            throw new InvalidDataException("Money-threshold replay criterion comparison is missing.");
        Require(receipt.TeacherEligibleCriterionIds.Contains(threshold.CriterionId, StringComparer.Ordinal) == comparison.TeacherPreferenceLabelEligible,
            "Money-threshold replay eligibility differs from its criterion comparison.");
        if (!comparison.TeacherPreferenceLabelEligible) return null;
        ValidatePreferenceHeader(threshold, comparison, receipt.ReferenceSourceStateHash, receipt.ReferenceLedgerRevision, receipt.RequestIds.Length);
        var ids = receipt.ConcreteAlternativeIds;
        Require(ids.Length == receipt.RequestIds.Length && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
            receipt.Alternatives.Length == ids.Length && receipt.Alternatives.Select(row => row.ReferenceConcreteAlternativeId)
                .Order(StringComparer.Ordinal).SequenceEqual(ids.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "Money-threshold replay comparison denominator is incomplete.");
        var selected = receipt.Alternatives.SingleOrDefault(row => row.ReferenceConcreteAlternativeId == comparison.SelectedAlternativeId) ??
            throw new InvalidDataException("Money-threshold mapped selected alternative is missing.");
        if (selected.ActualSourceStateHash != outcome.SourceCheckpoint.StateHash ||
            !SameReplayPath(selected.ActualEpisodePath, outcome.ExecutionEpisodePath) || selected.ActualEpisodeSha256 != outcome.ExecutionEpisodeSha256)
            return null;
        var evidence = selected.ActualComparisonEvidence;
        var exact = selected.SourceKind == "exact_reference_native_witness";
        Require(evidence.RequestId == selected.ActualRequestId &&
            (exact
                ? selected.ActualSourceStateHash == receipt.ReferenceSourceStateHash && selected.ActualRequestId == selected.ReferenceRequestId &&
                    evidence.ConcreteAlternativeId == selected.ReferenceConcreteAlternativeId && selected.SourceBindingReceiptPath.Length == 0 && selected.SourceBindingReceiptSha256.Length == 0
                : selected.SourceKind == "verified_shop_replay_source" && selected.ActualSourceStateHash != receipt.ReferenceSourceStateHash &&
                    selected.SourceBindingReceiptSha256.Length == 64 && receipt.SourceBindings.Any(binding =>
                        SameReplayPath(binding.Path, selected.SourceBindingReceiptPath) && binding.Sha256 == selected.SourceBindingReceiptSha256)),
            "Money-threshold selected replay source mapping is invalid.");
        ValidateSelectedNativeOutcome(evidence, outcome);
        var join = CompletePreference(comparison, ids);
        return new MoneyThresholdTeacherComparisonEvidence(threshold.CriterionId,
            receipt.ManifestPath, receipt.ManifestSha256, input.Path, input.Sha256,
            receipt.ReferenceSourceStateHash, ids.Length, join.PreferredAlternative, join.RejectedAlternatives,
            outcome.ExecutionEpisodeSha256, comparison.SelectionPolicy, outcome.PrimarySha256)
        {
            ShopReplayJoin = new(selected.ReferenceRequestId, selected.ActualRequestId, selected.ActualRequestSha256,
                evidence.ConcreteAlternativeId, selected.ActualSourceStateHash, selected.SourceKind,
                selected.SourceBindingReceiptPath, selected.SourceBindingReceiptSha256)
        };
    }

    internal static MoneyThresholdTeacherEvidenceRow BuildShopReplayRowForTest(string sourceId, string episodePath,
        string receiptPath, MoneyShopReplayComparisonReceipt receipt) => BuildRow(sourceId, VerifyNativeSale(episodePath),
            Array.Empty<MoneyThresholdTeacherComparisonSource>(), new(receiptPath, CurrentTeacherFrontierSupport.HashFile(receiptPath), receipt));

    private static bool SameReplayPath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record VerifiedShopReplayInput(string Path, string Sha256, MoneyShopReplayComparisonReceipt Receipt);
}
