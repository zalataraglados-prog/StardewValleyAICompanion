using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedSharedYieldClaimsOriginalNativeOnceThenSettlesAndReturnsTheSameOwner(bool product)
    {
        using var f = new SharedTransferReturnFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        CropTeacherManagedSeedOperation operation = null!;
        var hashes = new[] { f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath }.ToDictionary(path => path, SharedTransferReturnFixture.Hash);
        await f.Build(product, includePreparation: true, beforeNative: async fixture =>
        {
            var selection = PrepareSharedTransferSelection(fixture);
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSharedSourceYieldAsync(selection,
                f.Transition.ExecutionReceiptPath, f.Transition.AfterSnapshotPath, files.Native));
            operation = await owner.AdmitPreparedSharedTransferYieldAsync(selection, f.Transition.ExecutionReceiptPath,
                f.Transition.AfterSnapshotPath, files.Native, f.PreparationReference);
            await owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client);
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
        }, settle: false);
        await Assert.ThrowsAnyAsync<IOException>(() => owner.VerifySharedSourceExecutionAsync(operation));
        Assert.Equal(0, f.Runtime.Handler.Settlements);
        await owner.SettleSharedTransferExecutionAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request));
        await owner.VerifySharedSourceExecutionAsync(operation);
        await owner.RunSharedSourceReturnAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request), files.Post);
        Assert.Null(owner.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, owner.CurrentSnapshotPath);
        var trace = await owner.ReadJointProductionTraceAsync();
        var sourceReference = Assert.Single(trace.SharedSourceReturns).SourceReference!;
        Assert.Equal(f.PreparationReference, sourceReference.SettledMaterialTransfer!.Preparation);
        var transition = f.Transition with { NativeSharedSource = sourceReference };
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedMaterialTransfers(transition));
        Assert.Equal(f.Files.After.StateHash, MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedTransferYields(transition).AfterStateHash);
        Assert.Empty(trace.CropTrace.Transitions);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        Assert.All(hashes, pair => Assert.Equal(pair.Value, SharedTransferReturnFixture.Hash(pair.Key)));
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Equal(f.Transition.AfterSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        File.AppendAllText(sourceReference.SettledTransferReferencePath!, "\n");
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedTransferYields(transition));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedSharedYieldUsesTheMainOriginalExecutionAndSettlementHook(bool product)
    {
        using var f = new SharedTransferReturnFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var ready = new TaskCompletionSource<CropTeacherManagedSeedOperation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = f.Build(product, includePreparation: true, beforeNative: async fixture =>
        {
            var selection = PrepareSharedTransferSelection(fixture);
            ready.SetResult(await owner.AdmitPreparedSharedTransferYieldAsync(selection, f.Transition.ExecutionReceiptPath,
                f.Transition.AfterSnapshotPath, files.Native, f.PreparationReference));
            await invoke.Task;
        }, settle: false);
        await Task.WhenAny(ready.Task, native);
        if (!ready.Task.IsCompleted) await native;
        var operation = await ready.Task;
        var calls = 0;
        Func<Task<JsonObject>> execute = async () =>
        {
            calls++;
            Assert.True(File.Exists(Path.Combine(operation.RuntimeDirectory, "shared-source-native-claim.json")));
            invoke.TrySetResult();
            await native;
            return new JsonObject { ["execution_path"] = f.Transition.ExecutionReceiptPath, ["after_snapshot_path"] = f.Transition.AfterSnapshotPath };
        };
        try
        {
            await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client,
                f.Runtime.Options, owner, operation, f.Transition.QueuePath, execute);
        }
        finally { invoke.TrySetResult(); await native; }
        Assert.Equal(1, calls); Assert.Null(owner.Pending);
        Assert.Equal(f.Transition.AfterSnapshotPath, owner.CurrentSnapshotPath);
        Assert.Equal(1, f.Runtime.Handler.Settlements); Assert.Equal(2, f.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync",
            f.Runtime.Client, f.Runtime.Options, owner, operation, f.Transition.QueuePath, execute));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedSharedYieldRecoversOriginalSettlementWithoutAnotherNativeOrMutation(bool loseReply)
    {
        using var f = new SharedTransferReturnFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        var failed = false;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == (loseReply ? "shared-transfer-settlement-reply.json" : "shared-transfer-settlement.json"))
            {
                failed = true; throw new IOException("Declared shared transfer settlement publication failure.");
            }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        CropTeacherManagedSeedOperation operation = null!;
        await f.Build(includePreparation: true, beforeNative: async fixture =>
        {
            var selection = PrepareSharedTransferSelection(fixture);
            operation = await owner.AdmitPreparedSharedTransferYieldAsync(selection, f.Transition.ExecutionReceiptPath,
                f.Transition.AfterSnapshotPath, files.Native, f.PreparationReference);
            await owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client);
        }, settle: false);
        await Assert.ThrowsAsync<IOException>(() => owner.SettleSharedTransferExecutionAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request)));
        Assert.True(failed); Assert.Equal(1, f.Runtime.Handler.Settlements);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SettleSharedTransferExecutionAsync(operation, f.Runtime.Client,
            (_, _) => throw new Xunit.Sdk.XunitException("Must not retry settlement.")));
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        var pending = reopened.Pending!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Runtime.Client));
        await reopened.RecoverSharedTransferSettlementAsync(pending, f.Runtime.Client, files.Recovery, files.Post);
        await reopened.VerifySharedSourceExecutionAsync(pending);
        await reopened.RunSharedSourceReturnAsync(pending, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request), files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.Equal(loseReply ? 1 : 0, f.Runtime.Handler.Lookups);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }

    private static CropTeacherSharedSourceSelection PrepareSharedTransferSelection(SharedTransferReturnFixture f)
    {
        // Real ranker/compiler, declared availability and native observations; never game/Teacher credit.
        var old = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.Transition.QueuePath), JsonOptions)!;
        var parameters = Assert.Single(old.Items).NormalizedCommand.Parameters;
        var availability = new OptionAvailabilityEnvelope
        {
            StateHash = f.Files.Before.StateHash,
            Options = [new OptionAvailability { OptionId = "inventory.transfer_item", Available = true, ReadEligible = true,
                EventCandidates = [new EventCandidate { CandidateId = f.Transition.SelectedCandidateId, Kind = "transfer_inventory_item",
                    Available = true, LocationId = "Farm", EstimatedTicks = 60, Parameters = parameters }] }]
        };
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "earn_money");
        var plan = new DailyPlanCompiler().Compile(rows, f.Files.Before.StateHash, goalId: "earn_money",
            maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
        // Explicit seed terminal slice at the already reached chest; old daily seed defaults stay unchanged.
        plan.Steps = plan.Steps.Where(step => step.Kind == "transfer_material").ToArray();
        Assert.Single(plan.Steps).Parameters = Assert.Single(plan.Steps).Parameters.Concat(parameters.Where(row =>
            CropSeedMaterialTransferBinding.IsControlParameter(row.Name) || row.Name == "prepared_material_transfer_sha256")).ToArray();
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
        var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
        var queue = new ActionQueueCompiler().Compile(plan, f.Files.Before, f.Runtime.Repository.Get(f.Files.Before));
        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(item => item.BlockingReasons))); Assert.Single(queue.Items);
        var evidence = new JsonObject
        {
            ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = f.Files.Before.StateHash,
            ["goal_id"] = plan.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
            ["selected_queue_index"] = selected.QueueIndex, ["selected_candidate_id"] = selected.CandidateId,
            ["selected_option_id"] = selected.OptionId, ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
        };
        string Save<T>(string name, T value) { var path = Path.Combine(f.Files.RuntimePath, name); Fixture.Save(path, value); return path; }
        var queuePath = Save("shared-prepared-yield-queue.json", queue);
        f.Transition = f.Transition with { QueuePath = queuePath, SelectedCandidateId = selected.CandidateId,
            ExecutionReceiptPath = Path.Combine(f.Files.RuntimePath, "shared-prepared-native-receipt.json"),
            AfterSnapshotPath = Path.Combine(f.Files.RuntimePath, "shared-prepared-after.json") };
        return new(f.Files.SourcePath, Save("shared-prepared-decision.json", plan), Save("shared-prepared-ranking.json", ranking),
            Save("shared-prepared-fresh.json", plan), queuePath, Save("shared-prepared-refresh.json", evidence), selected.QueueIndex, [])
            { MaterialTransferPreparation = f.PreparationReference };
    }
}
