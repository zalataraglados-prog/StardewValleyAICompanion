using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public async Task LiveTransferDispatchUsesActualPostedBindingAfterMovementWithoutRevisingTheLedger()
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var root = NewPortfolioReplyRoot();
        await using var app = PortfolioReplyApplication(root);
        using var client = app.CreateClient();
        var store = app.Services.GetRequiredService<StateStore>();
        store.StoreSnapshot(fixture.Before);
        ReservationPortfolioCommitResult? committed = null;
        foreach (var request in new[]
                 {
                     PreparedMaterialTransferTestFixture.RouteRequest(fixture.Before),
                     PreparedMaterialTransferTestFixture.OtherRequest(fixture.Before), fixture.PreparationRequest
                 })
        {
            using var response = await client.PostAsJsonAsync(PortfolioEndpoint + "commit", request);
            response.EnsureSuccessStatusCode();
            committed = await response.Content.ReadFromJsonAsync<ReservationPortfolioCommitResult>();
        }
        var path = Assert.Single(Directory.GetFiles(root));
        var ledgerBytes = File.ReadAllBytes(path);
        var moved = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(fixture.Before))!;
        moved.GameTick++;
        moved.State["time"] = JsonSerializer.SerializeToElement(new { time_of_day = new { value = 620, status = "available" } });
        moved.StateHash = SnapshotHash.ComputeStateHash(moved.State);
        store.StoreSnapshot(moved);
        var item = PreparedMaterialTransferDispatchTests.Item(fixture);
        item.NormalizedCommand.Parameters.Single(row => row.Name == PreparedMaterialTransferContract.BindingParameter).Value =
            committed!.Ledger!.PreparedMaterialTransfer!.BindingSha256;
        var original = JsonSerializer.SerializeToNode(item)!.AsObject();
        var rebound = TeacherPreferenceQueueLoader.RebindQueueItemToState(original, moved.StateHash);
        Assert.Equal(fixture.Before.StateHash, original["normalized_command"]!["state_hash"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(original["normalized_command"]!["parameters"], rebound["normalized_command"]!["parameters"]));

        var program = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!;
        var read = program.GetMethod("ReadDispatchReadinessAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        async Task<JsonObject?> Evaluate(JsonObject actual) => await (Task<JsonObject?>)read.Invoke(null, new object[]
        {
            client, new LiveTrainingOptions { BackendUrl = client.BaseAddress!.ToString().TrimEnd('/') },
            actual, moved.StateHash, "local-precompiled-transfer"
        })!;
        var accepted = await Evaluate(rebound);
        Assert.True(accepted?["ready"]?.GetValue<bool>(), accepted?.ToJsonString());
        var parameters = rebound["normalized_command"]!["parameters"]!.AsArray();
        parameters.Remove(parameters.Single(row => row!["name"]!.GetValue<string>() == PreparedMaterialTransferContract.BindingParameter));
        var rejected = await Evaluate(rebound);
        Assert.False(rejected?["ready"]?.GetValue<bool>());
        Assert.Equal(ledgerBytes, File.ReadAllBytes(path));
        Assert.Equal(3, app.Services.GetRequiredService<IStrategyCommitmentRepository>().Get(moved).Revision);
    }

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
