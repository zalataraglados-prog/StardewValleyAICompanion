using System;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Fact]
    public void DailySubgoalResolverLeavesUnrelatedGoalUnchanged()
    {
        var snapshot = GrandpaSnapshot();
        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot,
            "goal.fishing.catch_fish",
            Array.Empty<PolicyEventCandidatePrediction>());

        Assert.Equal("not_applicable", result.Status);
        Assert.Equal("goal.fishing.catch_fish", result.EffectiveGoalId);
        Assert.Empty(result.DirectionId);
        Assert.Empty(result.BoundCandidateIds);
    }

    [Theory]
    [InlineData(GrandpaEvaluationGoalDefinition.GoalId)]
    [InlineData(GrandpaEvaluationGoalDefinition.StrategicGoal)]
    [InlineData(GrandpaEvaluationGoalDefinition.MaximumScoreEvidenceGoal)]
    public void DailySubgoalResolverAcceptsEveryMaximumScoreGoalAlias(
        string goalId)
    {
        var result = new GrandpaDailySubgoalResolver().Resolve(
            GrandpaSnapshot(),
            goalId,
            Array.Empty<PolicyEventCandidatePrediction>());

        Assert.NotEqual("not_applicable", result.Status);
        Assert.NotEqual(
            "requested_goal_is_not_grandpa_max_score",
            result.Reason);
        Assert.Equal(goalId, result.RequestedGoalId);
    }

    [Fact]
    public void DailySubgoalResolverUsesExactReadyMoneyCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var availability = new OptionAvailabilityEnvelope
        {
            CurrentTime = 600,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:item:1:0",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)24",
                            Quantity = 1,
                            TotalValue = 35
                        }
                    }
                }
            }
        };
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability);
        var resolver = new GrandpaDailySubgoalResolver();
        var detailed = resolver.ResolveWithBinding(
            snapshot,
            GrandpaEvaluationGoalDefinition.StrategicGoal,
            ranked);
        var result = detailed.GoalResolution;
        var rebound = resolver.ApplyBindingProvenance(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability),
            detailed);

        Assert.Equal("resolved", result.Status);
        Assert.Equal("earn_money", result.DirectionId);
        Assert.Equal("goal.economy.earn_money", result.EffectiveGoalId);
        Assert.Equal("economy", result.DemandFamily);
        Assert.Equal(snapshot.StateHash, result.SourceStateHash);
        Assert.Equal(
            new[] { "sell:item:1:0" },
            result.BoundCandidateIds);
        Assert.Equal(
            "grandpa.direct.earn_money",
            result.BindingRuleId);
        var candidate = Assert.Single(ranked);
        Assert.True(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal("ready_now", candidate.TimelineStatus);
        var reboundCandidate = Assert.Single(rebound);
        Assert.Contains(
            reboundCandidate.Parameters,
            parameter =>
                parameter.Name == "grandpa_direction_id" &&
                parameter.Value == "earn_money");
        Assert.Contains(
            reboundCandidate.Parameters,
            parameter =>
                parameter.Name == "grandpa_source_state_hash" &&
                parameter.Value == snapshot.StateHash);
    }

    [Fact]
    public void DailySubgoalResolverPrioritizesReadyStarterGiftOverSocialWork()
    {
        var snapshot = GrandpaSnapshot();
        var starterGift = new PolicyEventCandidatePrediction
        {
            CandidateId = "starter-gift:FarmHouse:3,7",
            OptionId = "rewards.claim_starter_gift",
            Kind = "claim_starter_gift",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };
        var social = new PolicyEventCandidatePrediction
        {
            CandidateId = "social_talk:Abigail:FarmHouse",
            OptionId = "social.talk_npc",
            Kind = "social_talk_current",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot,
            GrandpaEvaluationGoalDefinition.StrategicGoal,
            new[] { starterGift, social });

        Assert.Equal("resolved", result.Status);
        Assert.Equal("earn_money", result.DirectionId);
        Assert.Equal("goal.economy.earn_money", result.EffectiveGoalId);
        Assert.Equal(new[] { starterGift.CandidateId }, result.BoundCandidateIds);
    }

    [Fact]
    public void DailySubgoalResolverDoesNotInventCandidateBinding()
    {
        var snapshot = GrandpaSnapshot();
        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot,
            GrandpaEvaluationGoalDefinition.GoalId,
            Array.Empty<PolicyEventCandidatePrediction>());

        Assert.Equal("no_actionable_direction", result.Status);
        Assert.Equal(
            GrandpaEvaluationGoalDefinition.GoalId,
            result.EffectiveGoalId);
        Assert.Empty(result.DirectionId);
        Assert.Empty(result.BoundCandidateIds);
        Assert.NotEmpty(result.ConsideredDirectionIds);
    }

    [Fact]
    public void UnplantedSeedsRequireCropTeacherBeforeLowerPrioritySocialWork()
    {
        var snapshot = GrandpaSnapshot(withUnplantedSeedInventory: true);
        var socialCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "social_talk:Abigail:Farm",
            OptionId = "social.talk_npc",
            Kind = "social_talk_current",
            Rank = 1,
            Score = 0.35,
            Available = true,
            LocationId = "Farm",
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("npc_name", "Abigail"),
                Parameter("npc_location", "Farm"),
                Parameter("friendship_row_exists_before", "false"),
                Parameter("friendship_points_before", "0"),
                Parameter("expected_friendship_delta", "20"),
                Parameter("expected_friendship_points_after", "20")
            }
        };

        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot,
            GrandpaEvaluationGoalDefinition.StrategicGoal,
            new[] { socialCandidate });

        Assert.Equal("planning_handoff_required", result.Status);
        Assert.Equal("earn_money", result.DirectionId);
        Assert.Equal("goal.economy.earn_money", result.EffectiveGoalId);
        Assert.Empty(result.BoundCandidateIds);
        Assert.NotNull(result.PlanningHandoff);
        Assert.Equal(
            "crop_production_teacher",
            result.PlanningHandoff!.HandoffKind);
        Assert.Equal(
            new[] { "farm_layout" },
            result.PlanningHandoff.RequiredSnapshotProfiles);
        Assert.True(result.PlanningHandoff.AutomaticDispatchRequired);
    }

    [Fact]
    public void ExistingCropPreventsRepeatedInitialCropTeacherHandoff()
    {
        var snapshot = GrandpaSnapshot(
            withUnplantedSeedInventory: true,
            withExistingCrop: true);
        var socialCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "social_talk:Abigail:Farm",
            OptionId = "social.talk_npc",
            Kind = "social_talk_current",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("npc_name", "Abigail"),
                Parameter("npc_location", "Farm"),
                Parameter("friendship_row_exists_before", "false"),
                Parameter("friendship_points_before", "0"),
                Parameter("expected_friendship_delta", "20"),
                Parameter("expected_friendship_points_after", "20")
            }
        };

        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot,
            GrandpaEvaluationGoalDefinition.StrategicGoal,
            new[] { socialCandidate });

        Assert.Equal("resolved", result.Status);
        Assert.Equal("raise_friendships", result.DirectionId);
        Assert.Null(result.PlanningHandoff);
        Assert.Equal(
            new[] { socialCandidate.CandidateId },
            result.BoundCandidateIds);
    }

    [Fact]
    public void DailySubgoalResolverStopsWhenMaximumScoreIsComplete()
    {
        var snapshot = TargetCompleteSnapshot();
        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot,
            GrandpaEvaluationGoalDefinition.StrategicGoal,
            Array.Empty<PolicyEventCandidatePrediction>());

        Assert.Equal("target_complete", result.Status);
        Assert.Equal(
            GrandpaEvaluationGoalDefinition.StrategicGoal,
            result.EffectiveGoalId);
        Assert.Empty(result.ConsideredDirectionIds);
    }

    [Fact]
    public void ResolvedMoneyGoalActivatesOnlyProvenMachineSupport()
    {
        var snapshot = GrandpaSnapshot();
        var availability = new OptionAvailabilityEnvelope
        {
            CurrentTime = 600,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:item:1:0",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)24",
                            Quantity = 1,
                            TotalValue = 35
                        }
                    }
                },
                new OptionAvailability
                {
                    OptionId = "farm.process_machines",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "machine-craft:keg",
                            Kind = "craft_machine_item",
                            Available = true,
                            AllowedNow = true,
                            AllowedToday = true,
                            ExpectedEffect =
                                "machine_demand_priority=200;" +
                                "machine_demand_class=production_capacity_requirement;" +
                                "machine_build_window_open=true;" +
                                "required_additional_machine_count=1;" +
                                "machine_economic_value_status=bounded_current_backlog_positive;" +
                                "machine_capacity_deficit_processing_net_value=400;" +
                                "machine_craft_material_opportunity_cost_status=" +
                                "complete_exact_native_consumption_sale_value;" +
                                "machine_craft_material_opportunity_cost=60"
                        }
                    }
                }
            }
        };
        var ranker = new EventCandidateRanker();
        var broad = ranker.Rank(
            new BaselineTrainingReport(),
            availability,
            GrandpaEvaluationGoalDefinition.StrategicGoal);
        var resolution = new GrandpaDailySubgoalResolver()
            .Resolve(
                snapshot,
                GrandpaEvaluationGoalDefinition.StrategicGoal,
                broad);
        var effective = ranker.Rank(
            new BaselineTrainingReport(),
            availability,
            resolution.EffectiveGoalId);
        var machine = Assert.Single(effective.Where(candidate =>
            candidate.CandidateId == "machine-craft:keg"));

        Assert.Equal("resolved", resolution.Status);
        Assert.Equal(
            "goal.economy.earn_money",
            resolution.EffectiveGoalId);
        Assert.Contains(
            machine.Parameters,
            parameter =>
                parameter.Name == "goal_support_status" &&
                parameter.Value ==
                "supported_bounded_positive_net_benefit");
    }
}
