using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task WateringOwnerCannotEnterSeedRouteOrDuplicateCompilationCallbacks()
    {
        using var f = new WateringFixture();
        await f.Emit();
        await using var owner = await f.Shared.Files.Open();
        var operation = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, f.Shared.Files.Native);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareSeedAsync<CropTeacherSeedPurchaseRuntime>(operation, () =>
        { calls++; throw new InvalidOperationException("Wrong callback must not run."); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteSeedAsync(operation, _ =>
        { calls++; return Task.FromResult(0); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CompileAndAdmitOriginalRouteAsync(() =>
        { calls++; throw new InvalidOperationException("Pending maintenance cannot compile another route."); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.RunSupportingRouteAsync(operation, f.Shared.Runtime.Client,
            (_, _) => { calls++; throw new InvalidOperationException("Wrong route marker must not run."); }, f.Shared.Files.Post));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-started.json")));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("foreign_run")]
    [InlineData("foreign_executor")]
    [InlineData("uncovered")]
    public async Task WateringOwnerRejectsForeignIdentityBeforeAdmission(string changed)
    {
        using var f = new WateringFixture();
        await f.Emit();
        await using var owner = await f.Shared.Files.Open();
        var transition = changed == "foreign_run" ? f.Transition with { RunId = "foreign-run" } :
            changed == "foreign_executor" ? f.Transition with { ExecutorVersion = "foreign-executor" } : f.Transition;
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitWateringMaintenanceAsync(
            changed == "uncovered" ? "other-crop" : Id, transition, f.Shared.Files.Native));
        Assert.Null(owner.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("queue")]
    [InlineData("step")]
    [InlineData("receipt")]
    public async Task WateringOwnerPinsNativeInputsBeforeFirstMarkerInvocation(string changed)
    {
        using var f = new WateringFixture();
        await f.Emit();
        await using var owner = await f.Shared.Files.Open();
        var operation = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, f.Shared.Files.Native);
        var path = changed == "queue" ? f.QueuePath : changed == "receipt" ? f.Transition.ExecutionReceiptPath :
            Path.Combine(f.Shared.Runtime.LiveOptions.SnapshotDir, "water-step-0.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["unexpected_after_admission"] = true;
        Fixture.Save(path, node);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => RunWateringOwner(f, owner, operation,
            (_, _) => { calls++; throw new InvalidOperationException("Changed input cannot submit a marker."); }));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-started.json")));
    }

    [Fact]
    public async Task WateringOwnerUnknownMutationIsNotZeroAndNeverResent()
    {
        using var f = new WateringFixture();
        await f.Emit();
        await using var owner = await f.Shared.Files.Open();
        var operation = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, f.Shared.Files.Native);
        await Assert.ThrowsAsync<IOException>(() => RunWateringOwner(f, owner, operation,
            (_, _) => throw new IOException("Unknown original marker boundary without a durable journal.")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunWateringOwner(f, owner, operation,
            (_, _) => throw new InvalidOperationException("Must not resend.")));
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        await Assert.ThrowsAnyAsync<IOException>(() => reopened.RecoverPendingAsync(
            f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post));
        Assert.NotNull(reopened.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(0, f.Shared.Runtime.Handler.Lookups);
    }

    [Theory]
    [InlineData("bad_reply")]
    [InlineData("missing_journal")]
    [InlineData("backend_drift")]
    public async Task WateringOwnerRequiresOriginalJournalExactReplyAndCompleteAuthority(string changed)
    {
        using var f = new WateringFixture();
        await f.Emit();
        await using var owner = await f.Shared.Files.Open();
        var operation = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, f.Shared.Files.Native);
        if (changed == "backend_drift")
            Assert.True(f.Shared.Runtime.Repository.CommitReservationPortfolio(f.Before, new ReservationPortfolioCommitRequest
            {
                StateHash = f.Before.StateHash, ExpectedLedgerRevision = f.Shared.Source.Ledger.Revision,
                PortfolioId = "other-new-step", SourceDecisionId = "other-new-step", GoalId = "earn_money"
            }).Accepted);
        var error = await Record.ExceptionAsync(() => RunWateringOwner(f, owner, operation, async (diagnostic, request) =>
        {
            if (changed == "missing_journal") return JsonSerializer.SerializeToNode(
                new StardewAI.Core.Strategy.ReservationPortfolioLedgerService().Commit(f.Shared.Source.Ledger, f.After, request, RecordedAt), JsonOptions)!.AsObject();
            var reply = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Shared.Runtime.Client,
                f.Shared.Runtime.LiveOptions, diagnostic, request);
            if (changed == "bad_reply") reply["ledger"]!["currency_reservations"]![0]!["amount"] = 999;
            return reply;
        }));
        if (changed == "missing_journal") Assert.IsAssignableFrom<IOException>(error);
        else Assert.IsType<InvalidDataException>(error);
        Assert.Same(operation, owner.Pending);
        Assert.Equal(changed == "bad_reply" ? 1 : 0, f.Shared.Runtime.Handler.Commits);
        Assert.Empty(Directory.GetFiles(f.Shared.Files.OwnerRoot, "operation-*-accepted.json"));
    }
}
