using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class MoneyCropTeacherLedgerBridgeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacySettlementKeepsOriginalJournalAndLookupContract(bool lostReply)
    {
        using var files = new LedgerBridgeFiles();
        var request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = "legacy-state", ExpectedLedgerRevision = 3,
            PortfolioId = "legacy-portfolio", RouteSourceDecisionId = "legacy-route",
            SupportingTransitionReceiptSha256 = new string('a', 64)
        };
        using var handler = new LegacySettlementHandler(request, lostReply);
        using var http = new HttpClient(handler);
        var options = new LiveTrainingOptions { Root = files.Root, RunId = "rpc", BackendUrl = "http://test" };
        var diagnostic = new JsonObject();
        var task = (Task<JsonObject>)typeof(LiveTrainingOptions).Assembly.GetType("Program")!
            .GetMethod("PostJournaledPortfolioSettlementAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { http, options, diagnostic, request })!;
        var reply = await task;
        Assert.True(reply["accepted"]!.GetValue<bool>());
        Assert.Equal(1, handler.Mutations);
        Assert.Equal(lostReply ? 1 : 0, handler.Lookups);
        Assert.True(diagnostic.ContainsKey("material_reservation_settlement_request_journal_path"));
        Assert.True(diagnostic.ContainsKey("material_reservation_settlement_request_sha256"));
        Assert.DoesNotContain(diagnostic, row => row.Key.StartsWith("crop_teacher_ledger_commit_", StringComparison.Ordinal));
        var journal = JsonNode.Parse(File.ReadAllText(diagnostic["material_reservation_settlement_request_journal_path"]!.GetValue<string>()))!;
        Assert.Equal("settle-supporting-transition", journal["operation"]!.GetValue<string>());
        if (lostReply) Assert.True(diagnostic["material_reservation_settlement_reply_recovered"]!.GetValue<bool>());
    }

    private sealed class LegacySettlementHandler(ReservationPortfolioSupportingTransitionSettlementRequest request,
        bool lostReply) : HttpMessageHandler
    {
        public int Mutations { get; private set; }
        public int Lookups { get; private set; }
        private JsonObject? original;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
        {
            var body = JsonNode.Parse(await message.Content!.ReadAsStringAsync(token))!.AsObject();
            var reply = new JsonObject
            {
                ["accepted"] = true, ["portfolio_id"] = request.PortfolioId,
                ["route_source_decision_id"] = request.RouteSourceDecisionId,
                ["supporting_transition_receipt_sha256"] = request.SupportingTransitionReceiptSha256,
                ["committed_ledger_revision"] = 4, ["errors"] = new JsonArray(),
                ["ledger"] = new JsonObject { ["revision"] = 4, ["source_state_hash"] = request.StateHash }
            };
            if (message.RequestUri!.AbsolutePath.EndsWith("/settle-supporting-transition", StringComparison.Ordinal))
            {
                Mutations++;
                original = body;
                if (lostReply) throw new HttpRequestException("Legacy reply lost.");
            }
            else
            {
                Lookups++;
                Assert.EndsWith("/lookup-mutation", message.RequestUri.AbsolutePath);
                Assert.Equal("settle-supporting-transition", body["operation"]!.GetValue<string>());
                Assert.True(JsonNode.DeepEquals(original, body["request"]));
                reply = new JsonObject
                {
                    ["schema_version"] = "stardewai.portfolio_mutation_lookup.v1", ["status"] = "found",
                    ["execution_performed"] = false, ["ledger_mutation_performed"] = false,
                    ["native_outcome_verified"] = false, ["formal_product_training"] = false,
                    ["requires_independent_verification"] = true, ["reply"] = reply
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply.ToJsonString(JsonOptions), Encoding.UTF8, "application/json")
            };
        }
    }
}
