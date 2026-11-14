using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DailyPlanRequiredWorkTests
{
    [Fact]
    public void RequiredWorkPrecedesOptionalRanksAndConsumesTheSameClockEnergyAndCountWithoutMutatingInputs()
    {
        var rows = new[] { Work("optional", 1, 2), Work("required", 2, 10) };
        var before = JsonSerializer.Serialize(rows);
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(rows, "day", 900, new[] { "required" },
            maxCandidates: 2, availableMinutes: 20, energyBudget: 6);
        Assert.Equal(new[] { "required", "optional" }, plan.CandidateAudit.Select(row => row.CandidateId));
        Assert.All(plan.CandidateAudit, row => Assert.Equal("accepted", row.Decision));
        Assert.Equal(10, plan.CandidateAudit[1].RemainingMinutesBefore);
        Assert.Equal(4, plan.CandidateAudit[1].RemainingEnergyBefore);
        Assert.Contains(plan.Steps[1].Parameters, row => row.Name == "budget.candidate_start_time" && row.Value == "910");
        Assert.Equal(before, JsonSerializer.Serialize(rows));
    }

    [Theory]
    [InlineData("time", "aggregate_time_budget_exceeded")]
    [InlineData("energy", "aggregate_energy_budget_exceeded")]
    [InlineData("count", "max_candidates_reached")]
    public void OptionalWorkDoesNotBorrowCapacityFromRequiredWorkThatCouldNotBeScheduled(string limit, string reason)
    {
        var rows = new[] { Work("optional", 1, 1), Work("required-a", 2, 10), Work("required-b", 3, 10) };
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(rows, "day", 900, new[] { "required-a", "required-b" },
            maxCandidates: limit == "count" ? 1 : 4, availableMinutes: limit == "time" ? 11 : 60,
            energyBudget: limit == "energy" ? 3 : 20);
        Assert.Equal("accepted", plan.CandidateAudit[0].Decision);
        Assert.Contains(reason, plan.CandidateAudit[1].Reasons);
        Assert.Contains("required_work_not_fully_scheduled", plan.CandidateAudit[2].Reasons);
        Assert.Single(plan.Steps);
    }

    [Fact]
    public void RequiredShopPurchaseRetainsOriginalOpenBeforeBuySequence()
    {
        var open = new PolicyEventCandidatePrediction
        {
            CandidateId = "open", Kind = "interact_endpoint", Rank = 3, ShopId = "SeedShop", TimelineStatus = "ready_now",
            LocationId = "Town", TileX = 11, TileY = 10, ExpectedEffect = "move_to_adjacent=10,10;preview_interact=OpenShop", EstimatedTicks = 90
        };
        var buy = new PolicyEventCandidatePrediction
        {
            CandidateId = "buy", Kind = "buy_shop_item", Rank = 4, ShopId = "SeedShop", TimelineStatus = "ready_now",
            ItemId = "472", QualifiedItemId = "(O)472", Quantity = 1, UnitPrice = 20, EstimatedTicks = 60
        };
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(new[] { Work("optional", 1, 1), buy, open },
            "day", 900, new[] { "buy" });
        Assert.Equal(new[] { "open", "buy", "optional" }, plan.CandidateAudit.Select(row => row.CandidateId));
        Assert.All(plan.CandidateAudit, row => Assert.Equal("accepted", row.Decision));
    }

    [Fact]
    public void RequiredRollingWaitPreservesFreshReplanBarrierInsteadOfClaimingTheBusinessActionScheduled()
    {
        var wait = Work("required", 2, 1);
        wait.TimelineStatus = "deferred"; wait.ScheduledWaitCost = 600;
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(new[] { Work("optional", 1, 1), wait }, "day", 900, new[] { "required" });
        Assert.Equal("wait_ticks", Assert.Single(plan.Steps).Kind);
        Assert.Contains("rolling_horizon_fresh_replan_barrier", plan.CandidateAudit[1].Reasons);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate_id")]
    [InlineData("duplicate_row")]
    [InlineData("blocked")]
    public void RejectsAmbiguousOrUnavailableRequiredBindings(string fault)
    {
        var candidate = Work("required", 1, 1);
        if (fault == "blocked") candidate.TimelineStatus = "blocked";
        Assert.Throws<ArgumentException>(() => new DailyPlanCompiler().CompileWithJointCalendar(
            fault == "duplicate_row" ? new[] { candidate, candidate } : new[] { candidate }, "day", 900,
            fault == "missing" ? new[] { "absent" } : fault == "duplicate_id" ? new[] { "required", "required" } : new[] { "required" }));
    }

    [Fact]
    public void EmptyRequiredSetPreservesTheJointCalendarPlanAndAuditExactly()
    {
        var compiler = new DailyPlanCompiler();
        var rows = new[] { Work("one", 1, 1), Work("two", 2, 1) };
        var original = compiler.CompileWithJointCalendar(rows, "day", 900);
        var current = compiler.CompileWithJointCalendar(rows, "day", 900, Array.Empty<string>());
        Assert.Equal(JsonSerializer.Serialize(original.Steps), JsonSerializer.Serialize(current.Steps));
        Assert.Equal(JsonSerializer.Serialize(original.CandidateAudit), JsonSerializer.Serialize(current.CandidateAudit));
    }

    private static PolicyEventCandidatePrediction Work(string id, int rank, int minutes) => new()
    {
        CandidateId = id, Kind = "water_crop_tile", Rank = rank, TimelineStatus = "ready_now",
        LocationId = "Farm", TileX = rank, TileY = rank, EnergyCost = 2, EstimatedTicks = minutes * 42
    };
}
