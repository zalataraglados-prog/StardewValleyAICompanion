using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class StageOneInventoryCapacityRecoveryTests
{
    [Fact]
    public void SelectsExactLatestCatchShippingChainWithoutLearnerSignals()
    {
        var ranking = Ranking(
            ShippingCandidate(
                "ship:Farm:slot-8:fiber",
                "(O)771",
                slotIndex: 8,
                estimatedTicks: 200),
            ShippingCandidate(
                "ship:Farm:slot-11:seaweed",
                "(O)152",
                slotIndex: 11,
                estimatedTicks: 400));

        var decision = StageOneInventoryCapacityRecovery.TrySelect(
            ranking,
            Snapshot("(O)152"));

        Assert.NotNull(decision);
        Assert.Equal("(O)152", decision!.CaughtQualifiedItemId);
        Assert.Equal("(O)152", decision.ReleasedQualifiedItemId);
        Assert.Equal(
            "exact_latest_catch_singleton",
            decision.SelectionReason);
        Assert.Equal(
            "ship:Farm:slot-11:seaweed",
            decision.Candidate.CandidateId);
        Assert.Equal(1, decision.Candidate.Rank);
        Assert.Equal(0, decision.Candidate.Score);
        Assert.Null(decision.Candidate.ModelScore);
        Assert.Equal(
            "stage_one_inventory_capacity_recovery",
            decision.Candidate.PolicyModelSource);
        Assert.Equal(0, decision.Candidate.ExpectedReward);
    }

    [Fact]
    public void FailsClosedWithoutAnExactLatestCatchShippingChain()
    {
        var ranking = Ranking(ShippingCandidate(
            "ship:Farm:slot-8:fiber",
            "(O)771",
            slotIndex: 8,
            estimatedTicks: 200));

        Assert.Null(StageOneInventoryCapacityRecovery.TrySelect(
            ranking,
            Snapshot("(O)152")));
        Assert.Null(StageOneInventoryCapacityRecovery.TrySelect(
            ranking,
            Snapshot(string.Empty)));

        ranking.Availability.Options[0].EventCandidates[0].BlockReasons =
            Array.Empty<string>();
        Assert.Null(StageOneInventoryCapacityRecovery.TrySelect(
            ranking,
            Snapshot("(O)771")));
    }

    [Fact]
    public void CropHarvestRecoverySelectsOneObjectiveSafeSingleton()
    {
        var ranking = Ranking(
            ShippingCandidate(
                "ship:Farm:slot-6:cauliflower",
                "(O)190",
                slotIndex: 6,
                estimatedTicks: 100,
                availableStack: 6,
                fullShipmentContributes: true),
            ShippingCandidate(
                "ship:Farm:slot-7:clay",
                "(O)330",
                slotIndex: 7,
                estimatedTicks: 200,
                availableStack: 1,
                fullShipmentContributes: true),
            ShippingCandidate(
                "ship:Farm:slot-11:rare",
                "(O)999",
                slotIndex: 11,
                estimatedTicks: 50,
                availableStack: 1,
                unitPrice: 500));

        var decision = StageOneInventoryCapacityRecovery.TrySelect(
            ranking,
            Snapshot(string.Empty),
            allowObjectiveSafeSingleton: true,
            requireFishingAttemptBlocked: false);

        Assert.NotNull(decision);
        Assert.Equal(
            "ship:Farm:slot-7:clay",
            decision!.Candidate.CandidateId);
        Assert.Equal("(O)330", decision.ReleasedQualifiedItemId);
        Assert.Equal(string.Empty, decision.CaughtQualifiedItemId);
        Assert.Equal(
            "objective_safe_singleton",
            decision.SelectionReason);
        Assert.Equal(
            StageOneInventoryCapacityRecovery.CropHarvestTriggerReason,
            decision.TriggerReason);
    }

    [Fact]
    public void FishingRecoverySelectsObjectiveSafeSingletonWithoutLatestCatch()
    {
        var ranking = RankingWithFishingBlock(
            "fishing_inventory_full_requires_storage_transfer",
            ShippingCandidate(
                "ship:Farm:slot-7:clay",
                "(O)330",
                slotIndex: 7,
                estimatedTicks: 200,
                availableStack: 1,
                fullShipmentContributes: true));

        var decision = StageOneInventoryCapacityRecovery.TrySelect(
            ranking,
            Snapshot(string.Empty),
            allowObjectiveSafeSingleton: true);

        Assert.NotNull(decision);
        Assert.Equal(
            "ship:Farm:slot-7:clay",
            decision!.Candidate.CandidateId);
        Assert.Equal(
            "objective_safe_singleton",
            decision.SelectionReason);
        Assert.Equal(
            StageOneInventoryCapacityRecovery.PreAttemptTriggerReason,
            decision.TriggerReason);
    }

    [Fact]
    public void RecoveryContextRequiresBothFullInventoryAndLatestCatch()
    {
        Assert.True(StageOneInventoryCapacityRecovery.HasRecoveryContext(
            Snapshot("(O)152")));
        Assert.False(StageOneInventoryCapacityRecovery.HasRecoveryContext(
            Snapshot(string.Empty)));
        Assert.False(StageOneInventoryCapacityRecovery.HasRecoveryContext(
            Snapshot("(O)152", hasEmptySlot: true)));
    }

    private static AvailabilityAwarePolicyPredictionEnvelope Ranking(
        params PolicyEventCandidatePrediction[] shippingCandidates) =>
        RankingWithFishingBlock(
            "master_angler_no_exact_runtime_source_attempt",
            shippingCandidates);

    private static AvailabilityAwarePolicyPredictionEnvelope
        RankingWithFishingBlock(
            string fishingBlockReason,
            params PolicyEventCandidatePrediction[] shippingCandidates) =>
        new()
        {
            Availability = new OptionAvailabilityEnvelope
            {
                Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "fishing.catch_fish",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "fishing:catch:blocked",
                            Available = false,
                            BlockReasons = new[]
                            {
                                fishingBlockReason
                            }
                        }
                    }
                }
            }
            },
            RankedEventCandidates = shippingCandidates
        };

    private static PolicyEventCandidatePrediction ShippingCandidate(
        string candidateId,
        string qualifiedItemId,
        int slotIndex,
        int estimatedTicks,
        int availableStack = 1,
        bool fullShipmentContributes = false,
        int unitPrice = 20) => new()
        {
            CandidateId = candidateId,
            OptionId = "economy.ship_items",
            Kind = "route_connector_tile",
            Available = true,
            QualifiedItemId = qualifiedItemId,
            SlotIndex = slotIndex,
            Quantity = 1,
            UnitPrice = unitPrice,
            TotalValue = unitPrice,
            AvailableStack = availableStack,
            FullShipmentContributes = fullShipmentContributes,
            EstimatedTicks = estimatedTicks,
            Rank = 42,
            Score = 12.5,
            ModelScore = 3.25,
            PolicyModelSource = "learner",
            ExpectedReward = 7.5
        };

    private static JsonObject Snapshot(
        string caughtQualifiedItemId,
        bool hasEmptySlot = false) =>
        new()
        {
            ["state"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["inventory_capacity"] = new JsonObject
                    {
                        ["value"] = new JsonObject
                        {
                            ["max_items"] = 12,
                            ["occupied_item_stacks"] =
                                hasEmptySlot ? 11 : 12,
                            ["has_empty_slot"] = hasEmptySlot
                        }
                    }
                },
                ["fishing"] = new JsonObject
                {
                    ["active_cast_state"] = new JsonObject
                    {
                        ["value"] = new JsonObject
                        {
                            ["caught_qualified_item_id"] =
                                caughtQualifiedItemId
                        }
                    }
                }
            }
        };
}
