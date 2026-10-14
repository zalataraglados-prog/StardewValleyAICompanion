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
                    MoneyThresholdNativeOutcomeBatchVersions.ComparisonRequest or
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
                (request.SchemaVersion ==
                    MoneyThresholdNativeOutcomeBatchVersions.Request ||
                    comparisonAdmissions.All(admission =>
                        admission is not null &&
                        string.IsNullOrWhiteSpace(
                            admission.ExecutionEpisodePath))) &&
                !request.FormalProductTrainingAuthorized,
            "Money threshold native-outcome batch request is invalid.");

        var runs = runSummaryPaths
            .Select(MoneyThresholdNativeOutcomeRunVerifier.Verify)
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
                        var runAdmissions = comparisonAdmissions
                            .Where(admission => string.Equals(
                                admission.RunId,
                                value.VerifiedRun.RunId,
                                StringComparison.Ordinal))
                            .OrderBy(admission => admission.CriterionId,
                                StringComparer.Ordinal)
                            .ToArray();
                        var episodeOverrides = runAdmissions
                            .Where(admission => !string.IsNullOrWhiteSpace(
                                admission.ExecutionEpisodePath))
                            .Select(admission => RequiredFile(
                                admission.ExecutionEpisodePath,
                                "Money threshold comparison-selected native episode"))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        Require(episodeOverrides.Length <= 1,
                            "One Money threshold seed run cannot join multiple " +
                            "Teacher-selected native episodes.");
                        var executionEpisodePath = episodeOverrides
                            .SingleOrDefault() ??
                            value.VerifiedRun.ExecutionEpisodePath;
                        var comparisons = runAdmissions
                            .Select(admission =>
                                new MoneyThresholdTeacherComparisonSource(
                                    admission.CriterionId,
                                    admission.AdmissionManifestPath,
                                    admission.AdmissionReceiptPath))
                            .ToArray();
                        return new MoneyThresholdTeacherCorpusSource(
                            value.VerifiedRun.RunId,
                            executionEpisodePath,
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
        VerifiedMoneyThresholdNativeOutcomeRun[] runs)
    {
        Require(admissions.All(admission =>
                admission is not null &&
                !string.IsNullOrWhiteSpace(admission.RunId) &&
                !string.IsNullOrWhiteSpace(admission.CriterionId) &&
                !string.IsNullOrWhiteSpace(admission.AdmissionManifestPath) &&
                !string.IsNullOrWhiteSpace(admission.AdmissionReceiptPath) &&
                (string.IsNullOrWhiteSpace(admission.ExecutionEpisodePath) ||
                    File.Exists(Path.GetFullPath(
                        admission.ExecutionEpisodePath)))) &&
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

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

}
