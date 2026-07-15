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

public sealed class GrandpaDirectionDailyCandidateBindingTests
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
    public void OnlyThreeDirectionsHaveDirectBindingEnabled()
    {
        var directDirections = GrandpaDirectionCatalog.Entries
            .Where(e => e.DirectBindingEnabled)
            .Select(e => e.DirectionId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(3, directDirections.Length);
        Assert.Contains("earn_money", directDirections);
        Assert.Contains("raise_friendships", directDirections);
        Assert.Contains("complete_master_angler", directDirections);
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
    public void BindBlocksUnsupportedDirectionAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("direct binding is disabled"));
        Assert.Equal("blocked", result.BindingCoverageStatus);
        Assert.Empty(result.MissingTransparentFields);
        Assert.NotEmpty(result.CoveredTransparentFields);
        Assert.NotEmpty(result.MissingCapabilities);
    }

    [Fact]
    public void BindBlocksRaiseSkillLevelsAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "raise_skill_levels"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("planned contract gap"));
    }

    [Fact]
    public void BindBlocksObtainSkullKeyAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "obtain_skull_key"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("planned contract gap"));
    }

    [Fact]
    public void BindBlocksCompleteMuseumCollectionAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_museum_collection"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("planned contract gap"));
    }

    [Fact]
    public void BindBlocksObtainRustyKeyAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "obtain_rusty_key"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("planned contract gap"));
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
    public void BindBlocksEarnPetLoveAsPlannedContractGap()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "earn_pet_love"
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Contains(result.BlockReasons, r => r.Contains("planned contract gap"));
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
    public void BindNineNonDirectDirectionsAllReturnBlockedWithPlannedRequirements()
    {
        var blockedDirections = new[]
        {
            "complete_full_shipment",
            "raise_skill_levels",
            "obtain_skull_key",
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
            if (directionId == "complete_full_shipment")
            {
                Assert.NotEmpty(result.CoveredTransparentFields);
            }
            else
            {
                Assert.NotEmpty(result.MissingTransparentFields);
            }
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
    }
}
