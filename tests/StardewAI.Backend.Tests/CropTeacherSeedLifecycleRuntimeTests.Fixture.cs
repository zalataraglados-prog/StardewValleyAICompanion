using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleRuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "lifecycle-runtime-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<global::Program> app;
        internal CropTeacherSeedLifecycleTests.Fixture Files { get; }
        internal string Output => Path.Combine(root, "evidence");
        internal LiveTrainingOptions Options { get; }
        internal HttpClient Client { get; }
        internal LifecycleHandler Handler { get; }
        internal string Mode { get; set; }
        internal string Reason { get; set; }

        internal RuntimeFixture(bool completion = false, bool noResources = false, bool otherExpires = false,
            bool loseCancel = false, bool loseRelease = false, string fault = "")
        {
            Directory.CreateDirectory(root);
            Files = new(due: completion, noResources: noResources, otherExpiresDuringCancel: otherExpires);
            Mode = completion ? CropTeacherSeedLifecycleRuntime.CalendarCompletion : CropTeacherSeedLifecycleRuntime.ExplicitCancellation;
            Reason = completion ? "" : "explicit_runtime_cancel";
            app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(Files.Repository);
            }));
            using var setup = app.CreateClient();
            app.Services.GetRequiredService<StateStore>().Snapshots[Files.Snapshot.StateHash] = Files.Snapshot;
            Handler = new(app.Server.CreateHandler(), this, loseCancel, loseRelease, fault);
            Client = new HttpClient(Handler);
            Options = new LiveTrainingOptions
            {
                BackendUrl = "http://test", RunId = "run.seed.lifecycle",
                ManifestPath = Path.Combine(Output, "lifecycle-intent.json")
            };
        }

        internal Task<StrategyLedgerEvidence> Run() => InvokeLive<Task<StrategyLedgerEvidence>>("CloseCropTeacherSeedLifecycleAsync",
            Client, Options, Files.Open(), Output, Mode, Reason);

        internal Task<int> Command(string[]? arguments = null) => InvokeLive<Task<int>>("CloseCropSeedReservationsAsync", arguments ?? Arguments(), Client);

        internal string[] Arguments() => new[]
        {
            "--snapshot", Files.SnapshotPath, "--ledger", Files.LedgerPath, "--commitment-id", Files.Id,
            "--release-step-id", Files.Step, "--run-id", Options.RunId, "--mode", Mode,
            "--backend-url", Options.BackendUrl, "--output-dir", Output
        }.Concat(Reason == "" ? Array.Empty<string>() : new[] { "--reason", Reason }).ToArray();

        internal JsonObject Summary() => JsonNode.Parse(File.ReadAllText(Path.Combine(Output, "lifecycle-summary.json")))!.AsObject();

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            foreach (var directory in new[] { Path.Combine(Output, "teacher-ledgers"), Path.Combine(Output, "live-snapshots"), Output, root })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
            Files.Dispose();
        }

        internal sealed class LifecycleHandler(HttpMessageHandler inner, RuntimeFixture owner, bool loseCancel, bool loseRelease, string fault)
            : DelegatingHandler(inner)
        {
            internal int Cancellations { get; private set; }
            internal int Releases { get; private set; }
            internal int Reads { get; private set; }
            internal int Lookups { get; private set; }
            internal List<string> Events { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                var path = request.RequestUri!.AbsolutePath;
                var cancel = path.EndsWith("/cancel", StringComparison.Ordinal);
                var release = path.EndsWith("/reservation-portfolios/commit", StringComparison.Ordinal);
                var read = path.EndsWith("/commitments/latest", StringComparison.Ordinal);
                var lookup = path.EndsWith("/lookup-mutation", StringComparison.Ordinal);
                Assert.True(cancel || release || read || lookup, "Unexpected runtime/game endpoint: " + path);
                Assert.True(File.Exists(Path.Combine(owner.Output, "lifecycle-intent.json")));
                if (cancel)
                {
                    Cancellations++;
                    Events.Add("cancel");
                    Assert.True(File.Exists(Path.Combine(owner.Output, "crop-cancel-journal.json")));
                    Assert.True(File.Exists(Path.Combine(owner.Output, "crop-cancel-request.json")));
                    if (fault == "pre_cancel") throw new HttpRequestException("No original cancellation reached the backend.");
                }
                if (release)
                {
                    Releases++;
                    Events.Add("release");
                    Assert.True(File.Exists(Path.Combine(owner.Output, "verified-closure-ledger.json")));
                    var payload = (await request.Content!.ReadFromJsonAsync<ReservationPortfolioCommitRequest>(JsonOptions, token))!;
                    Assert.Equal(owner.Files.Step, payload.PortfolioId);
                    Assert.Equal(new[] { owner.Files.CashId, "owned-seeds" }.Order(), payload.ReleaseReservationIds.Order());
                }
                if (read) { Reads++; Events.Add("get"); }
                if (lookup) { Lookups++; Events.Add("lookup"); }
                var reply = await base.SendAsync(request, token);
                if (cancel && loseCancel || release && loseRelease)
                {
                    Assert.True(reply.IsSuccessStatusCode);
                    reply.Dispose();
                    if (fault == "request_journal") await File.AppendAllTextAsync(Path.Combine(owner.Output, "crop-cancel-request.json"), "\n", token);
                    throw new HttpRequestException("Original mutation committed; reply lost.");
                }
                if (fault == "bad_cancel_json" && cancel)
                {
                    reply.Dispose();
                    return new(HttpStatusCode.OK) { Content = new StringContent("{invalid-json") };
                }
                if (fault == "release_lookup" && lookup)
                {
                    var missing = JsonNode.Parse(await reply.Content.ReadAsStringAsync(token))!;
                    missing["status"] = "not_found";
                    reply.Dispose();
                    return new(HttpStatusCode.OK) { Content = new StringContent(missing.ToJsonString()) };
                }
                var tamper = fault == "cancel_reply" && cancel || fault == "release_reply" && release ||
                    fault == "final_readback" && read && Releases > 0 || fault == "completion_readback" && read && Reads == 1;
                if (!tamper) return reply;
                var body = JsonNode.Parse(await reply.Content.ReadAsStringAsync(token))!.AsObject();
                var ledger = read ? body : body["ledger"]!.AsObject();
                ledger["material_reservations"]!.AsArray().Single(row => row!["reservation_id"]!.GetValue<string>() == "outside")!["quantity"] = 2;
                reply.Dispose();
                return new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(JsonOptions)) };
            }
        }
    }
}
