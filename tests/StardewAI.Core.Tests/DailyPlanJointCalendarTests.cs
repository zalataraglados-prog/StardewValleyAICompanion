using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DailyPlanJointCalendarTests
{
    [Theory]
    [InlineData(10, 0, 600)]
    [InlineData(5, 210, 600)]
    [InlineData(5, 210, 0)]
    public void JointDayConsumesOnlyTheRemainingOpeningWaitAfterEarlierWork(int priorMinutes, int expectedWaitTicks, int originalWaitCost)
    {
        var window = Work("window", 2, 5, closes: 920);
        window.AllowedNow = false; window.NextOpenTime = 910; window.WaitCost = originalWaitCost;
        var scheduled = new EventCandidateTimelineScheduler().Schedule([Work("prior", 1, priorMinutes), window], 900);
        var joint = new DailyPlanCompiler().CompileWithJointCalendar(scheduled, "clock", 900);
        Assert.Equal(originalWaitCost, window.ScheduledWaitCost);
        Assert.Equal("deferred", window.TimelineStatus);
        var waits = joint.Steps.Where(step => step.Kind == "wait_ticks").ToArray();
        if (expectedWaitTicks == 0)
        {
            Assert.Empty(waits);
            var work = joint.Steps[^1]; Assert.Equal("water_crop", work.Kind);
            Assert.Contains(work.Parameters, p => p.Name == "budget.candidate_start_time" && p.Value == "910");
            Assert.Contains(work.Parameters, p => p.Name == "budget.candidate_end_time" && p.Value == "915");
        }
        else
        {
            var wait = Assert.Single(waits); Assert.Equal(expectedWaitTicks, wait.WaitTicks);
            Assert.Contains(wait.ExpectedEffects, effect => effect == "fresh_snapshot_replan_required=true");
            Assert.Contains(wait.Parameters, p => p.Name == "budget.candidate_end_time" && p.Value == "910");
        }
        Assert.Equal(2, joint.CandidateAudit.Count(row => row.Decision == "accepted"));
        var legacyWaits = new DailyPlanCompiler().Compile(scheduled, "legacy").Steps.Where(step => step.Kind == "wait_ticks");
        if (originalWaitCost > 0) Assert.Equal(originalWaitCost, Assert.Single(legacyWaits).WaitTicks);
        else Assert.Empty(legacyWaits);
    }

    [Fact]
    public void JointDayRejectsMalformedDeferredOpeningBeforeSchedulingAWait()
    {
        var window = Work("window", 1, 1, closes: 1700);
        window.TimelineStatus = "deferred"; window.NextOpenTime = 999; window.ScheduledWaitCost = 600;
        var plan = new DailyPlanCompiler().CompileWithJointCalendar([window], "clock", 900);
        Assert.Contains("aggregate_calendar_window_invalid", Assert.Single(plan.CandidateAudit).Reasons);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void JointDayDoesNotTurnAnAlreadyMissedDeferredWindowIntoAnotherWaitBarrier()
    {
        var window = Work("window", 2, 5, closes: 920);
        window.AllowedNow = false; window.NextOpenTime = 910; window.WaitCost = 600;
        var scheduled = new EventCandidateTimelineScheduler().Schedule(
            [Work("prior", 1, 25), window], 900);
        var joint = new DailyPlanCompiler().CompileWithJointCalendar(scheduled, "clock", 900);
        Assert.Contains("aggregate_calendar_window_missed", joint.CandidateAudit[1].Reasons);
        Assert.DoesNotContain(joint.Steps, step => step.Kind == "wait_ticks");
    }

    [Fact]
    public void JointDayRejectsIndividuallyOpenCandidateThatFinishesAfterCloseFollowingOtherWork()
    {
        var candidates = new[] { Work("crop", 1, 10), Work("shop", 2, 5, closes: 910), Work("social", 3, 3, closes: 930) };
        var independentlyScheduled = new EventCandidateTimelineScheduler().Schedule(candidates, 900);
        Assert.Equal(3, independentlyScheduled.Length);
        var compiler = new DailyPlanCompiler();
        var legacy = compiler.Compile(candidates, "shared.day", availableMinutes: 60, energyBudget: 10);
        var joint = compiler.CompileWithJointCalendar(candidates, "shared.day", 900, availableMinutes: 60, energyBudget: 10);
        Assert.Equal(3, legacy.CandidateAudit.Count(row => row.Decision == "accepted"));
        Assert.DoesNotContain(legacy.Steps, step => step.SafetyConstraints.Contains("daily_plan_joint_calendar_checked"));
        Assert.Equal("aggregate_calendar_window_missed", Assert.Single(joint.CandidateAudit[1].Reasons));
        Assert.Equal(new[] { "crop", "social" }, joint.CandidateAudit.Where(row => row.Decision == "accepted").Select(row => row.CandidateId));
        Assert.Contains(joint.Steps[1].Parameters, p => p.Name == "budget.candidate_start_time" && p.Value == "910");
        Assert.Contains(joint.Steps[1].Parameters, p => p.Name == "budget.candidate_end_time" && p.Value == "913");
        Assert.Equal(8, joint.CandidateAudit[2].RemainingEnergyBefore);
    }

    [Theory]
    [InlineData(950, 20, 1010, true)]
    [InlineData(950, 20, 1009, false)]
    [InlineData(2355, 10, 2405, true)]
    [InlineData(2555, 6, null, false)]
    public void JointDayUsesSharedNativeClockAndPreservesExistingInclusiveFinishBoundary(int start, int minutes, int? closes, bool accepted)
    {
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(new[] { Work("calendar", 1, minutes, closes) }, "clock", start);
        Assert.Equal(accepted ? "accepted" : "skipped", Assert.Single(plan.CandidateAudit).Decision);
        if (accepted) Assert.Contains(Assert.Single(plan.Steps).SafetyConstraints, value => value == "daily_plan_joint_calendar_checked");
    }

    [Theory]
    [InlineData(899)]
    [InlineData(-1)]
    [InlineData(2700)]
    public void JointDayRejectsInvalidWindowWithoutConsumingOtherCandidatesBudget(int closes)
    {
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(
            new[] { Work("invalid", 1, 1, closes), Work("next", 2, 2) }, "clock", 900, maxCandidates: 1, availableMinutes: 2);
        Assert.Equal("skipped", plan.CandidateAudit[0].Decision);
        Assert.Contains("aggregate_calendar_window_invalid", plan.CandidateAudit[0].Reasons);
        Assert.Equal("accepted", plan.CandidateAudit[1].Decision);
    }

    [Theory]
    [InlineData(930, "aggregate_calendar_window_not_open")]
    [InlineData(999, "aggregate_calendar_window_invalid")]
    public void JointDayChecksTheCompiledStartAgainstOpeningEvidence(int opens, string reason)
    {
        var candidate = Work("window", 1, 2, 1700);
        candidate.EffectiveOpenTime = opens;
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(new[] { candidate }, "clock", 900);
        Assert.Equal(reason, Assert.Single(Assert.Single(plan.CandidateAudit).Reasons));
    }

    [Fact]
    public void JointDayPreservesAggregateEnergyAndTimeAndRollingFreshSourceBarrier()
    {
        var wait = Work("wait", 2, 5, 905);
        wait.TimelineStatus = "deferred"; wait.ScheduledWaitCost = 1200;
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(
            new[] { Work("work", 1, 10), wait, Work("later", 3, 1) }, "clock", 900, availableMinutes: 60, energyBudget: 10);
        Assert.Equal(new[] { "accepted", "accepted", "skipped" }, plan.CandidateAudit.Select(row => row.Decision));
        Assert.Contains("rolling_horizon_fresh_replan_barrier", plan.CandidateAudit[2].Reasons);
        Assert.Equal("wait_ticks", plan.Steps[^1].Kind);
        Assert.Equal(600, plan.Steps[^1].WaitTicks);
        Assert.Contains(plan.Steps[^1].ExpectedEffects, effect => effect == "fresh_snapshot_replan_required=true");

        var energy = new DailyPlanCompiler().CompileWithJointCalendar(
            new[] { Work("first", 1, 1), Work("second", 2, 1) }, "clock", 900, energyBudget: 2);
        Assert.Contains("aggregate_energy_budget_exceeded", energy.CandidateAudit[1].Reasons);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(999)]
    [InlineData(2601)]
    public void JointDayRequiresOneValidCurrentNativeClock(int clock) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DailyPlanCompiler().CompileWithJointCalendar([], "clock", clock));

    internal static PolicyEventCandidatePrediction Work(string id, int rank, int minutes, int? closes = null) => new()
    {
        CandidateId = id, Kind = "water_crop_tile", Rank = rank, TimelineStatus = "ready_now", Score = 100 - rank,
        LocationId = "Farm", TileX = rank, TileY = rank, EnergyCost = 2, AllowedNow = true, AllowedToday = true,
        EstimatedTicks = minutes * GameClockBudgetPolicy.RuntimeUpdatesPerSecond * GameClockBudgetPolicy.RealMillisecondsPerGameMinute / 1000,
        ClosesAt = closes
    };
}
