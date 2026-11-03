using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.ProductExecutor;

namespace StardewAI.Backend.Tests;

public sealed partial class ProductExecutorTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("pending")]
    [InlineData("final")]
    [InlineData("conflict")]
    [InlineData("corrupt")]
    public async Task ReceiptLookupIsReadOnlyAndCannotDispatchOrResolvePending(string state)
    {
        var options = Options("run-1", Path.Combine(Path.GetTempPath(), "stardewai-lookup-" + Guid.NewGuid().ToString("N")));
        var request = Request(options, "run-1");
        request.CreatedAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var raw = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        var hash = ProductReceiptStore.Sha256(raw.ToJsonString());
        var store = new ProductReceiptStore(options);
        if (state is "pending" or "conflict")
            await store.WritePendingAsync(request, raw, hash, request.BeforeStateHash, 10, default);
        if (state is "final" or "corrupt")
            await store.WriteFinalAsync(request, hash, new JsonObject { ["status"] = "applied" }, default);
        if (state == "corrupt")
            await File.WriteAllTextAsync(Path.Combine(options.JournalRoot, store.ReceiptId(request) + ".final.json"), "{");
        if (state == "conflict") raw["wait_ticks"] = 999;
        var before = Files(options.JournalRoot);
        var handler = new ProductHttpHandler(request, new string('b', 64), new string('c', 64));
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), store, new HttpClient(handler));

        var result = await service.LookupReceiptAsync(raw, default);

        Assert.Equal(state, result["status"]!.GetValue<string>());
        Assert.False(result["native_dispatch_performed"]!.GetValue<bool>());
        Assert.False(result["execution_authorized"]!.GetValue<bool>());
        Assert.True(result["fresh_verification_required"]!.GetValue<bool>());
        Assert.Equal(before, Files(options.JournalRoot));
        Assert.Equal(0, handler.NativeDispatchCount);
        Assert.Equal(0, handler.SnapshotReadCount);
        if (state == "final")
        {
            Assert.Equal("applied", result["response"]!["status"]!.GetValue<string>());
            result["response"]!["status"] = "edited";
            Assert.Equal("applied", (await store.ReadAsync(request, hash, default)).Response!["status"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("null-option")]
    [InlineData("null-hash")]
    [InlineData("foreign-run")]
    [InlineData("foreign-save")]
    [InlineData("debug-option")]
    [InlineData("malformed-number")]
    public async Task ReceiptLookupRetainsSharedAuthorizationAndMalformedRequestBoundary(string mutation)
    {
        var options = Options("run-1", Path.Combine(Path.GetTempPath(), "stardewai-lookup-rejection-" + Guid.NewGuid().ToString("N")));
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        switch (mutation)
        {
            case "null-option": raw["option_id"] = null; break;
            case "null-hash": raw["before_state_hash"] = null; break;
            case "foreign-run": raw["run_id"] = "foreign"; break;
            case "foreign-save": raw["save_isolation_path"] = Path.Combine(options.AllowedSaveRoot, "nested"); break;
            case "debug-option": raw["option_id"] = "debug.setup_museum_donation"; break;
            case "malformed-number": raw["wait_ticks"] = "not-a-number"; break;
        }
        var handler = new ProductHttpHandler(request, new string('b', 64), new string('c', 64));
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), new ProductReceiptStore(options), new HttpClient(handler));

        var result = await service.LookupReceiptAsync(raw, default);

        Assert.Equal("rejected", result["status"]!.GetValue<string>());
        Assert.NotEmpty(result["block_reasons"]!.AsArray());
        Assert.Empty(Directory.GetFiles(options.JournalRoot));
        Assert.Equal(0, handler.NativeDispatchCount);
        Assert.Equal(0, handler.SnapshotReadCount);
        if (mutation is "null-option" or "null-hash")
            Assert.Equal("blocked", (await service.ExecuteAsync(raw, default))["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task LostClientAfterDurableIntentCannotCancelTheFinalNativeJournal()
    {
        var options = Options("run-1", Path.Combine(Path.GetTempPath(), "stardewai-lookup-disconnect-" + Guid.NewGuid().ToString("N")));
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        using var clientCancellation = new CancellationTokenSource();
        var handler = new ProductHttpHandler(request, new string('b', 64), new string('c', 64))
        {
            OnNativeDispatch = () => clientCancellation.Cancel()
        };
        var store = new ProductReceiptStore(options);
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), store, new HttpClient(handler));

        var completed = await service.ExecuteAsync(raw, clientCancellation.Token);
        var recovered = await service.LookupReceiptAsync(raw, default);

        Assert.True(clientCancellation.IsCancellationRequested);
        Assert.Equal("applied", completed["status"]!.GetValue<string>());
        Assert.Equal("final", recovered["status"]!.GetValue<string>());
        Assert.Equal("applied", recovered["response"]!["status"]!.GetValue<string>());
        Assert.Equal(1, handler.NativeDispatchCount);
        Assert.Equal(2, handler.SnapshotReadCount);
    }

    [Fact]
    public async Task CancellationBeforeIntentCannotDispatchANativeAction()
    {
        var options = Options("run-1", Path.Combine(Path.GetTempPath(), "stardewai-lookup-cancel-before-" + Guid.NewGuid().ToString("N")));
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new ProductHttpHandler(request, new string('b', 64), new string('c', 64));
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), new ProductReceiptStore(options), new HttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(raw, cancellation.Token));
        Assert.Empty(Directory.GetFiles(options.JournalRoot));
        Assert.Equal(0, handler.NativeDispatchCount);
        Assert.Equal(0, handler.SnapshotReadCount);
    }

    [Fact]
    public async Task ServiceOwnedDeadlinePersistsIndeterminateResultWithoutNativeReplay()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-lookup-deadline-" + Guid.NewGuid().ToString("N"));
        var options = new ProductExecutorOptions
        {
            JournalRoot = root, AllowedSaveRoot = Path.Combine(root, "saves"), RequiredRunId = "run-1", NativeTimeoutSeconds = 1
        };
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        var handler = new DeadlineHandler();
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), new ProductReceiptStore(options), new HttpClient(handler));

        var result = await service.ExecuteAsync(raw, default);
        var lookup = await service.LookupReceiptAsync(raw, default);

        Assert.Equal("blocked", result["status"]!.GetValue<string>());
        Assert.Contains("native_dispatch_indeterminate_no_replay", result["block_reasons"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Equal("final", lookup["status"]!.GetValue<string>());
        Assert.Equal("blocked", lookup["response"]!["status"]!.GetValue<string>());
        Assert.Equal(1, handler.DispatchCount);
    }

    [Theory]
    [InlineData("final")]
    [InlineData("pending")]
    [InlineData("foreign-result")]
    [InlineData("lookup-unavailable")]
    public async Task LiveDispatchReadsOneStoredProductReplyWithoutRedispatching(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-live-lookup-" + Guid.NewGuid().ToString("N"));
        var options = Options("run-1", Path.Combine(root, "product-journal"));
        var request = Request(options, "run-1");
        var native = new ProductHttpHandler(request, request.BeforeStateHash, new string('c', 64));
        var store = new ProductReceiptStore(options);
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), store, new HttpClient(native));
        var gateway = new LostReplyGateway(service, store, mode);
        using var http = new HttpClient(gateway);
        var live = new LiveTrainingOptions { Root = root, RunId = "run-1", UseProductExecutor = true, ExecutorUrl = "http://product.invalid" };

        if (mode == "final")
        {
            var recovered = await RuntimeExecutionRequestJournalTests.Dispatch(http, live, request);
            Assert.Equal("applied", recovered.Execution["status"]!.GetValue<string>());
            Assert.True(recovered.Execution["product_receipt_recovered_by_lookup"]!.GetValue<bool>());
            Assert.True(File.Exists(recovered.Execution["product_receipt_lookup_path"]!.GetValue<string>()));
        }
        else
        {
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => RuntimeExecutionRequestJournalTests.Dispatch(http, live, request));
            Assert.NotNull(error.Data["product_receipt_lookup_failure"]);
        }
        Assert.Equal(1, gateway.ExecuteCalls);
        Assert.Equal(1, gateway.LookupCalls);
        Assert.Equal(mode == "pending" ? 0 : 1, native.NativeDispatchCount);
        var observation = JsonNode.Parse(await File.ReadAllTextAsync(Directory.GetFiles(live.SnapshotDir, "execution-lookup-*.json").Single()))!;
        Assert.False(observation["native_outcome_verified"]!.GetValue<bool>());
    }

    private sealed class LostReplyGateway(ProductExecutionService service, ProductReceiptStore store, string mode) : HttpMessageHandler
    {
        public int ExecuteCalls { get; private set; }
        public int LookupCalls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            var raw = JsonNode.Parse(await message.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            if (message.RequestUri!.AbsolutePath == "/api/v1/product/execute")
            {
                ExecuteCalls++;
                if (mode == "pending")
                {
                    var typed = raw.Deserialize<StardewAI.Contracts.Training.TrainingExecutionRequest>()!;
                    await store.WritePendingAsync(typed, raw, ProductReceiptStore.Sha256(raw.ToJsonString()), typed.BeforeStateHash, 1, default);
                }
                else { await service.ExecuteAsync(raw, default); }
                throw new HttpRequestException("Synthetic lost Product reply.");
            }
            Assert.Equal("/api/v1/product/receipts/lookup", message.RequestUri.AbsolutePath);
            LookupCalls++;
            if (mode == "lookup-unavailable") throw new HttpRequestException("Synthetic unavailable receipt lookup.");
            var result = await service.LookupReceiptAsync(raw, cancellationToken);
            if (mode == "foreign-result") result["response"]!["queue_item_id"] = "foreign";
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(result.ToJsonString(), System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class DeadlineHandler : HttpMessageHandler
    {
        public int DispatchCount { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            if (message.Method == HttpMethod.Get)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(new JsonObject
                    {
                        ["schema_version"] = "snapshot.v1", ["state_hash"] = new string('a', 64), ["game_tick"] = 1
                    }.ToJsonString())
                };
            DispatchCount++;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Native dispatch deadline was not enforced.");
        }
    }

    private static string[] Files(string directory) => Directory.GetFiles(directory)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => path + ":" + ProductReceiptStore.Sha256(File.ReadAllText(path))).ToArray();
}
