using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;
using StardewAI.ProductExecutor;

namespace StardewAI.Backend.Tests;

public sealed class ProductReceiptRecoveryCommandTests
{
    [Fact]
    public async Task RealHttpRouteAndCliRecoverStoredResultWithoutStartingTheTrainingLoop()
    {
        var root = NewRoot();
        var options = new ProductExecutorOptions
        {
            ListenUrl = "http://127.0.0.1:0", JournalRoot = Path.Combine(root, "product-journal"),
            AllowedSaveRoot = Path.Combine(root, "isolated-saves"), RequiredRunId = "recovery-test"
        };
        await using var app = StardewAI.ProductExecutor.Program.BuildApplication(
            new[] { "--Logging:LogLevel:Default", "None" }, options);
        var queryCount = 0;
        app.Use(async (context, next) => { Interlocked.Increment(ref queryCount); await next(context); });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var request = Request(options);
            var raw = JsonSerializer.Serialize(request);
            var hash = ProductReceiptStore.Sha256(JsonNode.Parse(raw)!.ToJsonString());
            var store = app.Services.GetRequiredService<ProductReceiptStore>();
            await store.WriteFinalAsync(request, hash, new JsonObject
            {
                ["status"] = "applied", ["primitive_verification_status"] = "verified",
                ["run_id"] = request.RunId, ["queue_id"] = request.QueueId, ["queue_item_id"] = request.QueueItemId
            }, default);
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(Path.Combine(root, "requests"), 1, 1, raw,
                address + "/api/v1/product/execute", Path.Combine(root, "before.json"));
            var output = Path.Combine(root, "lookup.json");
            var first = await RunCli(journal.JournalPath, address, output);
            Assert.Equal(0, first.ExitCode);
            var observation = JsonNode.Parse(await File.ReadAllTextAsync(output))!.AsObject();
            Assert.Equal("final", observation["lookup_response"]!["status"]!.GetValue<string>());
            Assert.Equal("applied", observation["lookup_response"]!["response"]!["status"]!.GetValue<string>());
            Assert.False(observation["native_outcome_verified"]!.GetValue<bool>());
            Assert.False(observation["execution_authorized"]!.GetValue<bool>());
            Assert.Equal(1, queryCount);
            Assert.Single(Directory.GetFiles(options.JournalRoot));
            var before = await File.ReadAllBytesAsync(output);
            var repeat = await RunCli(journal.JournalPath, address, output);
            Assert.Equal(2, repeat.ExitCode);
            Assert.Contains("refusing overwrite", repeat.Error, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllBytesAsync(output));
            Assert.Equal(1, queryCount);
            using var client = new HttpClient();
            foreach (var malformed in new[] { "[]", "null", "{broken" })
            {
                using var response = await client.PostAsync(address + "/api/v1/product/receipts/lookup",
                    new StringContent(malformed, Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            Assert.Single(Directory.GetFiles(options.JournalRoot));
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData("foreign-endpoint")]
    [InlineData("foreign-hash")]
    [InlineData("execution-permission")]
    [InlineData("dispatch-performed")]
    [InlineData("no-fresh-verification")]
    public async Task RecoveryRejectsEndpointDriftOrAnUnboundAuthorizingResponse(string mutation)
    {
        var root = NewRoot();
        var request = Request(new ProductExecutorOptions { AllowedSaveRoot = Path.Combine(root, "saves") });
        var raw = JsonSerializer.Serialize(request);
        var record = await RuntimeExecutionRequestJournal.WriteAsync(root, 1, 1, raw,
            "http://127.0.0.1:8768/api/v1/product/execute", Path.Combine(root, "before.json"));
        var response = new JsonObject
        {
            ["schema_version"] = "stardewai.product_receipt_lookup.v1", ["status"] = "final",
            ["request_sha256"] = record.ProductRequestSha256,
            ["native_dispatch_performed"] = mutation == "dispatch-performed",
            ["execution_authorized"] = mutation == "execution-permission",
            ["fresh_verification_required"] = mutation != "no-fresh-verification"
        };
        if (mutation == "foreign-hash") response["request_sha256"] = new string('b', 64);
        var handler = new LookupHandler(response);
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() => ProductReceiptRecoveryCommand.LookupAsync(http,
            record.JournalPath, mutation == "foreign-endpoint" ? "http://127.0.0.1:8769" : "http://127.0.0.1:8768"));

        Assert.Equal(mutation == "foreign-endpoint" ? 0 : 1, handler.Calls);
        Assert.All(handler.Paths, path => Assert.Equal("/api/v1/product/receipts/lookup", path));
    }

    [Fact]
    public async Task PendingLookupDoesNotWaitForTheNativeDispatchLockOrChangeTheJournal()
    {
        var root = NewRoot();
        var options = new ProductExecutorOptions { JournalRoot = root, AllowedSaveRoot = Path.Combine(root, "saves") };
        var request = Request(options);
        var raw = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        var hash = ProductReceiptStore.Sha256(raw.ToJsonString());
        var store = new ProductReceiptStore(options);
        await store.WritePendingAsync(request, raw, hash, request.BeforeStateHash, 1, default);
        using var http = new HttpClient(new LookupHandler(new JsonObject()));
        var service = new ProductExecutionService(options, new ProductExecutionPolicy(options), store, http);
        var gate = (SemaphoreSlim)typeof(ProductExecutionService).GetField("dispatchLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await gate.WaitAsync();
        try
        {
            var result = await service.LookupReceiptAsync(raw, default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("pending", result["status"]!.GetValue<string>());
            Assert.Equal(ProductReceiptState.Pending, (await store.ReadAsync(request, hash, default)).State);
            Assert.Single(Directory.GetFiles(root));
        }
        finally { gate.Release(); }
    }

    private static async Task<(int ExitCode, string Error)> RunCli(string journal, string url, string output)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var value in new[] { typeof(LiveTrainingOptions).Assembly.Location, "lookup-product-receipt",
                     "--request-journal", journal, "--product-url", url, "--output", output }) start.ArgumentList.Add(value);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        await stdout;
        return (process.ExitCode, await stderr);
    }

    private static TrainingExecutionRequest Request(ProductExecutorOptions options) => new()
    {
        RunId = "recovery-test", QueueId = "queue-1", QueueItemId = "item-1", BeforeStateHash = new string('a', 64),
        OptionId = "executor.wait_ticks", RequestNonce = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
        ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
        Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer).ActorId,
        SaveIsolationPath = options.AllowedSaveRoot
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-recovery-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class LookupHandler(JsonObject response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string> Paths { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            Paths.Add(message.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToJsonString(), Encoding.UTF8, "application/json")
            });
        }
    }
}
