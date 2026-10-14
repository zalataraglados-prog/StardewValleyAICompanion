using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyThresholdNativeOutcomeSelection()
    {
        const string saveId = "money-selection-save";
        const string playerId = "money-selection-player";
        var totalDay = 0;
        string splitKey;
        string partition;
        do
        {
            splitKey = saveId + ":" + playerId + ":" + totalDay;
            partition = PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey);
            totalDay++;
        }
        while (partition != PolicyDatasetPartitions.Validation);
        totalDay--;

        var catalog = new MoneyRevenueCandidateDenominatorCatalog
        {
            CurrentSnapshotDenominatorAuthoritative = true,
            CurrentComparisonDenominatorReady = true,
            NodeCoverageComplete = true,
            SnapshotPath = "source-snapshot.json",
            SnapshotSha256 = new string('b', 64),
            ComparisonCandidateCount = 4,
            CatalogSha256 = new string('c', 64),
            Nodes = new[]
            {
                new MoneyRevenueCandidateNodeCoverage
                {
                    NodeId = "dependency.earn_money.sell",
                    OptionId = "economy.sell_items",
                    PolicyChoice = true,
                    EnumerationComplete = true,
                    Candidates = new[]
                    {
                        SaleSelectionCandidate("sale:exact", 20, 40),
                        SaleSelectionCandidate("sale:overshoot", 30, 10),
                        SaleSelectionCandidate(
                            "sale:debug",
                            21,
                            1,
                            new SmallModelActionParameter
                            {
                                Name = "debug.fixture",
                                Value = "true"
                            }),
                        SaleSelectionCandidate("sale:insufficient", 19, 1)
                    }
                }
            }
        };
        var checkpoint = new MoneyRevenueCheckpoint(
            new string('s', 64),
            saveId,
            playerId,
            totalDay,
            1_000,
            49_980,
            "shared",
            0,
            new string('0', 64));
        var selection = MoneyThresholdNativeOutcomeSelectionBuilder.BuildSelection(
            "candidate-denominator.json",
            new string('d', 64),
            catalog,
            checkpoint,
            "money_50000",
            partition);
        Require(selection.Status == "ready_for_native_outcome_collection" &&
                selection.CriterionId == "money_50000" &&
                selection.DatasetPartition ==
                    PolicyDatasetPartitions.Validation &&
                selection.SplitKey == splitKey &&
                selection.TotalMoneyEarnedBefore == 49_980 &&
                selection.SourceStateHash == new string('s', 64) &&
                selection.SourceSnapshotPath == "source-snapshot.json" &&
                selection.SourceSnapshotSha256 == new string('b', 64) &&
                selection.ExpectedTotalMoneyEarnedAfter == 50_000 &&
                selection.ExpectedCrossedCriterionIds.SequenceEqual(
                    new[] { "money_50000" },
                    StringComparer.Ordinal) &&
                selection.EligibleSaleCandidateCount == 3 &&
                selection.CrossingSaleCandidateCount == 2 &&
                selection.SelectedCandidate.CatalogCandidateId == "sale:exact" &&
                selection.ObjectiveParameters.Length == 8 &&
                selection.ObjectiveParameters.All(value =>
                    value.Name.StartsWith(
                        "continuation.",
                        StringComparison.Ordinal)) &&
                selection.CandidateDenominatorCount == 4 &&
                selection.SelectionSha256.Length == 64 &&
                !selection.FormalProductTrainingAuthorized,
            "Money threshold native-outcome selection did not preserve exact sampling authority.");

        var rejectedPartition = false;
        try
        {
            MoneyThresholdNativeOutcomeSelectionBuilder.BuildSelection(
                "candidate-denominator.json",
                new string('d', 64),
                catalog,
                checkpoint,
                "money_50000",
                PolicyDatasetPartitions.Test);
        }
        catch (InvalidDataException)
        {
            rejectedPartition = true;
        }
        Require(rejectedPartition,
            "Money threshold native-outcome selection admitted partition drift.");
    }

    private static MoneyRevenueCandidateCatalogEntry SaleSelectionCandidate(
        string id,
        int totalValue,
        int estimatedTicks,
        params SmallModelActionParameter[] extraParameters)
    {
        var parameters = new[]
        {
            new SmallModelActionParameter
            {
                Name = "continuation.option_id",
                Value = "economy.sell_items"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.shop_id",
                Value = "SeedShop"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.target_location",
                Value = "SeedShop"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.item_id",
                Value = "24"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.qualified_item_id",
                Value = "(O)24"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.slot_index",
                Value = "0"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.quantity",
                Value = "1"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.expected_unit_price",
                Value = totalValue.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            }
        }.Concat(extraParameters).ToArray();
        return new MoneyRevenueCandidateCatalogEntry
        {
            CatalogCandidateId = id,
            CandidateChannel = "event_candidate",
            CandidateId = id,
            CandidateKind = "route_connector_tile",
            SemanticKindBinding = "staged_continuation",
            Available = true,
            ComparisonEligible = true,
            LocationId = "Farm",
            ItemId = "24",
            QualifiedItemId = "(O)24",
            ShopId = "SeedShop",
            SlotIndex = 0,
            Quantity = 1,
            UnitPrice = totalValue,
            TotalValue = totalValue,
            EstimatedTicks = estimatedTicks,
            Parameters = parameters,
            CandidateSha256 = new string('a', 64)
        };
    }
}
