namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeOutcomeBatchBuilder
{
    public static MoneyThresholdNativeOutcomeBatch Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money threshold native-outcome batch request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdNativeOutcomeBatchRequest>(
                fullRequestPath,
                "Money threshold native-outcome batch request");
        var comparisonAdmissions = request.ComparisonAdmissions ??
            Array.Empty<MoneyThresholdNativeOutcomeComparisonAdmission>();
        var runSummaryPaths = request.RunSummaryPaths ?? Array.Empty<string>();
        Require((request.SchemaVersion is
                    MoneyThresholdNativeOutcomeBatchVersions.LegacyRequest or
                    MoneyThresholdNativeOutcomeBatchVersions.Request) &&
                !string.IsNullOrWhiteSpace(request.BatchId) &&
                runSummaryPaths.Length > 0 &&
                runSummaryPaths.All(path =>
                    !string.IsNullOrWhiteSpace(path)) &&
                runSummaryPaths.Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    runSummaryPaths.Length &&
                (request.SchemaVersion !=
                    MoneyThresholdNativeOutcomeBatchVersions.LegacyRequest ||
                    comparisonAdmissions.Length == 0) &&
                !request.FormalProductTrainingAuthorized,
            "Money threshold native-outcome batch request is invalid.");

        var runs = runSummaryPaths
            .Select(VerifyRun)
            .OrderBy(value => value.VerifiedRun.RunId, StringComparer.Ordinal)
            .ToArray();
        Require(runs.Select(value => value.VerifiedRun.RunId)
                .Distinct(StringComparer.Ordinal).Count() == runs.Length,
            "Money threshold native-outcome run IDs are duplicated.");
        ValidateComparisonAdmissions(comparisonAdmissions, runs);
        var corpus = MoneyThresholdTeacherCorpusBuilder.Build(
            new MoneyThresholdTeacherCorpusRequest
            {
                CorpusId = request.BatchId,
                Sources = runs.Select(value =>
                    {
                        var comparisons = comparisonAdmissions
                            .Where(admission => string.Equals(
                                admission.RunId,
                                value.VerifiedRun.RunId,
                                StringComparison.Ordinal))
                            .OrderBy(admission => admission.CriterionId,
                                StringComparer.Ordinal)
                            .Select(admission =>
                                new MoneyThresholdTeacherComparisonSource(
                                    admission.CriterionId,
                                    admission.AdmissionManifestPath,
                                    admission.AdmissionReceiptPath))
                            .ToArray();
                        return new MoneyThresholdTeacherCorpusSource(
                            value.VerifiedRun.RunId,
                            value.VerifiedRun.ExecutionEpisodePath,
                            comparisons);
                    })
                    .ToArray(),
                FormalProductTrainingAuthorized = false
            });
        var cells = MoneyThresholdEvidenceCellProjector.Build(corpus)
            .Select(cell => new MoneyThresholdNativeOutcomeCellStatus(
                cell.CriterionId,
                cell.DatasetPartition,
                cell.NativeOutcomeVerified,
                cell.TeacherComparisonVerified,
                cell.NativeOutcomeRowIds,
                cell.TeacherComparisonRowIds))
            .ToArray();
        var nativeComplete = cells.Count(cell => cell.NativeOutcomeVerified);
        var comparisonComplete = cells.Count(cell =>
            cell.TeacherComparisonVerified);
        var fullyComplete = cells.Count(cell =>
            cell.NativeOutcomeVerified && cell.TeacherComparisonVerified);
        return new MoneyThresholdNativeOutcomeBatch
        {
            Status = fullyComplete == cells.Length
                ? "money_threshold_evidence_complete"
                : nativeComplete == cells.Length
                    ? "teacher_comparison_work_remaining"
                    : "native_outcome_work_remaining",
            BatchId = request.BatchId,
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            VerifiedRunCount = runs.Length,
            NativeCompleteCellCount = nativeComplete,
            VerifiedComparisonAdmissionCount = comparisonAdmissions.Length,
            TeacherComparisonCompleteCellCount = comparisonComplete,
            FullyCompleteCellCount = fullyComplete,
            RequiredCellCount = cells.Length,
            Cells = cells,
            VerifiedRuns = runs.Select(value => value.VerifiedRun).ToArray(),
            Corpus = corpus,
            FormalProductTrainingAuthorized = false
        };
    }

    private static void ValidateComparisonAdmissions(
        MoneyThresholdNativeOutcomeComparisonAdmission[] admissions,
        VerifiedRunSource[] runs)
    {
        Require(admissions.All(admission =>
                admission is not null &&
                !string.IsNullOrWhiteSpace(admission.RunId) &&
                !string.IsNullOrWhiteSpace(admission.CriterionId) &&
                !string.IsNullOrWhiteSpace(admission.AdmissionManifestPath) &&
                !string.IsNullOrWhiteSpace(admission.AdmissionReceiptPath)) &&
            admissions.GroupBy(
                    admission => admission.RunId + "\n" +
                        admission.CriterionId,
                    StringComparer.Ordinal)
                .All(group => group.Count() == 1),
            "Money threshold comparison admissions are invalid or duplicated.");
        var runById = runs.ToDictionary(
            value => value.VerifiedRun.RunId,
            StringComparer.Ordinal);
        foreach (var admission in admissions)
        {
            Require(runById.TryGetValue(admission.RunId, out var run) &&
                    run.VerifiedRun.CrossedCriterionIds.Contains(
                        admission.CriterionId,
                        StringComparer.Ordinal),
                "Money threshold comparison admission does not bind a " +
                "criterion crossed by its native run: " + admission.RunId +
                "/" + admission.CriterionId + ".");
        }
    }

    private static VerifiedRunSource VerifyRun(string summaryPath)
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

        return new VerifiedRunSource(
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

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record VerifiedRunSource(
        MoneyThresholdNativeOutcomeVerifiedRun VerifiedRun);
}
