using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JointTerminalIngressUsesOriginalFarmRouteInsteadOfSleepingAtRecoveryTime(bool product)
    {
        using var f = new OwnedRouteFixture(productExecutor: product, prepareSource: source =>
        {
            ChangeState(source.Before, "time", node => node["time"]!["value"] = 1900);
            ChangeState(source.Before, "player", node => node["location_id"]!["value"] = "Town");
            ChangeState(source.Before, "current_location", node => node["map"]!["value"]!["id"] = "Town");
            ChangeState(source.Before, "farm", node =>
                node["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["location_id"] = "Town");
            ChangeState(source.Before, "locations", node =>
            {
                node["collision_grid"]!["value"]!["location_id"] = "Town";
                node["route_connectors"]!["value"] = JsonSerializer.SerializeToNode(new
                {
                    location_id = "Town", connectors = new[] { new { kind = "warp", tile_x = 1, tile_y = 0,
                        target_location = "Farm", target_x = 5, target_y = 6, resolved = true } }
                });
                node["route_graph"]!["value"] = JsonSerializer.SerializeToNode(new
                {
                    edges = new[] { new { kind = "warp", from_location = "Town", from_x = 1, from_y = 0,
                        target_location = "Farm", target_x = 5, target_y = 6, resolved = true } }
                });
            });
        });
        await using var owner = await f.Files.Open();
        f.Runtime.LiveOptions.UseJointDailyCalendar = true;
        // Caller/control protocol test, not evidence that this fixture completed the production root.
        var task = InvokeLive<Task>("BuildCropTeacherOwnedJointTerminalApproachAsync", f.Runtime.Client,
            f.Runtime.LiveOptions, 1, f.Step with { Stage = CropTeacherRunStageKinds.Complete }, f.Inputs.Plan, f.Inputs.Request, owner);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var operation = SelectionProperty<CropTeacherManagedSeedOperation>(result, "Operation")!;
        Assert.Same(operation, owner.Pending);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.QueuePath), JsonOptions)!;
        Assert.All(queue.Items, row => Assert.Equal("executor.traverse_connector", row.OptionId));
        Assert.Contains(queue.Items.Single().NormalizedCommand.Parameters,
            row => row.Name == "expected_target_location" && row.Value == "Farm");
        Assert.Equal(0, f.Runtime.Handler.Commits);
        await owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, () => f.EmitNative());
        await f.Rebind(owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Equal("Farm", JsonNode.Parse(File.ReadAllText(owner.CurrentSnapshotPath))!["state"]!["player"]!["location_id"]!["value"]!.GetValue<string>());
        Assert.Equal(0, owner.AcceptedTerminalDepositCount);
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1900)]
    public async Task JointTerminalAlreadyAtFarmDoesNotCompileAnotherRouteOrSleep(int time)
    {
        using var f = new OwnedRouteFixture(prepareSource: source =>
            ChangeState(source.Before, "time", node => node["time"]!["value"] = time));
        await using var owner = await f.Files.Open();
        f.Runtime.LiveOptions.UseJointDailyCalendar = true;
        var task = InvokeLive<Task>("BuildCropTeacherOwnedJointTerminalApproachAsync", f.Runtime.Client,
            f.Runtime.LiveOptions, 1, f.Step with { Stage = CropTeacherRunStageKinds.Complete }, f.Inputs.Plan, f.Inputs.Request, owner);
        await task;
        Assert.Null(task.GetType().GetProperty("Result")!.GetValue(task));
        Assert.Null(owner.Pending);
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.False(File.Exists(f.QueuePath));
    }
}
