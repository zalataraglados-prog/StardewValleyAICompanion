using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.Core.Tests.DailyPlanJointCalendarTests;

namespace StardewAI.Core.Tests;

public sealed class DailyPlanPrimaryGoalTests
{
    [Fact]
    public void PrimaryGoalAndOptionalWorkUseOneCalendarWithoutDroppingTheChosenGoal()
    {
        var rows = new[] { Work("optional-early", 1, 5, 905), Work("primary", 2, 10), Work("optional-fit", 3, 3) };
        var compiler = new DailyPlanCompiler();
        Assert.Equal("optional-early", compiler.CompileWithJointCalendar(rows, "legacy", 900).CandidateAudit[0].CandidateId);
        var plan = compiler.CompileWithPrimaryGoal(rows, "source", 900, [], ["primary"], availableMinutes: 60, energyBudget: 10);

        Assert.Equal(["primary", "optional-fit"], Accepted(plan));
        Assert.Contains("aggregate_calendar_window_missed", plan.CandidateAudit.Single(row => row.CandidateId == "optional-early").Reasons);
        Assert.Equal(47, plan.CandidateAudit[^1].RemainingMinutesAfter);
        Assert.Equal(6, plan.CandidateAudit[^1].RemainingEnergyAfter);
        Assert.Contains(plan.Steps[^1].Parameters, row => row.Name == "budget.candidate_start_time" && row.Value == "910");
        Assert.Equal(["primary"], plan.PrimaryCandidateIds);
    }

    [Theory]
    [InlineData(10, 2, "aggregate_time_budget_exceeded")]
    [InlineData(60, 2, "aggregate_energy_budget_exceeded")]
    public void OptionalWorkCannotSpendThePrimaryTimeOrEnergyAgain(int minutes, int energy, string reason)
    {
        var plan = new DailyPlanCompiler().CompileWithPrimaryGoal(
            [Work("optional", 1, 3), Work("primary", 2, 10)], "source", 900, [], ["primary"],
            availableMinutes: minutes, energyBudget: energy);
        Assert.Equal(["primary"], Accepted(plan));
        Assert.Contains(reason, plan.CandidateAudit.Single(row => row.CandidateId == "optional").Reasons);
    }

    [Fact]
    public void BlockedPrimaryAlternativeDoesNotRejectAnotherValidAlternative()
    {
        var blocked = Work("blocked", 1, 1); blocked.TimelineStatus = "blocked";
        var plan = new DailyPlanCompiler().CompileWithPrimaryGoal(
            [blocked, Work("optional", 2, 1), Work("ready", 3, 1)], "source", 900, [], ["ready", "blocked"]);
        Assert.Equal(["ready", "optional"], Accepted(plan));
        Assert.Equal(["blocked", "ready"], plan.PrimaryCandidateIds);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("blocked")]
    [InlineData("budget")]
    [InlineData("unsupported")]
    public void OptionalWorkDoesNotReplaceAnUnscheduledPrimaryGoal(string fault)
    {
        var primary = Work("primary", 2, fault == "budget" ? 100 : 1);
        if (fault == "blocked") primary.TimelineStatus = "blocked";
        if (fault == "unsupported") primary.Kind = "not-supported";
        var plan = new DailyPlanCompiler().CompileWithPrimaryGoal(
            [Work("optional", 1, 1), primary], "source", 900, [], fault == "empty" ? [] : ["primary"], availableMinutes: 5);
        Assert.Empty(plan.Steps);
        Assert.Contains("primary_goal_work_not_scheduled", plan.CandidateAudit.Single(row => row.CandidateId == "optional").Reasons);
    }

    [Fact]
    public void OriginalRequiredWorkStillPrecedesPrimaryAndOptionalWork()
    {
        var plan = new DailyPlanCompiler().CompileWithPrimaryGoal(
            [Work("optional", 1, 1), Work("primary", 2, 1), Work("required", 3, 1)], "source", 900,
            ["required"], ["primary"], availableMinutes: 10, energyBudget: 10);
        Assert.Equal(["required", "primary", "optional"], Accepted(plan));
        Assert.Equal(7, plan.CandidateAudit[^1].RemainingMinutesAfter);
    }

    [Fact]
    public void PrimaryWaitKeepsTheOriginalFreshSourceBarrier()
    {
        var primary = Work("primary", 2, 1);
        primary.TimelineStatus = "deferred"; primary.NextOpenTime = 1000; primary.ScheduledWaitCost = 1200;
        var plan = new DailyPlanCompiler().CompileWithPrimaryGoal([Work("optional", 1, 1), primary], "source", 900, [], ["primary"]);
        Assert.Equal("wait_ticks", Assert.Single(plan.Steps).Kind);
        Assert.Contains("rolling_horizon_fresh_replan_barrier", plan.CandidateAudit.Single(row => row.CandidateId == "optional").Reasons);
    }

    [Fact]
    public void PrimaryAndOptionalPlantingShareTheOriginalSeedReservation()
    {
        var primary = Seed("primary", 2, 2);
        var optional = Seed("optional", 1, 1);
        var plan = new DailyPlanCompiler().CompileWithPrimaryGoal([optional, primary], "source", 900, [], ["primary"]);
        Assert.Equal(["primary"], Accepted(plan));
        Assert.Contains(plan.CandidateAudit.Single(row => row.CandidateId == "optional").Reasons,
            reason => reason.Contains("reserved", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("absent")]
    [InlineData("blank")]
    public void PrimaryBindingsMustIdentifyTheirMaterializedSource(string fault)
    {
        string[] ids = fault switch { "duplicate" => ["primary", "primary"], "absent" => ["absent"], _ => [""] };
        Assert.Throws<ArgumentException>(() => new DailyPlanCompiler().CompileWithPrimaryGoal([Work("primary", 1, 1)], "source", 900, [], ids));
    }

    [Fact]
    public void DefaultWireAndBothExistingPublicOverloadsRemainUnchanged()
    {
        Assert.False(JsonSerializer.SerializeToElement(new DailyPlanCompileRequest()).TryGetProperty("primary_candidate_ids", out _));
        Assert.False(JsonSerializer.SerializeToElement(new SmallModelPlanEnvelope()).TryGetProperty("primary_candidate_ids", out _));
        Assert.Equal([8, 9], typeof(DailyPlanCompiler).GetMethods().Where(method => method.Name == "CompileWithJointCalendar")
            .Select(method => method.GetParameters().Length).OrderBy(count => count));
    }

    private static string[] Accepted(SmallModelPlanEnvelope plan) =>
        plan.CandidateAudit.Where(row => row.Decision == "accepted").Select(row => row.CandidateId).ToArray();

    private static PolicyEventCandidatePrediction Seed(string id, int rank, int x) => new()
    {
        CandidateId = id, Kind = "plant_seed_tile", Rank = rank, TimelineStatus = "ready_now", Score = 100 - rank,
        LocationId = "Farm", TileX = x, TileY = 1, ItemId = "472", QualifiedItemId = "(O)472", SlotIndex = 0, Quantity = 1,
        ExpectedEffect = "current_location.planting_context[" + x + ",1].has_crop=true;player.seed_inventory[472].stack_decreases;seed_id=472;adjusted_grow_days=4;days_remaining_in_season=20;expected_first_harvest_value=35",
        EstimatedTicks = 60
    };
}
