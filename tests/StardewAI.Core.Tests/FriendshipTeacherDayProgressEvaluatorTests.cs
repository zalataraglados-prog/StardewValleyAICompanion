using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FriendshipTeacherDayProgressEvaluatorTests
{
    private readonly FriendshipTeacherDayProgressEvaluator evaluator = new();

    [Fact]
    public void NetDayStartGainResetsNoProgressCounter()
    {
        var result = evaluator.Evaluate(1000, 1012, 2, 0);

        Assert.True(result.MadeNetProgress);
        Assert.Equal(12, result.NetPointDelta);
        Assert.Equal(0, result.ConsecutiveNoProgressDays);
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1000, 980)]
    public void NoNetDayStartGainIncrementsCounter(int before, int after)
    {
        var result = evaluator.Evaluate(before, after, 2, 0);

        Assert.False(result.MadeNetProgress);
        Assert.Equal(3, result.ConsecutiveNoProgressDays);
    }

    [Fact]
    public void NegativePriorCounterIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.Evaluate(1000, 1020, -1, 0));
    }

    [Fact]
    public void VerifiedGoalDirectedWorkResetsCounterDespitePopulationDecay()
    {
        var result = evaluator.Evaluate(8389, 8377, 2, 2);

        Assert.False(result.MadeNetProgress);
        Assert.True(result.MadeGoalDirectedProgress);
        Assert.True(result.MadeProgress);
        Assert.Equal(2, result.VerifiedGoalDirectedObjectiveCount);
        Assert.Equal(-12, result.NetPointDelta);
        Assert.Equal(0, result.ConsecutiveNoProgressDays);
    }

    [Fact]
    public void NegativeVerifiedObjectiveCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.Evaluate(1000, 1020, 0, -1));
    }
}
