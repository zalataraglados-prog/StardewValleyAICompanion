using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class StochasticRetryPolicyTests
{
    [Theory]
    [InlineData(1, 0.2d, 14)]
    [InlineData(2, 0.5d, 8)]
    [InlineData(3, 1d, 3)]
    public void FindsSmallestIndependentBinomialBudget(
        int requiredSuccesses,
        double probability,
        int expectedAttempts)
    {
        Assert.Equal(
            expectedAttempts,
            StochasticRetryPolicy.RequiredIndependentAttemptCount(
                requiredSuccesses,
                probability));
    }

    [Fact]
    public void ZeroProbabilityHasNoFiniteBudget()
    {
        Assert.Null(StochasticRetryPolicy.RequiredIndependentAttemptCount(1, 0d));
    }

    [Fact]
    public void CertaintyAndOversizedDemandHaveNoFinitePolicyBudget()
    {
        Assert.Null(
            StochasticRetryPolicy.RequiredIndependentAttemptCount(
                1,
                0.5d,
                1d));
        Assert.Null(
            StochasticRetryPolicy.RequiredIndependentAttemptCount(
                StochasticRetryPolicy.MaximumIndependentAttemptCount + 1,
                1d));
    }

    [Theory]
    [InlineData(0, 0.5d, 0.95d)]
    [InlineData(1, -0.1d, 0.95d)]
    [InlineData(1, 0.5d, 0d)]
    public void RejectsInvalidProbabilityContracts(
        int requiredSuccesses,
        double probability,
        double target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StochasticRetryPolicy.RequiredIndependentAttemptCount(
                requiredSuccesses,
                probability,
                target));
    }
}
