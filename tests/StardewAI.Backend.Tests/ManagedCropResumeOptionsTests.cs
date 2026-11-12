namespace StardewAI.Backend.Tests;

public sealed class ManagedCropResumeOptionsTests
{
    private static string EvidencePath(params string[] parts) => Path.Combine(
        new[] { Path.GetTempPath(), "declared-managed-resume-options" }.Concat(parts).ToArray());
    private static string OwnerPath => EvidencePath("owner");
    private static string ExecutablePath => EvidencePath("game", "StardewModdingAPI.exe");
    private static string LogPath => EvidencePath("game.log");

    [Fact]
    public void ManagedCropResumeOptionsDefaultsRemainLegacyAndValidationIsANoOpWithoutResumeFields()
    {
        foreach (var options in new[] { new LiveTrainingOptions(), LiveTrainingOptions.Parse(new[] { "--no-manifest" }) })
        {
            Assert.False(options.UseManagedCropTeacher);
            Assert.Empty(options.ResumeManagedCropOwnerDirectory);
            Assert.Empty(options.ManagedCropReloadObservationPath);
            Assert.Equal(0, options.ManagedCropReloadProcessId);
            Assert.Empty(options.ManagedCropReloadExecutablePath);
            Assert.Empty(options.ManagedCropReloadLogPath);
            var bridge = options.BridgeSnapshotUrl;
            options.ValidateManagedCropTeacherResumeMode();
            Assert.Equal(bridge, options.BridgeSnapshotUrl);
            // This validator must not impose new requirements on unrelated managed-mode callers either.
            options.UseManagedCropTeacher = true;
            options.ValidateManagedCropTeacherResumeMode();
        }
    }

    [Fact]
    public void ManagedCropResumeOptionsOwnerOnlyParsesAsReadOnlyWithoutReloadInputs()
    {
        var raw = EvidencePath("unused", "..", "owner");
        var options = Parse("--resume-managed-crop-owner", raw);
        Assert.Equal(Path.GetFullPath(raw), options.ResumeManagedCropOwnerDirectory);
        Assert.Empty(options.ManagedCropReloadObservationPath);
        Assert.Equal(0, options.ManagedCropReloadProcessId);
        Assert.Empty(options.ManagedCropReloadExecutablePath);
        Assert.Empty(options.ManagedCropReloadLogPath);
        Assert.True(options.SkipTraining);
        Assert.True(options.UseManagedCropTeacher);
        Assert.Empty(options.ResumeCropTeacherTransitionLedgerPath);
        options.ValidateManagedCropTeacherResumeMode();
    }

    [Fact]
    public void ManagedCropResumeOptionsObservationParsesAndNormalizesWithoutActualCaptureRequirements()
    {
        var raw = EvidencePath("unused", "..", "reload-observation.json");
        var options = Parse("--resume-managed-crop-owner", OwnerPath,
            "--managed-crop-reload-observation", raw,
            "--bridge-snapshot-url", "http://remote.invalid/not-a-capture");
        Assert.Equal(Path.GetFullPath(raw), options.ManagedCropReloadObservationPath);
        Assert.Equal(0, options.ManagedCropReloadProcessId);
        Assert.Empty(options.ManagedCropReloadExecutablePath);
        Assert.Empty(options.ManagedCropReloadLogPath);
        options.ValidateManagedCropTeacherResumeMode();
    }

    [Fact]
    public void ManagedCropResumeOptionsActualTripleParsesWithOriginalExecutableAndLoopbackBridge()
    {
        foreach (var bridge in new[] { "http://127.0.0.1:8765/api/v1/snapshot", "http://[::1]:8765/api/v1/snapshot", "https://localhost/api/v1/snapshot" })
        {
            var executable = EvidencePath("unused", "..", "game", "StardewModdingAPI.exe");
            var log = EvidencePath("unused", "..", "game.log");
            var options = Parse("--resume-managed-crop-owner", OwnerPath,
                "--managed-crop-reload-process-id", "123",
                "--managed-crop-reload-executable", executable,
                "--managed-crop-reload-log", log, "--bridge-snapshot-url", bridge);
            Assert.Equal(123, options.ManagedCropReloadProcessId);
            Assert.Equal(Path.GetFullPath(executable), options.ManagedCropReloadExecutablePath);
            Assert.Equal(Path.GetFullPath(log), options.ManagedCropReloadLogPath);
            Assert.Empty(options.ManagedCropReloadObservationPath);
            options.ValidateManagedCropTeacherResumeMode();
        }
    }

