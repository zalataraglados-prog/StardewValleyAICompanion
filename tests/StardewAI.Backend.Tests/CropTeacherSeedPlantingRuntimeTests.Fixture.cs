using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "planting-runtime-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<global::Program> app;
        internal CropTeacherSeedPlantingTests.Fixture Files { get; } = new();
        internal string Output => Path.Combine(root, "support");
        internal LiveTrainingOptions Options { get; }
        internal RuntimeHandler Handler { get; }
        internal HttpClient Client { get; }
        internal StateStore Store { get; }
        internal ActionQueueEnvelope Queue { get; private set; } = null!;
        internal CropTeacherSeedPlantingMarker Marker { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        internal int NativeRequests { get; private set; }
        internal int FullSnapshotReads { get; private set; }
        internal Action<string>? BeforeWrite { get; set; }

        internal RuntimeFixture(bool loseReplies = false, string fault = "")
        {
            CropTeacherSeedPlantingPrefixTests.CompleteGrowthContext(Files);
            Files.RefreshSource(snapshot =>
            {
                snapshot.RealTimestamp = "2026-11-08T06:00:00Z";
                foreach (var domain in new[] { "mods", "npcs", "quests", "modded_state" })
                    if (!snapshot.State.ContainsKey(domain)) snapshot.State[domain] = JsonSerializer.SerializeToElement(new JsonObject());
                foreach (var domain in snapshot.State.Keys.ToArray())
                {
                    var fields = JsonNode.Parse(snapshot.State[domain].GetRawText())!.AsObject();
                    foreach (var row in fields)
                        if (row.Value is JsonObject envelope && envelope.ContainsKey("status"))
                        {
                            envelope["source"] ??= new JsonObject { ["kind"] = "game_object", ["path"] = "fixture.runtime." + domain + "." + row.Key };
                            envelope["adapter"] ??= "test";
                            envelope["read_at_tick"] ??= snapshot.GameTick;
                            envelope["confidence"] ??= 1d;
                        }
                    snapshot.State[domain] = JsonSerializer.SerializeToElement(fields, JsonOptions);
                }
            });
            app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(Files.Repository);
            }));
            using var setup = app.CreateClient();
            Store = app.Services.GetRequiredService<StateStore>();
            Store.Snapshots[Files.Before.StateHash] = Files.Before;
            Handler = new RuntimeHandler(app.Server.CreateHandler(), loseReplies, fault, ledger =>
                ledger["material_reservations"]!.AsArray().Single(row => row!["reservation_id"]!.GetValue<string>() == "outside")!["quantity"] = 2);
            Client = new HttpClient(Handler);
            Options = new LiveTrainingOptions
            {
                BackendUrl = "http://test", BridgeSnapshotUrl = "http://test/fixture/snapshot", ExecutorUrl = "http://test",
                ManifestPath = Path.Combine(root, "run", "manifest.json"), RunId = "run.owned.planting",
                EmitQueueExecutionReceipt = true, MaxQueueItemAttempts = 8, Goal = "earn_money"
            };
            Directory.CreateDirectory(Options.SnapshotDir);
        }

        internal Task<CropTeacherSeedPlantingRuntime> Prepare(int consumed = 8, bool failedSuffix = false,
            Action<SnapshotEnvelope>? change = null) => CropTeacherSeedPlantingRuntime.PrepareAsync(Client,
            new(Options.BackendUrl, Output, Options.RunId, Options.PolicyTrajectoryExecutorVersion), Files.Open(), Files.Batch(), Files.OwnedSlotOne,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Client, Options, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", Client, Options, diagnostic, request),
            (path, json) =>
            {
                BeforeWrite?.Invoke(path);
                return InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            }, async (marker, batch) =>
            {
                Marker = marker;
                using var reply = await Client.PostAsJsonAsync(Options.BackendUrl + "/api/v1/small-model/action-queue/compile",
                    new SmallModelActionEnvelope
                    {
                        ModelOutputId = "planting-runtime-fixture", SourceModel = "deterministic.fixture", GoalId = "earn_money",
                        StateHash = Files.Before.StateHash, Actor = Options.TargetActor, Actions = marker.BuildActions(batch, Files.OwnedSlotOne)
                    });
                Assert.True(reply.IsSuccessStatusCode, await reply.Content.ReadAsStringAsync());
                Queue = (await reply.Content.ReadFromJsonAsync<ActionQueueEnvelope>())!;
                Transition = Files.NativeTransition(marker, consumed, failedSuffix, change, compiledQueue: Queue);
                return Transition.QueuePath;
            });

        internal async Task Observe(CropTeacherSeedPlantingRuntime runtime)
        {
            var current = Files.Before;
            var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(Transition.ExecutionReceiptPath), JsonOptions)!;
            for (var index = 0; index < receipt.StepResults.Length; index++)
            {
                await runtime.ValidateBeforeDispatchAsync(index, JsonSerializer.SerializeToNode(Queue.Items[index], JsonOptions)!.AsObject(),
                    JsonSerializer.SerializeToNode(current, JsonOptions)!.AsObject(), index == 0 ? Files.SourcePath : receipt.StepResults[index - 1].AfterSnapshotPath);
                await runtime.ObserveNativeStepAsync(JsonSerializer.SerializeToNode(receipt.StepResults[index], JsonOptions)!.AsObject(), receipt.StepResults[index].AfterSnapshotPath);
                current = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(receipt.StepResults[index].AfterSnapshotPath), JsonOptions)!;
            }
            Store.Snapshots[Files.After.StateHash] = Files.After;
        }

        internal async Task<CropTeacherSeedPlantingRuntime> BuildControl(bool managedScopes = false)
        {
            var scope = Files.Open();
            var step = new CropTeacherRunStep(CropTeacherRunStageKinds.ExecuteCropBatch, scope.TotalDay, scope.CommitmentId,
                "Farm", "FarmHouse", "(O)745", "", 0, 8, Files.Batch(), "owned_supply", SeedReservationId: Files.OwnedSlotOne);
            var arguments = new object[] { Client, Options, 1, step,
                new MoneyRevenueCropNativeProductionRunPlan(), new MoneyRevenueCropNativeProductionRunPlanRequest(),
                Files.SourcePath, JsonSerializer.SerializeToNode(Files.Before, JsonOptions)!.AsObject(), Files.Before.StateHash };
            var task = managedScopes
                ? InvokeLive<Task>("BuildCropTeacherManagedControlQueueAsync", arguments.Concat(new object[]
                {
                    CropTeacherManagedSeedScopes.Open(Files.RequestPath, Files.SourcePath, Files.LedgerPath, step,
                        scope.MarkerRequest.PortfolioId)
                }).ToArray())
                : InvokeLive<Task>("BuildCropTeacherControlQueueCoreAsync", arguments.Concat(new object[] { null!, null!, scope }).ToArray());
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var rawQueue = (JsonObject)result.GetType().GetProperty("Queue")!.GetValue(result)!;
            Queue = rawQueue.Deserialize<ActionQueueEnvelope>(JsonOptions)!;
            var lookup = Files.Repository.LookupPortfolioMutationReply(Files.Before, "commit", JsonSerializer.SerializeToElement(scope.MarkerRequest, JsonOptions));
            Marker = scope.VerifyMarkerReply(lookup["reply"]!.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!);
            Marker.BuildActions(Files.Batch(), Files.OwnedSlotOne);
            Transition = Files.NativeTransition(Marker, compiledQueue: Queue);
            CropTeacherSeedPlantingTests.Fixture.Save(Files.QueuePath, rawQueue);
            return (CropTeacherSeedPlantingRuntime)result.GetType().GetProperty("SeedPlantingSupport")!.GetValue(result)!;
        }

        internal async Task<JsonObject> Execute(CropTeacherSeedPlantingRuntime runtime, bool attachOwner = true)
        {
            using var transport = new NativeTransport(this);
            using var loopClient = new HttpClient(transport);
            return await InvokeLive<Task<JsonObject>>("ExecuteRuntimeTestHarnessCoreAsync", loopClient, loopClient, Options, 1,
                Files.SourcePath, JsonSerializer.SerializeToNode(Files.Before, JsonOptions)!.AsObject(),
                JsonSerializer.SerializeToNode(Queue, JsonOptions)!.AsObject(), Files.Before.StateHash, Queue.QueueId,
                "", "", Files.QueuePath, Files.SourcePath, Files.Before.StateHash, Transition.SelectedCandidateId, -1,
                Files.QueuePath, null!, new List<JsonObject>(), false, null!, null!,
                attachOwner ? runtime : null!);
        }

        internal JsonObject Summary() => JsonNode.Parse(File.ReadAllText(Path.Combine(Output, "completion-summary.json")))!.AsObject();

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            var blobs = Path.Combine(Options.SnapshotDir, "_snapshot-blobs");
            if (Directory.Exists(blobs))
                foreach (var shard in Directory.EnumerateDirectories(blobs))
                {
                    Assert.Matches("^[0-9a-f]{2}$", Path.GetFileName(shard));
                    Assert.False(File.GetAttributes(shard).HasFlag(FileAttributes.ReparsePoint));
                    foreach (var file in Directory.EnumerateFiles(shard)) File.Delete(file);
                    Directory.Delete(shard);
                }
            var control = Path.Combine(Options.SnapshotDir, "crop-teacher-seed-planting-0001");
            foreach (var directory in new[] { Path.Combine(control, "teacher-ledgers"), control, Path.Combine(Output, "teacher-ledgers"), Output,
                         Path.Combine(Options.SnapshotDir, "_snapshot-blobs"), Options.SnapshotDir, Options.RunDir, root })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
            Files.Dispose();
        }

        private sealed class NativeTransport : HttpMessageHandler
        {
            private readonly RuntimeFixture owner;
            private readonly HttpMessageInvoker backend;
            private readonly QueueExecutionReceiptEnvelope receipt;
            private SnapshotEnvelope current;
            internal NativeTransport(RuntimeFixture owner)
            {
                this.owner = owner;
                backend = new HttpMessageInvoker(owner.Handler, disposeHandler: false);
                current = owner.Files.Before;
                receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(owner.Transition.ExecutionReceiptPath), JsonOptions)!;
            }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                if (request.RequestUri!.AbsolutePath == "/fixture/snapshot")
                {
                    if (!request.RequestUri.Query.Contains("profile=clock", StringComparison.Ordinal)) owner.FullSnapshotReads++;
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(current, options: JsonOptions) };
                }
                if (request.RequestUri.AbsolutePath == owner.Options.ExecutorEndpointPath)
                {
                    var bound = (await request.Content!.ReadFromJsonAsync<TrainingExecutionRequest>(JsonOptions, token))!;
                    var index = owner.NativeRequests;
                    Assert.Equal(current.StateHash, bound.BeforeStateHash);
                    Assert.Equal(owner.Queue.Items[index].QueueItemId, bound.QueueItemId);
                    Assert.Equal(1, bound.InventorySlotIndex);
                    Assert.Equal(9 - index, bound.ExpectedStackBefore);
                    Assert.Equal(8 - index, bound.ExpectedStackAfter);
                    owner.NativeRequests++;
                    var step = receipt.StepResults[index];
                    current = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(step.AfterSnapshotPath), JsonOptions)!;
                    owner.Store.Snapshots[current.StateHash] = current;
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(step, options: JsonOptions) };
                }
                return await backend.SendAsync(request, token);
            }
            protected override void Dispose(bool disposing) { if (disposing) backend.Dispose(); base.Dispose(disposing); }
        }
    }
}
