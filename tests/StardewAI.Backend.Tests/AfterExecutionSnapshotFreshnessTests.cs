using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed class AfterExecutionSnapshotFreshnessTests
{
    [Theory]
    [InlineData(494, 511)]
    [InlineData(494, 494)]
    [InlineData(490, 511)]
    public async Task OlderOrSameTickCacheCannotProveFreshnessByHash(long cachedTick, long refreshedTick)
    {
        using var transport = new SnapshotTransport(Frame("previous-full", cachedTick), Frame("refreshed-full", refreshedTick));
        using var client = new HttpClient(transport);

        var result = await Read(client, Frame("bounded-before", 494), new JsonObject());

        Assert.True(result.Fresh);
        Assert.Equal("refreshed-full", result.Snapshot["state_hash"]!.GetValue<string>());
        Assert.Collection(transport.Requests,
            uri => Assert.DoesNotContain("fresh=1", uri.Query, StringComparison.Ordinal),
            uri => Assert.Contains("fresh=1", uri.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForcedRefreshCannotUseARegressedTickAsEvidence()
    {
        using var transport = new SnapshotTransport(Frame("old-full", 490), Frame("still-old-full", 490), Frame("clock", 494));
        using var client = new HttpClient(transport);

        var result = await Read(client, Frame("bounded-before", 494), new JsonObject());

        Assert.False(result.Fresh);
        Assert.Equal(3, transport.Requests.Count);
    }

    [Fact]
    public async Task LaterCachedTickStillNeedsOnlyOneRead()
    {
        using var transport = new SnapshotTransport(Frame("new-full", 511));
        using var client = new HttpClient(transport);

        var result = await Read(client, Frame("full-before", 494), new JsonObject());

        Assert.True(result.Fresh);
        Assert.Equal("state_hash_changed", result.Note);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExactNativeResultAnchorCanStillUseSameTickCache()
    {
        using var transport = new SnapshotTransport(Frame("anchored-full", 494));
        using var client = new HttpClient(transport);
        var receipt = new JsonObject
        {
            ["product_after_state_hash"] = "anchored-full",
            ["product_after_game_tick"] = 494L,
            ["action_origin_observation"] = new JsonObject { ["status"] = "observed", ["after_game_tick"] = 494L }
        };

        var result = await Read(client, Frame("full-before", 494), receipt, requiresAnchor: true);

        Assert.True(result.Fresh);
        Assert.Equal("product_after_snapshot_cache_match", result.Note);
        Assert.Single(transport.Requests);
    }

    private static JsonObject Frame(string hash, long tick) => new() { ["state_hash"] = hash, ["game_tick"] = tick };

    private static Task<(string Json, JsonObject Snapshot, bool Fresh, string Note)> Read(
        HttpClient client, JsonObject before, JsonObject receipt, bool requiresAnchor = false)
    {
        var options = new LiveTrainingOptions
        {
            BridgeSnapshotUrl = "http://localhost/api/v1/snapshot?profile=full&fresh=1",
            ExecutionSnapshotProfile = "full",
            AfterSnapshotWaitMs = 0,
            AfterSnapshotPollMs = 1
        };
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!
            .GetMethod("ReadAfterExecutionSnapshotCoreAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (Task<(string, JsonObject, bool, string)>)method.Invoke(null,
            new object?[] { client, options, before, receipt, requiresAnchor, null, null })!;
    }

    private sealed class SnapshotTransport(params JsonObject[] frames) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Requests.Count;
            Requests.Add(request.RequestUri!);
            Assert.True(index < frames.Length, "Unexpected extra snapshot read.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(frames[index].ToJsonString())
            });
        }
    }
}
