using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SiteClearanceMainKeepsFirstFrontierAndUsesSameManagedOwner()
    {
        var f = new SiteTillFiles(frontier: "clear", prepareSource: source =>
        {
            foreach (var domain in new[] { "options", "transport" }) source.Before.State[domain] = JsonSerializer.SerializeToElement(new JsonObject());
        });
        var shared = f.F; using var host = shared.Runtime;
        var observation = ReadSiteSnapshot(f.Observation);
        await using var owner = await shared.Files.Open();
        using var capture = new SiteApproachCaptureTransport(shared, observation);
        using var client = new HttpClient(capture);
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.PrepareSite, f.OpenClear().TotalDay,
            Id, "Farm", "Farm", string.Empty, string.Empty, 0, 0, null, "site-clearance-main-fixture");
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", client, shared.Runtime.LiveOptions,
            1, step, shared.Inputs.Plan, shared.Inputs.Request, owner);
        await task;
        var built = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var build = built.GetType().GetProperty("Build")!.GetValue(built)!;
        var operation = (CropTeacherManagedSeedOperation)built.GetType().GetProperty("Operation")!.GetValue(built)!;
        Assert.True(operation.IsSiteClearance); Assert.False(operation.IsSiteTilling); Assert.Same(owner.Pending, operation);
        Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
        var queue = ((JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        Assert.Equal("executor.clear_obstacle", Assert.Single(queue.Items).OptionId);
        Assert.Equal(shared.Source.Before.StateHash, queue.StateHash); Assert.Equal(1, capture.Captures);
        Assert.False(File.Exists(Path.Combine(shared.Files.Native, "clearance-native-claim.json")));
        Assert.Equal(0, shared.Runtime.Handler.Commits);
    }
}
