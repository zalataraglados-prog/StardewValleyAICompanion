using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyThresholdEvidenceCellProjector
{
    private static readonly string[] RequiredPartitions =
    {
        PolicyDatasetPartitions.Train,
        PolicyDatasetPartitions.Validation,
        PolicyDatasetPartitions.Test
    };

    public static bool IsRequiredPartition(string partition) =>
        RequiredPartitions.Contains(partition, StringComparer.Ordinal);

    public static int RequiredPartitionCount => RequiredPartitions.Length;

    public static MoneyThresholdEvidenceWorkCell[] Build(
        MoneyThresholdTeacherCorpus? corpus) =>
        GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SelectMany((threshold, thresholdOrder) =>
                RequiredPartitions.Select(partition => BuildCell(
                    corpus,
                    threshold,
                    thresholdOrder,
                    partition)))
            .ToArray();

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
}
