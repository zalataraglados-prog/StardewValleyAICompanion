using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SiteTillMainReusesOriginalFirstFrontierAndAdmitsOnlyItsFullSourceCompilation()
    {
        var f = new SiteTillNativeFixture(source =>
        {
            foreach (var domain in new[] { "options", "transport" }) source.Before.State[domain] = JsonSerializer.SerializeToElement(new JsonObject());
        });
        var shared = f.Files.F; using var host = shared.Runtime;
        var observation = ReadSiteSnapshot(f.Files.Observation);
        var originals = new[] { f.Files.Observation, f.Files.Narrow, f.Files.Full, f.Files.Queue }
            .ToDictionary(path => path, File.ReadAllBytes);
        await using var owner = await shared.Files.Open();
        using var capture = new SiteApproachCaptureTransport(shared, observation);
        using var client = new HttpClient(capture);
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.PrepareSite, f.Preparation.TotalDay,
            Id, "Farm", "Farm", string.Empty, string.Empty, 0, 0, null, "site-till-main-fixture");
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", client, shared.Runtime.LiveOptions,
            1, step, shared.Inputs.Plan, shared.Inputs.Request, owner);
        await task;
        var built = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var build = built.GetType().GetProperty("Build")!.GetValue(built)!;
        var operation = (CropTeacherManagedSeedOperation)built.GetType().GetProperty("Operation")!.GetValue(built)!;
        Assert.True(operation.IsSiteTilling); Assert.Same(owner.Pending, operation);
        Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
        Assert.Equal(1, capture.Captures);
        var queue = ((JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        Assert.Equal("executor.till_soil", Assert.Single(queue.Items).OptionId);
        Assert.Equal(shared.Source.Before.StateHash, queue.StateHash);
        var prefix = Path.Combine(shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-site-0001-model-action.json");
        var narrow = JsonNode.Parse(File.ReadAllText(prefix))!;
        var ownedPath = (string)build.GetType().GetProperty("ModelPlanPath")!.GetValue(build)!;
        var owned = JsonNode.Parse(File.ReadAllText(ownedPath))!;
        Assert.NotEqual(prefix, ownedPath);
        Assert.Equal(observation.StateHash, narrow["state_hash"]!.GetValue<string>());
        Assert.Equal(shared.Source.Before.StateHash, owned["state_hash"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(shared.Files.Native, "till-native-claim.json")));
        Assert.Equal(0, shared.Runtime.Handler.Commits);
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }
}
