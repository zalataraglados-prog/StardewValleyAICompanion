using System;
using System.Linq;
using StardewAI.Contracts.Capabilities;
using StardewAI.Core.Training;
using Xunit;

namespace StardewAI.Core.Tests;

public sealed class QuestActionCoverageCatalogTests
{
    [Fact]
    public void CatalogCoversEveryNativeQuestAndSpecialOrderObjectiveRuntimeType()
    {
        Assert.Equal(
            new[]
            {
                "CraftingQuest",
                "FishingQuest",
                "GoSomewhereQuest",
                "HaveBuildingQuest",
                "ItemDeliveryQuest",
                "ItemHarvestQuest",
                "LostItemQuest",
                "Quest",
                "ResourceCollectionQuest",
                "SecretLostItemQuest",
                "SlayMonsterQuest",
                "SocializeQuest"
            },
            QuestActionCoverageCatalog.OrdinaryRuntimeTypes);
        Assert.Equal(
            new[]
            {
                "CollectObjective",
                "DeliverObjective",
                "DonateObjective",
                "FishObjective",
                "GiftObjective",
                "JKScoreObjective",
                "ReachMineFloorObjective",
                "ShipObjective",
                "SlayObjective"
            },
            QuestActionCoverageCatalog.SpecialOrderObjectiveRuntimeTypes);
    }

    [Fact]
    public void EveryExecutableOrBlockedStageHasAuditableDetails()
    {
        Assert.All(
            QuestActionCoverageCatalog.All.Where(row => row.BindingStatus == QuestActionCoverageCatalog.Bound),
            row => Assert.NotEmpty(row.CandidateKinds));
        Assert.All(
            QuestActionCoverageCatalog.All.Where(row => row.BindingStatus == QuestActionCoverageCatalog.Blocked),
            row => Assert.False(string.IsNullOrWhiteSpace(row.GapReason)));
        Assert.DoesNotContain(
            QuestActionCoverageCatalog.All,
            row => string.IsNullOrWhiteSpace(row.RuntimeType) ||
                string.IsNullOrWhiteSpace(row.ActionStage) ||
                string.IsNullOrWhiteSpace(row.Evidence));
    }

    [Fact]
    public void BoundCatalogKindsExactlyMatchQuestDailyPlanCompilerDeclaration()
    {
        Assert.True(DailyPlanCompiler.HasOptionCompiler("quest.advance"));
        Assert.Equal(
            QuestActionCoverageCatalog.BoundCandidateKinds,
            DailyPlanCompiler.OptionCompilerCandidateKinds("quest.advance")
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.Contains("mining_reach_depth_plan_envelope", QuestActionCoverageCatalog.BoundCandidateKinds);
        Assert.Contains("ship_inventory_item_to_bin", QuestActionCoverageCatalog.BoundCandidateKinds);
        Assert.DoesNotContain("reach_mine_depth", QuestActionCoverageCatalog.BoundCandidateKinds);
        Assert.DoesNotContain("ship_inventory_item", QuestActionCoverageCatalog.BoundCandidateKinds);
    }

    [Fact]
    public void SecretLostItemAcquisitionIsARecordedFishingTransactionNotASecondQuestExecutor()
    {
        var row = Assert.Single(QuestActionCoverageCatalog.All.Where(candidate =>
            candidate.Family == "ordinary_quest" &&
            candidate.RuntimeType == "SecretLostItemQuest" &&
            candidate.ActionStage == "find_secret_lost_item"));

        Assert.Equal(QuestActionCoverageCatalog.NativeObservationOnly, row.BindingStatus);
        Assert.Empty(row.CandidateKinds);
        Assert.Contains("Railroad.getFish", row.GapReason, StringComparison.Ordinal);
        Assert.Contains("existing fishing transaction", row.GapReason, StringComparison.Ordinal);
    }
}
