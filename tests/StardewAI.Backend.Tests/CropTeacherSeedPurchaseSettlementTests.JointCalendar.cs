using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void JointCalendarPlanReusesTheOriginalWateringActionQueueCompilerWithoutNewNativeRequirements()
    {
        using var f = new WateringFixture();
        var time = SnapshotGameClockBudgetReader.CurrentTime(f.Before)!.Value;
        var candidates = f.Before.State["farm"].GetProperty("crops").GetProperty("value").EnumerateArray()
            .Select((crop, index) => new PolicyEventCandidatePrediction
            {
                CandidateId = "joint-water-" + index, Kind = "water_crop_tile", Rank = index + 1,
                TimelineStatus = "ready_now", LocationId = "Farm", TileX = crop.GetProperty("tile_x").GetInt32(),
                TileY = crop.GetProperty("tile_y").GetInt32(), EstimatedTicks = 60, EnergyCost = 2,
                ClosesAt = GameClockBudgetPolicy.AddClockMinutes(time, 60)
            }).ToArray();
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(candidates, f.Before.StateHash, time);
        var queue = new ActionQueueCompiler().Compile(plan, f.Before);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.CompilerDiagnostics);
        Assert.Equal(candidates.Length, queue.Items.Length);
        Assert.All(queue.Items, item =>
        {
            Assert.Equal("executor.water_crop", item.OptionId);
            Assert.Equal("pending", item.Status);
            Assert.Empty(item.BlockingReasons);
        });
    }
}
