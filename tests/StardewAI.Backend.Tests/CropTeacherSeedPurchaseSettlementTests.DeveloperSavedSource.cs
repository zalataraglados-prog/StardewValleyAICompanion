using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task DeveloperSavedSourcePreservesPlanImportsNightAndResumesWithoutReplay()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        var commits = f.Parent.Night.Shared.Runtime.Handler.Commits;
        string snapshot, ledger;
        await using (var owner = await f.Open())
        {
            Assert.Equal(f.Parent.LoadedPath, owner.CurrentSnapshotPath);
            Assert.NotEqual(f.Options.InitialSnapshotPath, owner.CurrentSnapshotPath);
            Assert.Null(owner.Pending);
            Assert.False(owner.HeadRecoveryRequired);
            var progress = ReadManagedPlanningProgress(owner);
            Assert.Single(progress.Nights);
            Assert.Empty(progress.Harvest);
            var step = CropTeacherRunPlanner.Evaluate(f.Parent.Night.Shared.Inputs.Plan, f.Parent.Night.Shared.Inputs.Request,
                f.Loaded, resourceLedger: LifecycleRead<StrategyCommitmentLedger>(owner.CurrentLedgerPath),
                completedHarvestTileKeys: progress.Harvest, completedDayBoundaryKeys: progress.Nights);
            Assert.NotEqual(CropTeacherRunStageKinds.ReplanRequired, step.Stage);
            var process = f.Parent.Witness.Observation.Process;
            var source = ReadLiveProcessMetadata(owner, f.Loaded, process.ProcessId, process.ExecutablePath, DateTimeOffset.Parse(process.StartedAtUtc));
            Assert.Null(source.GetType().GetProperty("ProcessReadback")!.GetValue(source));
            Assert.Equal(f.Loaded.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
                ContinuationProperty<int>(source, "DeveloperSavedSourceTotalDay"));
            Assert.Equal(f.Parent.Night.Shared.Inputs.Plan.SourceStateHash,
                ContinuationProperty<MoneyRevenueCropNativeProductionRunPlan>(source, "RunPlan").SourceStateHash);
            Assert.Throws<InvalidOperationException>(() => ReadManagedContinuationSource(owner));
            var stepId = typeof(CropTeacherManagedSeedOwner).GetMethod("StepId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(owner, [1])!.ToString();
            Assert.Contains("saved-source:" + SavedResourceFixture.Hash(f.ManifestPath) + ":000001", stepId);
            var oldFirst = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Parent.Night.Options.EvidenceDirectory, "operation-000001-intent.json")))!;
            Assert.NotEqual(oldFirst["support_step_id"]!.GetValue<string>(), stepId);
            snapshot = owner.CurrentSnapshotPath; ledger = owner.CurrentLedgerPath;
        }
        var bytes = File.ReadAllBytes(Path.Combine(f.Options.EvidenceDirectory, "owner-intent.json"));
        await using var reopened = await f.Resume();
        Assert.Equal(snapshot, reopened.CurrentSnapshotPath);
        Assert.Equal(ledger, reopened.CurrentLedgerPath);
        Assert.Single(ReadManagedPlanningProgress(reopened).Nights);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(f.Options.EvidenceDirectory, "owner-intent.json")));
        Assert.Equal(commits, f.Parent.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Parent.Night.NativeNightCalls);
    }

    [Theory]
    [InlineData("developer_only")]
    [InlineData("exclude_from_formal_training")]
    [InlineData("formal_product_training_authorized")]
    [InlineData("parent_header_sha256")]
    [InlineData("parent_acceptance_sha256")]
    [InlineData("parent_accepted_sequence")]
    [InlineData("snapshot_path")]
    [InlineData("save_binding")]
    [InlineData("process")]
    public async Task DeveloperSavedSourceRejectsInvalidOriginBeforeCreatingChild(string fault)
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        if (fault == "developer_only" || fault == "exclude_from_formal_training") f.Manifest[fault] = false;
        if (fault == "formal_product_training_authorized") f.Manifest[fault] = true;
        if (fault.EndsWith("sha256", StringComparison.Ordinal)) f.Manifest[fault] = new string('0', 64);
        if (fault == "parent_accepted_sequence") f.Manifest[fault] = 2;
        if (fault == "snapshot_path") f.Manifest[fault] = f.Options.InitialSnapshotPath;
        if (fault == "save_binding") f.Manifest[fault]!["saveSlot"] = "another-slot";
        if (fault == "process") f.Manifest[fault]!["startedAtUtc"] = DateTimeOffset.Parse(f.Loaded.RealTimestamp).AddSeconds(1).ToString("O");
        f.SaveManifest();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Open());
        Assert.False(Directory.Exists(f.Options.EvidenceDirectory));
        Assert.Equal(1, f.Parent.Night.NativeNightCalls);
    }

    [Fact]
    public async Task DeveloperSavedSourcePinsParentAndLoadedInputsDuringUseAndRejectsForeignProcess()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        await using var owner = await f.Open();
        var process = f.Parent.Witness.Observation.Process;
        Assert.Throws<InvalidDataException>(() => ReadLiveProcessMetadata(owner, f.Loaded, process.ProcessId + 1,
            process.ExecutablePath, DateTimeOffset.Parse(process.StartedAtUtc)));
        foreach (var path in new[] { f.Parent.LoadedPath, f.ManifestPath,
            Path.Combine(f.Parent.Night.Options.EvidenceDirectory, "operation-000001-accepted.json") })
        {
            var bytes = File.ReadAllBytes(path);
            try
            {
                File.AppendAllText(path, " ");
                Assert.Throws<InvalidDataException>(() => ReadManagedPlanningProgress(owner));
            }
            finally { File.WriteAllBytes(path, bytes); }
        }
    }

    [Fact]
    public async Task DeveloperSavedSourceMarkerRejectsResourceMutationAndForgedReply()
    {
        using var f = new DeveloperSavedSourceFixture();
        await f.Prepare();
        var request = LifecycleRead<ReservationPortfolioCommitRequest>(f.RequestPath);
        var reply = LifecycleRead<ReservationPortfolioCommitResult>(f.ReplyPath);
        var last = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Parent.Night.Options.EvidenceDirectory, "operation-000003-accepted.json")))!;
        var before = LifecycleRead<StrategyCommitmentLedger>(last["after_ledger_path"]!.GetValue<string>());
        Assert.Equal(reply.Ledger!.Revision, CropTeacherDeveloperSavedSourceRebind.Verify(request, before, f.Loaded, reply).Revision);
        request.ReleaseReservationIds = ["foreign-reservation"];
        Assert.Throws<InvalidDataException>(() => CropTeacherDeveloperSavedSourceRebind.Verify(request, before, f.Loaded, reply));
        request.ReleaseReservationIds = [];
        reply.Ledger.Revision++;
        Assert.Throws<InvalidDataException>(() => CropTeacherDeveloperSavedSourceRebind.Verify(request, before, f.Loaded, reply));
    }
}
