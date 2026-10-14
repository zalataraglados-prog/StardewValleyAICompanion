using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeOutcomeSelectionBuilder
{
    private static readonly HashSet<string> ObjectiveParameterNames = new(
        new[]
        {
            "continuation.option_id",
            "continuation.shop_id",
            "continuation.target_location",
            "continuation.item_id",
            "continuation.qualified_item_id",
            "continuation.slot_index",
            "continuation.quantity",
            "continuation.expected_unit_price"
        },
        StringComparer.Ordinal);

    public static MoneyThresholdNativeOutcomeSelection Build(
        string candidateDenominatorPath,
        string criterionId,
        string datasetPartition)
    {
        var fullPath = RequiredFile(
            candidateDenominatorPath,
            "Money Revenue candidate denominator");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCandidateDenominatorCatalog>(
                fullPath,
                "Money Revenue candidate denominator");
        var catalog = MoneyRevenueCandidateDenominatorBuilder.Build(
            persisted.SnapshotPath,
            persisted.DependencyExpansionPath);
        Require(EqualJson(persisted, catalog),
            "Money Revenue candidate denominator drifted from its source rebuild.");
        Require(catalog.CurrentSnapshotDenominatorAuthoritative &&
                catalog.CurrentComparisonDenominatorReady &&
                catalog.NodeCoverageComplete &&
                !catalog.FormalProductTrainingAuthorized,
            "Money Revenue candidate denominator is not current and authoritative.");

        var snapshot = CurrentTeacherFrontierSupport.Read<
            StardewAI.Contracts.State.SnapshotEnvelope>(
                catalog.SnapshotPath,
                "Money threshold source snapshot");
        var checkpoint = MoneyRevenueProgressionVerifier.Project(snapshot);
        return BuildSelection(
            fullPath,
            CurrentTeacherFrontierSupport.HashFile(fullPath),
            catalog,
            checkpoint,
            criterionId,
            datasetPartition);
    }

    internal static MoneyThresholdNativeOutcomeSelection BuildSelection(
        string candidateDenominatorPath,
        string candidateDenominatorSha256,
        MoneyRevenueCandidateDenominatorCatalog catalog,
        MoneyRevenueCheckpoint checkpoint,
        string criterionId,
        string datasetPartition)
    {
        var threshold = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SingleOrDefault(value => string.Equals(
                value.CriterionId,
                criterionId,
                StringComparison.Ordinal)) ??
            throw new InvalidDataException(
                "Money threshold criterion is not authoritative: " + criterionId);
        Require(datasetPartition is
                PolicyDatasetPartitions.Train or
                PolicyDatasetPartitions.Validation or
                PolicyDatasetPartitions.Test,
            "Money threshold dataset partition is invalid.");
        Require(catalog.CurrentSnapshotDenominatorAuthoritative &&
                catalog.CurrentComparisonDenominatorReady &&
                catalog.NodeCoverageComplete &&
                catalog.ComparisonCandidateCount >= 2 &&
                !catalog.FormalProductTrainingAuthorized,
            "Money Revenue candidate denominator is not comparison-ready.");
        Require(!string.IsNullOrWhiteSpace(checkpoint.StateHash) &&
                !string.IsNullOrWhiteSpace(checkpoint.SaveId) &&
                !string.IsNullOrWhiteSpace(checkpoint.PlayerId),
            "Money threshold source checkpoint identity is incomplete.");
        var splitKey = checkpoint.SaveId + ":" + checkpoint.PlayerId + ":" +
            checkpoint.TotalDay;
        var actualPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey);
        Require(actualPartition == datasetPartition,
            "Money threshold source belongs to partition " + actualPartition +
            ", not requested partition " + datasetPartition + ".");
        Require(checkpoint.TotalMoneyEarned < threshold.Target,
            "Money threshold source already satisfies the requested criterion.");

        var saleNodes = catalog.Nodes.Where(node =>
                node.PolicyChoice &&
                node.OptionId == "economy.sell_items")
            .ToArray();
        Require(saleNodes.Length == 1 && saleNodes[0].EnumerationComplete,
            "Money threshold source has no unique complete shop-sale node.");
        var eligible = saleNodes[0].Candidates
            .Where(IsExecutableShopSaleStage)
            .OrderBy(candidate => candidate.CatalogCandidateId,
                StringComparer.Ordinal)
            .ToArray();
        var crossing = eligible.Where(candidate =>
                checked(checkpoint.TotalMoneyEarned +
                    (uint)candidate.TotalValue) >= threshold.Target)
            .OrderBy(candidate => candidate.TotalValue)
            .ThenBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CatalogCandidateId,
                StringComparer.Ordinal)
            .ToArray();
        Require(crossing.Length > 0,
            "Money threshold source has no current shop-sale candidate that crosses " +
            threshold.CriterionId + ".");

        var selected = crossing[0];
        var objectiveParameters = selected.Parameters
            .Where(value => value.Name.StartsWith(
                "continuation.",
                StringComparison.Ordinal))
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        var expectedAfter = checked(
            checkpoint.TotalMoneyEarned + (uint)selected.TotalValue);
        var expectedCrossed = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .Where(value => checkpoint.TotalMoneyEarned < value.Target &&
                expectedAfter >= value.Target)
            .Select(value => value.CriterionId)
            .ToArray();
        Require(expectedCrossed.Contains(threshold.CriterionId,
                StringComparer.Ordinal),
            "Selected shop-sale candidate does not cross the requested criterion.");

        var selection = new MoneyThresholdNativeOutcomeSelection
        {
            Status = "ready_for_native_outcome_collection",
            CriterionId = threshold.CriterionId,
            TargetTotalMoneyEarned = threshold.Target,
            ExpectedCrossedCriterionIds = expectedCrossed,
            DatasetPartition = actualPartition,
            SplitKey = splitKey,
            SaveId = checkpoint.SaveId,
            PlayerId = checkpoint.PlayerId,
            TotalDay = checkpoint.TotalDay,
            TotalMoneyEarnedBefore = checkpoint.TotalMoneyEarned,
            SourceStateHash = checkpoint.StateHash,
            SourceSnapshotPath = catalog.SnapshotPath,
            SourceSnapshotSha256 = catalog.SnapshotSha256,
            ExpectedTotalMoneyEarnedAfter = expectedAfter,
            CandidateDenominatorPath = candidateDenominatorPath,
            CandidateDenominatorSha256 = candidateDenominatorSha256,
            CandidateDenominatorCatalogSha256 = catalog.CatalogSha256,
            CandidateDenominatorCount = catalog.ComparisonCandidateCount,
            EligibleSaleCandidateCount = eligible.Length,
            CrossingSaleCandidateCount = crossing.Length,
            SelectedCandidate = selected,
            ObjectiveParameters = objectiveParameters,
            FormalProductTrainingAuthorized = false
        };
        selection.SelectionSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                selection);
        return selection;
    }

    private static bool IsExecutableShopSaleStage(
        MoneyRevenueCandidateCatalogEntry candidate)
    {
        if (!candidate.Available ||
            !candidate.ComparisonEligible ||
            candidate.CandidateChannel != "event_candidate" ||
            candidate.TotalValue <= 0 ||
            candidate.Quantity <= 0 ||
            candidate.UnitPrice <= 0 ||
            !candidate.SlotIndex.HasValue ||
            string.IsNullOrWhiteSpace(candidate.ItemId) ||
            string.IsNullOrWhiteSpace(candidate.ShopId) ||
            string.IsNullOrWhiteSpace(candidate.QualifiedItemId) ||
            candidate.BlockingReasons.Length != 0)
        {
            return false;
        }

        var parameters = candidate.Parameters
            .GroupBy(value => value.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        return HasSingle(parameters, "continuation.option_id", "economy.sell_items") &&
            HasSingle(parameters, "continuation.shop_id", candidate.ShopId) &&
            HasSingleNonEmpty(parameters, "continuation.target_location") &&
            HasSingle(parameters, "continuation.item_id", candidate.ItemId) &&
            HasSingle(parameters, "continuation.qualified_item_id",
                candidate.QualifiedItemId) &&
            HasSingle(parameters, "continuation.slot_index",
                candidate.SlotIndex.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)) &&
            HasSingle(parameters, "continuation.quantity",
                candidate.Quantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)) &&
            HasSingle(parameters, "continuation.expected_unit_price",
                candidate.UnitPrice.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)) &&
            parameters.Keys.Where(name => name.StartsWith(
                    "continuation.",
                    StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(ObjectiveParameterNames) &&
            parameters.Keys.All(name =>
                !name.StartsWith("debug.", StringComparison.Ordinal)) &&
            parameters.Values.SelectMany(value => value).All(value =>
                !value.StartsWith("debug.", StringComparison.Ordinal));
    }

    private static bool HasSingle(
        IReadOnlyDictionary<string, string[]> parameters,
        string name,
        string expected) =>
        parameters.TryGetValue(name, out var values) &&
        values.Length == 1 &&
        string.Equals(values[0], expected, StringComparison.Ordinal);

    private static bool HasSingleNonEmpty(
        IReadOnlyDictionary<string, string[]> parameters,
        string name) =>
        parameters.TryGetValue(name, out var values) &&
        values.Length == 1 &&
        !string.IsNullOrWhiteSpace(values[0]);

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
            throw new InvalidDataException(message);
    }
}
