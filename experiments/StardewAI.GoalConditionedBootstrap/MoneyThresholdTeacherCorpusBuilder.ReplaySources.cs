namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static MoneyThresholdTeacherCorpusSource SourceForRow(MoneyThresholdTeacherEvidenceRow row)
    {
        var comparisons = row.CrossedThresholds.Where(value => value.TeacherComparison is not null).Select(value => value.TeacherComparison!).ToArray();
        var replay = comparisons.Where(value => value.ShopReplayJoin is not null).ToArray();
        var legacy = comparisons.Where(value => value.ShopReplayJoin is null).ToArray();
        var paths = replay.Select(value => Path.GetFullPath(value.AdmissionReceiptPath))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
        Require(paths.Length <= 1 && (replay.Length == 0 || legacy.Length == 0),
            "Money-threshold source mixes comparison kinds or replay groups.");
        return new(row.SourceId,
            row.NativeOutcomeKind == MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode ? row.ExecutionEpisodePath : string.Empty,
            legacy.Select(value => new MoneyThresholdTeacherComparisonSource(value.CriterionId, value.AdmissionManifestPath, value.AdmissionReceiptPath)).ToArray(),
            row.NativeOutcomeKind == MoneyThresholdNativeOutcomeKinds.RevenueTrajectory ? row.NativeTrajectoryReceiptPath : string.Empty)
        {
            ShopReplayComparisonReceiptPath = paths.SingleOrDefault()
        };
    }

    private static void ValidateReplayRequestVersion(MoneyThresholdTeacherCorpusRequest request)
    {
        var hasReplay = request.Sources.Any(source => source.ShopReplayComparisonReceiptPath is not null);
        Require(request.SchemaVersion == MoneyThresholdTeacherCorpusVersions.ShopReplayRequest ? hasReplay : !hasReplay,
            "Money-threshold request version does not match its replay-comparison sources.");
        foreach (var source in request.Sources.Where(source => source.ShopReplayComparisonReceiptPath is not null))
            Require(!string.IsNullOrWhiteSpace(source.ShopReplayComparisonReceiptPath) &&
                (source.ComparisonAdmissions?.Length ?? 0) == 0 && string.IsNullOrWhiteSpace(source.NativeTrajectoryReceiptPath) &&
                !string.IsNullOrWhiteSpace(source.ExecutionEpisodePath),
                "Money-threshold replay source must select one native sale and one comparison group, without legacy admissions.");
    }

    private static bool ValidShopReplayMetadata(MoneyThresholdTeacherComparisonEvidence? comparison, MoneyThresholdTeacherEvidenceRow row)
    {
        if (comparison?.ShopReplayJoin is not { } join) return true;
        return join.SchemaVersion == "money_threshold_shop_replay_comparison_join.v1" &&
            !string.IsNullOrWhiteSpace(join.ReferenceRequestId) && !string.IsNullOrWhiteSpace(join.ActualRequestId) &&
            join.ActualRequestSha256.Length == 64 && !string.IsNullOrWhiteSpace(join.ActualConcreteAlternativeId) &&
            join.ActualSourceStateHash == row.BeforeStateHash && row.NativeOutcomeKind == MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode &&
            (join.SourceKind == "exact_reference_native_witness"
                ? comparison.ComparisonSourceStateHash == join.ActualSourceStateHash && join.ReferenceRequestId == join.ActualRequestId &&
                    comparison.PreferredAlternative == join.ActualConcreteAlternativeId && join.SourceBindingReceiptPath.Length == 0 && join.SourceBindingReceiptSha256.Length == 0
                : join.SourceKind == "verified_shop_replay_source" && comparison.ComparisonSourceStateHash != join.ActualSourceStateHash &&
                    !string.IsNullOrWhiteSpace(join.SourceBindingReceiptPath) && join.SourceBindingReceiptSha256.Length == 64);
    }
}
