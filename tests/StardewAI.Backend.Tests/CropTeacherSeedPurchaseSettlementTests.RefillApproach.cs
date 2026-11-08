using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillApproachPinsOriginalCompilationWithoutDroppingRepairs(bool repair)
    {
        using var f = new RefillApproachFixture(repair);
        await f.Compile();
        var scope = f.Open();
        Assert.Equal(new MoneyRevenueCropPlantingTile(0, 1), scope.Target);
        Assert.Equal(repair ? 1 : 0, scope.ClearanceQueueItemIds.Count);
        Assert.Equal(f.Queue.QueueId, scope.QueueId);
        Assert.False(scope.NativeOutcomeVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
        scope.ValidateInputsUnchanged();
        f.Queue.Items[0].QueueItemId += ".changed";
        Fixture.Save(f.QueuePath, f.Queue);
        Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData("wrong-target")]
    [InlineData("wrong-source")]
    [InlineData("repair-disabled")]
    [InlineData("repair-budget")]
    [InlineData("duplicate-parameter")]
    [InlineData("candidate")]
    [InlineData("dropped-repair")]
    [InlineData("reordered-repair")]
    [InlineData("opaque-identity")]
    public async Task RefillApproachRejectsChangedOriginalPlanOrRepairQueue(string change)
    {
        using var f = new RefillApproachFixture(repair: true);
        await f.Compile();
        var plan = JsonSerializer.Deserialize<SmallModelPlanEnvelope>(File.ReadAllText(f.ModelPath), JsonOptions)!;
        var move = plan.Steps.Single();
        switch (change)
        {
            case "wrong-target": move.TargetTileX = 1; break;
            case "wrong-source": plan.SourceModel = "authoritative_crop_site_approach.v1"; break;
            case "repair-disabled": move.Parameters.Single(row => row.Name == "max_route_repair_clears").Value = "0"; break;
            case "repair-budget": move.Parameters.Single(row => row.Name == "max_route_repair_minutes").Value = "13"; break;
            case "duplicate-parameter": move.Parameters = move.Parameters.Append(move.Parameters[0]).ToArray(); break;
            case "candidate": move.Preconditions[0] = "candidate_id:unrelated"; break;
            case "dropped-repair": f.Queue.Items = f.Queue.Items.Where(item => item.OptionId != "executor.clear_obstacle").ToArray(); break;
            case "reordered-repair": Array.Reverse(f.Queue.Items); break;
            case "opaque-identity": f.Queue.Items[1].QueueItemId = f.Queue.Items[0].QueueItemId; break;
        }
        Fixture.Save(f.ModelPath, plan); Fixture.Save(f.QueuePath, f.Queue);
        Assert.Throws<InvalidDataException>(() => f.Open());
    }

    [Theory]
    [InlineData("not-empty")]
    [InlineData("already-there")]
    public async Task RefillApproachRejectsWrongOriginalBranch(string change)
    {
        using var f = new RefillApproachFixture(repair: false, water: change == "not-empty" ? 1 : 0,
            atApproach: change == "already-there");
        await f.Compile(forceOriginalApproach: true);
        var error = Assert.Throws<InvalidDataException>(() => f.Open());
        Assert.Contains("original empty-can approach branch", error.Message);
    }

    [Fact]
    public async Task RefillApproachRejectsSourceChangedWithoutAuthorityRebind()
    {
        using var f = new RefillApproachFixture(repair: false);
        await f.Compile();
        ChangeState(f.Watering.Before, "player", node => node["energy"]!["value"] = 99);
        Fixture.Save(f.Watering.Shared.Source.SourcePath, f.Watering.Before);
        Assert.Throws<InvalidDataException>(() => f.Open());
    }

    private sealed class RefillApproachFixture : IDisposable
    {
        internal WateringFixture Watering { get; }
        internal string ModelPath => Path.Combine(Watering.Shared.Runtime.LiveOptions.SnapshotDir,
            "crop-teacher-watering-refill-approach-0001-model-plan.json");
        internal string QueuePath => ModelPath.Replace("-model-plan.json", "-compiled-queue.json", StringComparison.Ordinal);
        internal ActionQueueEnvelope Queue { get; private set; } = null!;

        internal RefillApproachFixture(bool repair, int water = 0, bool atApproach = false)
        {
            Watering = new(water: water, prepareSource: source =>
            {
                ChangeState(source.Before, "player", node => node["tile_y"]!["value"] = atApproach ? 1 : 3);
                ChangeState(source.Before, "current_location", node =>
                {
                    node["map"]!["value"]!["width"] = 4;
                    node["map"]!["value"]!["height"] = 4;
                    node["objects"] = JsonSerializer.SerializeToNode(new { status = "available", value = Array.Empty<object>() });
                    node["terrain_features"] = JsonSerializer.SerializeToNode(new { status = "available", value = repair
                        ? new[] { new { tile_x = 0, tile_y = 2, type = "Grass" } } : Array.Empty<object>() });
                });
                ChangeState(source.Before, "locations", node =>
                {
                    var grid = node["collision_grid"]!["value"]!;
                    grid["width"] = 4;
                    grid["height"] = 4;
                    grid["notable_tiles"] = JsonSerializer.SerializeToNode(Enumerable.Range(repair ? 0 : 1, repair ? 4 : 3)
                        .Select(x => new { tile_x = x, tile_y = 2, collision_blocked = true }));
                });
            });
        }

        internal async Task Compile(bool forceOriginalApproach = false)
        {
            var shared = Watering.Shared;
            var snapshot = JsonNode.Parse(JsonSerializer.Serialize(Watering.Before, JsonOptions))!.AsObject();
            var task = forceOriginalApproach ? InvokeLive<Task>("BuildCropTeacherWateringRefillApproachQueueAsync", shared.Runtime.Client,
                shared.Runtime.LiveOptions, 1, shared.Source.SourcePath, snapshot, Watering.Before.StateHash,
                new MoneyRevenueCropPlantingTile(0, 1)) : InvokeLive<Task>("BuildCropTeacherControlQueueCoreAsync", shared.Runtime.Client,
                shared.Runtime.LiveOptions, 1, Watering.Step, shared.Inputs.Plan, shared.Inputs.Request,
                shared.Source.SourcePath, snapshot, Watering.Before.StateHash, null!, null!, null!);
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            Assert.Equal("crop_teacher_watering_refill_approach", result.GetType().GetProperty("InterruptKind")!.GetValue(result));
            Queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(QueuePath), JsonOptions)!;
            Assert.Equal("pending", Queue.Status);
        }

        internal CropTeacherRefillApproachCompilation Open() => CropTeacherRefillApproachCompilation.Open(
            Watering.Shared.Inputs.Plan.RequestPath, Watering.Shared.Source.SourcePath, Watering.Shared.Source.LedgerPath,
            Watering.Shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-approach.original", ModelPath, QueuePath);

        public void Dispose() => Watering.Dispose();
    }
}
