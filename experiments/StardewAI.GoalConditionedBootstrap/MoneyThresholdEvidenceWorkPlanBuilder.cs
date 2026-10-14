using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdEvidenceWorkPlanBuilder
{
    private static readonly string[] RequiredPartitions =
    {
        PolicyDatasetPartitions.Train,
        PolicyDatasetPartitions.Validation,
        PolicyDatasetPartitions.Test
    };

    public static MoneyThresholdEvidenceWorkPlan Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money-threshold evidence work-plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdEvidenceWorkPlanRequest>(
            fullRequestPath,
            "Money-threshold evidence work-plan request");
        if (request.SchemaVersion !=
                "money_threshold_evidence_work_plan_request.v1" ||
            string.IsNullOrWhiteSpace(request.PlanId) ||
            request.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Money-threshold evidence work-plan request is invalid.");
        }

        MoneyThresholdTeacherCorpus? corpus = null;
        var corpusPath = string.Empty;
        var corpusSha256 = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.MoneyThresholdCorpusPath))
        {
            corpusPath = RequiredFile(
                request.MoneyThresholdCorpusPath,
                "Money-threshold Teacher corpus");
            corpus = MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
            corpusSha256 = CurrentTeacherFrontierSupport.HashFile(corpusPath);
        }

        var cells = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SelectMany((threshold, thresholdOrder) =>
                RequiredPartitions.Select(partition => BuildCell(
                    corpus,
                    threshold,
                    thresholdOrder,
                    partition)))
            .ToArray();
        var fullyComplete = cells.Count(value =>
            value.NativeOutcomeVerified &&
            value.TeacherComparisonVerified);
        return new MoneyThresholdEvidenceWorkPlan
        {
            Status = fullyComplete == cells.Length
                ? "money_threshold_evidence_complete"
                : "money_threshold_evidence_work_remaining",
            PlanId = request.PlanId,
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            MoneyThresholdCorpusPath = corpusPath,
            MoneyThresholdCorpusSha256 = corpusSha256,
            CriterionCount =
                GrandpaEvaluationGoalEvaluator.MoneyThresholds.Count,
            RequiredPartitionCount = RequiredPartitions.Length,
            RequiredCellCount = cells.Length,
            NativeOutcomeCompleteCellCount = cells.Count(value =>
                value.NativeOutcomeVerified),
            TeacherComparisonCompleteCellCount = cells.Count(value =>
                value.TeacherComparisonVerified),
            FullyCompleteCellCount = fullyComplete,
            CoverageComplete = fullyComplete == cells.Length,
            Cells = cells,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyThresholdEvidenceWorkCell BuildCell(
        MoneyThresholdTeacherCorpus? corpus,
        GrandpaMoneyThreshold threshold,
        int thresholdOrder,
        string partition)
    {
        var rows = (corpus?.Rows ??
                Array.Empty<MoneyThresholdTeacherEvidenceRow>())
            .Where(row => row.DatasetPartition == partition &&
                row.CrossedThresholds.Any(value =>
                    value.CriterionId == threshold.CriterionId))
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        var nativeRows = rows
            .Where(row => row.NativeTerminalOutcomeVerified &&
                row.CrossedThresholds.Single(value =>
                    value.CriterionId == threshold.CriterionId)
                    .NativeTerminalOutcomeVerified)
            .Select(row => row.RowId)
            .ToArray();
        var comparisonRows = rows
            .Where(row => row.CrossedThresholds.Single(value =>
                value.CriterionId == threshold.CriterionId)
                .TeacherComparison is not null)
            .Select(row => row.RowId)
            .ToArray();
        var native = nativeRows.Length > 0;
        var comparison = comparisonRows.Length > 0;
        var status = native && comparison
            ? "complete"
            : native
                ? "teacher_comparison_missing"
                : "native_outcome_and_teacher_comparison_missing";
        var nextAction = native && comparison
            ? "none"
            : native
                ? "build_exact_comparison_admission_for_existing_outcome"
                : "collect_native_outcome_then_build_exact_comparison_admission";
        return new MoneyThresholdEvidenceWorkCell(
            threshold.CriterionId,
            thresholdOrder,
            threshold.Target,
            threshold.Points,
            partition,
            native,
            comparison,
            nativeRows,
            comparisonRows,
            status,
            nextAction);
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
