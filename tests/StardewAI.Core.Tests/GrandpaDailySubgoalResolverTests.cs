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
