using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputObservationClientTests
{
    [Fact]
    public void SnapshotAnchorIsExplicitAndOnlyAppliedToObservedOriginalItems()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeOutputSnapshotAnchor);
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-output-snapshot-anchor", "--skip-training"]));
        var options = LiveTrainingOptions.Parse(["--observe-native-output-snapshot-anchor", "--observe-native-outputs",
            "--skip-training", "--use-runtime-test-harness-executor"]);
        Assert.True(options.ObserveNativeOutputSnapshotAnchor);
        options.RunId = "run"; options.SaveIsolationPath = "isolated-saves";
        Assert.True(Build(options, "executor.move_to_tile").ActionOriginSnapshotAnchor);
        Assert.True(Build(options, "executor.clear_obstacle").ActionOriginSnapshotAnchor);
        Assert.False(Build(options, "executor.wait_ticks").ActionOriginSnapshotAnchor);
        options.ObserveNativeOutputSnapshotAnchor = false;
        Assert.False(Build(options, "executor.move_to_tile").ActionOriginSnapshotAnchor);
        options.ObserveNativeOutputSnapshotAnchor = true; options.ExecutionSnapshotProfile = "light";
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
    }

    [Fact]
    public async Task ExactSnapshotAnchorReusesTheExistingOneReadCacheProtocol()
    {
        var options = Options(); options.ObserveNativeOutputSnapshotAnchor = true;
        using var handler = new SnapshotAnchorHandler(Frame("after", 101));
        using var http = new HttpClient(handler);
        var result = await ReadAnchoredAfter(http, options, Execution());
        Assert.True(result.Fresh);
        Assert.Equal("after", result.Snapshot["state_hash"]!.GetValue<string>());
        Assert.Equal("product_after_snapshot_cache_match", result.Note);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("tick")]
    [InlineData("hash")]
    public async Task AnchoredSnapshotNeverFallsBackToANewerDifferentFrame(string change)
    {
        var options = Options(); options.ObserveNativeOutputSnapshotAnchor = true;
        using var handler = new SnapshotAnchorHandler(Frame(change == "hash" ? "different" : "after", change == "tick" ? 102 : 101));
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAnchoredAfter(http, options, Execution()));
        Assert.Contains("no later-frame fallback", error.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("missing-hash")]
    [InlineData("old-tick")]
    [InlineData("unavailable-observation")]
    [InlineData("missing-observation")]
    [InlineData("malformed-observation")]
    [InlineData("missing-both")]
    [InlineData("receipt-tick")]
    [InlineData("missing-receipt-tick")]
    public async Task MissingAnchorFailsBeforeSnapshotPollingOrFreshScan(string change)
    {
        var options = Options(); options.ObserveNativeOutputSnapshotAnchor = true;
        using var handler = new SnapshotAnchorHandler(Frame("after", 101));
        using var http = new HttpClient(handler);
        var execution = Execution();
        if (change == "missing-hash") execution.Remove("product_after_state_hash");
        if (change == "old-tick") execution["product_after_game_tick"] = 99L;
        if (change == "unavailable-observation") execution["action_origin_observation"]!["status"] = "unavailable";
        if (change == "missing-observation") { execution.Remove("action_origin_observation"); execution["option_id"] = "executor.move_to_tile"; }
        if (change == "malformed-observation") execution["action_origin_observation"] = "not-an-observation";
        if (change == "missing-both") { execution.Remove("action_origin_observation"); execution.Remove("option_id"); }
        if (change == "receipt-tick") execution["action_origin_observation"]!["after_game_tick"] = 100L;
        if (change == "missing-receipt-tick") execution["action_origin_observation"]!.AsObject().Remove("after_game_tick");
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAnchoredAfter(http, options, execution));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task DefaultAndNonObservedPathsKeepTheirExistingFreshnessBehavior()
    {
        var options = Options();
        using var handler = new SnapshotAnchorHandler(Frame("different", 102));
        using var http = new HttpClient(handler);
        Assert.True((await ReadAnchoredAfter(http, options, Execution(), requireAnchor: false)).Fresh);
        options.ObserveNativeOutputSnapshotAnchor = true;
        var nonObserved = new JsonObject();
        Assert.True((await ReadAnchoredAfter(http, options, nonObserved, requireAnchor: false)).Fresh);
        Assert.Equal(2, handler.Calls);
    }

    private static Task<(string Json, JsonObject Snapshot, bool Fresh, string Note)> ReadAnchoredAfter(
        HttpClient http, LiveTrainingOptions options, JsonObject execution, bool requireAnchor = true) =>
        (Task<(string Json, JsonObject Snapshot, bool Fresh, string Note)>)ProgramMethod("ReadAfterExecutionSnapshotCoreAsync")
            .Invoke(null, [http, options, Frame("before", 100), execution, requireAnchor, null, null])!;
    private static JsonObject Frame(string hash, long tick) => new() { ["state_hash"] = hash, ["game_tick"] = tick };
    private static JsonObject Execution() => new()
    {
        ["product_after_state_hash"] = "after", ["product_after_game_tick"] = 101L,
        ["action_origin_observation"] = new JsonObject { ["status"] = "observed", ["after_game_tick"] = 101L }
    };
    private sealed class SnapshotAnchorHandler(JsonObject frame) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(frame.ToJsonString(), Encoding.UTF8, "application/json") });
        }
    }
}
