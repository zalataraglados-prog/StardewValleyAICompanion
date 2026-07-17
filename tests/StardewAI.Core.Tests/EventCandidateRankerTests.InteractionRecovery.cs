using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class EventCandidateRankerTests
{
    [Fact]
    public void RankIncludesAvailableInteractEndpointCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.interact", AverageTotalReward = 0.05 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.interact",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "interact:Town:11,10:OpenShop:SeedShop",
                            Kind = "interact_endpoint",
                            Available = true,
                            LocationId = "Town",
                            TileX = 11,
                            TileY = 10,
                            ExpectedEffect = "move_to_adjacent=10,10;preview_interact=OpenShop",
                            EstimatedTicks = 30
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("interact:Town:11,10:OpenShop:SeedShop", candidate.CandidateId);
        Assert.Equal("interact_endpoint", candidate.Kind);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=OpenShop", candidate.ExpectedEffect);
        Assert.Equal(30, candidate.EstimatedTicks);
    }

    [Fact]
    public void RankDefersInteractEndpointWhenShopOpensLaterToday()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "executor.interact", AverageTotalReward = 0.05 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            CurrentTime = 800,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.interact",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "interact:Town:11,10:OpenShop:SeedShop",
                            Kind = "interact_endpoint",
                            Available = false,
                            LocationId = "Town",
                            TileX = 11,
                            TileY = 10,
                            ExpectedEffect = "move_to_adjacent=10,10;preview_interact=OpenShop",
                            EstimatedTicks = 30,
                            AvailabilityClass = "windowed_available",
                            AllowedNow = false,
                            AllowedToday = true,
                            NextOpenTime = 900,
                            EffectiveOpenTime = 900,
                            ClosesAt = 1700,
                            WaitCost = 3600,
                            GateReasons = new[] { "shop_not_open_yet" },
                            BlockReasons = new[] { "interact_shop_service_time_blocked" }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("deferred", candidate.TimelineStatus);
        Assert.Equal(900, candidate.ScheduledStartTime);
        Assert.Equal(3600, candidate.ScheduledWaitCost);
        Assert.Equal("windowed_available", candidate.AvailabilityClass);
        Assert.False(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal(900, candidate.NextOpenTime);
        Assert.Equal(1700, candidate.ClosesAt);
        Assert.Contains("candidate_deferred_until_open", candidate.TimelineReasons);
        Assert.Contains("shop_not_open_yet", candidate.TimelineReasons);
        Assert.Contains("interact_shop_service_time_blocked", candidate.TimelineReasons);
    }

    [Fact]
    public void RankSkipsInteractEndpointWhenShopCannotOpenToday()
    {
        var availability = new OptionAvailabilityEnvelope
        {
            CurrentTime = 1800,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.interact",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "interact:Town:11,10:OpenShop:SeedShop",
                            Kind = "interact_endpoint",
                            Available = false,
                            AllowedNow = false,
                            AllowedToday = false,
                            EffectiveOpenTime = 900,
                            ClosesAt = 1700,
                            GateReasons = new[] { "shop_closed_for_day" }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        Assert.Empty(ranked);
    }

    [Fact]
    public void RankSkipsInteractEndpointWhenItWouldFinishAfterClose()
    {
        var availability = new OptionAvailabilityEnvelope
        {
            CurrentTime = 1659,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "executor.interact",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "interact:Town:11,10:OpenShop:SeedShop",
                            Kind = "interact_endpoint",
                            Available = true,
                            AllowedNow = true,
                            AllowedToday = true,
                            ClosesAt = 1700,
                            EstimatedTicks = 120
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        Assert.Empty(ranked);
    }

    [Fact]
    public void RankIncludesAvailableRecoveryRefreshCandidates()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "recovery.stabilize_day", AverageTotalReward = 0.02 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "recovery.stabilize_day",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "recovery:refresh_plan_after_stabilization",
                            Kind = "recovery_refresh_plan",
                            Available = true,
                            ExpectedEffect = "executor.wait_ticks=30;urgent_risks_rechecked",
                            EstimatedTicks = 30
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("recovery:refresh_plan_after_stabilization", candidate.CandidateId);
        Assert.Equal("recovery_refresh_plan", candidate.Kind);
        Assert.Equal("executor.wait_ticks=30;urgent_risks_rechecked", candidate.ExpectedEffect);
        Assert.Equal(30, candidate.EstimatedTicks);
    }

    [Fact]
    public void RankUsesTransparentMachineValueSignals()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "farm.process_machines", AverageTotalReward = 0.03 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "farm.process_machines",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "machine-input:Farm:64,15:slot0:(O)262",
                            Kind = "load_machine_input_tile",
                            Available = true,
                            ExpectedEffect = "machine_input_opportunity_cost=15;machine_input_value_basis=transparent_input_sale_price_output_unknown",
                            EstimatedTicks = 90
                        },
                        new EventCandidate
                        {
                            CandidateId = "machine-output:Farm:65,15:(O)348",
                            Kind = "collect_machine_output_tile",
                            Available = true,
                            ExpectedEffect = "output_stack=1;output_sale_price=120;output_total_value=120;machine_value_basis=held_item_sale_price_times_stack",
                            EstimatedTicks = 90
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("machine-output:Farm:65,15:(O)348", ranked[0].CandidateId);
        Assert.Equal("collect_machine_output_tile", ranked[0].Kind);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void RankUsesPredictedMachineInputNetValueWhenAvailable()
    {
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "farm.process_machines",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "machine-input:Farm:64,15:slot0:(O)262",
                            Kind = "load_machine_input_tile",
                            Available = true,
                            ExpectedEffect = "machine_input_opportunity_cost=15;predicted_output_total_value=200;machine_additional_consumed_total_value=50;predicted_output_net_value=135;machine_input_value_basis=predicted_output_total_value_minus_transparent_input_and_additional_consumed_sale_price",
                            EstimatedTicks = 90
                        },
                        new EventCandidate
                        {
                            CandidateId = "machine-input:Farm:65,15:slot1:(O)262",
                            Kind = "load_machine_input_tile",
                            Available = true,
                            ExpectedEffect = "machine_input_opportunity_cost=15;machine_input_value_basis=transparent_input_sale_price_output_unknown",
                            EstimatedTicks = 90
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("machine-input:Farm:64,15:slot0:(O)262", ranked[0].CandidateId);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void RankPrioritizesProfitableMachineInputOverLowerValueReadyOutput()
    {
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "farm.process_machines",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "machine-output:Farm:65,15:(O)348",
                            Kind = "collect_machine_output_tile",
                            Available = true,
                            ExpectedEffect = "output_stack=1;output_sale_price=120;output_total_value=120;machine_value_basis=held_item_sale_price_times_stack",
                            EstimatedTicks = 90
                        },
                        new EventCandidate
                        {
                            CandidateId = "machine-input:Farm:64,15:slot0:(O)262",
                            Kind = "load_machine_input_tile",
                            Available = true,
                            ExpectedEffect = "machine_input_opportunity_cost=15;predicted_output_total_value=200;predicted_output_net_value=185;machine_input_value_basis=predicted_output_total_value_minus_transparent_input_sale_price",
                            EstimatedTicks = 90
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        Assert.Equal(2, ranked.Length);
        Assert.Equal("machine-input:Farm:64,15:slot0:(O)262", ranked[0].CandidateId);
        Assert.Equal("load_machine_input_tile", ranked[0].Kind);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

}
