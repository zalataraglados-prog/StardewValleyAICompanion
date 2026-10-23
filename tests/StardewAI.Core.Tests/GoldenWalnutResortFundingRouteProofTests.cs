using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutResortFundingRouteProofTests
{
    [Fact]
    public void PrefersExactDeterministicGourmandCapacityWithoutPretendingItIsImplemented()
    {
        var proof = GoldenWalnutResortFundingRouteProofBuilder.Build();

        Assert.Equal("golden_walnut_resort_funding_routes.v1", proof.SchemaVersion);
        Assert.Equal(15, proof.RequiredWalnutQuantity);
        Assert.Equal("deterministic_gourmand_requests", proof.PreferredRouteId);
        Assert.False(proof.RouteSelectionComplete);
        Assert.False(proof.TrainingAdmissionEligible);

        var stochastic = proof.Alternatives.Single(route =>
            route.RouteId == "existing_stochastic_three_family_route");
        Assert.Equal(15, stochastic.NativeWalnutQuantity);
        Assert.False(stochastic.Deterministic);
        Assert.True(stochastic.ActionBindingsComplete);
        Assert.False(stochastic.SupplyScheduleComplete);
        Assert.Equal(
            new[] { "IslandFarming", "IslandFishing", "MusselStone" },
            stochastic.SourceKeys);

        var gourmand = proof.Alternatives.Single(route =>
            route.RouteId == "deterministic_gourmand_requests");
        Assert.Equal(15, gourmand.NativeWalnutQuantity);
        Assert.True(gourmand.Deterministic);
        Assert.True(gourmand.ActionBindingsComplete);
        Assert.False(gourmand.SupplyScheduleComplete);
        Assert.Contains("island_gourmand_progress.v1", gourmand.BoundEvidenceIds);
        Assert.Contains("island.complete_gourmand_request", gourmand.BoundEvidenceIds);
        Assert.Contains("executor.advance_gourmand_request", gourmand.BoundEvidenceIds);
        Assert.Contains("EVD-341", gourmand.BoundEvidenceIds);
        Assert.DoesNotContain(
            "island_gourmand_transparent_request_projection",
            gourmand.MissingEvidenceIds);
        Assert.DoesNotContain(
            "island_gourmand_native_interaction_and_reward_receipt",
            gourmand.MissingEvidenceIds);
        Assert.Equal(
            new[] { "IslandGourmand1", "IslandGourmand2", "IslandGourmand3" },
            gourmand.SourceKeys);
        Assert.Equal(new[] { "island.complete_gourmand_request" }, gourmand.AcquisitionOptionIds);
    }

    [Fact]
    public void RejectsMissingGourmandSource()
    {
        var tampered = GoldenWalnutNativeSourceCatalog.Sources
            .Where(source => source.SourceKey != "IslandGourmand2")
            .ToArray();

        var error = Assert.Throws<InvalidOperationException>(() =>
            GoldenWalnutResortFundingRouteProofBuilder.Build(tampered));

        Assert.Contains("denominator drifted", error.Message, StringComparison.Ordinal);
    }
}
