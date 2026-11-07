using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task TransferOriginalJournalCompletesExactlyOnceWithoutAcquisition(bool loseReplies, bool encodedCopy)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f, loseReplies);
        var prepared = await runtime.Prepare();
        var transition = runtime.Transition;
        Assert.Equal(2, runtime.Handler.LedgerReads);
        if (encodedCopy)
        {
            var path = Path.Combine(Path.GetDirectoryName(transition.QueuePath)!, "encoded-queue.json");
            await File.WriteAllTextAsync(path, File.ReadAllText(transition.QueuePath), Encoding.UTF8);
            transition = transition with { QueuePath = path };
        }
        prepared.ValidateDispatchQueue(transition.QueuePath);
        runtime.ObserveAfter();
        var final = await prepared.CompleteAsync(transition.QueuePath, transition.ExecutionReceiptPath,
            transition.AfterSnapshotPath, transition.SelectedCandidateId);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(loseReplies ? 2 : 0, runtime.Handler.Lookups);
        Assert.Equal(3, runtime.Handler.LedgerReads);
        Assert.Equal(f.Ledger.Revision + 2, final.Ledger.Revision);
        Assert.Equal(10, CropTeacherOwnedSeedSupply.Read(f.After, final.Ledger, new[] { Id }).PlayerQuantity(new[] { Id }));
        Assert.Equal(JsonSerializer.Serialize(final.Ledger, JsonOptions), JsonSerializer.Serialize(
            new FileStrategyCommitmentRepository(f.RepositoryPath).Get(f.After), JsonOptions));
        var summary = runtime.Summary();
        Assert.Equal("crop_teacher_seed_transfer_runtime.v1", summary["schema_version"]!.GetValue<string>());
        Assert.Equal("crop_seed_support_roundtrip_verified", summary["status"]!.GetValue<string>());
        Assert.True(summary["resource_settlement_verified"]!.GetValue<bool>());
        Assert.False(summary["native_dispatch_performed_by_adapter"]!.GetValue<bool>());
        Assert.False(summary["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Null(summary["acquisition"]);
        foreach (var phase in new[] { "marker", "settlement" })
        {
            var journal = phase == "marker" ? "crop_teacher_ledger_commit_request_journal_path" : "material_reservation_settlement_request_journal_path";
            Assert.True(File.Exists(summary[phase]![journal]!.GetValue<string>()));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => prepared.CompleteAsync(transition));
        Assert.Equal(1, runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalControlCompilerReturnsSameOwnedTransferRuntime(bool managedScopes)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        var scope = f.Open();
        var result = await runtime.BuildControl(scope, managedScopes: managedScopes);
        var queue = result.Queue.Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.transfer_material", Assert.Single(queue.Items).OptionId);
        var lookup = runtime.Repository.LookupPortfolioMutationReply(f.Before, "commit",
            JsonSerializer.SerializeToElement(scope.PreparationRequest, JsonOptions));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        var markerReply = lookup["reply"]!.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!;
        var marker = scope.VerifyPreparationReply(markerReply);
        var transition = f.NativeTransition(marker, compiledQueue: queue);
        Fixture.Save(transition.QueuePath, result.Queue);
        result.Runtime.ValidateDispatchQueue(transition.QueuePath);
        runtime.ObserveAfter();
        var final = await result.Runtime.CompleteAsync(transition);
        Assert.Equal(10, CropTeacherOwnedSeedSupply.Read(f.After, final.Ledger, new[] { Id }).PlayerQuantity(new[] { Id }));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }
}
