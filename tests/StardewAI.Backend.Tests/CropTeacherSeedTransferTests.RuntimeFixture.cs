using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    private sealed class RuntimeFixture : IDisposable
    {
        private readonly WebApplicationFactory<global::Program> app;
        private readonly Fixture files;
        internal RuntimeHandler Handler { get; }
        internal HttpClient Client { get; }
        internal StateStore Store { get; }
        internal FileStrategyCommitmentRepository Repository { get; }
        internal LiveTrainingOptions Options { get; }
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        internal CropTeacherSeedTransferMarker Marker { get; private set; } = null!;
        internal Action<string>? BeforeWrite { get; set; }

        internal RuntimeFixture(Fixture files, bool loseReplies = false, string fault = "")
        {
            this.files = files;
            Repository = new FileStrategyCommitmentRepository(files.RepositoryPath);
            Assert.True(Repository.Upsert(files.Before, files.CropRequest).Accepted);
            Assert.True(Repository.CommitReservationPortfolio(files.Before, files.ResourceRequest).Accepted);
            files.AdoptLedger(Repository.Get(files.Before));
            app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IStrategyCommitmentRepository>();
                    services.AddSingleton<IStrategyCommitmentRepository>(Repository);
                }));
            using var setup = app.CreateClient();
            Store = app.Services.GetRequiredService<StateStore>();
            Store.Snapshots[files.Before.StateHash] = files.Before;
            Handler = new RuntimeHandler(app.Server.CreateHandler(), loseReplies, fault, ledger =>
            {
                var outside = ledger["material_reservations"]!.AsArray().Single(row =>
                    row!["reservation_id"]!.GetValue<string>() == "outside")!;
                outside["quantity"] = 2;
            });
            Client = new HttpClient(Handler);
            Options = new LiveTrainingOptions
            {
                BackendUrl = "http://test", ManifestPath = Path.Combine(files.RunPath, "run.json"),
                RunId = "run.owned.seed.transfer", EmitQueueExecutionReceipt = true
            };
        }

        internal Task<CropTeacherSeedTransferRuntime> Prepare(CropTeacherSeedTransferSupport? scope = null,
            Func<CropTeacherSeedTransferMarker, CropTeacherSeedTransferExecutionPlan, Task<string>>? compile = null,
            string? expectedLocation = null) => CropTeacherSeedTransferRuntime.PrepareAsync(Client,
                new(Options.BackendUrl, files.RuntimePath, Options.RunId, Options.PolicyTrajectoryExecutorVersion),
                scope ?? files.Open(),
                (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Client, Options, diagnostic, request),
                (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", Client, Options, diagnostic, request),
                (path, json) =>
                {
                    BeforeWrite?.Invoke(path);
                    return InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
                }, compile ?? Compile,
                expectedLocation);

        private async Task<string> Compile(CropTeacherSeedTransferMarker marker, CropTeacherSeedTransferExecutionPlan plan)
        {
            Marker = marker;
            Assert.Equal(files.Ledger.Revision + 1, Repository.Get(files.Before).Revision);
            var model = new SmallModelActionEnvelope
            {
                ModelOutputId = "owned-seed-runtime-fixture", SourceModel = "deterministic.fixture",
                GoalId = "earn_money", StateHash = files.Before.StateHash,
                ExecutionMode = Options.TargetExecutionMode, Actor = Options.TargetActor,
                Actions = new[] { marker.BuildAction(plan.StandTileX, plan.StandTileY) }
            };
            using var reply = await Client.PostAsJsonAsync(Options.BackendUrl + "/api/v1/small-model/action-queue/compile", model);
            Assert.True(reply.IsSuccessStatusCode, await reply.Content.ReadAsStringAsync());
            var queue = await reply.Content.ReadFromJsonAsync<ActionQueueEnvelope>();
            Transition = files.NativeTransition(marker, compiledQueue: queue!);
            return Transition.QueuePath;
        }

        internal CropTeacherRunStep StepFor(CropTeacherSeedTransferSupport scope) => new(
            CropTeacherRunStageKinds.TransferSeedSupply, scope.TotalDay, scope.CommitmentId, "Farm", "FarmHouse",
            "(O)745", "", 0, 10, null, "owned_supply", scope.PreparationRequest.MaterialTransferPreparation!.Intent,
            scope.ReservationId);

        internal async Task<(JsonObject Queue, CropTeacherSeedTransferRuntime Runtime)> BuildControl(
            CropTeacherSeedTransferSupport scope, CropTeacherRunStep? step = null, bool managedScopes = false)
        {
            var selected = step ?? StepFor(scope);
            var arguments = new object[] { Client, Options, 1, selected,
                new MoneyRevenueCropNativeProductionRunPlan(), new MoneyRevenueCropNativeProductionRunPlanRequest(),
                files.SourcePath, JsonNode.Parse(File.ReadAllText(files.SourcePath))!.AsObject(), files.Before.StateHash };
            var task = managedScopes
                ? InvokeLive<Task>("BuildCropTeacherManagedControlQueueAsync", arguments.Concat(new object[]
                {
                    CropTeacherManagedSeedScopes.Open(files.RequestPath, files.SourcePath, files.LedgerPath, selected,
                        scope.PreparationRequest.PortfolioId)
                }).ToArray())
                : InvokeLive<Task>("BuildCropTeacherControlQueueAsync", arguments.Concat(new object[] { null!, scope }).ToArray());
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var queue = (JsonObject)result.GetType().GetProperty("Queue")!.GetValue(result)!;
            var runtime = (CropTeacherSeedTransferRuntime)result.GetType().GetProperty("SeedTransferSupport")!.GetValue(result)!;
            return (queue, runtime);
        }

        internal void ObserveAfter() => Store.Snapshots[files.After.StateHash] = files.After;
        internal JsonObject Summary(string name = "completion-summary.json") =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(files.RuntimePath, name)))!.AsObject();

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
        }
    }
}
