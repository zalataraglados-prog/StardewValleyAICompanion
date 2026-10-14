namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyThresholdNativeOutcomeRunVerifier
{
    public static VerifiedMoneyThresholdNativeOutcomeRun Verify(
        string summaryPath)
    {
        var fullSummaryPath = RequiredFile(
            summaryPath,
            "Money threshold native-outcome run summary");
        var summary = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdNativeOutcomeRunSummary>(
                fullSummaryPath,
                "Money threshold native-outcome run summary");
        Require(summary.SchemaVersion ==
                    "money_threshold_native_outcome_run_summary.v1" &&
                summary.Status == "passed" &&
                !string.IsNullOrWhiteSpace(summary.RunId) &&
                !string.IsNullOrWhiteSpace(summary.CriterionId) &&
                MoneyThresholdEvidenceCellProjector.IsRequiredPartition(
                    summary.DatasetPartition) &&
                IsSha256(summary.SourceSaveSha256Before) &&
                summary.SourceSaveSha256Before ==
                    summary.SourceSaveSha256After &&
                IsSha256(summary.IsolatedSaveSha256Before) &&
                summary.IsolatedSaveSha256Before ==
                    summary.SourceSaveSha256Before &&
                !string.IsNullOrWhiteSpace(summary.SourceStateHash) &&
                IsSha256(summary.SelectionSha256) &&
                IsSha256(summary.CandidateDenominatorSha256) &&
                summary.CrossedCriterionIds.Length > 0 &&
                summary.CrossedCriterionIds.Distinct(StringComparer.Ordinal)
                    .Count() == summary.CrossedCriterionIds.Length &&
                summary.CrossedCriterionIds.Contains(
                    summary.CriterionId,
                    StringComparer.Ordinal) &&
                !summary.FormalProductTrainingAuthorized,
            "Money threshold native-outcome run summary is invalid.");

        var candidatePath = RequiredFile(
            summary.CandidateDenominatorPath,
            "Money Revenue candidate denominator");
        Require(CurrentTeacherFrontierSupport.HashFile(candidatePath) ==
                summary.CandidateDenominatorSha256,
            "Money threshold run candidate denominator hash drifted.");
        var selectionPath = RequiredFile(
            summary.SelectionPath,
            "Money threshold native-outcome selection");
        var persistedSelection = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdNativeOutcomeSelection>(
                selectionPath,
                "Money threshold native-outcome selection");
        var rebuiltSelection = MoneyThresholdNativeOutcomeSelectionBuilder.Build(
            candidatePath,
            summary.CriterionId,
            summary.DatasetPartition);
        Require(EqualJson(persistedSelection, rebuiltSelection) &&
                rebuiltSelection.SelectionSha256 == summary.SelectionSha256 &&
                rebuiltSelection.SourceStateHash == summary.SourceStateHash &&
                SamePath(
                    rebuiltSelection.CandidateDenominatorPath,
                    candidatePath) &&
                summary.CrossedCriterionIds.SequenceEqual(
                    rebuiltSelection.ExpectedCrossedCriterionIds,
                    StringComparer.Ordinal),
            "Money threshold native-outcome selection drifted from its run.");

        var corpusPath = RequiredFile(
            summary.MoneyThresholdCorpusPath,
            "Money threshold native-outcome corpus");
        var corpus = MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
        Require(corpus.Rows.Length == 1,
            "Money threshold native-outcome run corpus must contain one row.");
        var row = corpus.Rows[0];
        var rowCrossed = row.CrossedThresholds
            .Select(value => value.CriterionId)
            .ToArray();
        var episodePath = RequiredFile(
            summary.ExecutionEpisodePath,
            "Money threshold native shop-sale episode");
        Require(SamePath(row.ExecutionEpisodePath, episodePath) &&
                row.NativeOutcomeKind ==
                    MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode &&
                SamePath(row.NativeOutcomePrimaryPath, episodePath) &&
                row.SourceId == summary.RunId &&
                row.BeforeStateHash == summary.SourceStateHash &&
                row.DatasetPartition == summary.DatasetPartition &&
                row.TotalMoneyEarnedBefore ==
                    rebuiltSelection.TotalMoneyEarnedBefore &&
                row.TotalMoneyEarnedAfter ==
                    rebuiltSelection.ExpectedTotalMoneyEarnedAfter &&
                rowCrossed.SequenceEqual(
                    summary.CrossedCriterionIds,
                    StringComparer.Ordinal) &&
                row.NativeTerminalOutcomeVerified &&
                row.TeacherComparisonCount == 0,
            "Money threshold native-outcome corpus drifted from its run summary.");

        return new VerifiedMoneyThresholdNativeOutcomeRun(
            summary,
            rebuiltSelection,
            row,
            new MoneyThresholdNativeOutcomeVerifiedRun(
                summary.RunId,
                fullSummaryPath,
                CurrentTeacherFrontierSupport.HashFile(fullSummaryPath),
                summary.SelectionSha256,
                summary.CandidateDenominatorSha256,
                episodePath,
                summary.DatasetPartition,
                summary.CrossedCriterionIds));
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}

internal sealed record VerifiedMoneyThresholdNativeOutcomeRun(
    MoneyThresholdNativeOutcomeRunSummary Summary,
    MoneyThresholdNativeOutcomeSelection Selection,
    MoneyThresholdTeacherEvidenceRow CorpusRow,
    MoneyThresholdNativeOutcomeVerifiedRun VerifiedRun);
