using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class FullShipmentContributionTests
{
    [Fact]
    public void EventCandidateRankerCopiesFullShipmentFieldsFromEconomicCandidate()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:0",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)74",
                            DisplayName = "Prismatic Shard",
                            SlotIndex = 0,
                            Quantity = 1,
                            UnitPrice = 500,
                            TotalValue = 500,
                            CanShip = true,
                            CanShopSell = false,
                            FullShipmentKnown = true,
                            FullShipmentEligible = true,
                            FullShipmentCurrentShippedCount = 0,
                            FullShipmentAlreadyShipped = false,
                            FullShipmentContributes = true
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("sell:0", candidate.CandidateId);
        Assert.Equal(500, candidate.TotalValue);
        Assert.True(candidate.CanShip);
        Assert.False(candidate.CanShopSell);
        Assert.True(candidate.FullShipmentKnown);
        Assert.True(candidate.FullShipmentEligible);
        Assert.Equal(0, candidate.FullShipmentCurrentShippedCount);
        Assert.False(candidate.FullShipmentAlreadyShipped);
        Assert.True(candidate.FullShipmentContributes);
        Assert.True(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal(
            "current_economic_context",
            candidate.AvailabilityClass);
    }

    [Fact]
    public void EventCandidateRankerPreservesCanShipAndCanShopSellCorrectly()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:shop_only",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)24",
                            CanShip = false,
                            CanShopSell = true,
                            TotalValue = 35
                        },
                        new EconomicCandidate
                        {
                            CandidateId = "sell:bin_ok",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)74",
                            CanShip = true,
                            CanShopSell = false,
                            TotalValue = 500
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        var shopOnly = ranked.First(c => c.CandidateId == "sell:shop_only");
        Assert.False(shopOnly.CanShip);
        Assert.True(shopOnly.CanShopSell);

        var binOk = ranked.First(c => c.CandidateId == "sell:bin_ok");
        Assert.True(binOk.CanShip);
        Assert.False(binOk.CanShopSell);
    }

    [Fact]
    public void CloneCandidatePreservesFullShipmentFields()
    {
        var source = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Rank = 1,
            Score = 0.42,
            ExpectedReward = 0.1,
            Available = true,
            ItemId = "74",
            QualifiedItemId = "(O)74",
            DisplayName = "Prismatic Shard",
            ShopId = "ShippingBin",
            SlotIndex = 0,
            Quantity = 1,
            UnitPrice = 500,
            TotalValue = 500,
            CanShip = true,
            CanShopSell = false,
            FullShipmentKnown = true,
            FullShipmentEligible = true,
            FullShipmentCurrentShippedCount = 0,
            FullShipmentAlreadyShipped = false,
            FullShipmentContributes = true,
            LocationId = "Farm",
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = GrandpaSnapshot().StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { source }
        }, GrandpaSnapshot());

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];

        Assert.True(bound.CanShip);
        Assert.False(bound.CanShopSell);
        Assert.True(bound.FullShipmentKnown);
        Assert.True(bound.FullShipmentEligible);
        Assert.Equal(0, bound.FullShipmentCurrentShippedCount);
        Assert.False(bound.FullShipmentAlreadyShipped);
        Assert.True(bound.FullShipmentContributes);
        Assert.Equal("(O)74", bound.QualifiedItemId);
        Assert.Equal(500, bound.TotalValue);
    }

    [Fact]
    public void ShopSellOnlyCandidateRankedOnValueSignalButFullShipmentContributesFalse()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:shop_only_gem",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)60",
                            DisplayName = "Emerald",
                            Quantity = 1,
                            UnitPrice = 250,
                            TotalValue = 250,
                            CanShip = false,
                            CanShopSell = true,
                            FullShipmentKnown = true,
                            FullShipmentEligible = true,
                            FullShipmentCurrentShippedCount = 0,
                            FullShipmentAlreadyShipped = false,
                            FullShipmentContributes = false
                        },
                        new EconomicCandidate
                        {
                            CandidateId = "sell:ship_eligible_gem",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)74",
                            DisplayName = "Prismatic Shard",
                            Quantity = 1,
                            UnitPrice = 500,
                            TotalValue = 500,
                            CanShip = true,
                            CanShopSell = false,
                            FullShipmentKnown = true,
                            FullShipmentEligible = true,
                            FullShipmentCurrentShippedCount = 0,
                            FullShipmentAlreadyShipped = false,
                            FullShipmentContributes = true
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        var diamond = ranked.First(c => c.CandidateId == "sell:ship_eligible_gem");
        Assert.True(diamond.FullShipmentContributes);

        var emerald = ranked.First(c => c.CandidateId == "sell:shop_only_gem");
        Assert.False(emerald.FullShipmentContributes);
        Assert.False(emerald.CanShip);
    }

    // === Fail-closed and transparency tests ===

    [Fact]
    public void MissingStandTileBlocksCandidateWithTransparentReason()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));

        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "time": { "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "player": {
            "inventory": {{inventory}},
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":65,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":18,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true,"tile_x":68,"tile_y":15,"tile_width":2,"tile_height":1}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "full_shipment_progress": {{fsProgress}},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_location_id":"Farm","current_location_is_home":true,"bed_tile_x":10,"bed_tile_y":5,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"map_width":80,"map_height":80},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":80,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": { "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "quests": { "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":{"read_only":false,"held_item_present":false,"safety_timer":0,"categories_to_sell":[-75,-79,-81,-7,-2,-12,-18,-26]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.False(candidate.Available);
        Assert.Contains("shipping_bin_no_transparent_interaction_stand_tile", candidate.BlockReasons);
    }

    [Fact]
    public void ShippingBinContentsReadFromTransparentSnapshot()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));

        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "time": { "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "player": {
            "inventory": {{inventory}},
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":65,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":18,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true,"tile_x":68,"tile_y":15,"tile_width":2,"tile_height":1,"interaction_stand_tile_x":67,"interaction_stand_tile_y":15,"contents":[{"item_id":"24","qualified_item_id":"(O)24","count":3},{"item_id":"60","qualified_item_id":"(O)60","count":1}],"contents_total_count":4,"contents_distinct_item_count":2}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "full_shipment_progress": {{fsProgress}},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_location_id":"Farm","current_location_is_home":true,"bed_tile_x":10,"bed_tile_y":5,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"map_width":80,"map_height":80},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":80,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": { "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "quests": { "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":{"read_only":false,"held_item_present":false,"safety_timer":0,"categories_to_sell":[-75,-79,-81,-7,-2,-12,-18,-26]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.True(candidate.Available);
        Assert.Contains("bin_current_count_of_item=3", candidate.ExpectedEffect);
    }

    [Fact]
    public void ShippingBinContentsEmptyWhenContentsFieldMissing()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("bin_current_count_of_item=0", candidate.ExpectedEffect);
    }

    // === Static source-guard tests (adapter) ===

}
