using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("site-approach", "crop-site-approach", "authoritative_crop_site_approach.v1", "site_preparation_starts_only_after_approach")]
    [InlineData("watering-refill-approach", "crop-watering-refill-approach", "authoritative_crop_watering_refill_approach.v1", "water_crop_retries_only_after_native_refill_approach")]
    public async Task OriginalBoundedApproachProducerUsesSharedRecipeAndOriginalBackendCompiler(string artifact, string candidateKind,
        string sourceModel, string purpose)
    {
        var f = new OwnedRouteFixture(); using var host = f.Runtime;
        var beforeBytes = File.ReadAllBytes(f.Source.SourcePath);
        var source = JsonNode.Parse(File.ReadAllText(f.Source.SourcePath))!.AsObject();
        var options = f.Runtime.LiveOptions;
        var task = InvokeLive<Task>("BuildCropTeacherBoundedApproachQueueAsync", f.Runtime.Client, options, 1,
            f.Source.SourcePath, source, f.Source.Before.StateHash, options.BridgeSnapshotUrl, 2, 1, artifact,
            candidateKind, sourceModel, purpose, "farm_layout", string.Empty);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var path = (string)result.GetType().GetProperty("ModelPlanPath")!.GetValue(result)!;
        var actual = JsonNode.Parse(File.ReadAllText(path));
        var expected = CropBoundedApproachPlanBuilder.Build(f.Source.Before.StateHash, candidateKind + ":1:2,1",
            sourceModel, purpose, 2, 1, options.TargetExecutionMode, JsonSerializer.SerializeToNode(options.TargetActor, JsonOptions)!);
        Assert.True(JsonNode.DeepEquals(expected, actual));
        Assert.NotNull(f.Runtime.Handler.LastCompiledQueue);
        Assert.Equal("pending", f.Runtime.Handler.LastCompiledQueue!["status"]!.GetValue<string>());
        Assert.Equal(beforeBytes, File.ReadAllBytes(f.Source.SourcePath));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }
}
