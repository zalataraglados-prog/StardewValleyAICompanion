using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData("live")]
    [InlineData("pending")]
    [InlineData("summary_missing")]
    [InlineData("context_ack_lost")]
    [InlineData("lost_acceptance")]
    [InlineData("mutated_return")]
    [InlineData("lying_summary")]
    public async Task OwnedControllerStopClosesWithoutNativeOutcomeOrResendAndAllowsANewAdmission(string mode)
    {
        using var f = new ControllerStopFixture();
        await using var owner = await f.PrepareOwner(async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "lost_acceptance" && Path.GetFileName(path) == "operation-000001-accepted.json")
                throw new IOException("Durable stop acceptance acknowledgement lost.");
        });
        await f.Stop(owner);
        if (mode == "summary_missing") f.Runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "completion-summary.json");
        if (mode == "context_ack_lost") f.Runtime.AfterWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "controller-stop-transition.json");
        if (mode is "summary_missing" or "context_ack_lost") await Assert.ThrowsAsync<IOException>(() => f.Complete());
        else
        {
            var result = await f.Complete();
            if (mode == "mutated_return") result.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity = 999;
        }
        if (mode == "lying_summary") File.WriteAllText(Path.Combine(f.Files.Native, "completion-summary.json"), "{\"native_request_count\":999,\"success\":true}");
        f.AssertNoNativeEffect();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.AcceptLiveAsync(f.Operation, f.Seed));
        var live = mode is "live" or "mutated_return" or "lost_acceptance";
        if (mode == "lost_acceptance") await Assert.ThrowsAsync<IOException>(() => owner.AcceptStoppedSeedAsync(f.Operation, f.Seed));
        else if (live)
        {
            var reads = f.Runtime.Handler.LedgerReads;
            await owner.AcceptStoppedSeedAsync(f.Operation, f.Seed);
            Assert.Equal(reads, f.Runtime.Handler.LedgerReads);
        }
        await owner.DisposeAsync();
        await using var resumed = await f.Files.Open();
        if (live) await resumed.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else await resumed.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.Null(resumed.Pending);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal("controller_stop_no_native_dispatch", accepted["seed_boundary_kind"]!.GetValue<string>());
        Assert.False(accepted["native_outcome_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["native_request_count"]!.GetValue<int>());
        Assert.Null(accepted["original_proof_sha256"]);
        Assert.False(File.Exists(Path.Combine(f.Files.Native, "support-proof.json")));
        var current = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(resumed.CurrentLedgerPath), JsonOptions)!;
        Assert.Equal(9, current.MaterialReservations.Single(row => row.ReservationId == f.Runtime.Files.OwnedSlotOne).Quantity);
        var next = await resumed.AdmitAsync(f.Step, resumed.CurrentSnapshotPath, resumed.CurrentLedgerPath,
            Path.Combine(f.Files.OwnerRoot, "native-000002"));
        Assert.Equal(2, next.Sequence);
        Assert.NotEqual(f.Operation.SupportStepId, next.SupportStepId);
        f.AssertNoNativeEffect();
    }

    private sealed class ControllerStopFixture : IDisposable
    {
        internal RuntimeFixture Runtime { get; } = new();
        internal OwnerInputs Inputs { get; }
        internal OwnerFiles Files { get; }
        internal CropTeacherRunStep Step { get; }
        internal CropTeacherManagedSeedOperation Operation { get; private set; } = null!;
        internal CropTeacherSeedPlantingRuntime Seed { get; private set; } = null!;
        internal JsonObject Receipt { get; private set; } = null!;

        internal ControllerStopFixture()
        {
            Inputs = new(Runtime.Files.SourcePath, Runtime.Files.LedgerPath, Runtime.Files.RequestPath);
            Files = new(Runtime.Options, Runtime.Client, Runtime.Files.SourcePath, Runtime.Files.LedgerPath, Inputs);
            Step = CropTeacherRunPlanner.Evaluate(Inputs.Plan, Inputs.Request, Runtime.Files.Before,
                preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { Runtime.Files.Id }, resourceLedger: Runtime.Files.Ledger)
                with { SeedReservationId = Runtime.Files.OwnedSlotOne };
        }

        internal async Task<CropTeacherManagedSeedOwner> PrepareOwner(Func<string, string, Task>? writer = null)
        {
            var owner = await Files.Open(writer: writer);
            try
            {
                Operation = await owner.AdmitAsync(Step, Runtime.Files.SourcePath, Runtime.Files.LedgerPath, Files.Native);
                Seed = await owner.PrepareSeedAsync(Operation, () => Runtime.Prepare(selectedScope: Operation.Scopes.Planting,
                    runtimeDirectory: Files.Native, selectedStep: Operation.ReadStep()));
                return owner;
            }
            catch { await owner.DisposeAsync(); throw; }
        }

        internal async Task Stop(CropTeacherManagedSeedOwner owner) => Receipt = await owner.ExecuteSeedAsync(Operation, original =>
        {
            original.ValidateDispatchQueue(Runtime.Files.QueuePath);
            return Runtime.Execute(Seed, attachOwner: false);
        });

        internal Task<StrategyLedgerEvidence> Complete() => Seed.CompleteAsync(Runtime.Files.QueuePath,
            Receipt["execution_path"]!.GetValue<string>(), Receipt["after_snapshot_path"]!.GetValue<string>(), Runtime.Transition.SelectedCandidateId);

        internal void AssertNoNativeEffect()
        {
            Assert.Equal(0, Runtime.NativeRequests);
            Assert.Equal(0, Runtime.FullSnapshotReads);
            Assert.Equal(1, Runtime.Handler.Commits);
            Assert.Equal(0, Runtime.Handler.Settlements);
        }

        public void Dispose() { Files.Dispose(); Inputs.Dispose(); Runtime.Dispose(); }
    }
}
