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
    public void All12DirectionsExistInCatalogAndCorrespondToAdapter()
    {
        var snapshot = GrandpaSnapshot();
        var worldModel = new WorldModelProjector().Project(snapshot, "grandpa_four_candles_year3", "training_singleplayer");
        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
        var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);

        var catalogIds = GrandpaDirectionCatalog.Entries
            .Select(e => e.DirectionId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var adapterIds = sample.CandidateDirections
            .Select(c => c.DirectionId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogIds, adapterIds);

        foreach (var adapterDir in sample.CandidateDirections)
        {
            var catalogEntry = GrandpaDirectionCatalog.Entries
                .First(e => e.DirectionId == adapterDir.DirectionId);
            Assert.False(string.IsNullOrWhiteSpace(catalogEntry.BindingRuleId));
        }
    }

    [Fact]
    public void BindDirectionMetadataIsSourcedFromAdapterNotCatalog()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = Array.Empty<PolicyEventCandidatePrediction>()
        }, snapshot);

        Assert.True(result.DirectionKnown);
        Assert.False(result.DirectionBlocked);
        Assert.True(result.DirectionPriorityScore > 0);
        Assert.True(result.PotentialPoints > 0);
        Assert.Equal("economy", result.DirectionDomain);
        Assert.Equal("Increase total money earned", result.DirectionLabel);
        Assert.Equal("grandpa.money", result.FeedbackKey);
        Assert.NotEmpty(result.RelatedFactorIds);
    }

    [Fact]
    public void BindSingleCandidateIsReadyNotFull()
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
        Assert.Equal("ready", result.BindingCoverageStatus);
        Assert.Single(result.BoundCandidates);
    }

    [Fact]
    public void BindClonesCandidateArraysToPreventAliasing()
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
            Parameters = new[]
            {
                Parameter("p1", "v1")
            },
            GateReasons = new[] { "g1" },
            TimelineReasons = new[] { "t1" }
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

        Assert.NotSame(original.Parameters, bound.Parameters);
        Assert.NotSame(original.GateReasons, bound.GateReasons);
        Assert.NotSame(original.BlockReasons, bound.BlockReasons);
        Assert.NotSame(original.TimelineReasons, bound.TimelineReasons);
    }

    [Fact]
    public void BindSixNonDirectDirectionsAllReturnBlockedWithPlannedRequirements()
    {
        var blockedDirections = new[]
        {
            "complete_museum_collection",
            "obtain_rusty_key",
            "complete_community_center",
            "complete_joja_development",
            "marriage_and_house_upgrade",
            "earn_pet_love"
        };

        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();

        foreach (var directionId in blockedDirections)
        {
            var result = binding.Bind(new GrandpaDirectionBindingRequest
            {
                StateHash = snapshot.StateHash,
                DirectionId = directionId,
                RankedCandidates = new[]
                {
                    new PolicyEventCandidatePrediction
                    {
                        CandidateId = "any",
                        OptionId = "any",
                        Kind = "any",
                        Available = true,
                        AllowedNow = true,
                        AllowedToday = true,
                        TimelineStatus = "ready_now"
                    }
                }
            }, snapshot);

            Assert.Equal("blocked", result.BindingStatus);
            Assert.Equal("blocked", result.BindingCoverageStatus);
            Assert.NotEmpty(result.BlockReasons);
            Assert.NotEmpty(result.MissingTransparentFields);
            Assert.NotEmpty(result.MissingCapabilities);
        }
    }

    [Fact]
    public void BindCcJojaRowsAlwaysReportUnresolvedRouteCommitment()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();

        foreach (var ccJojaId in new[] { "complete_community_center", "complete_joja_development" })
        {
            var result = binding.Bind(new GrandpaDirectionBindingRequest
            {
                StateHash = snapshot.StateHash,
                DirectionId = ccJojaId
            }, snapshot);

            Assert.Equal("blocked", result.BindingStatus);
            Assert.Contains("cc_joja_route_commitment_unavailable", result.BlockReasons);
            Assert.False(result.Audit.CcJojaRouteCommitmentResolved);
            Assert.NotEmpty(result.MissingTransparentFields);
            Assert.NotEmpty(result.MissingCapabilities);
        }
    }

    [Fact]
    public void BindProvidesRejectionDetailForBlockedCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var blockedCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:blocked:1",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
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

        Assert.Contains(result.BlockReasons, r => r.Contains("candidate_blocked_timeline:"));
    }

    [Fact]
    public void BindRejectsCandidateWithBlockReasonsEvenWhenTimelineNotBlocked()
    {
        var snapshot = GrandpaSnapshot();
        var candidateWithBlockReasons = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:has_block_reasons",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            BlockReasons = new[] { "item_not_found_in_inventory" }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { candidateWithBlockReasons }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("candidate_has_block_reasons:"));
    }

    [Fact]
    public void BindPreservesCandidateIdScoreRankExpectedRewardAndActions()
    {
        var snapshot = GrandpaSnapshot();
        var original = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:preserved:3:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Rank = 3,
            Score = 1.75,
            ExpectedReward = 0.95,
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            ItemId = "Parsnip",
            QualifiedItemId = "(O)24",
            LocationId = "Farm",
            Quantity = 1,
            TotalValue = 35,
            Parameters = new[]
            {
                Parameter("source_kind", "inventory_sale")
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
        Assert.Equal("sell:preserved:3:0", bound.CandidateId);
        Assert.Equal(3, bound.Rank);
        Assert.Equal(1.75, bound.Score);
        Assert.Equal(0.95, bound.ExpectedReward);
        Assert.Equal("economy.sell_items", bound.OptionId);
        Assert.Equal("sell_shop_item", bound.Kind);
        Assert.Equal("Parsnip", bound.ItemId);
        Assert.Equal("(O)24", bound.QualifiedItemId);
        Assert.Equal("Farm", bound.LocationId);
        Assert.Equal(1, bound.Quantity);
        Assert.Equal(35, bound.TotalValue);
    }

    private static SnapshotEnvelope GrandpaSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "identity": {
            "save_id": {"value":"test-save","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"test-player","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "year": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":"ws://localhost/test","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return SnapshotFromState(state);
    }

    private static SnapshotEnvelope TargetCompleteSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "identity": {
            "save_id": {"value":"test-save","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"test-player","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "year": {"value":3,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":1200000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":25,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":2,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true,"tile_x":68,"tile_y":15,"tile_width":2,"tile_height":1}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[5,26,34],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":true,"completed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shipping_collection": {"value":{"status":"available"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[{"npc":"Abigail","points":2500}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":["petLoveMessage"],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":"ws://localhost/test","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return SnapshotFromState(state);
    }

    private static SnapshotEnvelope SnapshotFromState(Dictionary<string, JsonElement> state)
    {
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static ActionActorRef Actor()
    {
        return new ActionActorRef
        {
            ActorId = "training_farmer.main",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        };
    }

}
