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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CatalogHas12NonOverlappingEntries()
    {
        var entries = GrandpaDirectionCatalog.Entries;
        Assert.Equal(12, entries.Length);

        var directionIds = entries.Select(e => e.DirectionId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var expected = new[]
        {
            "complete_community_center",
            "complete_full_shipment",
            "complete_joja_development",
            "complete_master_angler",
            "complete_museum_collection",
            "earn_money",
            "earn_pet_love",
            "marriage_and_house_upgrade",
            "obtain_rusty_key",
            "obtain_skull_key",
            "raise_friendships",
            "raise_skill_levels"
        };
        Assert.Equal(expected, directionIds);

        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            Assert.True(uniqueIds.Add(entry.DirectionId), "Duplicate direction_id: " + entry.DirectionId);
            Assert.False(string.IsNullOrWhiteSpace(entry.BindingRuleId), "Missing binding_rule_id for: " + entry.DirectionId);
        }
    }

    [Fact]
    public void CatalogEntriesArePlanPolicyOnlyNoScoreMetadata()
    {
        var entries = GrandpaDirectionCatalog.Entries;
        Assert.All(entries, entry =>
        {
            Assert.NotEmpty(entry.DirectionId);
            Assert.NotEmpty(entry.BindingRuleId);
            Assert.NotNull(entry.PermittedOptionIds);
            Assert.NotNull(entry.PermittedCandidateKinds);
            Assert.NotNull(entry.RequiredTransparentFields);
            Assert.NotNull(entry.RequiredCapabilities);
            Assert.False(string.IsNullOrWhiteSpace(entry.BlockReasonTemplate));
        });
    }

    [Fact]
    public void NineDirectionsHaveDirectBindingEnabled()
    {
        var directDirections = GrandpaDirectionCatalog.Entries
            .Where(e => e.DirectBindingEnabled)
            .Select(e => e.DirectionId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(9, directDirections.Length);
        Assert.Contains("earn_money", directDirections);
        Assert.Contains("raise_friendships", directDirections);
        Assert.Contains("complete_master_angler", directDirections);
        Assert.Contains("complete_full_shipment", directDirections);
        Assert.Contains("obtain_skull_key", directDirections);
        Assert.Contains("raise_skill_levels", directDirections);
        Assert.Contains("earn_pet_love", directDirections);
        Assert.Contains("complete_museum_collection", directDirections);
        Assert.Contains("obtain_rusty_key", directDirections);
    }

    [Fact]
    public void BindRejectsEmptyStateHash()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = string.Empty,
            DirectionId = "earn_money"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("state_hash_is_empty", result.BlockReasons);
        Assert.True(result.Audit.StateHashEmptyOrUnknown);
        Assert.False(result.Audit.StateHashVerified);
    }

    [Fact]
    public void BindRejectsNullSnapshotWithKnownStateHash()
    {
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = "abc123",
            DirectionId = "earn_money"
        }, null);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("state_hash_unknown"));
        Assert.True(result.Audit.StateHashEmptyOrUnknown);
    }

    [Fact]
    public void BindRejectsStateHashMismatch()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = "mismatched_hash_value",
            DirectionId = "earn_money"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("state_hash_mismatch", result.BlockReasons[0]);
        Assert.False(result.Audit.StateHashVerified);
    }

    [Fact]
    public void BindRejectsEmptyDirectionId()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = string.Empty
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("direction_id_is_empty", result.BlockReasons);
    }

    [Fact]
    public void BindRejectsUnknownDirectionId()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "nonexistent_direction"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.StartsWith("unknown_direction_id:"));
    }

    [Fact]
    public void BindRejectsStaleDirectionWhenTargetAlreadyComplete()
    {
        var snapshot = TargetCompleteSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.True(result.TargetAlreadyComplete);
        Assert.Contains("target_already_complete", result.BlockReasons);
        Assert.Equal("blocked", result.BindingCoverageStatus);
    }

    [Fact]
    public void BindRejectsDirectionAbsentFromCandidateSetWhenTargetComplete()
    {
        var snapshot = TargetCompleteSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
    }

    [Fact]
    public void BindFullShipmentRejectsCandidateWithoutExactContributionEvidence()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "ship:unknown",
                    OptionId = "economy.ship_items",
                    Kind = "ship_inventory_item_to_bin",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    CanShip = true
                }
            }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("full_shipment_evidence_unknown"));
        Assert.Equal("blocked", result.BindingCoverageStatus);
        Assert.Empty(result.MissingTransparentFields);
        Assert.NotEmpty(result.CoveredTransparentFields);
        Assert.Empty(result.MissingCapabilities);
    }

    [Fact]
    public void BindAllowsRaiseSkillLevelsOnlyWithCompletePositiveExperienceEvidence()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "raise_skill_levels",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "harvest:Farm:10,10",
                    OptionId = "farm.maintain_crops",
                    Kind = "harvest_crop_tile",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    Parameters = new[]
                    {
                        Parameter("skill_experience_skill_id", "farming"),
                        Parameter("skill_experience_on_success_min", "8"),
                        Parameter("skill_experience_on_success_max", "8"),
                        Parameter("skill_experience_condition", "native_player_crop_harvest"),
                        Parameter("skill_experience_projection_status", "exact")
                    }
                }
            }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var candidate = Assert.Single(result.BoundCandidates);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "grandpa_direction_id" && parameter.Value == "raise_skill_levels");
    }

    [Fact]
    public void BindRejectsRaiseSkillLevelsWithoutPositiveCompleteExperienceEvidence()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "raise_skill_levels",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "clear:Farm:10,10:grass",
                    OptionId = "executor.clear_obstacle",
                    Kind = "clear_obstacle_tile",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    Parameters = new[]
                    {
                        Parameter("skill_experience_skill_id", "foraging"),
                        Parameter("skill_experience_on_success_min", "0"),
                        Parameter("skill_experience_on_success_max", "0"),
                        Parameter("skill_experience_condition", "native_grass_cut"),
                        Parameter("skill_experience_projection_status", "exact")
                    }
                }
            }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, reason => reason.Contains("skill_experience_not_positive"));
    }

    [Fact]
    public void BindObtainSkullKeyRequiresAndPreservesExactAcquisitionContract()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "obtain_skull_key",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "mining:obtain_skull_key",
                    OptionId = "mining.obtain_skull_key",
                    Kind = "mining_obtain_skull_key_plan_envelope",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    Parameters = new[]
                    {
                        Parameter("target_location_family", "ordinary_mines"),
                        Parameter("target_depth", "120"),
                        Parameter("required_terminal_interaction", "skull_key_reward_chest"),
                        Parameter("required_postcondition", "player.has_skull_key=true"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", "current_floor_step_executable")
                    }
                }
            }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal("grandpa.direct.obtain_skull_key", result.BindingRuleId);
        Assert.Single(result.BoundCandidates);
        Assert.Contains(result.BoundCandidates[0].Parameters, parameter =>
            parameter.Name == "required_postcondition" && parameter.Value == "player.has_skull_key=true");
    }

    [Fact]
    public void BindCompleteMuseumCollectionAcceptsExactPositiveDonationProgress()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_museum_collection",
            RankedCandidates = new[]
            {
                MuseumDonationCandidate(
                    "museum:collection-final",
                    Parameter("expected_donated_count_before", "94"),
                    Parameter("expected_donated_count_after", "95"),
                    Parameter("museum_total_donatable_items", "95"))
            }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal("grandpa.direct.complete_museum_collection", result.BindingRuleId);
        Assert.Single(result.BoundCandidates);
    }

    [Fact]
    public void BindObtainRustyKeyAcceptsExactThresholdDonationProgress()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "obtain_rusty_key",
            RankedCandidates = new[]
            {
                MuseumDonationCandidate(
                    "museum:rusty-key-threshold",
                    Parameter("expected_donated_count_before", "59"),
                    Parameter("expected_donated_count_after", "60"),
                    Parameter("rusty_key_donation_threshold", "60"),
                    Parameter("rusty_key_reward_action", "MarkEventSeen Host 295672"))
            }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal("grandpa.direct.obtain_rusty_key", result.BindingRuleId);
        Assert.Single(result.BoundCandidates);
    }

    [Fact]
    public void BindBlocksCompleteCommunityCenterWithUnresolvedRouteCommitment()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_community_center"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("cc_joja_route_commitment_unavailable", result.BlockReasons);
        Assert.False(result.Audit.CcJojaRouteCommitmentResolved);
    }

    [Fact]
    public void BindBlocksCompleteJojaDevelopmentWithUnresolvedRouteCommitment()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_joja_development"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("cc_joja_route_commitment_unavailable", result.BlockReasons);
        Assert.False(result.Audit.CcJojaRouteCommitmentResolved);
    }

    [Fact]
    public void BindBlocksMarriageAndHouseUpgradeAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "marriage_and_house_upgrade"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("planned contract gap"));
    }

    [Fact]
    public void BindEarnPetLoveNeedsPetCareCandidates()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_pet_love"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindEarnPetLoveAcceptsExactPositivePetCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "pet-daily-interaction:8c4b11d3-3660-4f5c-a4dc-f65bf8d6395a",
            OptionId = "farm.care_for_pets",
            Kind = "pet_daily_interaction",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("target_runtime_identity", "8c4b11d3-3660-4f5c-a4dc-f65bf8d6395a"),
                Parameter("pet_love_progress_delta", "12")
            }
        };

        var result = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_pet_love",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Single(result.BoundCandidates);
        Assert.Equal(candidate.CandidateId, result.BoundCandidates[0].CandidateId);
    }

    [Fact]
    public void BindEarnPetLoveRejectsBowlCandidateWithoutExactDelayedSettlement()
    {
        var snapshot = GrandpaSnapshot();
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "fill-pet-bowl:Farm:54,8",
            OptionId = "farm.care_for_pets",
            Kind = "fill_pet_bowl",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("target_runtime_identity", "8c4b11d3-3660-4f5c-a4dc-f65bf8d6395a"),
                Parameter("pet_love_progress_delta", "6")
            }
        };

        var result = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_pet_love",
            RankedCandidates = new[] { candidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, reason => reason.Contains("pet_love_delayed_settlement_missing", StringComparison.Ordinal));
    }

    [Fact]
    public void BindEarnMoneyNeedsSellOrShipCandidates()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = Array.Empty<PolicyEventCandidatePrediction>()
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindEarnMoneyBindsSellShipCandidatesWithProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var sellCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:Tulip:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Rank = 1,
            Score = 0.42,
            Available = true,
            ItemId = "Tulip",
            QualifiedItemId = "(O)591",
            Quantity = 1,
            TotalValue = 30,
            SlotIndex = 0,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
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
            RankedCandidates = new[] { sellCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Single(result.BoundCandidates);
        var bound = result.BoundCandidates[0];
        Assert.Equal("sell:Tulip:1:0", bound.CandidateId);
        Assert.Equal(0.42, bound.Score);
        Assert.Equal(1, bound.Rank);
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value == "earn_money");
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_source_state_hash" && p.Value == snapshot.StateHash);
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_binding_rule_id" && p.Value == "grandpa.direct.earn_money");
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_related_factor_ids");
        Assert.Equal("sell_shop_item", bound.Kind);
        Assert.Equal("(O)591", bound.QualifiedItemId);
        Assert.Equal(0, bound.SlotIndex);
        Assert.Equal("ready", result.BindingCoverageStatus);
    }

    [Fact]
    public void BindEarnMoneyDoesNotClaimGrandpaThresholdReached()
    {
        var snapshot = GrandpaSnapshot();
        var sellCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Available = true,
            TotalValue = 30,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { sellCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.DoesNotContain(bound.Parameters, p =>
            p.Name.Contains("threshold") || p.Name.Contains("grandpa_score"));
        Assert.DoesNotContain(bound.Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value.Contains("complete"));
    }

    [Fact]
    public void BindEarnMoneyRejectsNonSellKinds()
    {
        var snapshot = GrandpaSnapshot();
        var nonSellCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "water:crop:1:0",
            OptionId = "farm.maintain_crops",
            Kind = "water_crop_tile",
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
            RankedCandidates = new[] { nonSellCandidate }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains("no_current_permitted_candidate", result.BlockReasons);
    }

    [Fact]
    public void BindRaiseFriendshipsBindsSocialCandidatesWithProvenance()
    {
        var snapshot = GrandpaSnapshot();
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
                Parameter("npc_location", "Farm")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "raise_friendships",
            RankedCandidates = new[] { socialCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Single(result.BoundCandidates);
        var bound = result.BoundCandidates[0];
        Assert.Equal("social_talk:Abigail:Farm", bound.CandidateId);
        Assert.Equal(0.35, bound.Score);
        Assert.Equal(1, bound.Rank);
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value == "raise_friendships");
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_binding_rule_id" && p.Value == "grandpa.direct.raise_friendships");
    }

    [Fact]
    public void BindRaiseFriendshipsDoesNotPromiseFriendshipPoints()
    {
        var snapshot = GrandpaSnapshot();
        var socialCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "social_talk:NPC:Farm",
            OptionId = "social.talk_npc",
            Kind = "social_talk_current",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("npc_name", "Abigail")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "raise_friendships",
            RankedCandidates = new[] { socialCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.DoesNotContain(bound.Parameters, p =>
            p.Name.Contains("friendship_points") || p.Name.Contains("friendship_delta"));
    }

    [Fact]
    public void BindCompleteMasterAnglerBindsCatchFishCandidatesWithProvenance()
    {
        var snapshot = GrandpaSnapshot();
        var fishCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "catch_fish:Mountain:LargemouthBass:spring:600-900",
            OptionId = "fishing.catch_fish",
            Kind = "catch_fish",
            Rank = 1,
            Score = 0.28,
            Available = true,
            LocationId = "Mountain",
            TileX = 15,
            TileY = 38,
            EstimatedTicks = 300,
            EnergyCost = 8,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("rule_key", "mountain_largemouth_spring"),
                Parameter("outcome_distribution_complete", "true")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_master_angler",
            RankedCandidates = new[] { fishCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Single(result.BoundCandidates);
        var bound = result.BoundCandidates[0];
        Assert.Equal("catch_fish:Mountain:LargemouthBass:spring:600-900", bound.CandidateId);
        Assert.Equal(0.28, bound.Score);
        Assert.Equal(1, bound.Rank);
        Assert.Contains(bound.Parameters, p =>
            p.Name == "grandpa_direction_id" && p.Value == "complete_master_angler");
    }

    [Fact]
    public void BindCompleteMasterAnglerDoesNotPromiseSpecificCatchOrAchievement()
    {
        var snapshot = GrandpaSnapshot();
        var fishCandidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "catch_fish:Mountain:Fish:spring",
            OptionId = "fishing.catch_fish",
            Kind = "catch_fish",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("rule_key", "mountain_spring")
            }
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_master_angler",
            RankedCandidates = new[] { fishCandidate }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];
        Assert.DoesNotContain(bound.Parameters, p =>
            p.Name.Contains("achievement") || p.Name.Contains("master_angler_complete"));
    }

    private static PolicyEventCandidatePrediction MuseumDonationCandidate(
        string candidateId,
        params SmallModelActionParameter[] parameters)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidateId,
            OptionId = "museum.donate_items",
            Kind = "donate_museum_item",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = parameters
        };
    }

}
