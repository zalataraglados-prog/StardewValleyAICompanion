using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeOutcomeBatchBuilder
{
    private static readonly string[] RequiredPartitions =
    {
        PolicyDatasetPartitions.Train,
        PolicyDatasetPartitions.Validation,
        PolicyDatasetPartitions.Test
    };

    public static MoneyThresholdNativeOutcomeBatch Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money threshold native-outcome batch request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdNativeOutcomeBatchRequest>(
                fullRequestPath,
                "Money threshold native-outcome batch request");
        Require(request.SchemaVersion ==
                    "money_threshold_native_outcome_batch_request.v1" &&
                !string.IsNullOrWhiteSpace(request.BatchId) &&
                request.RunSummaryPaths.Length > 0 &&
                request.RunSummaryPaths.All(path =>
                    !string.IsNullOrWhiteSpace(path)) &&
                request.RunSummaryPaths.Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    request.RunSummaryPaths.Length &&
                !request.FormalProductTrainingAuthorized,
            "Money threshold native-outcome batch request is invalid.");

        var runs = request.RunSummaryPaths
            .Select(VerifyRun)
            .OrderBy(value => value.VerifiedRun.RunId, StringComparer.Ordinal)
            .ToArray();
        Require(runs.Select(value => value.VerifiedRun.RunId)
                .Distinct(StringComparer.Ordinal).Count() == runs.Length,
            "Money threshold native-outcome run IDs are duplicated.");
        var corpus = MoneyThresholdTeacherCorpusBuilder.Build(
            new MoneyThresholdTeacherCorpusRequest
            {
                CorpusId = request.BatchId,
                Sources = runs.Select(value =>
                        new MoneyThresholdTeacherCorpusSource(
                            value.VerifiedRun.RunId,
                            value.VerifiedRun.ExecutionEpisodePath))
                    .ToArray(),
                FormalProductTrainingAuthorized = false
            });
        var cells = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SelectMany(threshold => RequiredPartitions.Select(partition =>
            {
                var rows = corpus.Rows.Where(row =>
                        row.DatasetPartition == partition &&
                        row.CrossedThresholds.Any(value =>
                            value.CriterionId == threshold.CriterionId))
                    .Select(row => row.RowId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                return new MoneyThresholdNativeOutcomeCellStatus(
                    threshold.CriterionId,
                    partition,
                    rows.Length > 0,
                    rows);
            }))
            .ToArray();
        var complete = cells.Count(cell => cell.NativeOutcomeVerified);
        return new MoneyThresholdNativeOutcomeBatch
        {
            Status = complete == cells.Length
                ? "native_outcome_channel_complete"
                : "native_outcome_work_remaining",
            BatchId = request.BatchId,
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            VerifiedRunCount = runs.Length,
            NativeCompleteCellCount = complete,
            RequiredCellCount = cells.Length,
            Cells = cells,
            VerifiedRuns = runs.Select(value => value.VerifiedRun).ToArray(),
            Corpus = corpus,
            FormalProductTrainingAuthorized = false
        };
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
                RequiredPartitions.Contains(
                    summary.DatasetPartition,
                    StringComparer.Ordinal) &&
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

    private static bool EqualJson(object left, object right) =>
        JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

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
