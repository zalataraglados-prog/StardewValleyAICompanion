using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task DeveloperProductionTraceReopensRecordedFirstOperationInItsSavedSourceNamespace()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare(SavedReadbackOwnerFixture.RefreshDeclaredLoadedFieldTicks);
        var totalDay = f.Loaded.State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.RouteToFarm, totalDay, "", "Farm", "FarmHouse", "", "", 0, 0,
            null, "declared_return_to_crops");
        string stepId;
        var shared = f.Parent.Night.Shared;
        await using (var owner = await f.Open())
        {
            var pending = await shared.Prepare(owner, 4, step, Path.Combine(f.Options.EvidenceDirectory, "child-route"));
            stepId = pending.SupportStepId;
            Assert.Contains("saved-source:" + SavedResourceFixture.Hash(f.ManifestPath) + ":000001", stepId);
        }
        var path = Path.Combine(f.Options.EvidenceDirectory, "operation-000001-intent.json");
        var original = File.ReadAllBytes(path);
        var mutations = shared.Runtime.Handler.Commits;
        await using (var reopened = await f.Resume())
        {
            Assert.Equal(stepId, reopened.Pending!.SupportStepId);
            Assert.Equal(f.Parent.LoadedPath, reopened.CurrentSnapshotPath);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ReadRetainedJointProductionTraceAsync());
        }
        var changed = JsonNode.Parse(original)!;
        changed["support_step_id"] = f.Options.RunId + ":owned-seed:000001";
        try
        {
            Fixture.Save(path, changed);
            await Assert.ThrowsAsync<InvalidDataException>(() => f.Resume());
        }
        finally { File.WriteAllBytes(path, original); }
        Assert.Equal(mutations, shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task DeveloperProductionTraceReadsOnlyAcceptedParentPrefixWithExplicitUncreditedLoad()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        var parent = f.Parent.Night.Options.EvidenceDirectory;
        var later = Path.Combine(parent, "operation-000004-intent.json");
        File.WriteAllText(later, "not part of this accepted prefix");
        var calls = (f.Parent.Night.Shared.Runtime.Handler.Commits, f.Parent.Night.Shared.Runtime.Handler.LedgerReads);
        await using var owner = await f.Open();
        var trace = await owner.ReadRetainedJointProductionTraceAsync();
        var current = trace.CropTrace;
        var link = Assert.IsType<CropTeacherManagedDeveloperProductionBoundary>(current.DeveloperSavedBoundary);
        Assert.Equal(f.ManifestPath, link.ManifestPath);
        Assert.Equal(Path.Combine(parent, "operation-000003-accepted.json"), link.ParentAcceptancePath);
        Assert.Equal(Path.Combine(f.Parent.Night.NightRuntime, "overnight-save-boundary.json"), link.ParentSaveBoundaryPath);
        Assert.Equal(3, link.ParentAcceptedSequence);
        Assert.Equal(new[] { 1, 2, 3 }, link.Parent.CropTrace.Transitions.Select(row => row.OwnerSequence));
        Assert.Equal(new[] { "watering_maintenance", "supporting_route", "overnight_control" },
            link.Parent.CropTrace.Transitions.Select(row => row.OperationKind));
        Assert.Equal(f.Parent.Night.NightAfter, link.Parent.CropTrace.AcceptedSnapshotPath);
        Assert.Equal(current.NativeRunPlanPath, link.Parent.CropTrace.NativeRunPlanPath);
        Assert.Equal(f.Parent.LoadedPath, current.AcceptedSnapshotPath);
        Assert.Equal(current.AcceptedSnapshotPath, link.LoadedSnapshotPath);
        Assert.Equal(current.AcceptedLedgerPath, link.ReboundLedgerPath);
        Assert.Equal(f.RequestPath, link.RebindRequestPath);
        Assert.Equal(f.ReplyPath, link.RebindReplyPath);
        Assert.False(link.StrictNativeSavedReadbackVerified);
        Assert.True(link.ExcludeFromFormalTraining);
        Assert.Empty(current.Transitions);
        Assert.Empty(current.SavedContentReadbacks);
        Assert.Empty(link.Parent.CropTrace.SavedContentReadbacks);
        Assert.True(link.Parent.CropTrace.RetainedReplayOnly);
        Assert.False(trace.NativeCropLifecycleVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
        Assert.DoesNotContain(later, current.InputHashes.Keys);
        current.ValidateInputsUnchanged();
        Assert.Equal(calls, (f.Parent.Night.Shared.Runtime.Handler.Commits, f.Parent.Night.Shared.Runtime.Handler.LedgerReads));
        Assert.Contains("not part", File.ReadAllText(later));
    }

    [Fact]
    public async Task DeveloperProductionTraceRejectsChangedParentNativeEvidenceWithoutTouchingChild()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        await using var owner = await f.Open();
        var trace = await owner.ReadRetainedProductionTraceAsync();
        var receipt = trace.DeveloperSavedBoundary!.Parent.CropTrace.Transitions[0].NativeTransition.ExecutionReceiptPath;
        var bytes = File.ReadAllBytes(receipt);
        try
        {
            File.AppendAllText(receipt, " ");
            Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReadRetainedProductionTraceAsync());
            Assert.Null(owner.Pending);
            Assert.Equal(f.Parent.LoadedPath, owner.CurrentSnapshotPath);
        }
        finally { File.WriteAllBytes(receipt, bytes); }
    }

    [Fact]
    public async Task DeveloperProductionTraceCannotEmitOrAdoptNativeProductionCertificate()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        await using var owner = await f.Open();
        var output = Path.Combine(f.Options.EvidenceDirectory, "production");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => owner.BuildJointProductionEvidenceAsync(output));
        Assert.Contains("segmented evidence", error.Message);
        Assert.False(Directory.Exists(output));
        var reference = new CropTeacherTerminalProductionReference(Path.Combine(output, "production-evidence.json"), new string('0', 64));
        error = await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReadPreparedTerminalProductionAsync(reference));
        Assert.Contains("cannot adopt", error.Message);
        Assert.False(Directory.Exists(output));
        Assert.Null(owner.Pending);
    }
}
