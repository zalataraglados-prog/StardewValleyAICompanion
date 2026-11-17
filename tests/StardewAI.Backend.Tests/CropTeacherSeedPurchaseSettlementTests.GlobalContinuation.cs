using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalContinuationReleasesOnlyTheAcceptedTerminalOwnerWithoutAnotherNativeDispatch(bool globalDailyPlan)
    {
        await using var f = new GlobalContinuationFixture();
        await f.Initialize(); await f.CompleteNight();
        f.Options.ContinueAfterCropTeacherRun = !globalDailyPlan;
        f.Options.ContinueGlobalDailyPlanAfterCropTeacherRun = globalDailyPlan;
        var source = f.Owner.CurrentSnapshotPath; var ledger = f.Owner.CurrentLedgerPath;
        var commits = f.F.Shared.Runtime.Handler.Commits;
        var reportPath = await f.Continue();
        Assert.NotNull(reportPath);
        var report = JsonNode.Parse(File.ReadAllText(reportPath))!;
        Assert.Equal(source, report["accepted_snapshot_path"]!.GetValue<string>());
        Assert.Equal(ledger, report["accepted_ledger_path"]!.GetValue<string>());
        Assert.True(report["operational_handoff_ready"]!.GetValue<bool>());
        Assert.True(report["native_save_boundary_verified"]!.GetValue<bool>());
        foreach (var name in new[] { "terminal_settlement_verified", "semantic_saved_content_verified",
            "complete_managed_runtime_verified", "teacher_preference_label_eligible", "formal_product_training_authorized" })
            Assert.False(report[name]!.GetValue<bool>());
        foreach (var pair in report["input_artifact_hashes"]!.AsObject())
            Assert.Equal(pair.Value!.GetValue<string>(), SavedResourceFixture.Hash(pair.Key));
        foreach (var name in new[] { "Current", "DailySelection", "TerminalSelection", "TerminalProduction", "TerminalSettlement" })
            Assert.Null(f.Lifetime.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Lifetime));
        Assert.Equal(commits, f.F.Shared.Runtime.Handler.Commits);
        Assert.Null(await f.Continue());
        Assert.Single(File.ReadAllLines(f.Options.ProgressLogPath));
        Assert.Equal(globalDailyPlan, File.ReadAllText(f.Options.ProgressLogPath).Contains("continuation_mode=global_daily_plan", StringComparison.Ordinal));
        await using var reopened = await f.F.Shared.Files.Open(f.F.Shared.Files.Options with
        { NativeSaveBinding = new(f.Options.SaveIsolationPath, f.Options.SaveSlot) });
        Assert.True(reopened.HeadRecoveryRequired);
        Assert.Equal(source, reopened.CurrentSnapshotPath);
        Assert.Equal(ledger, reopened.CurrentLedgerPath);
        Assert.Equal(commits, f.F.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalContinuationDoesNotReleaseAnOrdinaryMaintenanceNight(bool globalDailyPlan)
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        var options = f.Shared.Runtime.LiveOptions;
        options.UseManagedCropTeacher = true;
        options.ContinueAfterCropTeacherRun = !globalDailyPlan;
        options.ContinueGlobalDailyPlanAfterCropTeacherRun = globalDailyPlan;
        var type = typeof(LiveTrainingOptions).Assembly.GetType("Program+CropTeacherOwnerLifetime", true)!;
        await using var lifetime = (IAsyncDisposable)Activator.CreateInstance(type, nonPublic: true)!;
        type.GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(lifetime, [owner, null]);
        Assert.Null(await InvokeLive<Task<string?>>("ContinueAfterManagedCropRunAsync", options, 3, lifetime, night));
        Assert.Same(owner, type.GetProperty("Current", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lifetime));
        Assert.False(File.Exists(Path.Combine(f.Options.EvidenceDirectory, "global-continuation-0003.json")));
    }

    [Fact]
    public async Task GlobalContinuationDefaultRetainsOriginalCalibrationOwnerAndStopBoundary()
    {
        await using var f = new GlobalContinuationFixture();
        await f.Initialize(); await f.CompleteNight();
        f.Options.ContinueAfterCropTeacherRun = false;
        Assert.Null(await f.Continue());
        Assert.Same(f.Owner, f.Lifetime.GetType().GetProperty("Current", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Lifetime));
        Assert.False(File.Exists(Path.Combine(f.F.Shared.Files.OwnerRoot, "global-continuation-0003.json")));
        Assert.True((await f.ReadBoundary())["native_save_boundary_verified"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalContinuationDoesNotReleaseAnUnacceptedNight(bool globalDailyPlan)
    {
        await using var f = new GlobalContinuationFixture();
        await f.Initialize();
        f.Options.ContinueAfterCropTeacherRun = !globalDailyPlan;
        f.Options.ContinueGlobalDailyPlanAfterCropTeacherRun = globalDailyPlan;
        await Assert.ThrowsAsync<InvalidOperationException>(f.Continue);
        Assert.Same(f.Night, f.Owner.Pending);
        Assert.False(File.Exists(Path.Combine(f.F.Shared.Files.OwnerRoot, "global-continuation-0003.json")));
    }

    [Theory]
    [InlineData("wrong_day", false)]
    [InlineData("wrong_count", false)]
    [InlineData("changed_save", false)]
    [InlineData("changed_production", false)]
    [InlineData("write_collision", false)]
    [InlineData("changed_save", true)]
    [InlineData("changed_production", true)]
    [InlineData("write_collision", true)]
    public async Task GlobalContinuationRejectsChangedEvidenceOrWriteFailureWithoutLosingTheOwner(string fault, bool globalDailyPlan)
    {
        await using var f = new GlobalContinuationFixture();
        await f.Initialize(); await f.CompleteNight();
        f.Options.ContinueAfterCropTeacherRun = !globalDailyPlan;
        f.Options.ContinueGlobalDailyPlanAfterCropTeacherRun = globalDailyPlan;
        if (fault == "wrong_day" || fault == "wrong_count")
        {
            var target = fault == "wrong_day" ? f.Target with { TotalDay = f.Target.TotalDay + 1 } :
                f.Target with { AcceptedDepositCount = 0 };
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.ReadBoundary(target));
        }
        else
        {
            var path = fault == "changed_save" ? (await f.ReadBoundary())["overnight_save_boundary_path"]!.GetValue<string>() :
                fault == "changed_production" ? f.Production.ManifestPath : Path.Combine(f.F.Shared.Files.OwnerRoot, "global-continuation-0003.json");
            File.WriteAllText(path, "retained-changed-boundary");
            await Assert.ThrowsAnyAsync<Exception>(f.Continue);
            Assert.Equal("retained-changed-boundary", File.ReadAllText(path));
        }
        Assert.Same(f.Owner, f.Lifetime.GetType().GetProperty("Current", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Lifetime));
        Assert.Null(f.Owner.Pending);
    }
}
