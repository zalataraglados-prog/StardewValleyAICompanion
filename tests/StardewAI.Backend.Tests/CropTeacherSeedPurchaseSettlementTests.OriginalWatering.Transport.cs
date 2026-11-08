using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task OriginalWateringAdapterUsesOriginalLoopAndCapturesOnlyActionBoundaries(int attempts)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        await f.Basis.Emit(retainedQueuePath: f.QueuePath);
        f.Shared.Runtime.LiveOptions.MaxQueueItemAttempts = attempts;
        using var transport = new OriginalWateringTransport(f);
        using var client = new HttpClient(transport);
        await InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync", client, client,
            f.Shared.Runtime.LiveOptions, 1, owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(attempts, transport.NativeRequests);
        Assert.InRange(transport.FullSnapshotReads, attempts, attempts * 3 + 1);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
        var acceptance = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal(attempts, acceptance["applied_tile_count"]!.GetValue<int>());
        Assert.Equal(attempts == 2, acceptance["complete_batch"]!.GetValue<bool>());
    }

    private sealed class OriginalWateringTransport : HttpMessageHandler
    {
        private readonly OwnedWateringFixture fixture;
        private readonly HttpMessageInvoker backend;
        private readonly QueueExecutionReceiptEnvelope receipt;
        private SnapshotEnvelope current;
        internal int NativeRequests { get; private set; }
        internal int FullSnapshotReads { get; private set; }
        internal OriginalWateringTransport(OwnedWateringFixture fixture)
        {
            this.fixture = fixture;
            backend = new(fixture.Shared.Runtime.Handler, disposeHandler: false);
            current = fixture.Basis.Before;
            receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(fixture.Basis.Transition.ExecutionReceiptPath), JsonOptions)!;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath == "/fixture/snapshot")
            {
                if (!request.RequestUri.Query.Contains("profile=clock", StringComparison.Ordinal)) FullSnapshotReads++;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(current, options: JsonOptions) };
            }
            if (request.RequestUri.AbsolutePath == fixture.Shared.Runtime.LiveOptions.ExecutorEndpointPath)
            {
                var bound = (await request.Content!.ReadFromJsonAsync<TrainingExecutionRequest>(JsonOptions, token))!;
                var next = receipt.StepResults[NativeRequests];
                Assert.Equal(current.StateHash, bound.BeforeStateHash);
                Assert.Equal(next.QueueItemId, bound.QueueItemId);
                NativeRequests++;
                current = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(next.AfterSnapshotPath!), JsonOptions)!;
                fixture.Shared.Runtime.Store.Snapshots[current.StateHash] = current;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(next, options: JsonOptions) };
            }
            return await backend.SendAsync(request, token);
        }
        protected override void Dispose(bool disposing) { if (disposing) backend.Dispose(); base.Dispose(disposing); }
    }
}
