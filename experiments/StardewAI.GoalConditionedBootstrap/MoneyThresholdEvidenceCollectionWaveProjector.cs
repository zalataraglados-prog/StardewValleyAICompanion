namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyThresholdEvidenceCollectionWaveProjector
{
    public static MoneyThresholdEvidenceCollectionWaves Build(
        MoneyThresholdEvidenceWorkCell[] cells,
        MoneyThresholdTeacherCorpus? corpus) =>
        new(
            BuildNative(cells),
            BuildComparison(cells, corpus));

    private static MoneyThresholdNativeCollectionWave[] BuildNative(
        MoneyThresholdEvidenceWorkCell[] cells) =>
        cells
            .Where(cell => !cell.NativeOutcomeVerified)
            .GroupBy(cell => cell.DatasetPartition, StringComparer.Ordinal)
            .Select(group =>
            {
                var missing = group
                    .OrderBy(cell => cell.ThresholdOrder)
                    .ToArray();
                var lowest = missing[0];
                var highest = missing[^1];
                return new MoneyThresholdNativeCollectionWave(
                    group.Key,
                    missing.Select(cell => cell.CriterionId).ToArray(),
                    lowest.TargetTotalMoneyEarned,
                    highest.TargetTotalMoneyEarned,
                    highest.CriterionId,
                    "conditional_native_outcome_collection_required",
                    "Use one isolated source whose total money earned is below the lowest missing threshold and one verified Revenue outcome that reaches the highest missing threshold. The native selection builder must prove every crossed criterion. If no single authoritative candidate can satisfy both bounds, split this wave and rebuild the work plan; never synthesize coverage.");
            })
            .OrderBy(wave => wave.DatasetPartition, StringComparer.Ordinal)
            .ToArray();

    private static MoneyThresholdComparisonCollectionWave[] BuildComparison(
        MoneyThresholdEvidenceWorkCell[] cells,
        MoneyThresholdTeacherCorpus? corpus)
    {
        if (corpus is null)
        {
            return Array.Empty<MoneyThresholdComparisonCollectionWave>();
        }

        var rowById = corpus.Rows.ToDictionary(
            row => row.RowId,
            StringComparer.Ordinal);
        return cells
            .Where(cell => cell.NativeOutcomeVerified &&
                !cell.TeacherComparisonVerified)
            .Select(cell => new ComparisonCellBinding(
                cell,
                cell.NativeOutcomeRowIds
                    .OrderBy(rowId => rowId, StringComparer.Ordinal)
                    .First()))
            .GroupBy(value => value.RowId, StringComparer.Ordinal)
            .Select(group => BuildComparisonWave(group, rowById))
            .OrderBy(wave => wave.DatasetPartition, StringComparer.Ordinal)
            .ThenBy(wave => wave.SourceRowId, StringComparer.Ordinal)
            .ToArray();
    }

    private static MoneyThresholdComparisonCollectionWave BuildComparisonWave(
        IGrouping<string, ComparisonCellBinding> group,
        IReadOnlyDictionary<string, MoneyThresholdTeacherEvidenceRow> rowById)
    {
        if (!rowById.TryGetValue(group.Key, out var row))
        {
            throw new InvalidDataException(
                "Money work-plan native row is absent from the verified corpus: " +
                group.Key + ".");
        }

        var criterionIds = group
            .Select(value => value.Cell)
            .OrderBy(cell => cell.ThresholdOrder)
            .Select(cell => cell.CriterionId)
            .ToArray();
        return new MoneyThresholdComparisonCollectionWave(
            row.DatasetPartition,
            row.RowId,
            row.SourceId,
            criterionIds,
            "exact_comparison_collection_required",
            "Initialize the Revenue comparison denominator once from this replay-verified native row, then build one criterion-local exact comparison admission for every listed criterion. All admissions attached to one seed must select the same native outcome; different selected alternatives require independent seed runs.");
    }

    private sealed record ComparisonCellBinding(
        MoneyThresholdEvidenceWorkCell Cell,
        string RowId);
}

internal sealed record MoneyThresholdEvidenceCollectionWaves(
    MoneyThresholdNativeCollectionWave[] Native,
    MoneyThresholdComparisonCollectionWave[] Comparison);