    [Fact]
    public void ManagedCropResumeOptionsRequireOwnerManagedModeAndAllOriginalModeGuards()
    {
        foreach (var field in new Action<LiveTrainingOptions>[]
        {
            options => options.ManagedCropReloadObservationPath = EvidencePath("observation.json"),
            options => options.ManagedCropReloadProcessId = 123,
            options => options.ManagedCropReloadExecutablePath = ExecutablePath,
            options => options.ManagedCropReloadLogPath = LogPath
        })
        {
            var options = ValidOptions();
            options.ResumeManagedCropOwnerDirectory = "";
            field(options);
            Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode);
        }
        foreach (var change in new Action<LiveTrainingOptions>[]
        {
            options => options.UseManagedCropTeacher = false,
            options => options.SkipTraining = false,
            options => options.UseDailyPlan = false,
            options => options.UseProductExecutor = false,
            options => options.UseRuntimeTestHarnessExecutor = false,
            options => options.RequireExecutorFeedback = false,
            options => options.EmitQueueExecutionReceipt = false,
            options => options.RequireNativeSaveBoundary = false,
            options => options.SaveIsolationPath = "",
            options => options.SaveSlot = "",
            options => options.TargetExecutionMode = "foreign-target",
            options => options.TeacherPreferencePath = "declared-teacher.json",
            options => options.PrecompiledQueuePath = "declared-queue.json",
            options => options.UsePlanOutput = true,
            options => options.UseParameterizedAction = true
        })
        {
            var options = ValidOptions();
            change(options);
            Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode);
        }
        var noMode = ManagedArgs().Where(argument => argument != "--use-managed-crop-teacher");
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(noMode.Concat(new[] { "--resume-managed-crop-owner", OwnerPath }).ToArray()));
        Assert.Throws<ArgumentException>(() => Parse("--managed-crop-reload-observation", EvidencePath("observation.json")));
    }

    [Fact]
    public void ManagedCropResumeOptionsRejectMixedObservationAndEveryIncompleteActualTriple()
    {
        for (var mask = 1; mask <= 7; mask++)
        {
            var options = ValidOptions();
            if ((mask & 1) != 0) options.ManagedCropReloadProcessId = 123;
            if ((mask & 2) != 0) options.ManagedCropReloadExecutablePath = ExecutablePath;
            if ((mask & 4) != 0) options.ManagedCropReloadLogPath = LogPath;
            if (mask != 7) Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode);
            options.ManagedCropReloadObservationPath = EvidencePath("observation.json");
            Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode);
        }
        Assert.Throws<ArgumentException>(() => Parse("--resume-managed-crop-owner", OwnerPath, "--managed-crop-reload-log", LogPath));
    }

    [Fact]
    public void ManagedCropResumeOptionsRejectBadPidPathsMissingArgumentsExecutableAndBridge()
    {
        foreach (var pid in new[] { "0", "-1", "not-an-int", "2147483648" })
            Assert.Throws<ArgumentException>(() => Parse("--resume-managed-crop-owner", OwnerPath,
                "--managed-crop-reload-process-id", pid, "--managed-crop-reload-process-id", "123"));
        foreach (var flag in new[] { "--resume-managed-crop-owner", "--managed-crop-reload-observation",
            "--managed-crop-reload-process-id", "--managed-crop-reload-executable", "--managed-crop-reload-log" })
        {
            Assert.Throws<ArgumentException>(() => Parse(flag));
            Assert.Throws<ArgumentException>(() => Parse(flag, ""));
            Assert.Throws<ArgumentException>(() => Parse(flag, "--skip-training"));
        }
        foreach (var change in new Action<LiveTrainingOptions>[]
        {
            options => options.ResumeManagedCropOwnerDirectory = "relative-owner",
            options => options.ResumeManagedCropOwnerDirectory = " ",
            options => options.ManagedCropReloadExecutablePath = "StardewModdingAPI.exe",
            options => options.ManagedCropReloadLogPath = "relative.log",
            options => options.ManagedCropReloadExecutablePath = EvidencePath("dotnet.exe"),
            options => options.ManagedCropReloadProcessId = -1
        })
        {
            var options = ActualOptions();
            change(options);
            Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode);
        }
        Assert.Throws<ArgumentException>(() => Parse("--resume-managed-crop-owner", "relative-owner"));
        Assert.Throws<ArgumentException>(() => Parse("--resume-managed-crop-owner", OwnerPath, "--managed-crop-reload-observation", "relative.json"));
        foreach (var bridge in new[] { "http://remote.invalid/api/v1/snapshot", "http://localhost/wrong", "relative",
            "file:///api/v1/snapshot", "http://user@localhost/api/v1/snapshot", "http://localhost/api/v1/snapshot?query=1", "http://localhost/api/v1/snapshot#fragment" })
        {
            var options = ActualOptions();
            options.BridgeSnapshotUrl = bridge;
            Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode);
        }
    }

    [Fact]
    public void ManagedCropResumeOptionsPreserveOriginalLegacyLedgerForkRejection()
    {
        var options = ValidOptions();
        options.ResumeCropTeacherTransitionLedgerPath = EvidencePath("legacy-ledger.json");
        Assert.Contains("cannot fork", Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherMode).Message);
        Assert.Contains("cannot fork", Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherResumeMode).Message);
        options.ResumeManagedCropOwnerDirectory = "";
        options.ValidateManagedCropTeacherResumeMode();
        Assert.Contains("cannot fork", Assert.Throws<ArgumentException>(options.ValidateManagedCropTeacherMode).Message);
    }

    private static LiveTrainingOptions ValidOptions() => new()
    {
        UseManagedCropTeacher = true, SkipTraining = true, UseDailyPlan = true, UseRuntimeTestHarnessExecutor = true,
        UseProductExecutor = true, EmitQueueExecutionReceipt = true, RequireExecutorFeedback = true,
        MaxAttempts = 1, RequiredVerifiedActions = 1, DailyPlanMaxCandidates = 1,
        DailyPlanCandidateKind = "declared-crop-teacher", DailyPlanCandidateId = "declared-candidate",
        RequireNativeSaveBoundary = true, SaveIsolationPath = EvidencePath("saves"), SaveSlot = "declared-slot",
        Root = EvidencePath("artifacts"), RunId = "declared-managed-resume", ResumeManagedCropOwnerDirectory = OwnerPath
    };

    private static LiveTrainingOptions ActualOptions()
    {
        var options = ValidOptions();
        options.ManagedCropReloadProcessId = 123;
        options.ManagedCropReloadExecutablePath = ExecutablePath;
        options.ManagedCropReloadLogPath = LogPath;
        return options;
    }

    private static string[] ManagedArgs() => new[]
    {
        "--root", EvidencePath("artifacts"), "--run-id", "declared-managed-resume", "--use-managed-crop-teacher",
        "--skip-training", "--use-daily-plan", "--use-product-executor", "--emit-queue-execution-receipt",
        "--max-attempts", "1", "--required-verified-actions", "1", "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-kind", "declared-crop-teacher", "--daily-plan-candidate-id", "declared-candidate",
        "--require-native-save-boundary", "--save-isolation-path", EvidencePath("saves"), "--save-slot", "declared-slot"
    };

    private static LiveTrainingOptions Parse(params string[] extra) => LiveTrainingOptions.Parse(ManagedArgs().Concat(extra).ToArray());
}
