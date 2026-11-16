using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("route", "")]
    [InlineData("wait", "")]
    [InlineData("dialogue", "")]
    [InlineData("wait", "rejected")]
    [InlineData("wait", "wrong_hash")]
    public async Task ManagedSupportingImportsExactRetainedBoundaryAfterNativeSnapshotEviction(string mode, string fault)
    {
        var f = new OwnedRouteFixture(prepareSource: mode == "wait" ? PrepareNativeSettleSource :
            mode == "dialogue" ? SetOwnedSafeDialogue : null);
        // Retain compressed evidence; dispose only the in-process backend host.
        using var runtime = f.Runtime;
        f.Runtime.LiveOptions.SnapshotArtifactMode = ContentAddressedJsonArtifactStore.ContentAddressedGzipMode;
        await using var owner = await f.Files.Open();
        var operation = mode == "wait" ? (await PrepareManagedSettle(f, owner))! :
            mode == "dialogue" ? (await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step))! : await f.Prepare(owner);
        var outcome = mode == "wait" ? BuildManagedSettleResult(f, owner) :
            mode == "dialogue" ? BuildOwnedControlResult(f, owner) : f.BuildNativeResult();
        var beforePath = owner.CurrentSnapshotPath;
        var beforeBytes = File.ReadAllBytes(beforePath);
        using var transport = new ManagedSnapshotEvictionTransport(f, outcome, fault);
        using var client = new HttpClient(transport);
        Task Execute() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync", client, client,
            f.Runtime.LiveOptions, 1, owner, operation,
            mode == "wait" ? ManagedSettleQueue(f) : mode == "dialogue" ? OwnedControlQueue(f) : f.QueuePath,
            (Func<Task<JsonObject>>)(() => throw new InvalidOperationException("No generic dispatch.")));
        if (fault.Length == 0)
        {
            await Execute();
            Assert.Null(owner.Pending);
            Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
            Assert.Equal(1, f.Runtime.Handler.Commits);
            Assert.True(transport.ExactSourceReimported);
            Assert.Contains(f.Source.Before.StateHash, f.Runtime.Store.Snapshots.Keys);
            Assert.Contains(outcome.After.StateHash, f.Runtime.Store.Snapshots.Keys);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(Execute);
            Assert.NotNull(owner.Pending);
            Assert.Equal(beforePath, owner.CurrentSnapshotPath);
            Assert.Equal(0, f.Runtime.Handler.Commits);
        }
        Assert.True(transport.SourceWasEvicted);
        Assert.Equal(1, transport.NativeRequests);
        Assert.Equal(2, f.Runtime.Store.Snapshots.Count);
        Assert.Equal(beforeBytes, File.ReadAllBytes(beforePath));
    }

    private sealed class ManagedSnapshotEvictionTransport : DelegatingHandler
    {
        private readonly OwnedRouteFixture fixture;
        private readonly SnapshotEnvelope after;
        private readonly string fault;
        private readonly OriginalRouteTransport native;
        internal bool SourceWasEvicted { get; private set; }
        internal bool ExactSourceReimported { get; private set; }
        internal int NativeRequests => native.NativeRequests;

        internal ManagedSnapshotEvictionTransport(OwnedRouteFixture fixture,
            (SnapshotEnvelope After, QueueExecutionReceiptEnvelope Receipt) outcome, string fault)
            : base(new OriginalRouteTransport(fixture, outcome))
        {
            this.fixture = fixture; after = outcome.After; this.fault = fault;
            native = (OriginalRouteTransport)InnerHandler!;
            fixture.Runtime.Store.Snapshots.Remove(fixture.Source.Before.StateHash);
            fixture.Runtime.Store.StoreSnapshot(fixture.Source.Before);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (NativeRequests == 1 && path == "/api/v1/snapshots" && request.Method == HttpMethod.Post)
            {
                var snapshot = (await request.Content!.ReadFromJsonAsync<SnapshotEnvelope>(JsonOptions, token))!;
                if (snapshot.StateHash == fixture.Source.Before.StateHash)
                {
                    ExactSourceReimported = true;
                    if (fault.Length > 0) return new(HttpStatusCode.OK)
                    { Content = JsonContent.Create(new { accepted = fault != "rejected",
                        state_hash = fault == "wrong_hash" ? "wrong" : snapshot.StateHash }) };
                }
            }
            var reply = await base.SendAsync(request, token);
            if (path == fixture.Runtime.LiveOptions.ExecutorEndpointPath)
            {
                var store = fixture.Runtime.Store;
                store.Snapshots.Remove(after.StateHash);
                store.PrepareForSnapshotIngest(); store.StoreSnapshot(after);
                for (var i = 1; i <= 2; i++)
                {
                    var observation = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(after, JsonOptions), JsonOptions)!;
                    ChangeState(observation, "player", player => player["tile_x"]!["value"] = 10 + i);
                    observation.GameTick += i;
                    store.PrepareForSnapshotIngest(); store.StoreSnapshot(observation);
                }
                SourceWasEvicted = !store.Snapshots.ContainsKey(fixture.Source.Before.StateHash);
            }
            return reply;
        }
    }
}
