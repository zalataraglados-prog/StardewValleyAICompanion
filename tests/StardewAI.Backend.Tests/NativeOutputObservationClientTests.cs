using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class NativeOutputObservationClientTests
{
    [Theory]
    [InlineData("executor.move_to_tile")]
    [InlineData("executor.clear_obstacle")]
    public void OriginalRequestMapperAddsOnlyOptInObservation(string option)
    {
        var options = Options();
        var baseline = Build(options, option);
        Assert.Null(baseline.ActionOriginObservationScopeId);
        options.ObserveNativeOutputs = true;
        var observed = Build(options, option);
        Assert.Matches("^native-output[.]v1:[0-9a-f]{64}$", observed.ActionOriginObservationScopeId!);
        var originalScope = observed.ActionOriginObservationScopeId;
        observed.ActionOriginObservationScopeId = null;
        observed.RequestNonce = baseline.RequestNonce;
        observed.CreatedAt = baseline.CreatedAt;
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(baseline), JsonSerializer.SerializeToNode(observed)));
        Assert.NotEqual(originalScope, NativeOutputObservationMode.CreateScopeId(options, observed));
    }

    [Theory]
    [InlineData("executor.wait_ticks")]
    [InlineData("executor.water_crop")]
    [InlineData("executor.place_bomb")]
    public void OtherOriginalQueueItemsRemainUnobserved(string option)
    {
        var options = Options(); options.ObserveNativeOutputs = true;
        Assert.Null(Build(options, option).ActionOriginObservationScopeId);
    }

    [Fact]
    public void CliIsOffByDefaultAndExplicitModeKeepsTheOriginalEndpoint()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeOutputs);
        var options = LiveTrainingOptions.Parse(["--observe-native-outputs", "--skip-training", "--use-runtime-test-harness-executor"]);
        Assert.True(options.ObserveNativeOutputs);
        Assert.Equal("/api/v1/training/execute", options.ExecutorEndpointPath);
    }

    [Theory]
    [InlineData("--use-product-executor")]
    [InlineData("--use-sandbox-executor")]
    [InlineData("--no-executor-feedback-required")]
    [InlineData("--target-execution-mode", "coop_companion")]
    public void IncompatibleCliFailsBeforeDispatch(params string[] incompatible)
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(
            new[] { "--observe-native-outputs", "--skip-training" }.Concat(incompatible).ToArray()));
    }

    [Fact]
    public void ObservationCannotAuthorizeFormalTraining()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-outputs"]));
        var options = Options(); options.ObserveNativeOutputs = true; options.SkipTraining = false;
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.CreateScopeId(options, new()));
    }

    [Theory]
    [InlineData("run")]
    [InlineData("queue")]
    [InlineData("item")]
    [InlineData("nonce")]
    [InlineData("before")]
    [InlineData("source")]
    [InlineData("save")]
    [InlineData("option")]
    public void ScopeBindsEveryOriginalRequestIdentity(string changed)
    {
        var options = Options(); options.ObserveNativeOutputs = true;
        var request = Build(options, "executor.move_to_tile");
        var original = request.ActionOriginObservationScopeId;
        Assert.Equal(original, NativeOutputObservationMode.CreateScopeId(options, request));
        switch (changed)
        {
            case "run": request.RunId = options.RunId += "2"; break;
            case "queue": request.QueueId += "2"; break;
            case "item": request.QueueItemId += "2"; break;
            case "nonce": request.RequestNonce += "2"; break;
            case "before": request.BeforeStateHash += "2"; break;
            case "source": request.SourcePlanningOptionId += "2"; break;
            case "save": request.SaveIsolationPath = options.SaveIsolationPath += "2"; break;
            case "option": request.OptionId = "executor.clear_obstacle"; break;
        }
        Assert.NotEqual(original, NativeOutputObservationMode.CreateScopeId(options, request));
        request.RequestNonce = "";
        Assert.Throws<InvalidOperationException>(() => NativeOutputObservationMode.CreateScopeId(options, request));
    }

    [Fact]
    public void ScopeIsBoundedAndStructuredEvenForLongOrDelimiterCollidingIds()
    {
        var options = Options(); options.ObserveNativeOutputs = true; options.RunId = new string('a', 512);
        var request = Build(options, "executor.move_to_tile");
        Assert.True(request.ActionOriginObservationScopeId!.Length <= 128);
        request.QueueId = "a|b"; request.QueueItemId = "c";
        var first = NativeOutputObservationMode.CreateScopeId(options, request);
        request.QueueId = "a"; request.QueueItemId = "b|c";
        Assert.NotEqual(first, NativeOutputObservationMode.CreateScopeId(options, request));
    }

    [Theory]
    [InlineData("run")]
    [InlineData("save")]
    [InlineData("mode")]
    public void ScopeRejectsRequestOptionsDriftBeforeNetwork(string changed)
    {
        var options = Options(); options.ObserveNativeOutputs = true;
        var request = Build(options, "executor.move_to_tile");
        if (changed == "run") request.RunId += "2";
        if (changed == "save") request.SaveIsolationPath += "2";
        if (changed == "mode") request.ExecutionMode = "coop_companion";
        Assert.Throws<InvalidOperationException>(() => NativeOutputObservationMode.CreateScopeId(options, request));
    }

    [Theory]
    [InlineData("observed")]
    [InlineData("unavailable")]
    public async Task OriginalJournalAndDispatchRetainScopeAndIndependentObservationStatus(string status)
    {
        var options = Options(); options.ObserveNativeOutputs = true;
        options.Root = Path.Combine(Path.GetTempPath(), "stardewai-output-client-" + Guid.NewGuid().ToString("N"));
        var request = Build(options, "executor.move_to_tile");
        using var handler = new ObservationHandler(options, request, status);
        using var http = new HttpClient(handler);
        var method = ProgramMethod("PostJournaledExecutionRequestAsync");
        var result = await (Task<(JsonObject Execution, RuntimeExecutionRequestRecord Journal)>)method.Invoke(null,
            new object[] { http, options, 1, 1, request, Path.Combine(options.Root, "before.json") })!;
        Assert.Equal(1, handler.Calls);
        Assert.Equal("blocked", result.Execution["status"]!.GetValue<string>());
        Assert.Equal(status, result.Execution["action_origin_observation"]!["status"]!.GetValue<string>());
        Assert.False(result.Execution["action_origin_observation"]!["native_outcome_verified"]!.GetValue<bool>());
        var loaded = await RuntimeExecutionRequestJournal.ReadAsync(result.Journal.JournalPath);
        Assert.Equal(request.ActionOriginObservationScopeId,
            JsonSerializer.Deserialize<TrainingExecutionRequest>(loaded.RequestJson)!.ActionOriginObservationScopeId);
        Assert.Equal(request.RequestNonce, loaded.Record.RequestNonce);
    }

    private static LiveTrainingOptions Options() => new() { RunId = "original-output-client", SkipTraining = true };
    private static MethodInfo ProgramMethod(string name) => typeof(LiveTrainingOptions).Assembly.GetType("Program")!
        .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    private static TrainingExecutionRequest Build(LiveTrainingOptions options, string option) =>
        (TrainingExecutionRequest)ProgramMethod("BuildExecutionRequest").Invoke(null, new object[] { options,
            new JsonObject { ["queue_item_id"] = "original-item", ["option_id"] = option,
                ["normalized_command"] = new JsonObject { ["parameters"] = new JsonArray() } },
            new string('a', 64), "original-queue", "economy.earn_money" })!;

    private sealed class ObservationHandler(LiveTrainingOptions options, TrainingExecutionRequest request, string status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
        {
            Calls++;
            Assert.Equal("/api/v1/training/execute", message.RequestUri!.AbsolutePath);
            var journal = await RuntimeExecutionRequestJournal.ReadAsync(
                Directory.GetFiles(options.SnapshotDir, "execution-request-*.json").Single());
            Assert.Equal(await message.Content!.ReadAsStringAsync(token), journal.RequestJson);
            Assert.Equal(request.ActionOriginObservationScopeId,
                JsonSerializer.Deserialize<TrainingExecutionRequest>(journal.RequestJson)!.ActionOriginObservationScopeId);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new TrainingExecutionResult
            {
                Status = "blocked", ActionOriginObservation = new() { ScopeId = request.ActionOriginObservationScopeId!, Status = status }
            })) };
        }
    }
}
