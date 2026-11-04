using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedTransferSurvivesRestartAndSnapshotEvictionAndUsesOriginalReplyRecovery(bool losePreparationReply)
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var root = NewPortfolioReplyRoot();
        ReservationPortfolioCommitResult? prepared;
        await using (var setup = PortfolioReplyApplication(root))
        {
            using var client = setup.CreateClient();
            setup.Services.GetRequiredService<StateStore>().StoreSnapshot(fixture.Before);
            foreach (var request in new[]
                     {
                         PreparedMaterialTransferTestFixture.RouteRequest(fixture.Before),
                         PreparedMaterialTransferTestFixture.OtherRequest(fixture.Before)
                     })
            {
                var response = await client.PostAsJsonAsync(PortfolioEndpoint + "commit", request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            if (losePreparationReply)
            {
                using var transport = new LostPortfolioReplyHandler(setup.Server.CreateHandler(), "commit");
                using var lost = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
                await Assert.ThrowsAsync<HttpRequestException>(() => PostPortfolioAccepted(lost, "commit", fixture.PreparationRequest));
                using var recoveredResponse = await client.PostAsync(PortfolioEndpoint + "lookup-mutation",
                    ReplyContent(new { operation = "commit", request = fixture.PreparationRequest }));
                Assert.Equal(HttpStatusCode.OK, recoveredResponse.StatusCode);
                var recovered = JsonNode.Parse(await recoveredResponse.Content.ReadAsStringAsync())!;
                Assert.Equal("found", recovered["status"]!.GetValue<string>());
                Assert.False(recovered["execution_performed"]!.GetValue<bool>());
                prepared = recovered["reply"]!.Deserialize<ReservationPortfolioCommitResult>();
                Assert.Equal(1, transport.MutationCalls);
            }
            else
            {
                var preparation = await client.PostAsJsonAsync(PortfolioEndpoint + "commit", fixture.PreparationRequest);
                Assert.Equal(HttpStatusCode.OK, preparation.StatusCode);
                prepared = await preparation.Content.ReadFromJsonAsync<ReservationPortfolioCommitResult>();
            }
        }
        Assert.NotNull(prepared!.Ledger!.PreparedMaterialTransfer);
        var path = Assert.Single(Directory.GetFiles(root));
        var preparationBytes = File.ReadAllBytes(path);
        Assert.True(JsonNode.Parse(preparationBytes)!.AsObject().ContainsKey("prepared_material_transfer"));

        await using var app = PortfolioReplyApplication(root);
        using var http = app.CreateClient();
        var store = app.Services.GetRequiredService<StateStore>();
        store.StoreSnapshot(fixture.Before);
        store.StoreSnapshot(new SnapshotEnvelope { StateHash = "synthetic-intermediate-for-retention-test", GameTick = 150 });
        store.StoreSnapshot(fixture.After);
        Assert.Equal(2, store.Snapshots.Count);
        Assert.False(store.Snapshots.ContainsKey(fixture.Before.StateHash));
        var settlement = fixture.Settlement();
        settlement.PreparedMaterialTransferSha256 = prepared.Ledger.PreparedMaterialTransfer!.BindingSha256;

        var responseAfterRestart = await http.PostAsJsonAsync(
            PortfolioEndpoint + "settle-supporting-transition", settlement);

        Assert.Equal(HttpStatusCode.OK, responseAfterRestart.StatusCode);
        var result = await responseAfterRestart.Content.ReadFromJsonAsync<ReservationPortfolioSupportingTransitionSettlementResult>();
        Assert.True(result!.Accepted);
        Assert.Equal(4, result.CommittedLedgerRevision);
        Assert.Null(result.Ledger!.PreparedMaterialTransfer);
        Assert.Equal(3, result.MaterialTransferReservations!.ReservationTransitions.Length);
        var publishedBytes = File.ReadAllBytes(path);
        var persisted = JsonNode.Parse(publishedBytes)!.AsObject();
        Assert.False(persisted.ContainsKey("prepared_material_transfer"));
        Assert.False(JsonNode.DeepEquals(JsonNode.Parse(preparationBytes), persisted));

        var restarted = new FileStrategyCommitmentRepository(root);
        var lookup = restarted.LookupPortfolioMutationReply(fixture.After, "settle-supporting-transition", ReplyElement(settlement));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(result, ReplyJson), lookup["reply"]));
        Assert.False(lookup["execution_performed"]!.GetValue<bool>());
        Assert.False(lookup["ledger_mutation_performed"]!.GetValue<bool>());
        Assert.False(lookup["native_outcome_verified"]!.GetValue<bool>());
        Assert.Equal(publishedBytes, File.ReadAllBytes(path));
        var repeated = await http.PostAsJsonAsync(
            PortfolioEndpoint + "settle-supporting-transition", settlement);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(publishedBytes, File.ReadAllBytes(path));
    }
}
