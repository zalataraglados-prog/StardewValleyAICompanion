using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeOutcomeWaveSelectionBuilder
{
    public static MoneyThresholdNativeOutcomeSelection Build(
        string candidateDenominatorPath,
        string workPlanPath,
        string datasetPartition)
    {
        var inputs = MoneyThresholdNativeOutcomeSelectionBuilder.LoadInputs(
            candidateDenominatorPath);
        var fullWorkPlanPath = RequiredFile(
            workPlanPath,
            "Money threshold evidence work plan");
        var workPlan = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdEvidenceWorkPlan>(
                fullWorkPlanPath,
                "Money threshold evidence work plan");
        return BuildSelection(
            inputs.CandidateDenominatorPath,
            inputs.CandidateDenominatorSha256,
            inputs.Catalog,
            inputs.Checkpoint,
            workPlan,
            datasetPartition);
    }

    internal static MoneyThresholdNativeOutcomeSelection BuildSelection(
        string candidateDenominatorPath,
        string candidateDenominatorSha256,
        MoneyRevenueCandidateDenominatorCatalog catalog,
        MoneyRevenueCheckpoint checkpoint,
        MoneyThresholdEvidenceWorkPlan workPlan,
        string datasetPartition)
    {
        Require(workPlan.SchemaVersion ==
                "money_threshold_evidence_work_plan.v1" &&
                workPlan.RequiredCellCount == 18 &&
                workPlan.CriterionCount == 6 &&
                workPlan.RequiredPartitionCount == 3 &&
                !workPlan.FormalProductTrainingAuthorized,
            "Money threshold evidence work plan is invalid.");
        var waves = workPlan.NativeCollectionWaves.Where(value =>
                value.DatasetPartition == datasetPartition)
            .ToArray();
        Require(waves.Length == 1,
            "Money threshold work plan must expose one native wave for " +
            datasetPartition + ".");
        var wave = waves[0];
        var authority = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .ToDictionary(value => value.CriterionId, StringComparer.Ordinal);
        var missing = wave.MissingCriterionIds
            .Select(id => authority.TryGetValue(id, out var threshold)
                ? threshold
                : throw new InvalidDataException(
                    "Money collection wave contains a non-authoritative criterion: " +
                    id + "."))
            .OrderBy(value => value.Target)
            .ToArray();
        Require(missing.Length > 0 &&
                missing.Select(value => value.CriterionId)
                    .Distinct(StringComparer.Ordinal).Count() == missing.Length &&
                workPlan.Cells.Count(cell =>
                    cell.DatasetPartition == datasetPartition &&
                    !cell.NativeOutcomeVerified &&
                    missing.Any(value =>
                        value.CriterionId == cell.CriterionId)) == missing.Length &&
                wave.SourceTotalMoneyEarnedMaxExclusive == missing[0].Target &&
                wave.OutcomeTotalMoneyEarnedMinInclusive == missing[^1].Target &&
                wave.RecommendedCriterionId == missing[^1].CriterionId &&
                wave.Status == "conditional_native_outcome_collection_required",
            "Money threshold native collection wave is inconsistent.");
        Require(checkpoint.TotalMoneyEarned <
                wave.SourceTotalMoneyEarnedMaxExclusive,
            "Money threshold source is not below the collection wave lower bound.");

        var maximumSaleValue = catalog.Nodes
            .Where(node => node.PolicyChoice &&
                node.OptionId == "economy.sell_items" &&
                node.EnumerationComplete)
            .SelectMany(node => node.Candidates)
            .Where(MoneyThresholdNativeOutcomeSelectionBuilder
                .IsExecutableShopSaleStage)
            .Select(candidate => (uint)candidate.TotalValue)
            .DefaultIfEmpty(0u)
            .Max();
        var maximumOutcome = checked(
            checkpoint.TotalMoneyEarned + maximumSaleValue);
        var selectedThreshold = missing
            .Where(value => checkpoint.TotalMoneyEarned < value.Target &&
                maximumOutcome >= value.Target)
            .OrderByDescending(value => value.Target)
            .FirstOrDefault() ?? throw new InvalidDataException(
                "Money threshold source has no current shop-sale candidate " +
                "that advances its collection wave.");
        var selection = MoneyThresholdNativeOutcomeSelectionBuilder
            .BuildSelection(
                candidateDenominatorPath,
                candidateDenominatorSha256,
                catalog,
                checkpoint,
                selectedThreshold.CriterionId,
                datasetPartition);
        var scheduledCrossings = missing
            .Where(value => value.Target <= selectedThreshold.Target)
            .Select(value => value.CriterionId)
            .ToArray();
        Require(scheduledCrossings.All(value =>
                selection.ExpectedCrossedCriterionIds.Contains(
                    value,
                    StringComparer.Ordinal)),
            "Money wave selection did not close its reachable criterion prefix.");
        return selection;
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
