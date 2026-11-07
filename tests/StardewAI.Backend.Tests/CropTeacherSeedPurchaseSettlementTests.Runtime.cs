using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OriginalHttpJournalAndFileRepositoryCompleteOwnedPurchaseExactlyOnce(bool loseReplies, bool encodedCopy)
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f, loseReplies);
        var transition = f.Transition();
        var prepared = await runtime.Prepare(transition);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(2, runtime.Handler.LedgerReads);
        if (encodedCopy)
        {
            var path = Path.Combine(Path.GetDirectoryName(transition.QueuePath)!, "native-encoded-queue.json");
            await File.WriteAllTextAsync(path, File.ReadAllText(transition.QueuePath), Encoding.UTF8);
            transition = transition with { QueuePath = path };
        }
        prepared.ValidateDispatchQueue(transition.QueuePath);
        runtime.ObserveAfter();
        var final = await prepared.CompleteAsync(transition);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(loseReplies ? 3 : 0, runtime.Handler.Lookups);
        Assert.Equal(3, runtime.Handler.LedgerReads);
        Assert.Equal(f.Ledger.Revision + 3, final.Ledger.Revision);
        Assert.Equal(2, CropTeacherOwnedSeedSupply.Read(f.After, final.Ledger, new[] { Id }).PlayerQuantity(new[] { Id }));
        AssertOtherRootsUnchanged(f.Ledger, final.Ledger);
        Assert.Equal(JsonSerializer.Serialize(final.Ledger, JsonOptions), JsonSerializer.Serialize(
            new FileStrategyCommitmentRepository(f.RepositoryPath).Get(f.After), JsonOptions));
        var summary = runtime.Summary();
        Assert.Equal("crop_seed_support_roundtrip_verified", summary["status"]!.GetValue<string>());
        Assert.False(summary["formal_product_training_authorized"]!.GetValue<bool>());
        foreach (var phase in new[] { "marker", "settlement", "acquisition" })
        {
            var journal = phase == "settlement" ? "material_reservation_settlement_request_journal_path" : "crop_teacher_ledger_commit_request_journal_path";
            Assert.True(File.Exists(summary[phase]![journal]!.GetValue<string>()));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => prepared.CompleteAsync(transition));
        Assert.Equal(2, runtime.Handler.Commits);
    }

    [Fact]
    public async Task NeutralNativeSliceOnlyRebindsWithoutAcquisitionCommit()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var transition = f.Transition(quantity: 0);
        var prepared = await runtime.Prepare(transition);
        runtime.ObserveAfter();
        var final = await prepared.CompleteAsync(transition);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        AssertOtherRootsUnchanged(f.Ledger, final.Ledger);
        Assert.Null(runtime.Summary()["acquisition"]);
    }

    [Theory]
    [InlineData("marker_reply")]
    [InlineData("marker_readback")]
    public async Task InvalidMarkerNeverReachesCompilation(string fault)
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f, fault: fault);
        var transition = f.Transition();
        var compiled = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Prepare(transition, () =>
        {
            compiled = true;
            return Task.FromResult(transition.QueuePath);
        }));
        Assert.False(compiled);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal("crop_seed_support_preparation_incomplete", runtime.Summary("preparation-summary.json")["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("settlement_reply", 1)]
    [InlineData("acquisition_reply", 2)]
    [InlineData("final_readback", 2)]
    public async Task CorruptMutationOrReadbackCannotBecomeSuccessfulRoundtrip(string fault, int commits)
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f, fault: fault);
        var transition = f.Transition();
        var prepared = await runtime.Prepare(transition);
        runtime.ObserveAfter();
        await Assert.ThrowsAsync<InvalidDataException>(() => prepared.CompleteAsync(transition));
        Assert.Equal(commits, runtime.Handler.Commits);
        Assert.Equal("crop_seed_support_completion_incomplete", runtime.Summary()["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task ChangedCompiledQueueIsRejectedBeforeDispatchOrSettlement()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var transition = f.Transition();
        var prepared = await runtime.Prepare(transition);
        var queue = JsonNode.Parse(File.ReadAllText(transition.QueuePath))!;
        queue["queue_id"] = "different-queue";
        await File.WriteAllTextAsync(transition.QueuePath, queue.ToJsonString(JsonOptions));
        Assert.Throws<InvalidDataException>(() => prepared.ValidateDispatchQueue(transition.QueuePath));
        await Assert.ThrowsAsync<InvalidDataException>(() => prepared.CompleteAsync(transition));
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(1, runtime.Handler.Commits);
    }

    [Fact]
    public async Task CompilerFailurePreservesMarkerAndNeverAttemptsNativeSettlement()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var transition = f.Transition();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Prepare(transition,
            () => throw new InvalidOperationException("original compiler rejected candidate")));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(f.Ledger.Revision + 1, runtime.Repository.Get(f.Before).Revision);
    }
}
