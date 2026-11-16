using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task SelectedSaleStockMainProducesOwnedPreparationAndFullOriginalFreshQueue()
    {
        using var f = new SelectedSaleStockFixture();
        await using var owner = await f.OwnerFiles.Open();
        await f.RetainDecision(owner);
        var originalSource = owner.CurrentSnapshotPath; var originalLedger = owner.CurrentLedgerPath;
        var result = await f.PrepareMain(owner);
        var other = result.GetType().GetProperty("OtherGoalPreparation")!.GetValue(result)!;
        Assert.Same(owner.Pending, other.GetType().GetProperty("Operation")!.GetValue(other));
        var source = (CropTeacherSharedSourceSelection)other.GetType().GetProperty("SourceSelection")!.GetValue(other)!;
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(source.QueuePath), JsonOptions)!;
        Assert.Equal("pending", queue.Status); Assert.Equal("earn_money", queue.GoalId);
        Assert.Contains(queue.Items, row => row.OptionId == "executor.move_to_tile");
        var transfer = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.transfer_material"));
        Assert.True(CropSaleStockMaterialTransferBinding.IsBound(transfer, f.Transfer.Runtime.Repository.Get(f.Transfer.Files.Before)));
        Assert.DoesNotContain(transfer.NormalizedCommand.Parameters, row => row.Name.EndsWith("prepare_request_json", StringComparison.Ordinal));
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits); Assert.Equal(0, f.Transfer.Runtime.Handler.Settlements);
        Assert.Equal(originalSource, owner.CurrentSnapshotPath); Assert.Equal(originalLedger, owner.CurrentLedgerPath);
        Assert.NotNull(source.MaterialTransferPreparation); Assert.False(File.Exists(Path.Combine(f.Options.SnapshotDir, "execution-0002.json")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.PrepareMain(owner, 3));
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await using var reopened = await f.OwnerFiles.Open();
        Assert.NotNull(reopened.Pending);
        var claim = await reopened.ClaimSharedSourceExecutionAsync(reopened.Pending!, f.Transfer.Runtime.Client);
        Assert.Equal(source.QueuePath, claim.Transition.QueuePath);
    }

    [Theory]
    [InlineData("unknown_control")]
    [InlineData("duplicate_marker")]
    [InlineData("marker")]
    [InlineData("binding")]
    public async Task SelectedSaleStockMainRejectsChangedOriginalPreviewBeforeAdmissionOrMutation(string fault)
    {
        using var f = new SelectedSaleStockFixture(fault);
        await using var owner = await f.OwnerFiles.Open();
        await f.RetainDecision(owner);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.PrepareMain(owner));
        Assert.Null(owner.Pending); Assert.Equal(0, f.Transfer.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task SelectedSaleStockMainExplicitRecoveryCompilesAcceptedPreparationWithoutAnotherCommitOrNative()
    {
        using var f = new SelectedSaleStockFixture(); var failed = false;
        await using var owner = await f.OwnerFiles.Open(writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == "marker-reply.json")
            { failed = true; throw new IOException("Declared selected stock accepted reply publication loss."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.RetainDecision(owner);
        await Assert.ThrowsAsync<IOException>(() => f.PrepareMain(owner));
        Assert.True(failed); Assert.Equal(1, f.Transfer.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await using var reopened = await f.OwnerFiles.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeLive<Task<string>>("ContinueCropTeacherSelectedSaleStockWithOwnerAsync",
            f.Transfer.Runtime.Client, f.Options, reopened));
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits);
        var path = await InvokeLive<Task<string>>("RecoverCropTeacherSelectedSaleStockWithOwnerAsync", f.Transfer.Runtime.Client, f.Options, reopened);
        var report = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.True(report["preparation_only"]!.GetValue<bool>());
        Assert.False(report["native_dispatch_performed"]!.GetValue<bool>());
        Assert.Equal("pending", JsonNode.Parse(File.ReadAllText(report["queue_path"]!.GetValue<string>()))!["status"]!.GetValue<string>());
        Assert.Equal(1, f.Transfer.Runtime.Handler.Lookups); Assert.Equal(1, f.Transfer.Runtime.Handler.Commits);
        Assert.NotNull(reopened.Pending); Assert.Equal(0, f.Transfer.Runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedSaleStockMainExplicitFirstPreparationContinuationPreservesUnattemptedEvidence(bool admissionOnly)
    {
        using var f = new SelectedSaleStockFixture(); var failed = false;
        await using var owner = await f.OwnerFiles.Open(writer: async (path, json) =>
        {
            if (!admissionOnly && !failed && Path.GetFileName(path) == "support-intent.json")
            { failed = true; throw new IOException("Declared interruption before the first preparation RPC."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.RetainDecision(owner);
        if (admissionOnly)
            await owner.AdmitSelectedSaleStockPreparationAsync(f.Decision,
                Path.Combine(f.Options.SnapshotDir, "execution-0002.json"), Path.Combine(f.Options.SnapshotDir, "after-snapshot-0002.json"),
                Path.Combine(f.OwnerFiles.OwnerRoot, "shared-source-0002"));
        else await Assert.ThrowsAsync<IOException>(() => f.PrepareMain(owner));
        Assert.Equal(0, f.Transfer.Runtime.Handler.Commits);
        var summary = owner.Pending!.RuntimeDirectory + "-prepare" + Path.DirectorySeparatorChar + "preparation-summary.json";
        var hash = admissionOnly ? null : SharedTransferReturnFixture.Hash(summary);
        await owner.DisposeAsync();
        await using var reopened = await f.OwnerFiles.Open();
        var path = await InvokeLive<Task<string>>("ContinueCropTeacherSelectedSaleStockWithOwnerAsync", f.Transfer.Runtime.Client, f.Options, reopened);
        var report = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.True(report["first_preparation_continued"]!.GetValue<bool>());
        Assert.False(report["original_preparation_recovered"]!.GetValue<bool>());
        Assert.False(report["native_dispatch_performed"]!.GetValue<bool>());
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits); Assert.Equal(0, f.Transfer.Runtime.Handler.Lookups);
        if (hash is not null) Assert.Equal(hash, SharedTransferReturnFixture.Hash(summary));
        Assert.False(Directory.Exists(reopened.Pending!.RuntimeDirectory));
    }

    [Fact]
    public async Task SelectedSaleStockMainRetainsOriginalPreviewAcrossAcceptedOtherWorkAndReallocatesDestinationOnly()
    {
        using var f = new SelectedSaleStockFixture();
        await using var owner = await f.OwnerFiles.Open();
        await f.RetainDecision(owner);
        var before = owner.CurrentSnapshotPath;
        var (seedDecision, request) = PreviewSharedPreparation(f.Transfer);
        var receipt = Path.Combine(f.Options.SnapshotDir, "declared-seed-receipt.json");
        var after = Path.Combine(f.Options.SnapshotDir, "declared-seed-after.json");
        var seed = await owner.AdmitSharedTransferPreparationAsync(seedDecision, request, receipt, after, f.OwnerFiles.Native + "-seed");
        var prepared = await owner.PrepareSharedTransferAsync(seed, f.Transfer.Runtime.Client,
            (diagnostic, actual) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Transfer.Runtime.Client, f.Options, diagnostic, actual),
            (ledger, output) => Task.FromResult(PrepareSharedTransferSelection(f.Transfer, ledger, output, seedDecision, updateTransition: false)));
        f.Transfer.PreparationReference = prepared.MaterialTransferPreparation!;
        f.Transfer.Transition = f.Transfer.Transition with { QueuePath = prepared.QueuePath, ExecutionReceiptPath = receipt, AfterSnapshotPath = after };
        await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Transfer.Runtime.Client, f.Options,
            owner, seed, prepared.QueuePath, (Func<Task<JsonObject>>)(async () =>
            {
                Directory.CreateDirectory(f.Options.SnapshotDir);
                await f.Transfer.Build(includePreparation: true, settle: false, alreadyPrepared: true);
                return new() { ["execution_path"] = receipt, ["after_snapshot_path"] = after };
            }));
        Assert.Null(owner.Pending); Assert.NotEqual(before, owner.CurrentSnapshotPath);
        var result = await f.PrepareMain(owner);
        var actual = f.Transfer.Runtime.Repository.Get(f.Transfer.Files.After).PreparedMaterialTransfer!;
        Assert.Equal(0, Assert.Single(f.Preview.Request.MaterialTransferPreparation!.MaterialRelocations).DestinationSlotIndex);
        Assert.Equal(1, Assert.Single(actual.MaterialRelocations).DestinationSlotIndex);
        Assert.Equal(f.Preview.Request.MaterialTransferPreparation.Intent.DestinationNodeId, actual.Intent.DestinationNodeId);
        Assert.Equal(f.Preview.Request.MaterialTransferPreparation.MaterialRelocations[0].MaterialReservationId,
            Assert.Single(actual.MaterialRelocations).MaterialReservationId);
        Assert.Equal(3, f.Transfer.Runtime.Handler.Commits); Assert.Equal(1, f.Transfer.Runtime.Handler.Settlements);
        var other = result.GetType().GetProperty("OtherGoalPreparation")!.GetValue(result)!;
        var source = (CropTeacherSharedSourceSelection)other.GetType().GetProperty("SourceSelection")!.GetValue(other)!;
        Assert.Equal(f.Decision.SnapshotPath, source.DecisionSnapshotPath);
        Assert.Equal("grandpa21", JsonNode.Parse(File.ReadAllText(source.DecisionPlanPath))!["goal_id"]!.GetValue<string>());
        Assert.Equal("earn_money", JsonNode.Parse(File.ReadAllText(source.ModelPlanPath))!["goal_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task SelectedSaleStockMainRecoveryCommandReopensOnlyTheOriginalBoundOwner()
    {
        using var f = new SelectedSaleStockFixture(); var failed = false;
        f.Options.UseManagedCropTeacher = true; f.Options.UseProductExecutor = true;
        f.Options.RequireExecutorFeedback = true; f.Options.RequireNativeSaveBoundary = true;
        f.Options.SaveIsolationPath = Path.Combine(Path.GetTempPath(), "declared-stock-save-" + Guid.NewGuid().ToString("N"));
        f.Options.SaveSlot = "declared-stock-command-slot";
        f.Options.ResumeManagedCropOwnerDirectory = f.OwnerFiles.OwnerRoot;
        var options = f.OwnerFiles.Options with { ExecutorVersion = f.Options.PolicyTrajectoryExecutorVersion,
            UseSelectedRefillResourceEffects = true,
            NativeSaveBinding = new(f.Options.SaveIsolationPath, f.Options.SaveSlot) };
        await using var owner = await f.OwnerFiles.Open(options, writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == "marker-reply.json")
            { failed = true; throw new IOException("Declared command-entry recovery reply loss."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.RetainDecision(owner);
        await Assert.ThrowsAsync<IOException>(() => f.PrepareMain(owner));
        await owner.DisposeAsync();
        var header = Path.Combine(f.OwnerFiles.OwnerRoot, "owner-intent.json");
        var hash = SharedTransferReturnFixture.Hash(header);
        var report = await InvokeLive<Task<string>>("RecoverCropTeacherSelectedSaleStockAsync", f.Transfer.Runtime.Client, f.Options);
        Assert.Equal(hash, SharedTransferReturnFixture.Hash(header));
        Assert.True(JsonNode.Parse(File.ReadAllText(report))!["preparation_only"]!.GetValue<bool>());
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits); Assert.Equal(1, f.Transfer.Runtime.Handler.Lookups);
    }
}
