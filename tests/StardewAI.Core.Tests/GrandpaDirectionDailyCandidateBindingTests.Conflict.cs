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
    public void BindRejectsDuplicateMatchingProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:dup_match:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("grandpa_direction_id", "earn_money"),
                Parameter("grandpa_direction_id", "earn_money")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r =>
            r.Contains("candidate_provenance_duplicate:sell:dup_match:1:0:grandpa_direction_id"));
    }

    [Fact]
    public void BindRejectsDuplicateConflictingProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:dup_conflict:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("grandpa_direction_id", "earn_money"),
                Parameter("grandpa_direction_id", "complete_master_angler")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r =>
            r.Contains("candidate_provenance_duplicate:sell:dup_conflict:1:0:grandpa_direction_id"));
    }

    [Fact]
    public void BindPreservesMatchingExistingProvenance()
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
    }

    [Fact]
    public void BindRejectsCandidateWithConflictingProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:stale:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("grandpa_source_state_hash", "stale_state_hash_value"),
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

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r =>
            r.Contains("candidate_provenance_conflict:sell:stale:1:0:grandpa_source_state_hash"));
    }

    [Fact]
    public void BindRejectsCandidateWithConflictingDirectionIdProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:wrong_dir:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("grandpa_direction_id", "complete_master_angler"),
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

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r =>
            r.Contains("candidate_provenance_conflict:sell:wrong_dir:1:0:grandpa_direction_id"));
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter
        {
            Name = name,
            Value = value
        };
    }}
