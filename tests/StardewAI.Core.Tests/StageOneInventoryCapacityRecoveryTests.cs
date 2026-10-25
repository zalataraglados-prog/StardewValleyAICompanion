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

    private static AvailabilityAwarePolicyPredictionEnvelope Ranking(
        params PolicyEventCandidatePrediction[] shippingCandidates) => new()
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
                                "master_angler_no_exact_runtime_source_attempt"
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
        int estimatedTicks) => new()
        {
            CandidateId = candidateId,
            OptionId = "economy.ship_items",
            Kind = "route_connector_tile",
            Available = true,
            QualifiedItemId = qualifiedItemId,
            SlotIndex = slotIndex,
            Quantity = 1,
            UnitPrice = 20,
            TotalValue = 20,
            EstimatedTicks = estimatedTicks,
            Rank = 42,
            Score = 12.5,
            ModelScore = 3.25,
            PolicyModelSource = "learner",
            ExpectedReward = 7.5
        };

    private static JsonObject Snapshot(string caughtQualifiedItemId) =>
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
                            ["occupied_item_stacks"] = 12,
                            ["has_empty_slot"] = false
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
