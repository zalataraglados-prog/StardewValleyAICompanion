using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

internal sealed class LivePortfolioSettlementTestHandler(HttpMessageHandler inner, string mode, string journalDirectory)
    : DelegatingHandler(inner)
{
    public int MutationCalls { get; private set; }
    public int LookupCalls { get; private set; }
    private JsonNode? originalRequest;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get)
        {
            Assert.EndsWith("/strategy/commitments/latest", path);
            return await base.SendAsync(request, token);
        }
        if (path.EndsWith("/lookup-mutation", StringComparison.Ordinal))
        {
            LookupCalls++;
            var lookup = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!;
            Assert.True(JsonNode.DeepEquals(originalRequest, lookup["request"]));
            Assert.Equal("settle-supporting-transition", lookup["operation"]!.GetValue<string>());
            if (mode == "unavailable") return Reply(HttpStatusCode.ServiceUnavailable, "{}");
            if (mode == "cancelled") throw new TaskCanceledException("test lookup cancelled");
            var response = await base.SendAsync(request, token);
            if (mode is not ("not-found" or "wrong-reply" or "claims-write")) return response;
            var value = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
            response.Dispose();
            if (mode == "not-found") { value["status"] = "not_found"; value["reply"] = null; }
            if (mode == "wrong-reply") value["reply"]!["route_source_decision_id"] = "foreign-request";
            if (mode == "claims-write") value["ledger_mutation_performed"] = true;
            return Reply(HttpStatusCode.OK, value.ToJsonString());
        }
        Assert.EndsWith("/reservation-portfolios/settle-supporting-transition", path);
        MutationCalls++;
        var raw = await request.Content!.ReadAsStringAsync(token);
        originalRequest = JsonNode.Parse(raw);
        var journalPath = Assert.Single(Directory.GetFiles(journalDirectory, "*-rpc-journal.json"));
        var journal = JsonNode.Parse(File.ReadAllText(journalPath))!;
        var requestPath = Path.Combine(journalDirectory, journal["request_file"]!.GetValue<string>());
        Assert.Equal("stardewai.portfolio_rpc_journal.v1", journal["schema_version"]!.GetValue<string>());
        Assert.Equal(raw, File.ReadAllText(requestPath));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(),
            journal["request_sha256"]!.GetValue<string>());
        if (mode == "http-rejected") return Reply(HttpStatusCode.Conflict, "{\"errors\":[\"ledger_revision_conflict\"]}");
        if (mode == "http-rejected-bad-body") return new(HttpStatusCode.Conflict) { Content = new BrokenBody() };
        var result = await base.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        if (mode == "normal") return result;
        result.Dispose();
        if (mode == "request-drift") File.WriteAllText(requestPath, "{}");
        if (mode == "journal-drift") File.WriteAllText(journalPath, "{}");
        if (mode == "http-server-error") return Reply(HttpStatusCode.InternalServerError, "reply failed after publish");
        if (mode == "malformed-reply") return Reply(HttpStatusCode.OK, "{");
        if (mode == "slow-body") return new(HttpStatusCode.OK) { Content = new SlowBody() };
        throw new HttpRequestException("test accepted mutation reply lost");
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class BrokenBody : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new IOException("test rejection body interrupted"));
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class SlowBody : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.Delay(Timeout.InfiniteTimeSpan);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token) =>
            Task.Delay(Timeout.InfiniteTimeSpan, token);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
