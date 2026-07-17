using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Fact]
    public void BindBoundCandidateHandoffToDailyPlanCompilerDoesNotFail()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Rank = 1,
            Score = 0.42,
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            QualifiedItemId = "(O)24",
            Quantity = 1,
            TotalValue = 35
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);

        var plan = new DailyPlanCompiler().Compile(
            result.BoundCandidates,
            snapshot.StateHash);

        Assert.NotNull(plan);
        Assert.Single(plan.CandidateAudit);

        Assert.Contains(result.BoundCandidates[0].Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value == "earn_money");
    }

    [Fact]
    public void BindHandlesGiftCandidateForRaiseFriendships()
    {
        var snapshot = GrandpaSnapshot();
        var giftCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "social_gift:Abigail:Farm:amethyst",
            OptionId = "social.gift_npc",
            Kind = "social_gift_current",
            Rank = 1,
            Score = 0.5,
            Available = true,
            LocationId = "Farm",
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("npc_name", "Abigail"),
                Parameter("qualified_item_id", "(O)66")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "raise_friendships",
            RankedCandidates = new[] { giftCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Single(result.BoundCandidates);
        var bound = result.BoundCandidates[0];
        Assert.Equal("social_gift_current", bound.Kind);
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value == "raise_friendships");
    }

    [Fact]
    public void BindMiningCandidateDoesNotMatchEarnMoney()
    {
        var snapshot = GrandpaSnapshot();
        var miningCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "mining:reach_depth:120",
            OptionId = "mining.reach_depth",
            Kind = "reach_depth",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { miningCandidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
    }

    [Fact]
    public void BindQuestCandidateDoesNotMatchEarnMoneyOrRaiseFriendships()
    {
        var snapshot = GrandpaSnapshot();
        var questCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "quest:advance:some_quest",
            OptionId = "quest.advance",
            Kind = "advance_quest",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { questCandidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
    }

    [Fact]
    public void BindPurchaseCandidateDoesNotMatchEarnMoney()
    {
        var snapshot = GrandpaSnapshot();
        var purchaseCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "buy:seed:parsnip",
            OptionId = "economy.buy_supplies",
            Kind = "buy_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            UnitPrice = 20
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { purchaseCandidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
    }

    [Fact]
    public void BindCatchFishCandidateMatchesMasterAnglerButNotEarnMoney()
    {
        var snapshot = GrandpaSnapshot();
        var fishCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "catch_fish:test",
            OptionId = "fishing.catch_fish",
            Kind = "catch_fish",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();

        var earnMoneyResult = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { fishCandidate }
        }, snapshot);
        Assert.Equal("blocked", earnMoneyResult.BindingStatus);

        var anglerResult = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_master_angler",
            RankedCandidates = new[] { fishCandidate }
        }, snapshot);
        Assert.Equal("ready", anglerResult.BindingStatus);
    }

    [Fact]
    public void BindFarmingCandidateDoesNotMatchAnyDirectBinding()
    {
        var snapshot = GrandpaSnapshot();
        var farmingCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "plant:crop:seed",
            OptionId = "farm.maintain_crops",
            Kind = "plant_seed_tile",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();

        foreach (var directId in new[] { "earn_money", "raise_friendships", "complete_master_angler" })
        {
            var result = binding.Bind(new GrandpaDirectionBindingRequest
            {
                StateHash = snapshot.StateHash,
                DirectionId = directId,
                RankedCandidates = new[] { farmingCandidate }
            }, snapshot);
            Assert.Equal("blocked", result.BindingStatus);
        }
    }

    [Fact]
    public void BindRejectsStaleStateHashInSourceStateHashPreservation()
    {
        var snapshot1 = GrandpaSnapshot();
        var snapshot2 = GrandpaSnapshot();

        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot1.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot1);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal(snapshot1.StateHash, result.SourceStateHash);

        var result2 = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot2.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot2);
        Assert.Equal(snapshot2.StateHash, result2.SourceStateHash);
    }

    [Fact]
    public void BindDoesNotIncludeDuplicateGrandpaProvenanceParams()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        var bound = result.BoundCandidates[0];
        var provenanceNames = new[] { "grandpa_direction_id", "grandpa_source_state_hash", "grandpa_related_factor_ids", "grandpa_binding_rule_id" };
        foreach (var name in provenanceNames)
        {
            var count = bound.Parameters.Count(p => p.Name == name);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public void BindDoesNotAddProvenanceIfAlreadyPresentOnSourceCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("grandpa_direction_id", "earn_money"),
                Parameter("source_kind", "inventory_sale")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];

        Assert.Equal(1, bound.Parameters.Count(p => p.Name == "grandpa_direction_id"));
        Assert.Equal("earn_money", bound.Parameters.First(p => p.Name == "grandpa_direction_id").Value);
        Assert.Equal(1, bound.Parameters.Count(p => p.Name == "grandpa_source_state_hash"));
        Assert.Equal(1, bound.Parameters.Count(p => p.Name == "grandpa_related_factor_ids"));
    }

}
