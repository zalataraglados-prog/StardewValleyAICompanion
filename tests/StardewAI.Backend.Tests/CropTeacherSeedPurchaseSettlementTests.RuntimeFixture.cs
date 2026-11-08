using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    internal sealed class RuntimeFixture : IDisposable
    {
        private readonly WebApplicationFactory<global::Program> app;
        private readonly Fixture files;
        public RuntimeHandler Handler { get; }
        public HttpClient Client { get; }
        public StateStore Store { get; }
        public FileStrategyCommitmentRepository Repository { get; }
        public LiveTrainingOptions LiveOptions { get; }
        internal Action<string>? BeforeWrite { get; set; }

        public RuntimeFixture(Fixture files, bool loseReplies = false, string fault = "")
        {
            this.files = files;
            Repository = new FileStrategyCommitmentRepository(files.RepositoryPath);
            Assert.True(Repository.Upsert(files.Before, files.CropRequest).Accepted);
            Assert.True(Repository.CommitReservationPortfolio(files.Before, files.OtherRequest).Accepted);
            Assert.True(Repository.CommitReservationPortfolio(files.Before, files.OwnRequest).Accepted);
            files.Ledger = Repository.Get(files.Before);
            Fixture.Save(files.LedgerPath, files.Ledger);
            app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IStrategyCommitmentRepository>();
                    services.AddSingleton<IStrategyCommitmentRepository>(Repository);
                }));
            using var setup = app.CreateClient();
            Store = app.Services.GetRequiredService<StateStore>();
            Store.Snapshots[files.Before.StateHash] = files.Before;
            Handler = new RuntimeHandler(app.Server.CreateHandler(), loseReplies, fault);
            Client = new HttpClient(Handler);
            LiveOptions = new LiveTrainingOptions
            {
                BackendUrl = "http://test", ManifestPath = Path.Combine(files.RuntimePath, "run.json")
            };
        }

        public Task<CropTeacherSeedPurchaseRuntime> Prepare(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
            Func<Task<string>>? compile = null, CropTeacherSeedPurchaseSupport? selectedScope = null,
            string? runtimeDirectory = null) => CropTeacherSeedPurchaseRuntime.PrepareAsync(Client,
                new(LiveOptions.BackendUrl, runtimeDirectory ?? files.RuntimePath, transition.RunId, transition.ExecutorVersion), selectedScope ?? files.Open(),
                (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Client, LiveOptions, diagnostic, request),
                (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", Client, LiveOptions, diagnostic, request),
                (path, json) =>
                {
                    BeforeWrite?.Invoke(path);
                    return InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
                },
                compile ?? (() =>
                {
                    Assert.Equal(files.Ledger.Revision + 1, Repository.Get(files.Before).Revision);
                    return Task.FromResult(transition.QueuePath);
                }));

        public void ObserveAfter() => Store.Snapshots[files.After.StateHash] = files.After;

        public JsonObject Summary(string name = "completion-summary.json") =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(files.RuntimePath, name)))!.AsObject();

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
        }
    }

    internal static T InvokeLive<T>(string name, params object[] arguments) => (T)typeof(LiveTrainingOptions).Assembly
        .GetType("Program")!.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    internal sealed class RuntimeHandler(HttpMessageHandler inner, bool loseReplies, string fault,
        Action<JsonObject>? corruptLedger = null) : DelegatingHandler(inner)
    {
        public int Commits { get; private set; }
        public int Settlements { get; private set; }
        public int Lookups { get; private set; }
        public int LedgerReads { get; private set; }
        public int Cancellations { get; private set; }
        internal JsonObject? LastCompiledQueue { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            var commit = path.EndsWith("/reservation-portfolios/commit", StringComparison.Ordinal);
            var settlement = path.EndsWith("/settle-supporting-transition", StringComparison.Ordinal);
            var read = path.EndsWith("/commitments/latest", StringComparison.Ordinal);
            if (commit) Commits++;
            if (settlement) Settlements++;
            if (read) LedgerReads++;
            if (path.EndsWith("/cancel", StringComparison.Ordinal)) Cancellations++;
            if (path.EndsWith("/lookup-mutation", StringComparison.Ordinal)) Lookups++;
            var reply = await base.SendAsync(request, token);
            if (path.EndsWith("/action-queue/compile", StringComparison.Ordinal) && reply.IsSuccessStatusCode)
                LastCompiledQueue = JsonNode.Parse(await reply.Content.ReadAsStringAsync(token))!.AsObject();
            if (fault == "missing_recovery_lookup" && path.EndsWith("/lookup-mutation", StringComparison.Ordinal))
            {
                var missing = JsonNode.Parse(await reply.Content.ReadAsStringAsync(token))!;
                missing["status"] = "not_found";
                reply.Dispose();
                return new(HttpStatusCode.OK) { Content = new StringContent(missing.ToJsonString(JsonOptions)) };
            }
            if (loseReplies && (commit || settlement))
            {
                Assert.True(reply.IsSuccessStatusCode);
                reply.Dispose();
                throw new HttpRequestException("Committed original mutation; reply lost.");
            }
            var tamper = fault == "marker_reply" && commit && Commits == 1 ||
                fault == "settlement_reply" && settlement || fault == "acquisition_reply" && commit && Commits == 2 ||
                fault == "marker_readback" && read && LedgerReads == 2 || fault == "final_readback" && read && LedgerReads == 3;
            if (!tamper) return reply;
            var body = JsonNode.Parse(await reply.Content.ReadAsStringAsync(token))!.AsObject();
            var ledger = read ? body : body["ledger"]!.AsObject();
            if (corruptLedger is not null) corruptLedger(ledger);
            else
            {
                var other = ledger["currency_reservations"]!.AsArray().Single(row => row!["reservation_id"]!.GetValue<string>() == "other-money")!;
                other["amount"] = 349;
            }
            var changed = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(JsonOptions)) };
            reply.Dispose();
            return changed;
        }
    }
}
