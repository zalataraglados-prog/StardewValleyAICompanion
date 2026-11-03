using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;
using StardewAI.ProductExecutor;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeExecutionRequestJournalTests
{
    [Fact]
    public async Task JournalPreservesExactWireBodyAndProductHashInTwoSmallFiles()
    {
        var root = NewRoot();
        var request = Request();
        var raw = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
        var record = await RuntimeExecutionRequestJournal.WriteAsync(root, 7, 1, raw,
            "http://127.0.0.1:8768/api/v1/product/execute", Path.Combine(root, "before.json"));
        var loaded = await RuntimeExecutionRequestJournal.ReadAsync(record.JournalPath);

        Assert.Equal(raw, loaded.RequestJson);
        Assert.Equal(record, loaded.Record);
        Assert.Equal(request.RequestNonce, loaded.Record.RequestNonce);
        Assert.Equal(ProductReceiptStore.Sha256(JsonNode.Parse(raw)!.ToJsonString()), loaded.Record.ProductRequestSha256);
        Assert.Equal(2, Directory.GetFiles(root).Length);
        Assert.True(new FileInfo(record.JournalPath + ".gz").Length < record.RequestBytes);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("blob-path")]
    [InlineData("byte-count")]
    [InlineData("schema")]
    [InlineData("payload-hash")]
    [InlineData("gzip")]
    public async Task ReaderRejectsTamperedIdentityScopeAndPayload(string change)
    {
        var root = NewRoot();
        var record = await RuntimeExecutionRequestJournal.WriteAsync(root, 1, 1, JsonSerializer.Serialize(Request()),
            "http://127.0.0.1:8768/api/v1/product/execute", Path.Combine(root, "before.json"));
        var metadata = JsonNode.Parse(await File.ReadAllTextAsync(record.JournalPath))!.AsObject();
        switch (change)
        {
            case "nonce": metadata["request_nonce"] = Guid.NewGuid().ToString("N"); break;
            case "blob-path": metadata["blob_file_name"] = "../outside.json.gz"; break;
            case "byte-count": metadata["request_bytes"] = record.RequestBytes - 1; break;
            case "schema": metadata["schema_version"] = "forged"; break;
            case "payload-hash": metadata["product_request_sha256"] = new string('0', 64); break;
            case "gzip": await File.WriteAllBytesAsync(record.JournalPath + ".gz", new byte[] { 1, 2, 3 }); break;
        }
        await File.WriteAllTextAsync(record.JournalPath, metadata.ToJsonString());

        await Assert.ThrowsAnyAsync<InvalidDataException>(() => RuntimeExecutionRequestJournal.ReadAsync(record.JournalPath));
        if (change == "schema")
        {
            metadata.Remove("schema_version");
            await File.WriteAllTextAsync(record.JournalPath, metadata.ToJsonString());
            await Assert.ThrowsAnyAsync<InvalidDataException>(() => RuntimeExecutionRequestJournal.ReadAsync(record.JournalPath));
        }
    }

    [Fact]
    public async Task ActualDispatchHelperRetainsRequestBeforeLostResponseAndDoesNotRetry()
    {
        var options = new LiveTrainingOptions { Root = NewRoot(), RunId = "journal-test", UseProductExecutor = false };
        var request = Request();
        var handler = new JournalInspectingHandler(options, request, loseResponse: true);
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => Dispatch(http, options, request));

        Assert.Equal(1, handler.Calls);
        Assert.True(handler.JournalWasReadableBeforePost);
        var path = Directory.GetFiles(options.SnapshotDir, "execution-request-*.json").Single();
        Assert.Equal(request.RequestNonce, (await RuntimeExecutionRequestJournal.ReadAsync(path)).Record.RequestNonce);
    }

    [Fact]
    public async Task ActualDispatchHelperStampsJournalAndRefusesExistingOrdinalBeforeHttp()
    {
        var options = new LiveTrainingOptions { Root = NewRoot(), RunId = "journal-test", UseProductExecutor = true };
        var request = Request();
        var handler = new JournalInspectingHandler(options, request, loseResponse: false);
        using var http = new HttpClient(handler);
        var result = await Dispatch(http, options, request);

        Assert.Equal(result.Journal.JournalPath, result.Execution["execution_request_journal_path"]!.GetValue<string>());
        Assert.Equal(request.RequestNonce, result.Execution["execution_request_nonce"]!.GetValue<string>());
        await Assert.ThrowsAsync<IOException>(() => Dispatch(http, options, Request()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task InvalidOrdinalOrOversizedRequestCannotCreateAJournal()
    {
        var root = NewRoot();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RuntimeExecutionRequestJournal.WriteAsync(root, 1, 0, "{}", "unused", "before.json"));
        await Assert.ThrowsAsync<InvalidDataException>(() => RuntimeExecutionRequestJournal.WriteAsync(root, 1, 1,
            new string('x', RuntimeExecutionRequestJournal.MaximumRequestBytes + 1), "unused", "before.json"));
        Assert.Empty(Directory.GetFiles(root));
    }

    internal static async Task<(JsonObject Execution, RuntimeExecutionRequestRecord Journal)> Dispatch(
        HttpClient http, LiveTrainingOptions options, TrainingExecutionRequest request)
    {
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program")!.GetMethod(
            "PostJournaledExecutionRequestAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        return await (Task<(JsonObject, RuntimeExecutionRequestRecord)>)method.Invoke(null,
            new object[] { http, options, 7, 1, request, Path.Combine(options.Root, "before.json") })!;
    }

    private static TrainingExecutionRequest Request() => new()
    {
        RunId = "journal-test", QueueId = "queue-1", QueueItemId = "item-1", BeforeStateHash = new string('a', 64),
        OptionId = "executor.wait_ticks", RequestNonce = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow.ToString("O")
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-request-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class JournalInspectingHandler(LiveTrainingOptions options, TrainingExecutionRequest request, bool loseResponse) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool JournalWasReadableBeforePost { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            var path = Directory.GetFiles(options.SnapshotDir, "execution-request-*.json").Single();
            var loaded = await RuntimeExecutionRequestJournal.ReadAsync(path);
            Assert.Equal(await message.Content!.ReadAsStringAsync(cancellationToken), loaded.RequestJson);
            Assert.Equal(request.RequestNonce, loaded.Record.RequestNonce);
            JournalWasReadableBeforePost = true;
            if (loseResponse) throw new HttpRequestException("Synthetic lost response after dispatch boundary.");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"applied\"}") };
        }
    }
}
