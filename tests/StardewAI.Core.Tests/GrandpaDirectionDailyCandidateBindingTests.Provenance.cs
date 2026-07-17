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
    public void BindPreservesAllSourceCandidateFields()
    {
        var snapshot = GrandpaSnapshot();
        var original = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:Turnip:5:2",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Rank = 3,
            Score = 1.5,
            ExpectedReward = 1.2,
            Available = true,
            ItemId = "Turnip",
            QualifiedItemId = "(O)343",
            DisplayName = "Turnip",
            ShopId = "ShippingBin",
            SlotIndex = 2,
            Quantity = 5,
            UnitPrice = 60,
            TotalValue = 300,
            LocationId = "Farm",
            TileX = 65,
            TileY = 15,
            ExpectedEffect = "sell_inventory_item=Turnip;total_value=300",
            EstimatedTicks = 120,
            EnergyCost = 2,
            AvailabilityClass = "always",
            AllowedNow = true,
            AllowedToday = true,
            NextOpenTime = 600,
            EffectiveOpenTime = 600,
            ClosesAt = 2600,
            WaitCost = 0,
            GateReasons = new[] { "gate_a" },
            BlockReasons = Array.Empty<string>(),
            Parameters = new[]
            {
                Parameter("source_kind", "inventory_sale"),
                Parameter("slot_index", "2"),
                Parameter("unit_price", "60")
            },
            TimelineStatus = "ready_now",
            ScheduledStartTime = 600,
            ScheduledWaitCost = 0,
            TimelineReasons = new[] { "candidate_ready_now" }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { original }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.Equal("sell:Turnip:5:2", bound.CandidateId);
        Assert.Equal(3, bound.Rank);
        Assert.Equal(1.5, bound.Score);
        Assert.Equal(1.2, bound.ExpectedReward);
        Assert.Equal("sell_shop_item", bound.Kind);
        Assert.Equal("economy.sell_items", bound.OptionId);
        Assert.Equal("Turnip", bound.ItemId);
        Assert.Equal("(O)343", bound.QualifiedItemId);
        Assert.Equal("Turnip", bound.DisplayName);
        Assert.Equal(2, bound.SlotIndex);
        Assert.Equal(5, bound.Quantity);
        Assert.Equal(60, bound.UnitPrice);
        Assert.Equal(300, bound.TotalValue);
        Assert.Equal("Farm", bound.LocationId);
        Assert.Equal(65, bound.TileX);
        Assert.Equal(15, bound.TileY);
        Assert.Equal("sell_inventory_item=Turnip;total_value=300", bound.ExpectedEffect);
        Assert.Equal(120, bound.EstimatedTicks);
        Assert.Equal(2, bound.EnergyCost);
        Assert.True(bound.Available);
        Assert.True(bound.AllowedNow);
        Assert.True(bound.AllowedToday);

        Assert.Contains(bound.Parameters, p => p.Name == "source_kind" && p.Value == "inventory_sale");
        Assert.Contains(bound.Parameters, p => p.Name == "slot_index" && p.Value == "2");
        Assert.Contains(bound.Parameters, p => p.Name == "unit_price" && p.Value == "60");
    }

    [Fact]
    public void BindDoesNotOverwriteSourceParametersWithProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var original = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            ItemId = "Parsnip",
            QualifiedItemId = "(O)24",
            Quantity = 1,
            TotalValue = 35,
            Parameters = new[]
            {
                Parameter("source_kind", "inventory_sale"),
                Parameter("slot_index", "0")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { original }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.Contains(bound.Parameters, p => p.Name == "source_kind" && p.Value == "inventory_sale");
        Assert.Contains(bound.Parameters, p => p.Name == "slot_index" && p.Value == "0");

        Assert.DoesNotContain(bound.Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value != "earn_money");
    }

    [Fact]
    public void BindDoesNotFabricateTileItemShopTimeEnergyQuantity()
    {
        var snapshot = GrandpaSnapshot();
        var minimal = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:minimal",
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
            RankedCandidates = new[] { minimal }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.Null(bound.TileX);
        Assert.Null(bound.TileY);
        Assert.Empty(bound.ItemId);
        Assert.Empty(bound.QualifiedItemId);
        Assert.Equal(0, bound.Quantity);
        Assert.Equal(0, bound.UnitPrice);
        Assert.Equal(0, bound.EnergyCost);
        Assert.Equal(0, bound.EstimatedTicks);
    }

    [Fact]
    public void BindEarnMoneyCandidateBindsSourceFieldsWithoutOverwriting()
    {
        var snapshot = GrandpaSnapshot();
        var sourceParams = new[]
        {
            Parameter("source_kind", "inventory_sale"),
            Parameter("slot_index", "0"),
            Parameter("unit_price", "100")
        };
        var original = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            ItemId = "Parsnip",
            QualifiedItemId = "(O)24",
            Quantity = 1,
            TotalValue = 100,
            Parameters = sourceParams
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { original }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.Equal(1, bound.Quantity);
        Assert.Equal("(O)24", bound.QualifiedItemId);

        var paramNames = bound.Parameters.Select(p => p.Name).ToList();
        Assert.Equal(4 + 3, paramNames.Distinct().Count());
    }

    [Fact]
    public void BindBlocksCandidateWhenOptionIdNotPermitted()
    {
        var snapshot = GrandpaSnapshot();
        var wrongCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "catch_fish:wrong",
            OptionId = "fishing.catch_fish",
            Kind = "catch_fish",
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
            RankedCandidates = new[] { wrongCandidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindReturnsProvenanceFieldsInResult()
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

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal(snapshot.StateHash, result.SourceStateHash);
        Assert.Equal("economy", result.DirectionDomain);
        Assert.NotEmpty(result.RelatedFactorIds);
        Assert.True(result.DirectionPriorityScore > 0);
        Assert.True(result.DirectionHorizonRequiredMinutes > 0);
        Assert.Equal("grandpa.money", result.FeedbackKey);
        Assert.Equal("grandpa.direct.earn_money", result.BindingRuleId);
        Assert.Equal("ready", result.BindingCoverageStatus);
    }

    [Fact]
    public void BindAuditHasProvenance()
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

        Assert.True(result.Audit.StateHashVerified);
        Assert.True(result.Audit.DirectionSetRebuiltFromSnapshot);
        Assert.Equal("", result.Audit.DirectionRejectedReason);
        Assert.False(result.Audit.CcJojaRouteCommitmentResolved);
        Assert.Contains("GrandpaDirectionDailyCandidateBinding", result.Audit.Binder);
    }

    [Fact]
    public void BindBlocksNonSellKindsForEarnMoney()
    {
        var snapshot = GrandpaSnapshot();
        var nonSellCandidates = new[]
        {
            new PolicyEventCandidatePrediction
            {
                CandidateId = "farm:crop:water",
                OptionId = "farm.maintain_crops",
                Kind = "water_crop_tile",
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                TimelineStatus = "ready_now"
            },
            new PolicyEventCandidatePrediction
            {
                CandidateId = "buy:seed",
                OptionId = "economy.buy_supplies",
                Kind = "buy_shop_item",
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                TimelineStatus = "ready_now",
                ShopId = "SeedShop"
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = nonSellCandidates
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("no_current_permitted_candidate"));
    }

    [Fact]
    public void BindSkipsBlockedCandidates()
    {
        var snapshot = GrandpaSnapshot();
        var blockedCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:blocked:item",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = false,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "blocked"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { blockedCandidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindSkipsCandidatesWhereAllowedNowIsFalse()
    {
        var snapshot = GrandpaSnapshot();
        var notAllowedNow = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:not_allowed",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = false,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { notAllowedNow }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindSkipsCandidatesWhereAllowedTodayIsNotTrue()
    {
        var snapshot = GrandpaSnapshot();
        var notAllowedToday = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:not_allowed_today",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = false,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { notAllowedToday }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindSkipsUnavailableCandidates()
    {
        var snapshot = GrandpaSnapshot();
        var unavailable = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:unavailable",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = false,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { unavailable }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindHandlesMultipleCandidatesForSameDirection()
    {
        var snapshot = GrandpaSnapshot();
        var candidates = new[]
        {
            new PolicyEventCandidatePrediction
            {
                CandidateId = "sell:item1:1:0",
                OptionId = "economy.sell_items",
                Kind = "sell_shop_item",
                Rank = 1,
                Score = 1.0,
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                TimelineStatus = "ready_now"
            },
            new PolicyEventCandidatePrediction
            {
                CandidateId = "sell:item2:5:1",
                OptionId = "economy.sell_items",
                Kind = "sell_shop_item",
                Rank = 2,
                Score = 0.9,
                Available = true,
                AllowedNow = true,
                AllowedToday = true,
                TimelineStatus = "ready_now"
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = candidates
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal(2, result.BoundCandidates.Length);
        Assert.All(result.BoundCandidates, c =>
            Assert.Contains(c.Parameters, p => p.Name == "grandpa_direction_id"));
        Assert.Equal("ready", result.BindingCoverageStatus);
    }

}
