using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalRouteAdapterCallsOriginalExecutionLoopWithBoundedFullAfterCapture()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        using var transport = new OriginalRouteTransport(f);
        using var client = new HttpClient(transport);
        await InvokeLive<Task>("ExecuteCropTeacherOwnedRouteAsync", client, client, f.Runtime.LiveOptions, 1, owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(1, transport.NativeRequests);
        Assert.InRange(transport.FullSnapshotReads, 1, 3);
        Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
    }

    private sealed class OriginalRouteTransport : HttpMessageHandler
    {
        private readonly OwnedRouteFixture fixture;
        private readonly HttpMessageInvoker backend;
        private readonly SnapshotEnvelope after;
        private readonly QueueExecutionReceiptEnvelope receipt;
        private SnapshotEnvelope current;
        internal int NativeRequests { get; private set; }
        internal int FullSnapshotReads { get; private set; }
        internal OriginalRouteTransport(OwnedRouteFixture fixture)
        {
            this.fixture = fixture;
            backend = new(fixture.Runtime.Handler, disposeHandler: false);
            current = fixture.Source.Before;
            (after, receipt) = fixture.BuildNativeResult();
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath == "/fixture/snapshot")
            {
                if (!request.RequestUri.Query.Contains("profile=clock", StringComparison.Ordinal)) FullSnapshotReads++;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(current, options: JsonOptions) };
            }
            if (request.RequestUri.AbsolutePath == fixture.Runtime.LiveOptions.ExecutorEndpointPath)
            {
                var bound = (await request.Content!.ReadFromJsonAsync<TrainingExecutionRequest>(JsonOptions, token))!;
                Assert.Equal(current.StateHash, bound.BeforeStateHash);
                Assert.Equal(receipt.StepResults[0].QueueItemId, bound.QueueItemId);
                Assert.Equal(0, NativeRequests);
                NativeRequests++;
                current = after;
                fixture.Runtime.Store.Snapshots[after.StateHash] = after;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(receipt.StepResults[0], options: JsonOptions) };
            }
            return await backend.SendAsync(request, token);
        }
        protected override void Dispose(bool disposing) { if (disposing) backend.Dispose(); base.Dispose(disposing); }
    }
}
