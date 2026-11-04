using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class EventCandidateTimelineSchedulerTests
{
    [Theory]
    [InlineData(1650, 600, false)]
    [InlineData(1645, 630, true)]
    [InlineData(1645, 631, false)]
    [InlineData(1659, 42, true)]
    [InlineData(1659, 43, false)]
    [InlineData(1700, 0, true)]
    [InlineData(1650, -1, false)]
    public void ClosingWindowUsesTheSharedRuntimeTickConversion(int time, int ticks, bool fits)
    {
        var candidate = Candidate(ticks);
        var result = new EventCandidateTimelineScheduler().Schedule(new[] { candidate }, time);

        Assert.Equal(fits, result.Length == 1);
        if (!fits) Assert.Contains("candidate_would_finish_after_close", candidate.TimelineReasons);
    }

    [Fact]
    public void DeferredWorkChecksDurationAfterOpeningWithoutChangingLegacyWaitUnits()
    {
        var candidate = Candidate(631);
        candidate.AllowedNow = false;
        candidate.AllowedToday = true;
        candidate.NextOpenTime = 1645;
        candidate.WaitCost = 45 * 60;

        Assert.Empty(new EventCandidateTimelineScheduler().Schedule(new[] { candidate }, 1600));
        Assert.Contains("candidate_would_finish_after_close", candidate.TimelineReasons);
    }

    [Fact]
    public void LegalDeferredWorkRetainsTheOriginalBoundedWaitContract()
    {
        var candidate = Candidate(630);
        candidate.AllowedNow = false;
        candidate.AllowedToday = true;
        candidate.NextOpenTime = 1645;
        candidate.WaitCost = 45 * 60;

        var scheduled = Assert.Single(new EventCandidateTimelineScheduler().Schedule(new[] { candidate }, 1600));
        Assert.Equal("deferred", scheduled.TimelineStatus);
        Assert.Equal(1645, scheduled.ScheduledStartTime);
        Assert.Equal(2700, scheduled.ScheduledWaitCost);
    }

    private static PolicyEventCandidatePrediction Candidate(int ticks) => new()
    {
        CandidateId = "window-boundary", AllowedNow = true, ClosesAt = 1700, EstimatedTicks = ticks
    };
}
