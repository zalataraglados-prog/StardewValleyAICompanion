using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SameLiveEntryReusesOriginalResumeAllocatedDirectoryWithoutSavedReloadOrNativeReplay()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using (var owner = await OpenManagedResumeMainOwner(f)) await f.CompleteNight(owner);
        var options = ManagedResumeMainOptions(f, observation: false);
        var commits = f.Night.Shared.Runtime.Handler.Commits;
        var header = Path.Combine(f.Night.Options.EvidenceDirectory, "owner-intent.json");
        var original = File.ReadAllBytes(header);
        var first = await InvokeLive<Task<string>>("ResumeCropTeacherManagedOwnerAsync", f.Night.Shared.Runtime.Client, options, string.Empty);
        var repeated = await InvokeLive<Task<string>>("ResumeCropTeacherManagedOwnerAsync", f.Night.Shared.Runtime.Client, options, string.Empty);
        Assert.NotEqual(first, repeated);
        foreach (var path in new[] { first, repeated })
        {
            var output = Path.GetDirectoryName(path)!;
            Assert.Equal(f.Night.Options.EvidenceDirectory, Path.GetDirectoryName(output));
            var report = JsonNode.Parse(File.ReadAllText(path))!;
            Assert.False(report["native_dispatch_performed"]!.GetValue<bool>());
            Assert.False(report["fresh_accepted_head_verified"]!.GetValue<bool>());
            Assert.False(report["native_saved_content_readback_verified"]!.GetValue<bool>());
            Assert.False(report["complete_managed_runtime_verified"]!.GetValue<bool>());
            Assert.False(report["formal_product_training_authorized"]!.GetValue<bool>());
        }
        Assert.Equal(commits, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(original, File.ReadAllBytes(header));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("hash")]
    [InlineData("self_reported_hash")]
    [InlineData("tick")]
    [InlineData("actor")]
    [InlineData("save")]
    [InlineData("version")]
    [InlineData("new_process")]
    public async Task SameLiveMetadataRequiresTheExactAcceptedWorldAndOriginalProcessLifetime(string fault)
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        Assert.Throws<InvalidOperationException>(() => ReadManagedContinuationSource(owner));
        var initial = LifecycleRead<SnapshotEnvelope>(f.Night.Options.InitialSnapshotPath);
        var live = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var started = DateTimeOffset.Parse(initial.RealTimestamp).AddSeconds(-1);
        var raw = JsonSerializer.SerializeToNode(live, JsonOptions)!;
        if (fault == "hash") raw["state_hash"] = "not-the-original-hash";
        if (fault == "self_reported_hash") raw["state"]!["unexpected_changed_state"] = "changed";
        if (fault == "tick") raw["game_tick"] = live.GameTick + 1;
        if (fault == "actor") raw["player_id"]!["value"] = "another-actor";
        if (fault == "save") raw["save_id"]!["value"] = "another-save";
        if (fault == "version") raw["game_version"] = "another-native-version";
        if (fault == "new_process") started = DateTimeOffset.Parse(initial.RealTimestamp).AddSeconds(1);
        live = raw.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        var commits = f.Night.Shared.Runtime.Handler.Commits;
        if (fault == "valid")
        {
            var source = ReadLiveMetadata(owner, live, started);
            Assert.Equal(owner.CurrentSnapshotPath, ContinuationProperty<string>(source, "SnapshotPath"));
            Assert.Equal(initial.StateHash, ContinuationProperty<MoneyRevenueCropNativeProductionRunPlan>(source, "RunPlan").SourceStateHash);
            Assert.Throws<InvalidOperationException>(() => ReadManagedContinuationSource(owner));
        }
        else Assert.Throws<InvalidDataException>(() => ReadLiveMetadata(owner, live, started));
        Assert.Equal(commits, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public async Task SameLiveMetadataStillRejectsAnUnrestoredHeadOrPendingOperation()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var live = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var started = DateTimeOffset.Parse(LifecycleRead<SnapshotEnvelope>(f.Night.Options.InitialSnapshotPath).RealTimestamp).AddSeconds(-1);
        await owner.DisposeAsync();
        await using var reopened = await f.Night.Open();
        Assert.True(reopened.HeadRecoveryRequired);
        Assert.Throws<InvalidOperationException>(() => ReadLiveMetadata(reopened, live, started));
        await reopened.RestoreHeadAsync(f.Night.Shared.Runtime.Client, f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post);
        await f.Admit(reopened);
        Assert.NotNull(reopened.Pending);
        Assert.Throws<InvalidOperationException>(() => ReadLiveMetadata(reopened, live, started));
    }

    private static object ReadLiveMetadata(CropTeacherManagedSeedOwner owner, SnapshotEnvelope snapshot, DateTimeOffset started)
    {
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("ReadLiveContinuationSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try { return method.Invoke(owner, [snapshot, started])!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
}
