using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class EventCandidateRankerTests
{
    [Fact]
    public void RankOrdersAvailableEconomicCandidatesByOptionScoreAndValueSignal()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 },
                new BaselineOptionScore { OptionId = "economy.buy_supplies", AverageTotalReward = 0.2 }
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
                            QualifiedItemId = "(O)24",
                            DisplayName = "Parsnip",
                            SlotIndex = 0,
                            Quantity = 3,
                            UnitPrice = 35,
                            TotalValue = 105
                        }
                    }
                },
                new OptionAvailability
                {
                    OptionId = "economy.buy_supplies",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "buy:0",
                            Kind = "buy_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)472",
                            DisplayName = "Parsnip Seeds",
                            Quantity = 1,
                            UnitPrice = 20,
                            TotalValue = 20
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal(1, ranked[0].Rank);
        Assert.Equal("sell:0", ranked[0].CandidateId);
        Assert.Equal("economy.sell_items", ranked[0].OptionId);
        Assert.Equal("buy:0", ranked[1].CandidateId);
    }

    [Fact]
    public void RankSkipsBlockedEconomicCandidates()
    {
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
                            Available = false,
                            BlockReasons = new[] { "inventory_item_protected_from_auto_sell" }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        Assert.Empty(ranked);
    }

    [Fact]
    public void RankIncludesAvailableWateringEventCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "farm.maintain_crops", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "farm.maintain_crops",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "water:Farm:1,2",
                            Kind = "water_crop_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 1,
                            TileY = 2,
                            ExpectedEffect = "farm.crops[1,2].needs_watering=false",
                            EstimatedTicks = 60,
                            EnergyCost = 2
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("water:Farm:1,2", candidate.CandidateId);
        Assert.Equal("farm.maintain_crops", candidate.OptionId);
        Assert.Equal("water_crop_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(1, candidate.TileX);
        Assert.Equal(2, candidate.TileY);
        Assert.Equal("farm.crops[1,2].needs_watering=false", candidate.ExpectedEffect);
        Assert.Equal(60, candidate.EstimatedTicks);
        Assert.Equal(2, candidate.EnergyCost);
    }

    [Fact]
    public void RankIncludesAvailableHarvestCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "farm.maintain_crops", AverageTotalReward = 0.05 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "farm.maintain_crops",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "harvest:Farm:7,8",
                            Kind = "harvest_crop_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 7,
                            TileY = 8,
                            ExpectedEffect = "farm.crops[7,8].ready_for_harvest=false;harvest_executor_status=runtime_verified",
                            EstimatedTicks = 60
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("harvest:Farm:7,8", candidate.CandidateId);
        Assert.Equal("harvest_crop_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(7, candidate.TileX);
        Assert.Equal(8, candidate.TileY);
        Assert.Equal("farm.crops[7,8].ready_for_harvest=false;harvest_executor_status=runtime_verified", candidate.ExpectedEffect);
    }

    [Fact]
    public void RankIncludesAvailableClearObstacleEventCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.clear_obstacle", AverageTotalReward = 0.04 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.clear_obstacle",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "clear:Farm:11,10:grass",
                            Kind = "clear_obstacle_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 11,
                            TileY = 10,
                            ExpectedEffect = "current_location.obstacle[11,10]=clear;clear_kind=grass;source=Grass",
                            EstimatedTicks = 60,
                            EnergyCost = 0
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("clear:Farm:11,10:grass", candidate.CandidateId);
        Assert.Equal("executor.clear_obstacle", candidate.OptionId);
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("current_location.obstacle[11,10]=clear;clear_kind=grass;source=Grass", candidate.ExpectedEffect);
        Assert.Equal(60, candidate.EstimatedTicks);
    }

    [Fact]
    public void RankPrioritizesPlantingCandidatesWithTighterMaturitySlack()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.plant_seed", AverageTotalReward = 0.03 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.plant_seed",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,6:472",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 6,
                            ItemId = "472",
                            QualifiedItemId = "(O)472",
                            Quantity = 3,
                            ExpectedEffect = "current_location.planting_context[5,6].has_crop=true;player.seed_inventory[472].stack_decreases;seed_id=472;adjusted_grow_days=4;days_remaining_in_season=20",
                            EstimatedTicks = 60
                        },
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,7:473",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 7,
                            ItemId = "473",
                            QualifiedItemId = "(O)473",
                            Quantity = 3,
                            ExpectedEffect = "current_location.planting_context[5,7].has_crop=true;player.seed_inventory[473].stack_decreases;seed_id=473;adjusted_grow_days=6;days_remaining_in_season=7",
                            EstimatedTicks = 60
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("plant:Farm:5,7:473", ranked[0].CandidateId);
        Assert.Equal("plant_seed_tile", ranked[0].Kind);
        Assert.True(ranked[0].Score > ranked[1].Score);
        Assert.Equal("473", ranked[0].ItemId);
        Assert.Equal(3, ranked[0].Quantity);
    }

    [Fact]
    public void RankAddsTransparentFirstHarvestValueSignalForPlantingCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.plant_seed", AverageTotalReward = 0.03 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.plant_seed",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,6:472",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 6,
                            ItemId = "472",
                            Quantity = 3,
                            ExpectedEffect = "seed_id=472;adjusted_grow_days=4;days_remaining_in_season=20;expected_first_harvest_value=35",
                            EstimatedTicks = 60
                        },
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,7:999",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 7,
                            ItemId = "999",
                            Quantity = 3,
                            ExpectedEffect = "seed_id=999;adjusted_grow_days=4;days_remaining_in_season=20;expected_first_harvest_value=150",
                            EstimatedTicks = 60
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("plant:Farm:5,7:999", ranked[0].CandidateId);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void RankPrefersNetPlantingValueWhenTransparentSeedCostExists()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.plant_seed", AverageTotalReward = 0.03 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.plant_seed",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,6:cheap",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 6,
                            ItemId = "cheap",
                            ExpectedEffect = "seed_id=cheap;adjusted_grow_days=4;days_remaining_in_season=20;estimated_first_harvest_value=80;seed_unit_cost=20;estimated_first_harvest_net_value=60",
                            EstimatedTicks = 60
                        },
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,7:expensive",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 7,
                            ItemId = "expensive",
                            ExpectedEffect = "seed_id=expensive;adjusted_grow_days=4;days_remaining_in_season=20;estimated_first_harvest_value=150;seed_unit_cost=140;estimated_first_harvest_net_value=10",
                            EstimatedTicks = 60
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("plant:Farm:5,6:cheap", ranked[0].CandidateId);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void RankPrefersSeasonPlantingValueWhenRegrowEstimateExists()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.plant_seed", AverageTotalReward = 0.03 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.plant_seed",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,6:single",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 6,
                            ItemId = "single",
                            ExpectedEffect = "seed_id=single;adjusted_grow_days=4;days_remaining_in_season=20;estimated_first_harvest_net_value=90;estimated_season_harvest_net_value=90",
                            EstimatedTicks = 60
                        },
                        new EventCandidate
                        {
                            CandidateId = "plant:Farm:5,7:regrow",
                            Kind = "plant_seed_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 5,
                            TileY = 7,
                            ItemId = "regrow",
                            ExpectedEffect = "seed_id=regrow;adjusted_grow_days=4;days_remaining_in_season=20;estimated_first_harvest_net_value=40;estimated_regrow_harvest_count=4;estimated_total_harvest_count=5;estimated_season_harvest_net_value=180",
                            EstimatedTicks = 60
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("plant:Farm:5,7:regrow", ranked[0].CandidateId);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void RankIncludesAvailableRouteConnectorEventCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "exploration.visit_location", AverageTotalReward = 0.08 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "exploration.visit_location",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "route:Farm:12,10:warp",
                            Kind = "route_connector_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 12,
                            TileY = 10,
                            ExpectedEffect = "player.tile=12,10;route_connector=warp",
                            EstimatedTicks = 120,
                            EnergyCost = 0
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("route:Farm:12,10:warp", candidate.CandidateId);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(12, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("player.tile=12,10;route_connector=warp", candidate.ExpectedEffect);
        Assert.Equal(120, candidate.EstimatedTicks);
    }

}
