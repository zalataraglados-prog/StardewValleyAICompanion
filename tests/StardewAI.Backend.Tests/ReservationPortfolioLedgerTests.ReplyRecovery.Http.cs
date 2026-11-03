using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData("commit")]
    [InlineData("settle-completed-route")]
    [InlineData("settle-supporting-transition")]
    public async Task LostPortfolioHttpReplyIsRetrievedFromActualRepositoryWithoutSecondMutation(string operation)
    {
        var root = NewPortfolioReplyRoot();
        await using var app = PortfolioReplyApplication(root);
        using var setup = app.CreateClient();
        var store = app.Services.GetRequiredService<StateStore>();
        var snapshot = Snapshot();
        store.Snapshots[snapshot.StateHash] = snapshot;
        object request = Request(snapshot, 1, 300);
        if (operation != "commit")
        {
            await PostPortfolioAccepted(setup, "commit", request);
            request = SettlementRequest(snapshot, 1);
            if (operation == "settle-supporting-transition")
            {
                await PostPortfolioAccepted(setup, "commit", new ReservationPortfolioCommitRequest
                {
                    StateHash = snapshot.StateHash, ExpectedLedgerRevision = 1,
                    PortfolioId = "support:route-a", GoalId = "goal.grandpa_21", SourceDecisionId = "support:route-a"
                });
                request = SupportingSettlementRequest(snapshot, 2);
            }
        }
        using var transport = new LostPortfolioReplyHandler(app.Server.CreateHandler(), operation);
        using var client = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
        await Assert.ThrowsAsync<HttpRequestException>(() => PostPortfolioAccepted(client, operation, request));
        var auditCount = store.Audit.Count;
        var path = Assert.Single(Directory.GetFiles(root));
        var bytes = File.ReadAllBytes(path);

        using var lookupResponse = await client.PostAsync(PortfolioEndpoint + "lookup-mutation", ReplyContent(new
        {
            operation, request
        }));
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        var lookup = JsonNode.Parse(await lookupResponse.Content.ReadAsStringAsync())!;
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.True(lookup["reply"]!["accepted"]!.GetValue<bool>());
        Assert.Equal(operation == "commit" ? 1 : operation == "settle-completed-route" ? 2 : 3,
            lookup["reply"]!["ledger"]!["revision"]!.GetValue<int>());
        Assert.Equal(1, transport.MutationCalls);
        Assert.Equal(1, transport.LookupCalls);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(auditCount, store.Audit.Count);
        var persistedReply = JsonNode.Parse(bytes)!["_last_portfolio_reply"]!["reply"]!;
        var retrievedCompact = lookup["reply"]!.DeepClone().AsObject();
        retrievedCompact.Remove("ledger");
        Assert.True(JsonNode.DeepEquals(persistedReply, retrievedCompact));
    }

    [Fact]
    public async Task PortfolioLookupUsesSameWebRequestParsingAsMutationEndpoint()
    {
        await using var app = PortfolioReplyApplication(NewPortfolioReplyRoot());
        using var client = app.CreateClient();
        var snapshot = Snapshot();
        app.Services.GetRequiredService<StateStore>().Snapshots[snapshot.StateHash] = snapshot;
        var request = JsonSerializer.SerializeToNode(Request(snapshot, 1, 300), ReplyJson)!.AsObject();
        request["STATE_HASH"] = request["state_hash"]!.DeepClone();
        request.Remove("state_hash");
        request["expected_ledger_revision"] = "0";
        using var committed = await client.PostAsync(PortfolioEndpoint + "commit",
            new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        using var lookup = await client.PostAsync(PortfolioEndpoint + "lookup-mutation", ReplyContent(new
        {
            operation = "commit", request
        }));
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        Assert.Equal("found", JsonNode.Parse(await lookup.Content.ReadAsStringAsync())!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task PortfolioLookupRequiresIngestedOriginalStateAndValidRequest()
    {
        await using var app = PortfolioReplyApplication(NewPortfolioReplyRoot());
        using var client = app.CreateClient();
        foreach (var body in new[]
        {
            "{}", "{\"operation\":\"commit\",\"request\":null}",
            "{\"operation\":\"commit\",\"request\":{\"state_hash\":\"unknown\"}}",
            "{\"operation\":\"unsupported\",\"request\":{\"state_hash\":\"unknown\"}}"
        })
        {
            using var response = await client.PostAsync(PortfolioEndpoint + "lookup-mutation",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.Equal("rejected", result["status"]!.GetValue<string>());
            Assert.False(result["ledger_mutation_performed"]!.GetValue<bool>());
        }
    }

    private const string PortfolioEndpoint = "/api/v1/strategy/commitments/reservation-portfolios/";

    private static WebApplicationFactory<global::Program> PortfolioReplyApplication(string root) =>
        new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStrategyCommitmentRepository>();
            services.AddSingleton<IStrategyCommitmentRepository>(new FileStrategyCommitmentRepository(root));
        }));

    private static StringContent ReplyContent(object value) => new(
        JsonSerializer.Serialize(value, value.GetType(), ReplyJson), Encoding.UTF8, "application/json");

    private static async Task PostPortfolioAccepted(HttpClient client, string operation, object request)
    {
        using var response = await client.PostAsync(PortfolioEndpoint + operation, ReplyContent(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(result["accepted"]!.GetValue<bool>());
    }

    private sealed class LostPortfolioReplyHandler(HttpMessageHandler inner, string operation) : DelegatingHandler(inner)
    {
        public int MutationCalls { get; private set; }
        public int LookupCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/lookup-mutation", StringComparison.Ordinal))
            {
                LookupCalls++;
                return await base.SendAsync(request, token);
            }
            Assert.EndsWith("/" + operation, request.RequestUri.AbsolutePath);
            MutationCalls++;
            var response = await base.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
            throw new HttpRequestException("test transport lost accepted reply");
        }
    }
}
