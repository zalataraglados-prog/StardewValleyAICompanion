using System;
using System.Linq;
using StardewAI.Contracts.Capabilities;
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
}
