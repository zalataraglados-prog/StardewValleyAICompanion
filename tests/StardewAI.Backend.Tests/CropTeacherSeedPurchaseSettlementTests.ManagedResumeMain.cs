using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private static LiveTrainingOptions ManagedResumeMainOptions(SavedReadbackOwnerFixture f, bool observation = true)
    {
        var options = f.Night.Shared.Runtime.LiveOptions;
        options.UseManagedCropTeacher = true; options.SkipTraining = true; options.UseDailyPlan = true;
        options.UseProductExecutor = true; options.UseRuntimeTestHarnessExecutor = true; options.RequireExecutorFeedback = true;
        options.EmitQueueExecutionReceipt = true; options.RequireNativeSaveBoundary = true;
        options.TargetExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer;
        options.ResumeManagedCropOwnerDirectory = f.Night.Options.EvidenceDirectory;
        options.ManagedCropReloadObservationPath = observation ? f.Witness.ObservationPath : string.Empty;
        return options;
    }

    private static Task<CropTeacherManagedSeedOwner> OpenManagedResumeMainOwner(SavedReadbackOwnerFixture f,
        Func<string, string, Task>? writer = null) => f.Night.Shared.Files.Open(f.Night.Options with
        { UseSelectedRefillResourceEffects = true }, writer);

    private static Task<string> RunManagedResumeMain(SavedReadbackOwnerFixture f, LiveTrainingOptions options, string suffix) =>
        InvokeLive<Task<string>>("ResumeCropTeacherManagedOwnerAsync", f.Night.Shared.Runtime.Client, options,
            Path.Combine(f.Night.Options.EvidenceDirectory, suffix));

    [Fact]
    public async Task ManagedResumeMainReopensSameOwnerImportsExactHeadAndDoesNotTrustOldHigherTickLatest()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using (var owner = await OpenManagedResumeMainOwner(f)) await f.CompleteNight(owner);
        var options = ManagedResumeMainOptions(f);
        options.BackendUrl = "HTTP://TEST/";
        var old = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.Night.NightAfter), JsonOptions)!;
        old.GameTick = 900000;
        ChangeState(old, "player", player => player["tile_x"]!["value"] = 99);
        f.Night.Shared.Runtime.Store.Snapshots[old.StateHash] = old;
        var header = Path.Combine(f.Night.Options.EvidenceDirectory, "owner-intent.json");
        var original = File.ReadAllBytes(header);
        var result = JsonNode.Parse(File.ReadAllText(await RunManagedResumeMain(f, options, "main-reload")))!;
        Assert.True(result["native_saved_content_readback_verified"]!.GetValue<bool>());
        Assert.True(result["fresh_accepted_head_verified"]!.GetValue<bool>());
        Assert.False(result["complete_managed_runtime_verified"]!.GetValue<bool>());
        Assert.False(result["terminal_settlement_verified"]!.GetValue<bool>());
        Assert.Null(result["saved_terminal_closeout"]);
        Assert.False(result["native_dispatch_performed"]!.GetValue<bool>());
        Assert.False(result["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Equal(f.LoadedPath, result["snapshot_path"]!.GetValue<string>());
        Assert.NotEqual(old.StateHash, result["state_hash"]!.GetValue<string>());
        Assert.Equal(old.StateHash, f.Night.Shared.Runtime.Store.LatestSnapshot()!.StateHash);
        Assert.Equal(original, File.ReadAllBytes(header));
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
        options.ManagedCropReloadObservationPath = string.Empty;
        var restored = JsonNode.Parse(File.ReadAllText(await RunManagedResumeMain(f, options, "main-historical")))!;
        Assert.True(restored["restored_historical_readback_verified"]!.GetValue<bool>());
        Assert.False(restored["fresh_accepted_head_verified"]!.GetValue<bool>());
        Assert.False(restored["native_saved_content_readback_verified"]!.GetValue<bool>());
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
        options.ManagedCropReloadObservationPath = f.Witness.ObservationPath;
        var repeated = JsonNode.Parse(File.ReadAllText(await RunManagedResumeMain(f, options, "main-explicit-accepted")))!;
        Assert.True(repeated["restored_historical_readback_verified"]!.GetValue<bool>());
        Assert.False(repeated["fresh_accepted_head_verified"]!.GetValue<bool>());
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("admitted", true)]
    [InlineData("marker_reply_lost", false)]
    [InlineData("marker_reply_lost", true)]
    [InlineData("acceptance_ack_lost", false)]
    [InlineData("acceptance_ack_lost", true)]
    public async Task ManagedResumeMainRestoresRetainedReadbackWithoutAnotherNativeAction(string mode, bool explicitObservation)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        var armed = false; var lose = true;
        await using (var owner = await OpenManagedResumeMainOwner(f, async (path, json) =>
        {
            var name = Path.GetFileName(path);
            if (armed && lose && mode == "marker_reply_lost" && name == "marker-reply.json")
            { lose = false; throw new IOException("Marker already committed."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (armed && lose && mode == "acceptance_ack_lost" && name == "operation-000004-accepted.json")
            { lose = false; throw new IOException("Acceptance already durable."); }
        }))
        {
            await f.CompleteNight(owner);
            var operation = await f.Admit(owner); armed = true;
            if (mode != "admitted") await Assert.ThrowsAsync<IOException>(() => f.Run(owner, operation));
        }
        var result = JsonNode.Parse(File.ReadAllText(await RunManagedResumeMain(f,
            ManagedResumeMainOptions(f, observation: explicitObservation), "main-recovery")))!;
        Assert.Equal(f.LoadedPath, result["snapshot_path"]!.GetValue<string>());
        var newlyAccepted = explicitObservation && mode != "acceptance_ack_lost";
        Assert.Equal(!newlyAccepted, result["restored_historical_readback_verified"]!.GetValue<bool>());
        Assert.Equal(newlyAccepted, result["fresh_accepted_head_verified"]!.GetValue<bool>());
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Night.Shared.Runtime.Handler.Lookups);
    }

    [Fact]
    public async Task ManagedResumeMainReadOnlyCannotStartUnclaimedReadbackMarker()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using (var owner = await OpenManagedResumeMainOwner(f))
        {
            await f.CompleteNight(owner);
            await f.Admit(owner);
        }
        var reads = f.Night.Shared.Runtime.Handler.LedgerReads;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunManagedResumeMain(f,
            ManagedResumeMainOptions(f, observation: false), "main-readonly-unclaimed"));
        Assert.Contains("cannot create an unclaimed mutation", error.Message);
        Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(reads, f.Night.Shared.Runtime.Handler.LedgerReads);
        Assert.False(File.Exists(Path.Combine(f.Night.Options.EvidenceDirectory, "operation-000004-started.json")));
    }

    [Fact]
    public async Task ManagedResumeMainRejectsCopiedSourceBeforeReadbackMarker()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using (var owner = await OpenManagedResumeMainOwner(f)) await f.CompleteNight(owner);
        var copy = Path.Combine(f.Witness.Root, "copied-owner-source.json");
        File.Copy(f.Witness.Observation.SourceSnapshotPath, copy);
        f.Witness.Observation = f.Witness.Observation with { SourceSnapshotPath = copy };
        f.Witness.PinAgain(copy); f.Witness.SaveObservation();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => RunManagedResumeMain(f, ManagedResumeMainOptions(f), "main-foreign"));
        Assert.Contains("owner/run/source/slot", error.Message);
        Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.False(File.Exists(Path.Combine(f.Night.Options.EvidenceDirectory, "operation-000004-intent.json")));
    }

    [Fact]
    public async Task ManagedResumeMainActualCaptureRejectsNonGameProcessWithoutReadbackMutation()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using (var owner = await OpenManagedResumeMainOwner(f)) await f.CompleteNight(owner);
        var options = ManagedResumeMainOptions(f, observation: false);
        options.BridgeSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot";
        options.ManagedCropReloadProcessId = Environment.ProcessId;
        options.ManagedCropReloadExecutablePath = f.Witness.Observation.Process.ExecutablePath;
        options.ManagedCropReloadLogPath = f.Witness.Observation.SourceLogPath;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => RunManagedResumeMain(f, options, "main-wrong-process"));
        Assert.Contains("new exact original game process", error.Message);
        Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.False(Directory.Exists(Path.Combine(f.Night.Options.EvidenceDirectory, "main-wrong-process", "native-load")));
    }

    [Fact]
    public async Task ManagedResumeMainRejectsOutputAtOwnerRootBeforeRecoveryWrites()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using (var owner = await OpenManagedResumeMainOwner(f)) await f.CompleteNight(owner);
        var options = ManagedResumeMainOptions(f, observation: false);
        var reads = f.Night.Shared.Runtime.Handler.LedgerReads;
        await Assert.ThrowsAsync<IOException>(() => InvokeLive<Task<string>>("ResumeCropTeacherManagedOwnerAsync",
            f.Night.Shared.Runtime.Client, options, f.Night.Options.EvidenceDirectory));
        Assert.Equal(reads, f.Night.Shared.Runtime.Handler.LedgerReads);
        Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits);
    }
}
