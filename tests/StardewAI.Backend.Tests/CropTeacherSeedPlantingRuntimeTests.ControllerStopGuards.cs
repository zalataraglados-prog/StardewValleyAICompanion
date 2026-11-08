using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData("index")]
    [InlineData("request")]
    [InlineData("count")]
    [InlineData("tick")]
    [InlineData("success")]
    [InlineData("source_path")]
    [InlineData("native_artifact")]
    [InlineData("missing_receipt")]
    [InlineData("missing_context")]
    [InlineData("context_not_written")]
    public async Task OwnedStopRecoveryReusesOriginalStrictChecksAndNeverInfersZeroFromMissingEvidence(string fault)
    {
        using var f = new ControllerStopFixture();
        await using var owner = await f.PrepareOwner();
        await f.Stop(owner);
        if (fault == "context_not_written")
        {
            f.Runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "controller-stop-transition.json");
            await Assert.ThrowsAsync<IOException>(() => f.Complete());
        }
        else await f.Complete();
        await owner.DisposeAsync();
        var receiptPath = f.Receipt["execution_path"]!.GetValue<string>();
        var transitionPath = Path.Combine(f.Files.Native, "controller-stop-transition.json");
        if (fault is "index" or "request" or "count" or "tick" or "success")
        {
            var receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!;
            if (fault == "index") receipt["owned_seed_controller_stop"]!["index"] = 1;
            if (fault == "request") receipt["owned_seed_controller_stop"]!["native_request_performed"] = true;
            if (fault == "count") receipt["executed_item_count"] = 1;
            if (fault == "tick") receipt["after_game_tick"] = receipt["after_game_tick"]!.GetValue<long>() + 1;
            if (fault == "success") receipt["success"] = true;
            File.WriteAllText(receiptPath, receipt.ToJsonString());
        }
        if (fault == "source_path")
        {
            var otherPath = Path.Combine(f.Files.Native, "same-content-other-path.json");
            File.Copy(f.Runtime.Files.SourcePath, otherPath);
            var transition = JsonNode.Parse(File.ReadAllText(transitionPath))!;
            transition["before_snapshot_path"] = otherPath;
            File.WriteAllText(transitionPath, transition.ToJsonString());
        }
        if (fault == "native_artifact") File.WriteAllText(Path.Combine(f.Files.Native, "support-proof.json"), "{}");
        if (fault == "missing_receipt") File.Delete(receiptPath);
        if (fault == "missing_context") File.Delete(transitionPath);
        await using var resumed = await f.Files.Open();
        var reads = f.Runtime.Handler.LedgerReads;
        if (fault.StartsWith("missing_", StringComparison.Ordinal) || fault == "context_not_written")
            await Assert.ThrowsAnyAsync<IOException>(() => resumed.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post));
        else await Assert.ThrowsAsync<InvalidDataException>(() => resumed.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post));
        Assert.Equal(reads, f.Runtime.Handler.LedgerReads);
        Assert.NotNull(resumed.Pending);
        Assert.False(File.Exists(Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.ExecuteSeedAsync(resumed.Pending!, _ => Task.FromResult(true)));
        f.AssertNoNativeEffect();
    }

    [Fact]
    public async Task OwnedStopRecoveryRejectsWholeBackendAuthorityDriftEvenWhenAllLocalStopFilesAreValid()
    {
        using var f = new ControllerStopFixture();
        await using var owner = await f.PrepareOwner();
        await f.Stop(owner);
        await f.Complete();
        await owner.DisposeAsync();
        var repository = f.Runtime.Files.Repository;
        var result = repository.CommitReservationPortfolio(f.Runtime.Files.Before, new ReservationPortfolioCommitRequest
        {
            StateHash = f.Runtime.Files.Before.StateHash,
            ExpectedLedgerRevision = repository.Get(f.Runtime.Files.Before).Revision,
            PortfolioId = "other-root.after-controller-stop", SourceDecisionId = "other-root.after-controller-stop", GoalId = "earn_money"
        });
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        await using var resumed = await f.Files.Open();
        var reads = f.Runtime.Handler.LedgerReads;
        await Assert.ThrowsAsync<InvalidDataException>(() => resumed.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post));
        Assert.Equal(reads + 1, f.Runtime.Handler.LedgerReads);
        Assert.NotNull(resumed.Pending);
        f.AssertNoNativeEffect();
    }

    [Theory]
    [InlineData("native_outcome")]
    [InlineData("missing_dispatch")]
    [InlineData("changed_transition")]
    public async Task OwnedStopAcceptanceIndexCannotBePromotedOrDetachedFromItsOriginalDispatch(string fault)
    {
        using var f = new ControllerStopFixture();
        await using var owner = await f.PrepareOwner();
        await f.Stop(owner);
        await f.Complete();
        await owner.AcceptStoppedSeedAsync(f.Operation, f.Seed);
        await owner.DisposeAsync();
        if (fault == "native_outcome")
        {
            var path = Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json");
            var accepted = JsonNode.Parse(File.ReadAllText(path))!;
            accepted["native_outcome_verified"] = true;
            File.WriteAllText(path, accepted.ToJsonString());
        }
        if (fault == "missing_dispatch") File.Delete(Path.Combine(f.Files.OwnerRoot, "operation-000001-dispatch.json"));
        if (fault == "changed_transition") File.AppendAllText(Path.Combine(f.Files.Native, "controller-stop-transition.json"), " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Files.Open());
        f.AssertNoNativeEffect();
    }
}
