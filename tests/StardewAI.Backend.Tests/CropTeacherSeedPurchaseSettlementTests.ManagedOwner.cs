using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void ManagedOwnerFixtureUsesTheOriginalCompleteNativePlanBuilder()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var inputs = new OwnerInputs(f);
        Assert.True(inputs.Plan.NativeProductionRunReady);
        Assert.Equal(new[] { Id }, inputs.Plan.CoveredCommitmentIds);
        Assert.False(inputs.Plan.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("live_tampered_return")]
    [InlineData("recover")]
    [InlineData("lost_reply")]
    [InlineData("lost_acceptance")]
    public async Task ManagedOwnerOwnsOriginalPurchaseAndAcceptsLiveOrRestartedBoundaryWithoutResend(string mode)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        runtime.LiveOptions.RunId = transition.RunId;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "lost_acceptance" && Path.GetFileName(path) == "operation-000001-accepted.json")
                throw new IOException("Durable acceptance written; acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, step.Stage);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory);
        await owner.PinPreparedAsync(operation, prepared);
        runtime.ObserveAfter();
        if (mode == "lost_reply") runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "acquisition-reply.json");
        if (mode == "lost_reply") await Assert.ThrowsAsync<IOException>(() => prepared.CompleteAsync(transition));
        else
        {
            var completion = await prepared.CompleteAsync(transition);
            if (mode == "live_tampered_return") completion.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-seeds").Quantity = 999;
        }
        var acceptedBeforeRestart = mode.StartsWith("live", StringComparison.Ordinal) || mode == "lost_acceptance";
        if (acceptedBeforeRestart)
        {
            var reads = runtime.Handler.LedgerReads;
            if (mode == "lost_acceptance")
            {
                await Assert.ThrowsAsync<IOException>(() => owner.AcceptLiveAsync(operation, prepared));
                Assert.Same(operation, owner.Pending);
            }
            else
            {
                await owner.AcceptLiveAsync(operation, prepared);
                Assert.Null(owner.Pending);
                Assert.Equal(transition.AfterSnapshotPath, owner.CurrentSnapshotPath);
            }
            Assert.Equal(reads, runtime.Handler.LedgerReads);
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.AcceptLiveAsync(operation, prepared));
        }
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        if (acceptedBeforeRestart)
        {
            Assert.True(restarted.HeadRecoveryRequired);
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.AdmitAsync(step,
                restarted.CurrentSnapshotPath, restarted.CurrentLedgerPath, files.Native + "-next"));
            await restarted.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
            Assert.False(restarted.HeadRecoveryRequired);
        }
        else
        {
            Assert.NotNull(restarted.Pending);
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native + "-next"));
            await restarted.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
            Assert.Null(restarted.Pending);
        }
        var final = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(restarted.CurrentLedgerPath), JsonOptions)!;
        Assert.Equal(JsonSerializer.Serialize(runtime.Repository.Get(f.After), JsonOptions), JsonSerializer.Serialize(final, JsonOptions));
        AssertOtherRootsUnchanged(f.Ledger, final);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(mode == "lost_reply" ? 1 : 0, runtime.Handler.Lookups);
    }

    internal sealed class OwnerInputs : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "managed-owner-inputs-" + Guid.NewGuid().ToString("N"));
        internal MoneyRevenueCropNativeProductionRunPlan Plan { get; }
        internal MoneyRevenueCropNativeProductionRunPlanRequest Request { get; }
        internal string PlanPath => Path.Combine(root, "native-plan.json");

        internal OwnerInputs(Fixture files) : this(files.SourcePath, files.LedgerPath, files.RequestPath) { }
        internal OwnerInputs(string sourcePath, string ledgerPath, string runRequestPath)
        {
            Directory.CreateDirectory(root);
            var obligationRequest = Write("obligation-request.json", new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = sourcePath, StrategyLedgerPath = ledgerPath
            });
            var obligationPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(obligationRequest);
            var obligation = Write("obligation.json", obligationPlan);
            var request = JsonSerializer.Deserialize<MoneyRevenueCropNativeProductionRunPlanRequest>(File.ReadAllText(runRequestPath), JsonOptions)!;
            request.CropProductionPlanPath = obligation;
            var shortfallIds = obligationPlan.Commitments.Where(row => row.SeedQuantityShortfall > 0)
                .Select(row => row.CommitmentId).ToHashSet(StringComparer.Ordinal);
            request.SeedAcquisitionIntents = request.SeedAcquisitionIntents.Where(row => shortfallIds.Contains(row.CommitmentId)).ToArray();
            request.LayoutIntents = request.LayoutIntents.Select(layout => layout with
            {
                WateringRefillPreparationTiles = Array.Empty<MoneyRevenueCropPlantingTile>(),
                WateringRefillApproachTile = new(0, 1)
            }).ToArray();
            Request = request;
            Plan = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(Write("native-plan-request.json", request));
            Write("native-plan.json", Plan);
        }

        private string Write<T>(string name, T value)
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
            return path;
        }

        public void Dispose()
        {
            foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }

    internal sealed class OwnerFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "managed-owner-" + Guid.NewGuid().ToString("N"));
        private readonly HttpClient client;
        internal string OwnerRoot => Path.Combine(root, "owner");
        internal string Native => Path.Combine(OwnerRoot, "native-000001");
        internal string Recovery => Path.Combine(root, "recovery");
        internal CropTeacherManagedSeedOwnerOptions Options { get; }

        internal OwnerFiles(Fixture files, RuntimeFixture runtime, OwnerInputs inputs)
            : this(runtime.LiveOptions, runtime.Client, files.SourcePath, files.LedgerPath, inputs) { }
        internal OwnerFiles(LiveTrainingOptions options, HttpClient client, string sourcePath, string ledgerPath, OwnerInputs inputs)
        {
            this.client = client;
            Directory.CreateDirectory(root);
            Options = new(OwnerRoot, string.IsNullOrEmpty(options.RunId) ? "run.seed-acquisition.fixture" : options.RunId, options.PolicyTrajectoryExecutorVersion,
                options.BackendUrl, inputs.PlanPath, sourcePath, ledgerPath, options.SnapshotDir);
        }
        internal Task<CropTeacherManagedSeedOwner> Open(CropTeacherManagedSeedOwnerOptions? options = null,
            Func<string, string, Task>? writer = null) => CropTeacherManagedSeedOwner.OpenAsync(options ?? Options,
                writer ?? ((path, json) => InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json)));
        internal Task<JsonObject> Post(string endpoint, string raw) => InvokeLive<Task<JsonObject>>(
            "PostJsonStringAsync", client, endpoint, raw, CancellationToken.None);

        public void Dispose()
        {
            if (Directory.Exists(OwnerRoot))
                foreach (var native in Directory.EnumerateDirectories(OwnerRoot))
                {
                    var continuations = Path.Combine(native, "preparation-continuations");
                    if (Directory.Exists(continuations))
                    {
                        foreach (var attempt in Directory.EnumerateDirectories(continuations)) Clean(attempt);
                        Clean(continuations);
                    }
                    Clean(Path.Combine(native, "teacher-ledgers")); Clean(native);
                }
            Clean(OwnerRoot);
            if (Directory.Exists(root))
                foreach (var recovery in Directory.EnumerateDirectories(root))
                { Clean(Path.Combine(recovery, "teacher-ledgers")); Clean(recovery); }
            Clean(root);
        }
        private static void Clean(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.EnumerateFiles(path)) File.Delete(file);
            Directory.Delete(path);
        }
    }
}
