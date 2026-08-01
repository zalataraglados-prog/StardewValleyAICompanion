using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileMaterialTransferCarriesExactProjectionToRuntime()
    {
        var snapshot = MaterialTransferSnapshot(locked: false);
        var request = Request(snapshot.StateHash, "executor.transfer_material");
        request.Actions[0].Parameters = MaterialTransferParameters();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("transfer_material", step.StepType);
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "location_id" &&
                parameter.Value == "Farm");
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "target_tile_x" &&
                parameter.Value == "4");
        var intentJson = Assert.Single(
            item.NormalizedCommand.Parameters,
            parameter => parameter.Name == "material_transfer_intent_json").Value;
        var projectionJson = Assert.Single(
            item.NormalizedCommand.Parameters,
            parameter => parameter.Name == "material_transfer_projection_json").Value;
        var intent = System.Text.Json.JsonSerializer.Deserialize<MaterialTransferIntent>(intentJson)!;
        var projection = System.Text.Json.JsonSerializer.Deserialize<MaterialTransferProjection>(projectionJson)!;
        Assert.Equal(10, intent.Quantity);
        Assert.Equal("projected", projection.Status);
        Assert.Equal(10, projection.DestinationQuantityAfter - projection.DestinationQuantityBefore);
    }

    [Fact]
    public void CompileMaterialTransferBlocksLockedChestUpstream()
    {
        var snapshot = MaterialTransferSnapshot(locked: true);
        var request = Request(snapshot.StateHash, "executor.transfer_material");
        request.Actions[0].Parameters = MaterialTransferParameters();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "material_transfer_chest_locked_by_other_player",
            queue.Items[0].BlockingReasons);
    }

    private static SmallModelActionParameter[] MaterialTransferParameters() =>
        MaterialTransferTestFixture.Parameters()
            .Concat(new[]
            {
                new SmallModelActionParameter { Name = "MATERIAL_TRANSFER_PROJECTION_JSON", Value = """{"status":"projected","destination_quantity_after":999999}""" },
                new SmallModelActionParameter { Name = "LOCATION_ID", Value = "forged" },
                new SmallModelActionParameter { Name = "TARGET_TILE_X", Value = "999" }
            })
            .ToArray();

    private static StardewAI.Contracts.State.SnapshotEnvelope MaterialTransferSnapshot(bool locked) =>
        MaterialTransferTestFixture.Snapshot(locked);
}
