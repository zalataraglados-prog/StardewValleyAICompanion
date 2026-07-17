using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class EventCandidateRankerTests
{
    [Fact]
    public void RankIncludesAvailableTalkSocialCandidate()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "social.talk_npc", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.talk_npc",
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_talk:Abigail:Town",
                            Kind = "social_talk_current",
                            Available = true,
                            LocationId = "Town",
                            TileX = 9,
                            TileY = 11,
                            EstimatedTicks = 160,
                            EnergyCost = 0,
                            Parameters = new[]
                            {
                                new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
                                new SmallModelActionParameter { Name = "npc_tile_x", Value = "10" },
                                new SmallModelActionParameter { Name = "npc_tile_y", Value = "10" },
                                new SmallModelActionParameter { Name = "stand_tile_x", Value = "9" },
                                new SmallModelActionParameter { Name = "stand_tile_y", Value = "11" }
                            }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("social_talk:Abigail:Town", candidate.CandidateId);
        Assert.Equal("social.talk_npc", candidate.OptionId);
        Assert.Equal("social_talk_current", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(9, candidate.TileX);
        Assert.Equal(11, candidate.TileY);
        Assert.Equal(160, candidate.EstimatedTicks);
    }

    [Fact]
    public void RankIncludesAvailableGiftSocialCandidate()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "social.gift_npc", AverageTotalReward = 0.15 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.gift_npc",
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_gift:Abigail:Town:(O)66",
                            Kind = "social_gift_current",
                            Available = true,
                            LocationId = "Town",
                            TileX = 9,
                            TileY = 11,
                            SlotIndex = 0,
                            QualifiedItemId = "(O)66",
                            EstimatedTicks = 180,
                            EnergyCost = 0,
                            Parameters = new[]
                            {
                                new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
                                new SmallModelActionParameter { Name = "npc_tile_x", Value = "10" },
                                new SmallModelActionParameter { Name = "npc_tile_y", Value = "10" },
                                new SmallModelActionParameter { Name = "stand_tile_x", Value = "9" },
                                new SmallModelActionParameter { Name = "stand_tile_y", Value = "11" },
                                new SmallModelActionParameter { Name = "slot_index", Value = "0" },
                                new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)66" },
                                new SmallModelActionParameter { Name = "item_stack_before", Value = "5" },
                                new SmallModelActionParameter { Name = "expected_friendship_delta", Value = "80" }
                            }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("social_gift:Abigail:Town:(O)66", candidate.CandidateId);
        Assert.Equal("social.gift_npc", candidate.OptionId);
        Assert.Equal("social_gift_current", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.Equal("(O)66", candidate.QualifiedItemId);
    }

    [Fact]
    public void RankSkipsBlockedSocialCandidate()
    {
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.talk_npc",
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_talk:Abigail:Town",
                            Kind = "social_talk_current",
                            Available = false,
                            LocationId = "Town",
                            BlockReasons = new[] { "social_npc_not_in_player_location" },
                            EstimatedTicks = -1
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        Assert.Empty(ranked);
    }

    [Fact]
    public void RankPreservesSocialCandidateParameters()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "social.talk_npc", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.talk_npc",
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_talk:Abigail:Town",
                            Kind = "social_talk_current",
                            Available = true,
                            LocationId = "Town",
                            TileX = 9,
                            TileY = 11,
                            EstimatedTicks = 160,
                            Parameters = new[]
                            {
                                new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
                                new SmallModelActionParameter { Name = "npc_tile_x", Value = "10" },
                                new SmallModelActionParameter { Name = "npc_tile_y", Value = "10" },
                                new SmallModelActionParameter { Name = "stand_tile_x", Value = "9" },
                                new SmallModelActionParameter { Name = "stand_tile_y", Value = "11" },
                                new SmallModelActionParameter { Name = "route_distance_tiles", Value = "3" },
                                new SmallModelActionParameter { Name = "route_distance_ticks", Value = "40" }
                            }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Contains(candidate.Parameters, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(candidate.Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(candidate.Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(candidate.Parameters, p => p.Name == "stand_tile_x" && p.Value == "9");
        Assert.Contains(candidate.Parameters, p => p.Name == "stand_tile_y" && p.Value == "11");
        Assert.Contains(candidate.Parameters, p => p.Name == "route_distance_tiles" && p.Value == "3");
        Assert.Contains(candidate.Parameters, p => p.Name == "route_distance_ticks" && p.Value == "40");
    }

    [Fact]
    public void RankDeduplicatesByIdAcrossEventAndSocial()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "social.talk_npc", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.talk_npc",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_talk:Abigail:Town",
                            Kind = "water_crop_tile",
                            Available = true,
                            LocationId = "Farm",
                            TileX = 1,
                            TileY = 2,
                            ExpectedEffect = "farm.crops[1,2].needs_watering=false",
                            EstimatedTicks = 60,
                            EnergyCost = 2
                        }
                    },
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_talk:Abigail:Town",
                            Kind = "social_talk_current",
                            Available = true,
                            LocationId = "Town",
                            TileX = 9,
                            TileY = 11,
                            EstimatedTicks = 160,
                            EnergyCost = 0
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Single(ranked);
        Assert.Equal("water_crop_tile", ranked[0].Kind);
        Assert.Equal("Farm", ranked[0].LocationId);
    }

    [Fact]
    public void RankedTalkSocialCandidateCompilesIntoMoveAndSocialInteract()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "social.talk_npc", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.talk_npc",
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "social_talk:Abigail:Town",
                            Kind = "social_talk_current",
                            Available = true,
                            LocationId = "Town",
                            TileX = 8,
                            TileY = 10,
                            EstimatedTicks = 160,
                            EnergyCost = 0,
                            Parameters = new[]
                            {
                                new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
                                new SmallModelActionParameter { Name = "npc_tile_x", Value = "10" },
                                new SmallModelActionParameter { Name = "npc_tile_y", Value = "10" },
                                new SmallModelActionParameter { Name = "stand_tile_x", Value = "8" },
                                new SmallModelActionParameter { Name = "stand_tile_y", Value = "10" }
                            }
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);
        var candidate = Assert.Single(ranked);

        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate }, "test_state_hash");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("social_interact", plan.Steps[1].Kind);
        Assert.Contains(plan.Steps[1].Parameters,
            p => p.Name == "social_action_kind" && p.Value == "talk");
    }

    [Fact]
    public void RankPreservesEmptyCandidateIdFromSocialCandidate()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "social.talk_npc", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "social.talk_npc",
                    SocialCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "",
                            Kind = "social_talk_current",
                            Available = true,
                            LocationId = "Town",
                            TileX = 9,
                            TileY = 11,
                            EstimatedTicks = 160,
                            EnergyCost = 0
                        },
                        new EventCandidate
                        {
                            CandidateId = "",
                            Kind = "social_talk_current",
                            Available = true,
                            LocationId = "Forest",
                            TileX = 3,
                            TileY = 3,
                            EstimatedTicks = 160,
                            EnergyCost = 0
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
    }}
