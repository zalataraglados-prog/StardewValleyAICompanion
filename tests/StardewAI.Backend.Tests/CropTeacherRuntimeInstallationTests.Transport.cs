using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherRuntimeInstallationTests
{
    // Real persisted repository, scripted transport loss. This is not native-game evidence.
    private sealed class RepositoryHandler(string repositoryPath, SnapshotEnvelope source) : HttpMessageHandler
    {
        public bool LoseCropReply { get; init; }
        public bool LoseResourceReply { get; init; }
        public string Failure { get; init; } = string.Empty;
        public string? EvidenceRoot { get; init; }
        public int CropPosts { get; private set; }
        public int ResourcePosts { get; private set; }
        public int Lookups { get; private set; }
        public int Reads { get; private set; }
        public JsonObject? ResourceRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var repository = new FileStrategyCommitmentRepository(repositoryPath);
            if (request.Method == HttpMethod.Get)
            {
                Reads++;
                Assert.Equal(source.StateHash, System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["stateHash"]);
                var ledger = repository.Get(source);
                if (Failure == "base_tamper" || (Failure == "final_readback_tamper" && ResourcePosts > 0))
                    ledger.CurrencyReservations[0].Amount--;
                return Reply(ledger);
            }
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!.AsObject();
            var endpoint = request.RequestUri!.AbsolutePath;
            var evidence = EvidenceRoot ?? Path.Combine(Path.GetDirectoryName(repositoryPath)!, "output");
            if (endpoint.EndsWith("/crops/upsert", StringComparison.Ordinal))
            {
                CropPosts++;
                Assert.True(File.Exists(Path.Combine(evidence, "crop-upsert-journal.json")));
                if (Failure == "crop_rejected") return Rejected();
                if (Failure == "crop_missing") throw new HttpRequestException("Crop not persisted.");
                var result = repository.Upsert(source, body.Deserialize<CropPlantingCommitmentUpsertRequest>(JsonOptions)!);
                Assert.True(result.Accepted, string.Join(",", result.Errors));
                if (Failure == "crop_intervening")
                {
                    var next = body.Deserialize<CropPlantingCommitmentUpsertRequest>(JsonOptions)!;
                    next.ExpectedLedgerRevision = result.Ledger!.Revision;
                    next.SourceDecisionId = "another-writer";
                    var intervening = repository.Upsert(source, next);
                    Assert.True(intervening.Accepted, string.Join(",", intervening.Errors));
                    throw new HttpRequestException("Reply lost with intervening writer.");
                }
                if (Failure == "crop_claim_tamper") result.Ledger!.MaterialReservations[0].Quantity--;
                if (Failure == "crop_history_tamper") result.Ledger!.History = Array.Empty<StrategyCommitmentHistoryEntry>();
                if (Failure == "crop_journal_tamper")
                {
                    File.AppendAllText(Path.Combine(evidence, "crop-upsert-request.json"), " ");
                    throw new HttpRequestException("Reply lost after journal tampering.");
                }
                if (LoseCropReply) throw new HttpRequestException("Crop persisted, reply lost.");
                return Reply(result);
            }
            if (endpoint.EndsWith("/reservation-portfolios/commit", StringComparison.Ordinal))
            {
                ResourcePosts++;
                ResourceRequest = body.DeepClone().AsObject();
                if (Failure == "resource_rejected") return Rejected();
                var result = repository.CommitReservationPortfolio(source, body.Deserialize<ReservationPortfolioCommitRequest>(JsonOptions)!);
                Assert.True(result.Accepted, string.Join(",", result.Errors));
                if (Failure == "resource_tamper") result.Ledger!.CurrencyReservations.Single(row => row.ReservationId != "other-money").Amount++;
                if (LoseResourceReply) throw new HttpRequestException("Resources persisted, reply lost.");
                return Reply(result);
            }
            Assert.EndsWith("/reservation-portfolios/lookup-mutation", endpoint);
            Lookups++;
            Assert.Equal("commit", body["operation"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(ResourceRequest, body["request"]));
            return Reply(repository.LookupPortfolioMutationReply(source, "commit", JsonSerializer.SerializeToElement(body["request"], JsonOptions)));
        }

        private static HttpResponseMessage Reply<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
        };

        private static HttpResponseMessage Rejected() => new(HttpStatusCode.Conflict) { Content = new StringContent("revision conflict") };
    }
}
