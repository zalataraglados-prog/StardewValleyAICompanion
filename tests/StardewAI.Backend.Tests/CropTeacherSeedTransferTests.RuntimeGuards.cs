using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("marker_reply")]
    [InlineData("marker_readback")]
    public async Task TransferCorruptPreparationNeverReachesCompiler(string fault)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f, fault: fault);
        var compiled = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Prepare(compile: (_, _) =>
        {
            compiled = true;
            throw new InvalidOperationException("must not compile");
        }));
        Assert.False(compiled);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal("crop_seed_support_preparation_incomplete", runtime.Summary("preparation-summary.json")["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("settlement_reply")]
    [InlineData("final_readback")]
    public async Task TransferCorruptSettlementOrReadbackCannotReportSuccess(string fault)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f, fault: fault);
        var prepared = await runtime.Prepare();
        runtime.ObserveAfter();
        await Assert.ThrowsAsync<InvalidDataException>(() => prepared.CompleteAsync(runtime.Transition));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal("crop_seed_support_completion_incomplete", runtime.Summary()["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("queue")]
    [InlineData("source")]
    [InlineData("candidate")]
    public async Task TransferExecutionIdentityDriftRejectsBeforeSettlement(string fault)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        var prepared = await runtime.Prepare();
        var transition = runtime.Transition;
        if (fault == "candidate") transition = transition with { SelectedCandidateId = "another-candidate" };
        else
        {
            var path = fault == "source" ? f.SourcePath : transition.QueuePath;
            await File.AppendAllTextAsync(path, " ");
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => prepared.CompleteAsync(transition));
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(1, runtime.Handler.Commits);
    }

    [Fact]
    public async Task TransferCompileFailureAndDuplicatePreparationRetainPendingClaim()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        var scope = f.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Prepare(scope, (_, _) =>
            throw new InvalidOperationException("compiler rejected")));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(f.Ledger.Revision + 1, runtime.Repository.Get(f.Before).Revision);
        Assert.NotNull(runtime.Repository.Get(f.Before).PreparedMaterialTransfer);
        await Assert.ThrowsAsync<IOException>(() => runtime.Prepare(scope));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }

    [Fact]
    public async Task TransferWrongLocationRejectsBeforeAnyMutation()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Prepare(expectedLocation: "Town"));
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.LedgerReads);
        Assert.False(Directory.Exists(f.RuntimePath));
    }

    [Theory]
    [InlineData("reservation")]
    [InlineData("intent")]
    [InlineData("quantity")]
    [InlineData("day")]
    [InlineData("executor")]
    public async Task TransferOriginalControlRejectsMismatchedStepBeforePreparation(string fault)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        var scope = f.Open();
        var step = runtime.StepFor(scope);
        if (fault == "reservation") step = step with { SeedReservationId = "other-seeds" };
        if (fault == "intent") step = step with { SeedTransferIntent = null };
        if (fault == "quantity") step = step with { RemainingQuantity = 9 };
        if (fault == "day") step = step with { TotalDay = step.TotalDay + 1 };
        if (fault == "executor") runtime.Options.EmitQueueExecutionReceipt = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.BuildControl(scope, step));
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }
}
