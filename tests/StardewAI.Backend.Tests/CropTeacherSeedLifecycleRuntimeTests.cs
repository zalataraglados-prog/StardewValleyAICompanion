using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleRuntimeTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    public async Task OriginalRuntimeClosesReleasesAndReadsBackExactlyOnce(bool completion, bool loseCancel, bool loseRelease)
    {
        using var f = new RuntimeFixture(completion: completion, loseCancel: loseCancel, loseRelease: loseRelease);
        var originalOutside = JsonSerializer.Serialize(f.Files.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions);
        var final = await f.Run();
        Assert.Equal(completion ? 0 : 1, f.Handler.Cancellations);
        Assert.Equal(1, f.Handler.Releases);
        Assert.Equal(loseRelease ? 1 : 0, f.Handler.Lookups);
        Assert.Equal(completion || loseCancel ? 2 : 1, f.Handler.Reads);
        Assert.Equal(completion ? "completed" : "cancelled", final.Ledger.CropPlantingCommitments.Single().Status);
        Assert.All(final.Ledger.MaterialReservations.Where(row => row.ReservationId == "owned-seeds"), row => Assert.Equal("cancelled", row.Status));
        Assert.Equal("cancelled", final.Ledger.CurrencyReservations.Single(row => row.ReservationId == f.Files.CashId).Status);
        Assert.Equal(originalOutside, JsonSerializer.Serialize(final.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(final.Ledger, JsonOptions), JsonSerializer.Serialize(
            new FileStrategyCommitmentRepository(f.Files.RepositoryPath).Get(f.Files.Snapshot), JsonOptions));
        var summary = f.Summary();
        Assert.Equal("crop_seed_lifecycle_roundtrip_verified", summary["status"]!.GetValue<string>());
        Assert.True(summary["crop_closure_verified"]!.GetValue<bool>());
        Assert.True(summary["resource_release_verified"]!.GetValue<bool>());
        foreach (var name in new[] { "execution_authorized", "native_outcome_verified", "formal_product_training_authorized" })
            Assert.False(summary[name]!.GetValue<bool>());
        Assert.Equal(loseCancel, summary["cancellation_recovered_by_exact_readback"]?.GetValue<bool>() ?? false);
        if (!completion) Assert.Equal("cancel", f.Handler.Events[0]);
        if (loseRelease) Assert.Equal("lookup", f.Handler.Events[f.Handler.Events.IndexOf("release") + 1]);
        await Assert.ThrowsAsync<IOException>(() => f.Run());
        Assert.Equal(1, f.Handler.Releases);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoActiveResourcesDoesNotSendEmptyReleaseMutation(bool completion)
    {
        using var f = new RuntimeFixture(completion: completion, noResources: true);
        await f.Run();
        Assert.Equal(0, f.Handler.Releases);
        Assert.Equal(0, f.Handler.Lookups);
        Assert.False(f.Summary()["release_required"]!.GetValue<bool>());
        Assert.False(f.Summary()["release_mutation_performed"]!.GetValue<bool>());
        Assert.True(f.Summary()["resource_release_verified"]!.GetValue<bool>());
    }

    [Fact]
    public async Task LostCancellationReplyReplaysOtherOriginalExpiryBeforeOriginalReleaseLookup()
    {
        using var f = new RuntimeFixture(otherExpires: true, loseCancel: true, loseRelease: true);
        var final = await f.Run();
        Assert.Equal("cancelled", final.Ledger.CropPlantingCommitments.Single(row => row.CommitmentId == f.Files.Id).Status);
        Assert.Equal("completed", final.Ledger.CropPlantingCommitments.Single(row => row.CommitmentId != f.Files.Id).Status);
        Assert.Equal(new[] { "cancel", "get", "release", "lookup", "get" }, f.Handler.Events);
    }

    [Theory]
    [InlineData("cancel_reply", false)]
    [InlineData("release_reply", false)]
    [InlineData("final_readback", false)]
    [InlineData("completion_readback", true)]
    public async Task FullReplyAndReadbackDriftCannotReleaseOrFinishUnverifiedResources(string fault, bool completion)
    {
        using var f = new RuntimeFixture(completion: completion, fault: fault);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Run());
        Assert.Equal(fault is "cancel_reply" or "completion_readback" ? 0 : 1, f.Handler.Releases);
        Assert.False(f.Summary()["resource_release_verified"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("pre_cancel", false)]
    [InlineData("request_journal", true)]
    public async Task MissingOriginalMutationOrChangedJournalDoesNotResendOrRelease(string fault, bool loseReply)
    {
        using var f = new RuntimeFixture(loseCancel: loseReply, fault: fault);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => f.Run());
        Assert.NotNull(error.Data["crop_cancellation_readback_failure"]);
        Assert.Equal(1, f.Handler.Cancellations);
        Assert.Equal(0, f.Handler.Releases);
        Assert.Equal(0, f.Handler.Lookups);
        Assert.Equal(fault == "pre_cancel" ? 1 : 0, f.Handler.Reads);
        Assert.False(f.Summary()["crop_closure_verified"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitCommandUsesOriginalRuntimeWithoutStartingTheTrainingLoop(bool completion)
    {
        using var f = new RuntimeFixture(completion: completion);
        Assert.Equal(0, await f.Command());
        Assert.True(f.Summary()["resource_release_verified"]!.GetValue<bool>());
        Assert.Equal(completion ? new[] { "get", "release", "get" } : new[] { "cancel", "release", "get" }, f.Handler.Events);
        Assert.False(Directory.Exists(Path.Combine(f.Output, "datasets")));
    }
}
