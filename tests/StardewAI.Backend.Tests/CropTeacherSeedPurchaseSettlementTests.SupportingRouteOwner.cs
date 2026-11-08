using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("live")]
    [InlineData("admitted_restart")]
    [InlineData("lost_reply")]
    [InlineData("lost_acceptance")]
    public async Task SupportingRouteOwnerRebindsAndRestartsWithoutRedispatchOrDuplicateMutation(string mode)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = CreateOwnedSupportingRoute(f, runtime, files);
        var loseWrite = true;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (mode == "lost_reply" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Original marker committed; local reply not retained."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "lost_acceptance" && loseWrite && Path.GetFileName(path) == "operation-000001-accepted.json")
            { loseWrite = false; throw new IOException("Original acceptance durable; acknowledgement lost."); }
        });
        var operation = await owner.AdmitSupportingRouteAsync(transition, files.Native);
        Assert.True(operation.IsSupportingRoute);
        Assert.Throws<InvalidOperationException>(() => operation.ReadStep());
        Task Run(CropTeacherManagedSeedOwner value, CropTeacherManagedSeedOperation pending) =>
            value.RunSupportingRouteAsync(pending, runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post);
        if (mode == "live")
        {
            await Run(owner, operation);
            Assert.Null(owner.Pending);
        }
        else if (mode != "admitted_restart") await Assert.ThrowsAsync<IOException>(() => Run(owner, operation));
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        if (mode == "admitted_restart") await Run(restarted, restarted.Pending!);
        else if (mode == "lost_reply") await restarted.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
        else await restarted.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        Assert.Null(restarted.Pending);
        Assert.False(restarted.HeadRecoveryRequired);
        Assert.Equal(transition.AfterSnapshotPath, restarted.CurrentSnapshotPath);
        var final = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(restarted.CurrentLedgerPath), JsonOptions)!;
        AssertOtherRootsUnchanged(f.Ledger, final);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(mode == "lost_reply" ? 1 : 0, runtime.Handler.Lookups);
        var acceptance = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal("supporting_route", acceptance["operation_kind"]!.GetValue<string>());
        Assert.False(acceptance["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(acceptance["teacher_preference_label_eligible"]!.GetValue<bool>());
        var next = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request,
            JsonSerializer.Deserialize<StardewAI.Contracts.State.SnapshotEnvelope>(File.ReadAllText(transition.AfterSnapshotPath), JsonOptions)!,
            resourceLedger: final);
        var seed = await restarted.AdmitAsync(next, restarted.CurrentSnapshotPath, restarted.CurrentLedgerPath, files.Native + "-next");
        Assert.Equal(2, seed.Sequence);
        Assert.NotNull(seed.Scopes.Purchase);
    }

    [Fact]
    public async Task SupportingRouteOwnerUnknownMutationDoesNotBecomeZeroOrResend()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = CreateOwnedSupportingRoute(f, runtime, files);
        await using var owner = await files.Open();
        var operation = await owner.AdmitSupportingRouteAsync(transition, files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.RunSupportingRouteAsync(operation, runtime.Client,
            (_, _) => throw new IOException("Unknown boundary before durable portfolio journal."), files.Post));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.RunSupportingRouteAsync(operation, runtime.Client,
            (_, _) => throw new InvalidOperationException("Must not resend."), files.Post));
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        await Assert.ThrowsAnyAsync<IOException>(() => restarted.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post));
        Assert.NotNull(restarted.Pending);
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Lookups);
    }

    private static MoneyRevenueCropNativeLifecycleTransitionRequest CreateOwnedSupportingRoute(Fixture f, RuntimeFixture runtime, OwnerFiles files)
    {
        var (transition, after) = CreateSupportingRoute(f);
        transition = transition with { RunId = files.Options.RunId, ExecutorVersion = files.Options.ExecutorVersion };
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
        receipt.RunId = transition.RunId;
        Fixture.Save(transition.ExecutionReceiptPath, receipt);
        runtime.Store.Snapshots[after.StateHash] = after;
        return transition;
    }
}
