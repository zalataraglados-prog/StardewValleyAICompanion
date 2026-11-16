using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SiteApproachMainRebuildsForFullAcceptedSourceAndExecutesThroughOriginalAdapter()
    {
        var f = new OwnedRouteFixture(prepareSource: source =>
        {
            ChangeState(source.Before, "player", player => { player["tile_x"]!["value"] = 8; player["tile_y"]!["value"] = 8; });
            ChangeState(source.Before, "current_location", location => location["map"]!["value"]!["id"] = "");
            foreach (var domain in new[] { "options", "transport" }) source.Before.State[domain] = JsonSerializer.SerializeToElement(new JsonObject());
        });
        using var host = f.Runtime;
        var observation = WriteValidSiteMainObservation(f); var original = File.ReadAllBytes(observation);
        await using var owner = await f.Files.Open();
        using var capture = new SiteApproachCaptureTransport(f, ReadSiteSnapshot(observation));
        using var client = new HttpClient(capture);
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.PrepareSite,
            f.Source.Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
            Id, "Farm", "Farm", string.Empty, string.Empty, 0, 0, null, "site-main-fixture");
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", client, f.Runtime.LiveOptions, 1,
            step, f.Inputs.Plan, f.Inputs.Request, owner);
        await task;
        var built = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var build = built.GetType().GetProperty("Build")!.GetValue(built)!;
        var operation = (CropTeacherManagedSeedOperation)built.GetType().GetProperty("Operation")!.GetValue(built)!;
        Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
        Assert.Equal(1, capture.Captures);
        var narrowModel = Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "crop-teacher-site-approach-0001-model-plan.json");
        var narrow = JsonNode.Parse(File.ReadAllText(narrowModel))!;
        var ownedPath = (string)build.GetType().GetProperty("ModelPlanPath")!.GetValue(build)!;
        var owned = JsonNode.Parse(File.ReadAllText(ownedPath))!;
        Assert.NotEqual(narrowModel, ownedPath);
        Assert.Equal(ReadSiteSnapshot(observation).StateHash, narrow["state_hash"]!.GetValue<string>());
        Assert.Equal(f.Source.Before.StateHash, owned["state_hash"]!.GetValue<string>());
        var queue = ((JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        var native = SiteOwnerNativeResult(f, queue, new(2, 0));
        using var transport = new OriginalRouteTransport(f, native); using var runtime = new HttpClient(transport);
        await InvokeLive<Task>("ExecuteCropTeacherOwnedRouteAsync", runtime, runtime, f.Runtime.LiveOptions, 1, owner, operation);
        Assert.Null(owner.Pending); Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Runtime.Handler.Commits);
        AssertSiteOwnerAcceptance(f, new(2, 0)); Assert.Equal(original, File.ReadAllBytes(observation));
        Assert.True(JsonNode.DeepEquals(narrow, JsonNode.Parse(File.ReadAllText(narrowModel))));
    }

    private static string WriteValidSiteMainObservation(OwnedRouteFixture fixture)
    {
        var observed = ReadSiteSnapshot(WriteSiteObservation(fixture));
        ChangeState(observed, "farm", farm =>
        {
            var field = JsonNode.Parse(fixture.Source.Before.State["farm"].GetProperty("material_inventory_graph").GetRawText())!;
            field["status"] = "unavailable"; field["value"] = null; field["reason"] = "not_requested_by_snapshot_profile";
            farm["material_inventory_graph"] = field;
        });
        ChangeState(observed, "current_location", location =>
        {
            var field = location["site_preparation_context"]!;
            field["source"] = new JsonObject { ["kind"] = "test", ["path"] = "site-main-fixture" };
            field["adapter"] = "test"; field["confidence"] = 1d;
        });
        var path = Path.Combine(fixture.Source.RuntimePath, "valid-site-observation.json");
        Fixture.Save(path, observed); return path;
    }

    private sealed class SiteApproachCaptureTransport : HttpMessageHandler
    {
        private readonly HttpMessageInvoker backend;
        private readonly SnapshotEnvelope observation;
        internal int Captures { get; private set; }
        internal SiteApproachCaptureTransport(OwnedRouteFixture fixture, SnapshotEnvelope observation)
        { backend = new(fixture.Runtime.Handler, disposeHandler: false); this.observation = observation; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath != "/fixture/snapshot") return backend.SendAsync(request, cancellationToken);
            Assert.Contains("profile=farm_layout", request.RequestUri.Query, StringComparison.Ordinal);
            Captures++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(observation, options: JsonOptions) });
        }
        protected override void Dispose(bool disposing) { if (disposing) backend.Dispose(); base.Dispose(disposing); }
    }
}
