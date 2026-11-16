using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.LiveTrainingLoop;
using StardewAI.GoalConditionedBootstrap;
using Fixture = StardewAI.Testing.MoneyCropTeacherFixture;
using static StardewAI.Testing.MoneyCropTestReservations;

namespace StardewAI.Backend.Tests;

public sealed partial class MoneyCropTeacherLedgerBridgeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData(false, false, 0, 0)]
    [InlineData(true, false, 1, 0)]
    [InlineData(true, true, 1, 1)]
    public async Task CompleteLedgerReachesTeacherWithVerifiedAnchorAndNoMutationReplay(
        bool stale, bool lostReply, int commits, int lookups)
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        using var files = new LedgerBridgeFiles();
        var prior = fixture.EmptyLedger();
        if (stale) prior.SourceStateHash = new string('e', 64);
        prior.MaterialReservations = new[] { SeedReservation(prior, 2) };
        prior.CurrencyReservations = new[] { MoneyReservation(prior, 350) };
        var original = JsonSerializer.Serialize(prior, JsonOptions);
        using var handler = new LedgerBridgeHandler(prior, fixture.SourceSnapshot) { LostReply = lostReply };
        using var http = new HttpClient(handler);
        var options = new LiveTrainingOptions { Root = files.Root, RunId = "rpc", BackendUrl = "http://test" };
        var evidence = await CropTeacherStrategyLedgerBridge.PrepareAsync(http, options.BackendUrl,
            files.Root, fixture.SourceSnapshot.StateHash, fixture.SnapshotPath, "crop-binding", JsonOptions,
            (diagnostic, request) => JournaledCommit(http, options, diagnostic, request));
        Assert.Equal(commits, handler.Commits);
        Assert.Equal(lookups, handler.Lookups);
        Assert.Equal(1, handler.Reads);
        Assert.Equal(fixture.SourceSnapshot.StateHash, evidence.Ledger.SourceStateHash);
        Assert.Equal(prior.Revision + commits, evidence.Ledger.Revision);
        Assert.Equal(original, JsonSerializer.Serialize(prior, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(prior.MaterialReservations, JsonOptions),
            JsonSerializer.Serialize(evidence.Ledger.MaterialReservations, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(prior.CurrencyReservations, JsonOptions),
            JsonSerializer.Serialize(evidence.Ledger.CurrencyReservations, JsonOptions));
        fixture.SetLedger(evidence.Ledger);
        var choice = fixture.Build().TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(1, choice.ExistingSeedQuantity);
        Assert.Equal(2, choice.TileCount);
        Assert.Equal(80, choice.SeedAcquisitionCost);
        var capturePath = MoneyCropTeacherCaptureFixture.Save(fixture, Path.Combine(files.Root, "capture.json"));
        var dispatch = MoneyRevenueCropTeacherDispatchBuilder.Build(capturePath, Path.Combine(files.Root, "dispatch"),
            "bound-dispatch", "2026-11-07T00:02:00Z", baseStrategyLedgerPath: evidence.Path);
        Assert.Equal(evidence.Path, dispatch.BaseStrategyLedgerPath);
        Assert.Equal(FileHash(evidence.Path), dispatch.BaseStrategyLedgerSha256);
        var emitted = JsonSerializer.Deserialize<MoneyRevenueCropProductionTeacherRequest>(
            File.ReadAllText(dispatch.TeacherRequestPath), JsonOptions)!;
        Assert.Equal(evidence.Path, emitted.BaseStrategyLedgerPath);
    }

    [Theory]
    [InlineData("money", true)]
    [InlineData("money", false)]
    [InlineData("material", true)]
    [InlineData("material", false)]
    [InlineData("active_crop", true)]
    [InlineData("active_crop", false)]
    [InlineData("actor", true)]
    [InlineData("actor", false)]
    public async Task InvalidOrActiveLedgerCannotBeRelabeledBeforePlanning(string mutation, bool stale)
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        using var files = new LedgerBridgeFiles();
        var prior = mutation == "active_crop" ? fixture.Receipt(fixture.Build()).FinalLedger! : fixture.EmptyLedger();
        if (stale) prior.SourceStateHash = new string('e', 64);
        var originalHash = prior.SourceStateHash;
        if (mutation == "money") prior.CurrencyReservations = new[] { MoneyReservation(prior, 451) };
        if (mutation == "material") prior.MaterialReservations = new[] { SeedReservation(prior, 4) };
        if (mutation == "actor") prior.PlayerId = "999";
        using var handler = new LedgerBridgeHandler(prior, fixture.SourceSnapshot);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => CropTeacherStrategyLedgerBridge.PrepareAsync(http,
            "http://test", files.Root, fixture.SourceSnapshot.StateHash, fixture.SnapshotPath, "invalid", JsonOptions,
            (_, _) => throw new InvalidOperationException("Invalid evidence must not POST.")));
        Assert.Equal(0, handler.Commits);
        Assert.Equal(originalHash, prior.SourceStateHash);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("claim")]
    [InlineData("history")]
    public async Task MatchingSourceHashDoesNotExcuseAlteredCommitResult(string mutation)
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        using var files = new LedgerBridgeFiles();
        var prior = fixture.EmptyLedger();
        prior.SourceStateHash = new string('e', 64);
        prior.MaterialReservations = new[] { SeedReservation(prior, 2) };
        using var handler = new LedgerBridgeHandler(prior, fixture.SourceSnapshot) { Mutation = mutation };
        using var http = new HttpClient(handler);
        var options = new LiveTrainingOptions { Root = files.Root, RunId = "rpc", BackendUrl = "http://test" };
        await Assert.ThrowsAsync<InvalidDataException>(() => CropTeacherStrategyLedgerBridge.PrepareAsync(http,
            options.BackendUrl, files.Root, fixture.SourceSnapshot.StateHash, fixture.SnapshotPath,
            "altered", JsonOptions, (diagnostic, request) => JournaledCommit(http, options, diagnostic, request)));
        Assert.Equal(1, handler.Commits);
        Assert.Equal(0, handler.Lookups);
        Assert.Equal(2, prior.MaterialReservations[0].Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionAndUntrustedLookupNeverReplayCommit(bool lostReply)
    {
        using var fixture = new Fixture();
        using var files = new LedgerBridgeFiles();
        var prior = fixture.EmptyLedger();
        prior.SourceStateHash = new string('e', 64);
        using var handler = new LedgerBridgeHandler(prior, fixture.SourceSnapshot)
        {
            LostReply = lostReply, RejectCommit = !lostReply, UntrustedLookup = lostReply
        };
        using var http = new HttpClient(handler);
        var options = new LiveTrainingOptions { Root = files.Root, RunId = "rpc", BackendUrl = "http://test" };
        Task<StrategyLedgerEvidence> Run() => CropTeacherStrategyLedgerBridge.PrepareAsync(http,
            options.BackendUrl, files.Root, fixture.SourceSnapshot.StateHash, fixture.SnapshotPath,
            "rejected", JsonOptions, (diagnostic, request) => JournaledCommit(http, options, diagnostic, request));
        if (lostReply) await Assert.ThrowsAsync<HttpRequestException>(Run);
        else await Assert.ThrowsAsync<InvalidOperationException>(Run);
        Assert.Equal(1, handler.Commits);
        Assert.Equal(lostReply ? 1 : 0, handler.Lookups);
        Assert.Equal(new string('e', 64), prior.SourceStateHash);
    }

    [Fact]
    public async Task QueryIdentityMismatchFailsBeforeCommit()
    {
        using var fixture = new Fixture();
        using var files = new LedgerBridgeFiles();
        var prior = fixture.EmptyLedger();
        prior.SourceStateHash = new string('e', 64);
        using var handler = new LedgerBridgeHandler(prior, fixture.SourceSnapshot);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => CropTeacherStrategyLedgerBridge.PrepareAsync(http,
            "http://test", files.Root, new string('f', 64), fixture.SnapshotPath, "wrong-query", JsonOptions,
            (_, _) => throw new InvalidOperationException("Mismatched source must not POST.")));
        Assert.Equal(0, handler.Commits);
    }

    [Fact]
    public async Task OriginalRepositoryPersistsAnchorAndRecoversReplyWithoutChangingClaims()
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        using var files = new LedgerBridgeFiles();
        var directory = Path.Combine(files.Root, "repository");
        var repository = new FileStrategyCommitmentRepository(directory);
        var old = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(fixture.SourceSnapshot, JsonOptions), JsonOptions)!;
        var material = repository.UpsertMaterial(old, new MaterialReservationUpsertRequest
        {
            StateHash = old.StateHash, ExpectedLedgerRevision = 0, ReservationId = "existing-seeds",
            SourceDecisionId = "other-root", GoalId = "complete_full_shipment", NodeId = "player:inventory",
            SlotIndex = 0, QualifiedItemId = "(O)474", Quantity = 2, Purpose = "other root"
        });
        Assert.True(material.Accepted, string.Join(",", material.Errors));
        var money = repository.UpsertCurrency(old, new CurrencyReservationUpsertRequest
        {
            StateHash = old.StateHash, ExpectedLedgerRevision = 1, ReservationId = "existing-cash",
            SourceDecisionId = "other-root", GoalId = "marriage_and_house_upgrade",
            CurrencyId = NativeShopCurrencies.Money, Amount = 350, Purpose = "other root"
        });
        Assert.True(money.Accepted, string.Join(",", money.Errors));
        fixture.ChangePlayer(player => player["energy"]!["value"] = 269);
        var source = fixture.SourceSnapshot;
        var before = repository.Get(source);
        Assert.Equal(old.StateHash, before.SourceStateHash);
        Assert.NotEqual(source.StateHash, before.SourceStateHash);
        var evidence = await StrategyLedgerEvidenceStore.SaveAsync(
            JsonSerializer.SerializeToNode(before, JsonOptions)!.AsObject(), files.Root, JsonOptions, "Test");
        var request = CropTeacherLedgerSourceBinder.BuildRefreshRequest(fixture.SnapshotPath, evidence.Path, "persisted");
        var result = repository.CommitReservationPortfolio(source, request);
        var bound = CropTeacherLedgerSourceBinder.VerifyRefreshResult(fixture.SnapshotPath, evidence.Path, request, result);
        Assert.Equal(3, bound.Revision);
        Assert.Equal(old.StateHash, bound.MaterialReservations[0].SourceStateHash);
        Assert.Equal(350, bound.CurrencyReservations[0].Amount);
        var reloaded = new FileStrategyCommitmentRepository(directory);
        var lookup = reloaded.LookupPortfolioMutationReply(source, "commit", JsonSerializer.SerializeToElement(request, JsonOptions));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.False(lookup["ledger_mutation_performed"]!.GetValue<bool>());
        Assert.False(lookup["native_outcome_verified"]!.GetValue<bool>());
        var recovered = lookup["reply"]!.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!;
        Assert.Equal(JsonSerializer.Serialize(bound, JsonOptions), JsonSerializer.Serialize(
            CropTeacherLedgerSourceBinder.VerifyRefreshResult(fixture.SnapshotPath, evidence.Path, request, recovered), JsonOptions));
        Assert.Equal(old.StateHash, before.SourceStateHash);
    }

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static Task<JsonObject> JournaledCommit(HttpClient http, LiveTrainingOptions options,
        JsonObject diagnostic, ReservationPortfolioCommitRequest request) =>
        (Task<JsonObject>)typeof(LiveTrainingOptions).Assembly.GetType("Program")!
            .GetMethod("PostJournaledPortfolioCommitAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { http, options, diagnostic, request })!;

    private sealed class LedgerBridgeHandler(StrategyCommitmentLedger prior, SnapshotEnvelope snapshot) : HttpMessageHandler
    {
        public bool LostReply { get; init; }
        public bool RejectCommit { get; init; }
        public bool UntrustedLookup { get; init; }
        public string Mutation { get; init; } = string.Empty;
        public int Commits { get; private set; }
        public int Lookups { get; private set; }
        public int Reads { get; private set; }
        private ReservationPortfolioCommitResult? committed;
        private JsonObject? committedRequest;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
        {
            if (message.Method == HttpMethod.Get)
            {
                Reads++;
                return Reply(JsonSerializer.SerializeToNode(prior, JsonOptions)!);
            }
            var body = JsonNode.Parse(await message.Content!.ReadAsStringAsync(token))!.AsObject();
            if (message.RequestUri!.AbsolutePath.EndsWith("/commit", StringComparison.Ordinal))
            {
                Commits++;
                committedRequest = body.DeepClone().AsObject();
                if (RejectCommit) return new HttpResponseMessage(HttpStatusCode.Conflict)
                    { Content = new StringContent("revision conflict") };
                var request = body.Deserialize<ReservationPortfolioCommitRequest>(JsonOptions)!;
                Assert.Empty(request.MaterialClaims);
                Assert.Empty(request.CurrencyClaims);
                Assert.Empty(request.ReleaseReservationIds);
                committed = new ReservationPortfolioLedgerService().Commit(prior, snapshot, request, "2026-11-07T00:01:00Z");
                Assert.True(committed.Accepted, string.Join(",", committed.Errors));
                if (Mutation == "revision") committed.Ledger!.Revision++;
                if (Mutation == "claim") committed.Ledger!.MaterialReservations[0].Quantity--;
                if (Mutation == "history") committed.Ledger!.History = Array.Empty<StrategyCommitmentHistoryEntry>();
                if (LostReply) throw new HttpRequestException("Committed, reply lost.");
                return Reply(JsonSerializer.SerializeToNode(committed, JsonOptions)!);
            }
            Assert.EndsWith("/lookup-mutation", message.RequestUri.AbsolutePath);
            Lookups++;
            Assert.Equal("commit", body["operation"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(committedRequest, body["request"]));
            return Reply(new JsonObject
            {
                ["schema_version"] = "stardewai.portfolio_mutation_lookup.v1", ["status"] = "found",
                ["execution_performed"] = false, ["ledger_mutation_performed"] = false,
                ["native_outcome_verified"] = UntrustedLookup, ["formal_product_training"] = false,
                ["requires_independent_verification"] = true,
                ["reply"] = JsonSerializer.SerializeToNode(committed, JsonOptions)
            });
        }

        private static HttpResponseMessage Reply(JsonNode body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json")
        };
    }

    private sealed class LedgerBridgeFiles : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "crop-ledger-bridge-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            foreach (var path in new[] { Path.Combine(Root, "repository"), Path.Combine(Root, "dispatch"), Path.Combine(Root, "runs", "rpc", "live-snapshots"),
                         Path.Combine(Root, "runs", "rpc"), Path.Combine(Root, "runs"),
                         Path.Combine(Root, "teacher-ledgers"), Root })
            {
                if (!Directory.Exists(path)) continue;
                foreach (var file in Directory.EnumerateFiles(path)) File.Delete(file);
                Directory.Delete(path);
            }
        }
    }
}
