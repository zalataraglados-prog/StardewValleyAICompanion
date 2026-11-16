using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedDayBoundaryCaptureDoesNotStartASecondLegacyReceipt()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        var live = f.Shared.Runtime.LiveOptions;
        live.SaveIsolationPath = Path.Combine(live.SaveIsolationPath, "no-legacy-save-probe");
        var observed = await CaptureManagedLoopDayBoundary(f, owner);
        Assert.Null(observed);
        Assert.False(Directory.Exists(live.SaveIsolationPath));
        Assert.Equal(0, f.NativeNightCalls);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ManagedDayBoundaryCaptureKeepsOriginalUnmanagedSaveObservation()
    {
        using var f = new OvernightFixture();
        var observed = await CaptureManagedLoopDayBoundary(f, null);
        Assert.NotNull(observed);
        Assert.NotNull(observed.GetType().GetProperty("BeforeSave")!.GetValue(observed));
        Assert.Equal(ReadSiteSnapshot(f.Options.InitialSnapshotPath).StateHash,
            observed.GetType().GetProperty("BeforeStateHash")!.GetValue(observed));
        Assert.Equal(0, f.NativeNightCalls);
    }

    [Fact]
    public async Task ManagedDayBoundaryCapturePreservesOldUnmanagedSaveRequirement()
    {
        using var f = new OvernightFixture();
        f.Shared.Runtime.LiveOptions.RequireNativeSaveBoundary = false;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CaptureManagedLoopDayBoundary(f, null));
        Assert.Equal("crop_teacher_run_requires_native_save_boundary", error.Message);
    }

    private static async Task<object?> CaptureManagedLoopDayBoundary(OvernightFixture f, CropTeacherManagedSeedOwner? owner)
    {
        var snapshot = ReadSiteSnapshot(f.Options.InitialSnapshotPath);
        var day = snapshot.State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.NativeSleep, day, "", "FarmHouse", "FarmHouse",
            "", "", 0, 0, null, "declared_capture_ownership");
        var before = JsonSerializer.SerializeToNode(snapshot, JsonOptions)!.AsObject();
        var task = InvokeLive<Task>("CaptureCropTeacherLoopDayBoundaryStartAsync", f.Shared.Runtime.LiveOptions,
            owner!, step, before, snapshot.StateHash);
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }
}
