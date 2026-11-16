using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SavedReadbackOwnerRebindsOriginalLedgerOnceAndReopensWithoutNativeRedispatch()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var operation = await f.Admit(owner);
        Assert.True(operation.IsSavedContentReadback); Assert.True(operation.IsSupportingTransition);
        Assert.False(operation.IsOvernight); Assert.False(operation.IsSupportingRoute);
        await f.Run(owner, operation);
        Assert.Equal(f.LoadedPath, owner.CurrentSnapshotPath);
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.False(File.Exists(Path.Combine(f.Runtime, "saved-readback-native-claim.json")));
        var acceptedPath = Path.Combine(f.Night.Options.EvidenceDirectory, "operation-000004-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(acceptedPath))!;
        Assert.Equal("saved_content_readback", accepted["operation_kind"]!.GetValue<string>());
        Assert.True(accepted["native_saved_content_readback_verified"]!.GetValue<bool>());
        Assert.True(accepted["native_reload_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Null(accepted["native_save_boundary_verified"]);
        var trace = await owner.ReadProductionTraceAsync();
        Assert.Equal(3, trace.Transitions.Count);
        Assert.DoesNotContain(trace.Transitions, row => row.OperationKind == "saved_content_readback");
        trace.ValidateInputsUnchanged();
        await owner.DisposeAsync();
        await using var reopened = await f.Night.Open();
        Assert.True(reopened.HeadRecoveryRequired);
        await reopened.RestoreHeadAsync(f.Night.Shared.Runtime.Client, f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post);
        Assert.Equal(f.LoadedPath, reopened.CurrentSnapshotPath);
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
    }

    [Theory]
    [InlineData("admitted_restart")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    public async Task SavedReadbackOwnerRecoversOriginalMarkerWithoutAnotherLoadOrMutation(string mode)
    {
        using var f = new SavedReadbackOwnerFixture();
        var armed = false; var loseWrite = true;
        await using var owner = await f.Night.Open(async (path, json) =>
        {
            var name = Path.GetFileName(path);
            if (armed && loseWrite && mode == "marker_reply_lost" && name == "marker-reply.json")
            { loseWrite = false; throw new IOException("Readback marker already committed."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (armed && loseWrite && mode == "acceptance_ack_lost" && name == "operation-000004-accepted.json")
            { loseWrite = false; throw new IOException("Readback acceptance durable before acknowledgement."); }
        });
        await f.CompleteNight(owner);
        var operation = await f.Admit(owner); armed = true;
        if (mode != "admitted_restart") await Assert.ThrowsAsync<IOException>(() => f.Run(owner, operation));
        await owner.DisposeAsync();
        await using var reopened = await f.Night.Open();
        if (mode == "admitted_restart") await f.Run(reopened, reopened.Pending!);
        else if (mode == "marker_reply_lost")
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Run(reopened, reopened.Pending!));
            await reopened.RecoverPendingAsync(f.Night.Shared.Runtime.Client, f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post);
        }
        else await reopened.RestoreHeadAsync(f.Night.Shared.Runtime.Client, f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(f.LoadedPath, reopened.CurrentSnapshotPath);
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Night.Shared.Runtime.Handler.Lookups);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("ledger")]
    [InlineData("save_report")]
    public async Task SavedReadbackOwnerRejectsDifferentOwnerBindingsBeforeClaim(string drift)
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var observation = f.Witness.Observation;
        if (drift == "run")
        {
            // Keep the leaf valid so the owner, not a malformed witness, rejects this identity.
            var path = Path.Combine(f.Witness.Root, "foreign-save-report.json");
            var report = JsonNode.Parse(File.ReadAllText(observation.OvernightSaveBoundaryPath))!;
            report["run_id"] = "another-run"; Fixture.Save(path, report);
            observation = observation with { RunId = "another-run", OvernightSaveBoundaryPath = path };
            f.Witness.Observation = observation; f.Witness.PinAgain(path);
        }
        else
        {
            var source = drift == "ledger" ? observation.LedgerPath : observation.OvernightSaveBoundaryPath;
            var path = Path.Combine(f.Witness.Root, "copied-" + drift + ".json"); File.Copy(source, path);
            f.Witness.Observation = drift == "ledger" ? observation with { LedgerPath = path } : observation with { OvernightSaveBoundaryPath = path };
            f.Witness.PinAgain(path);
        }
        f.Witness.SaveObservation();
        _ = f.Witness.Open();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Admit(owner));
        Assert.Contains(drift == "save_report" ? "accepted original overnight" : "owner/run/source/slot", error.Message);
        Assert.Null(owner.Pending); Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.Night.Options.EvidenceDirectory, "operation-000004-intent.json")));
    }

    [Fact]
    public async Task SavedReadbackOwnerCannotEnterNativeMainDispatchOrClaimCropProgress()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var operation = await f.Admit(owner);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeLive<Task<JsonObject>>(
            "ExecuteCropTeacherManagedOperationAsync", f.Night.Shared.Runtime.Client, f.Night.Shared.Runtime.Client,
            f.Night.Shared.Runtime.LiveOptions, 4, owner, operation, "unused-no-queue",
            (Func<Task<JsonObject>>)(() => throw new InvalidOperationException("Never invoke native execution."))));
        Assert.Contains("not a native action queue", error.Message);
        await f.Run(owner, operation);
        await owner.DisposeAsync();
        var path = Path.Combine(f.Night.Options.EvidenceDirectory, "operation-000004-accepted.json");
        var accepted = JsonNode.Parse(File.ReadAllText(path))!;
        accepted["native_refill_resource_effects_verified"] = true; Fixture.Save(path, accepted);
        var changed = await Assert.ThrowsAsync<InvalidDataException>(() => f.Night.Open());
        Assert.Contains("unrelated native progress", changed.Message);
    }

    [Fact]
    public async Task SavedReadbackOwnerUnknownRebindClaimDoesNotRetryMutation()
    {
        using var f = new SavedReadbackOwnerFixture();
        var armed = false;
        await using var owner = await f.Night.Open(async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (armed && Path.GetFileName(path) == "operation-000004-started.json")
                throw new IOException("Rebind invocation claim durable; acknowledgement lost.");
        });
        await f.CompleteNight(owner);
        var operation = await f.Admit(owner); armed = true;
        await Assert.ThrowsAsync<IOException>(() => f.Run(owner, operation));
        await owner.DisposeAsync();
        await using var reopened = await f.Night.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Run(reopened, reopened.Pending!));
        await Assert.ThrowsAnyAsync<IOException>(() => reopened.RecoverPendingAsync(f.Night.Shared.Runtime.Client,
            f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post));
        Assert.NotNull(reopened.Pending); Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
    }
}
