using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutUnlockScheduleProofTests
{
    [Fact]
    public void ProvesFourStageCapacityWithoutPretendingDeadlineIsClosed()
    {
        var proof = GoldenWalnutUnlockScheduleProofBuilder.Build();

        Assert.Equal("golden_walnut_unlock_schedule_proof.v1", proof.SchemaVersion);
        Assert.Equal(51, proof.BoundSourceCapacity);
        Assert.Equal(51, proof.RequiredCumulativeSpend);
        Assert.True(proof.PhaseCapacityComplete);
        Assert.True(proof.IndependentRetryBudgetComplete);
        Assert.False(proof.YearThreeDeadlineCompositionComplete);
        Assert.False(proof.TrainingAdmissionEligible);
        Assert.Equal(new[] { 1, 10, 20, 20 }, proof.Milestones.Select(row => row.WalnutCost));
        Assert.Equal(new[] { 7, 25, 51, 51 }, proof.Milestones.Select(row => row.CumulativeCapacity));
        Assert.All(proof.Milestones, row => Assert.True(row.CapacitySufficient));
        Assert.All(proof.Milestones.Take(3), row => Assert.True(row.DeterministicCapacitySufficient));
        Assert.False(proof.Milestones[3].DeterministicCapacitySufficient);
    }

    [Fact]
    public void FinalResortPaymentRequiresExactlyTheThreeBoundRandomFamilies()
    {
        var proof = GoldenWalnutUnlockScheduleProofBuilder.Build();
        var resort = proof.Milestones.Single(row => row.MilestoneId == "island_resort");

        Assert.Equal(36, resort.DeterministicCapacity);
        Assert.Equal(15, resort.StochasticCapacity);
        Assert.Equal(5, resort.DeterministicWalletBeforePayment);
        Assert.Equal(20, resort.CapacityWalletBeforePayment);
        Assert.Equal(
            new[] { "IslandFarming", "IslandFishing", "MusselStone" },
            proof.RetryBudgets.Select(row => row.SourceKey));
        Assert.Equal(
            new[] { 214, 69, 105 },
            proof.RetryBudgets.Select(row => row.RequiredIndependentAttemptCount));
        Assert.All(proof.RetryBudgets, row =>
        {
            Assert.Equal(5, row.RequiredSuccessCount);
            Assert.True(row.RequiredIndependentAttemptCount > row.RequiredSuccessCount);
        });
        Assert.True(
            proof.ProvenCombinedRetrySuccessProbabilityLowerBound >=
            proof.TargetCombinedRetrySuccessProbability);
    }

    [Fact]
    public void RejectsNativeDenominatorTampering()
    {
        var tampered = GoldenWalnutNativeSourceCatalog.Sources.Skip(1).ToArray();

        var error = Assert.Throws<InvalidOperationException>(() =>
            GoldenWalnutUnlockScheduleProofBuilder.Build(tampered));

        Assert.Contains("denominator drifted", error.Message, StringComparison.Ordinal);
    }
}
