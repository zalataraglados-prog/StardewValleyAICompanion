using System;
using System.Linq;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
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
        Assert.DoesNotContain(
            QuestActionCoverageCatalog.All,
            row => row.BindingStatus == QuestActionCoverageCatalog.Blocked);
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
        Assert.Contains("play_junimo_kart", QuestActionCoverageCatalog.BoundCandidateKinds);
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

    [Fact]
    public void TypeElevenWeedingIsRecordedAsAnUnreachableCompatibilityConstant()
    {
        var row = Assert.Single(QuestActionCoverageCatalog.All.Where(candidate =>
            candidate.Family == "ordinary_quest" &&
            candidate.RuntimeType == "Quest" &&
            candidate.ActionStage == "weeding_no_subclass"));

        Assert.Equal(QuestActionCoverageCatalog.NativeUnreachable, row.BindingStatus);
        Assert.Empty(row.CandidateKinds);
        Assert.Contains("type_weeding=11", row.GapReason, StringComparison.Ordinal);
        Assert.Contains("Data/Quests has no such row", row.GapReason, StringComparison.Ordinal);
        Assert.Contains("never assign questType 11", row.GapReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternallyInjectedTypeElevenQuestFailsClosedInsteadOfInventingAWeedingExecutor()
    {
        var candidate = Assert.Single(QuestCandidateBuilder.BuildOrdinaryCandidates(new[]
        {
            new QuestProgressRef
            {
                Id = "legacy_or_modded_type_11",
                RuntimeType = "Quest",
                QuestType = 11,
                Accepted = true,
                PerTypeFields = new PerTypeQuestFields { Available = true, IsBaseQuest = true }
            }
        }));

        Assert.False(candidate.Available);
        Assert.Equal("weeding_no_subclass", candidate.NextActionCategory);
        Assert.Contains("quest_type_11_unreachable_in_vanilla_1_6_15", candidate.BlockedDiagnostics);
    }

    [Fact]
    public void NativeQuestThirteenUsesItsLockedBeachEventRoute()
    {
        var candidate = Assert.Single(
            QuestCandidateBuilder.BuildOrdinaryCandidates(new[]
            {
                new QuestProgressRef
                {
                    Id = "13",
                    RuntimeType = "Quest",
                    QuestType = 1,
                    Accepted = false,
                    Completed = false,
                    PerTypeFields = new PerTypeQuestFields
                    {
                        Available = true,
                        IsBaseQuest = true
                    }
                }
            }));

        Assert.True(candidate.Available);
        Assert.Equal("Quest", candidate.RuntimeType);
        Assert.Equal("trigger_location_event", candidate.NextActionCategory);
        Assert.Equal("Beach", candidate.RequiredTargetLocation);
        Assert.Contains(
            "Data/Events/Beach",
            candidate.Provenance,
            StringComparison.Ordinal);
        Assert.Contains(
            "739330/t 600 1710/*n spring_2_1",
            candidate.Provenance,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuestRouteCompilerPreservesAuthorityThroughMechanicalConnectorStep()
    {
        var plan = new DailyPlanCompiler().Compile(
            new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "quest:13:Quest:bound:route:Farm:80,15:warp:to=BusStop:arrival=11,23",
                    OptionId = "quest.advance",
                    Kind = "route_connector_tile",
                    Rank = 1,
                    TimelineStatus = "ready_now",
                    LocationId = "Farm",
                    TileX = 80,
                    TileY = 15,
                    ExpectedEffect = "player.location_id=BusStop;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 600,
                    Parameters = new[]
                    {
                        Parameter("connector_kind", "warp"),
                        Parameter("expected_target_location", "BusStop"),
                        Parameter("quest_id", "13"),
                        Parameter("quest_runtime_type", "Quest"),
                        Parameter("quest_next_action", "trigger_location_event"),
                        Parameter("quest_target_location", "Beach"),
                        Parameter("quest_event_id", "739330"),
                        Parameter("quest_event_required_mail_id", "spring_2_1"),
                        Parameter("quest_event_source_asset", "Data/Events/Beach"),
                        Parameter("quest_event_source_key", "739330/t 600 1710/*n spring_2_1")
                    }
                }
            },
            "state-hash");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("traverse_connector", step.Kind);
        AssertParameter(step.Parameters, "quest_id", "13");
        AssertParameter(step.Parameters, "quest_runtime_type", "Quest");
        AssertParameter(step.Parameters, "quest_next_action", "trigger_location_event");
        AssertParameter(step.Parameters, "quest_target_location", "Beach");
        AssertParameter(step.Parameters, "quest_event_id", "739330");
        AssertParameter(step.Parameters, "quest_event_required_mail_id", "spring_2_1");
        AssertParameter(step.Parameters, "quest_event_source_asset", "Data/Events/Beach");
        AssertParameter(step.Parameters, "quest_event_source_key", "739330/t 600 1710/*n spring_2_1");
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
    }

    private static void AssertParameter(
        SmallModelActionParameter[] parameters,
        string name,
        string value)
    {
        Assert.Contains(parameters, parameter =>
            parameter.Name == name && parameter.Value == value);
    }
}
