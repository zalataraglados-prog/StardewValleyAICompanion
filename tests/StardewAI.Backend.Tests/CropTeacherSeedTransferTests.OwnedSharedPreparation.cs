using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("earn_money")]
    [InlineData("grandpa21")]
    public async Task SharedPreparationOwnsOriginalAdmissionBeforeCommitAndReturnsThroughOriginalMain(string parentGoal)
    {
        using var f = new SharedTransferReturnFixture();
        var (decision, request) = PreviewSharedPreparation(f, parentGoal);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        await using var owner = await files.Open();
        var receipt = Path.Combine(files.Native + "-outputs", "receipt.json");
        var after = Path.Combine(files.Native + "-outputs", "after.json");
        var originalHashes = new[] { f.Files.SourcePath, f.Files.LedgerPath }.ToDictionary(path => path, SharedTransferReturnFixture.Hash);
        var operation = await owner.AdmitSharedTransferPreparationAsync(decision, request, receipt, after, files.Native);
        Assert.Equal(0, f.Runtime.Handler.Commits); Assert.Same(operation, owner.Pending);
        var admission = Directory.GetFiles(Path.GetDirectoryName(files.Native)!, "operation-*-intent.json").Single();
        var admissionHash = SharedTransferReturnFixture.Hash(admission);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
        var selection = await owner.PrepareSharedTransferAsync(operation, f.Runtime.Client, async (diagnostic, actual) =>
        {
            Assert.Same(operation, owner.Pending); Assert.Equal(admissionHash, SharedTransferReturnFixture.Hash(admission));
            return await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, actual);
        }, (ledger, root) => Task.FromResult(PrepareSharedTransferSelection(f, ledger, root, decision, updateTransition: false)));
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(admissionHash, SharedTransferReturnFixture.Hash(admission));
        Assert.NotNull(selection.MaterialTransferPreparation);
        f.PreparationReference = selection.MaterialTransferPreparation!;
        f.Transition = f.Transition with { QueuePath = selection.QueuePath, ExecutionReceiptPath = receipt, AfterSnapshotPath = after };
        var calls = 0;
        await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client, f.Runtime.Options,
            owner, operation, selection.QueuePath, (Func<Task<JsonObject>>)(async () =>
            {
                calls++;
                Directory.CreateDirectory(Path.GetDirectoryName(after)!);
                await f.Build(includePreparation: true, settle: false, alreadyPrepared: true);
                return new() { ["execution_path"] = receipt, ["after_snapshot_path"] = after };
            }));
        Assert.Equal(1, calls); Assert.Null(owner.Pending);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        Assert.All(originalHashes, pair => Assert.Equal(pair.Value, SharedTransferReturnFixture.Hash(pair.Key)));
        Assert.Single((await owner.ReadJointProductionTraceAsync()).SharedSourceReturns);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(after, reopened.CurrentSnapshotPath);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(2, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("reply")]
    [InlineData("compile")]
    [InlineData("prepared_record")]
    public async Task SharedPreparationRecoversAcceptedReplyAndFreshQueueWithoutAnotherCommit(string failure)
    {
        using var f = new SharedTransferReturnFixture();
        var (decision, request) = PreviewSharedPreparation(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        var failed = false;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (!failed && failure == "prepared_record" && Path.GetFileName(path).EndsWith("-prepared.json", StringComparison.Ordinal))
            { failed = true; throw new IOException("Declared prepared-record publication interruption."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var operation = await owner.AdmitSharedTransferPreparationAsync(decision, request,
            Path.Combine(files.Native + "-outputs", "receipt.json"), Path.Combine(files.Native + "-outputs", "after.json"), files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSharedTransferAsync(operation, f.Runtime.Client,
            async (diagnostic, actual) =>
            {
                var reply = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, actual);
                if (failure == "reply") throw new IOException("Declared accepted reply loss.");
                return reply;
            }, (ledger, root) => failure == "compile" ? throw new IOException("Declared fresh compiler interruption.") :
                Task.FromResult(PrepareSharedTransferSelection(f, ledger, root, decision, updateTransition: false))));
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Same(operation, owner.Pending);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        var pending = reopened.Pending!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.PrepareSharedTransferAsync(pending, f.Runtime.Client,
            (_, _) => throw new Xunit.Sdk.XunitException("Must not resend preparation."),
            (_, _) => throw new Xunit.Sdk.XunitException("Must use explicit recovery.")));
        var selection = await reopened.RecoverSharedTransferPreparationAsync(pending, f.Runtime.Client, files.Post,
            (ledger, root) => Task.FromResult(PrepareSharedTransferSelection(f, ledger, root, decision, updateTransition: false)));
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(failure == "reply" ? 1 : 0, f.Runtime.Handler.Lookups);
        Assert.Equal(selection.QueuePath, (await reopened.ClaimSharedSourceExecutionAsync(pending, f.Runtime.Client)).Transition.QueuePath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Runtime.Client));
        await reopened.DisposeAsync();
        await using var claimed = await files.Open();
        Assert.NotNull(claimed.Pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => claimed.ClaimSharedSourceExecutionAsync(claimed.Pending!, f.Runtime.Client));
        Assert.Equal(1, f.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task SharedPreparationRejectsFreshSelectionFromAnotherOriginalDecision()
    {
        using var f = new SharedTransferReturnFixture();
        var (decision, request) = PreviewSharedPreparation(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        await using var owner = await files.Open();
        var operation = await owner.AdmitSharedTransferPreparationAsync(decision, request,
            files.Native + "-receipt.json", files.Native + "-after.json", files.Native);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.PrepareSharedTransferAsync(operation, f.Runtime.Client,
            (diagnostic, actual) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, actual),
            (ledger, root) => Task.FromResult(PrepareSharedTransferSelection(f, ledger, root, decision, updateTransition: false)
                with { SelectedQueueIndex = decision.SelectedQueueIndex + 1 })));
        Assert.Equal(1, f.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
        Assert.False(Directory.Exists(files.Native));
    }

    private static (CropTeacherSharedTransferDecision Decision, ReservationPortfolioCommitRequest Request) PreviewSharedPreparation(
        SharedTransferReturnFixture f, string parentGoal = "earn_money")
    {
        // Prospective ORIGINAL seed transfer is a declared supporting fixture, not stock/game/Teacher evidence.
        var scope = f.Files.Open();
        var request = scope.PreparationRequest;
        var preview = new ReservationPortfolioLedgerService().Commit(f.Files.Ledger, f.Files.Before, request, Timestamp);
        var marker = scope.VerifyPreparationReply(preview);
        f.NativeTemplate = f.Files.NativeTransition(marker);
        f.Transition = f.NativeTemplate with { TransitionKind = "shared_source_return" };
        var selection = PrepareSharedTransferSelection(f, preview.Ledger!, updateTransition: false);
        var decisionPlan = JsonNode.Parse(File.ReadAllText(selection.DecisionPlanPath))!.AsObject();
        decisionPlan["goal_id"] = parentGoal;
        Fixture.Save(selection.DecisionPlanPath, decisionPlan);
        return (new(selection.DecisionSnapshotPath, selection.DecisionPlanPath, selection.DecisionRankingPath,
            selection.SelectedQueueIndex, selection.OwnedCandidateIds), request);
    }

    [Fact]
    public async Task SharedPreparationCannotReplacePreparedRecordBeforeClaim()
    {
        using var f = new SharedTransferReturnFixture();
        var (decision, request) = PreviewSharedPreparation(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        await using var owner = await files.Open();
        var operation = await owner.AdmitSharedTransferPreparationAsync(decision, request,
            files.Native + "-receipt.json", files.Native + "-after.json", files.Native);
        await owner.PrepareSharedTransferAsync(operation, f.Runtime.Client,
            (diagnostic, actual) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, actual),
            (ledger, root) => Task.FromResult(PrepareSharedTransferSelection(f, ledger, root, decision, updateTransition: false)));
        File.AppendAllText(Directory.GetFiles(files.OwnerRoot, "operation-*-prepared.json").Single(), "\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
        Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.False(Directory.Exists(files.Native));
    }
}
