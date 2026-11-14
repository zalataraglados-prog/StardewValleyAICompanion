using System.Diagnostics;
using System.Reflection;

namespace StardewAI.Backend.Tests;

public sealed class ManagedContinuationEntryTests
{
    [Fact]
    public void ExplicitContinuationRequiresCurrentProcessMetadataWithoutChangingReadonlyResume()
    {
        var options = Options();
        options.ValidateManagedCropTeacherContinueMode();
        Assert.False(Directory.Exists(options.Root));
        options.ManagedCropReloadProcessId = 0;
        options.ManagedCropReloadExecutablePath = string.Empty;
        options.ManagedCropReloadLogPath = string.Empty;
        options.ValidateManagedCropTeacherResumeMode();
        Assert.Throws<ArgumentException>(() => options.ValidateManagedCropTeacherContinueMode());
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("process")]
    [InlineData("executable")]
    [InlineData("log")]
    [InlineData("retained_observation")]
    [InlineData("legacy_fork")]
    [InlineData("training")]
    [InlineData("target")]
    [InlineData("feedback")]
    [InlineData("managed_mode")]
    public void ContinuationDoesNotBypassOriginalModeAndSaveGuards(string fault)
    {
        var options = Options();
        switch (fault)
        {
            case "owner": options.ResumeManagedCropOwnerDirectory = string.Empty; break;
            case "process": options.ManagedCropReloadProcessId = 0; break;
            case "executable": options.ManagedCropReloadExecutablePath = string.Empty; break;
            case "log": options.ManagedCropReloadLogPath = string.Empty; break;
            case "retained_observation": options.ManagedCropReloadObservationPath = Path.Combine(options.Root, "old-observation.json"); break;
            case "legacy_fork": options.ResumeCropTeacherTransitionLedgerPath = Path.Combine(options.Root, "legacy.json"); break;
            case "training": options.SkipTraining = false; break;
            case "target": options.TargetExecutionMode = "not_training_singleplayer"; break;
            case "feedback": options.RequireExecutorFeedback = false; break;
            case "managed_mode": options.UseManagedCropTeacher = false; break;
        }
        Assert.Throws<ArgumentException>(() => options.ValidateManagedCropTeacherContinueMode());
        Assert.False(Directory.Exists(options.Root));
    }

    [Fact]
    public async Task BoundContinuationProcessMustStayAliveWhileUnboundOriginalLifetimeStaysUnchanged()
    {
        var type = typeof(LiveTrainingOptions).Assembly.GetType("Program+CropTeacherOwnerLifetime", throwOnError: true)!;
        var lifetime = Activator.CreateInstance(type, nonPublic: true)!;
        var require = type.GetMethod("RequireLiveContinuation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var processField = type.GetField("continuationProcess", BindingFlags.NonPublic | BindingFlags.Instance)!;
        require.Invoke(lifetime, null);
        using var current = Process.GetCurrentProcess();
        processField.SetValue(lifetime, current);
        require.Invoke(lifetime, null);
        using var ended = Process.Start(new ProcessStartInfo("dotnet", "--version")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        try { Assert.True(ended.WaitForExit(10000)); }
        finally { if (!ended.HasExited) ended.Kill(); }
        processField.SetValue(lifetime, ended);
        var error = Assert.Throws<TargetInvocationException>(() => require.Invoke(lifetime, null));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("game exited", error.InnerException!.Message);
        processField.SetValue(lifetime, null);
        require.Invoke(lifetime, null);
        await ((IAsyncDisposable)lifetime).DisposeAsync();
    }

    private static LiveTrainingOptions Options()
    {
        var root = Path.Combine(Path.GetTempPath(), "managed-continuation-options-" + Guid.NewGuid().ToString("N"));
        return new LiveTrainingOptions
        {
            Root = Path.Combine(root, "output"), RunId = "declared-continuation", BackendUrl = "http://test",
            UseManagedCropTeacher = true, UseDailyPlan = true, SkipTraining = true,
            UseRuntimeTestHarnessExecutor = true, UseProductExecutor = true, EmitQueueExecutionReceipt = true,
            RequireNativeSaveBoundary = true, SaveIsolationPath = Path.Combine(root, "saves"), SaveSlot = "DeclaredSlot",
            ResumeManagedCropOwnerDirectory = Path.Combine(root, "owner"), ManagedCropReloadProcessId = 123,
            ManagedCropReloadExecutablePath = Path.Combine(root, "game", "StardewModdingAPI.exe"),
            ManagedCropReloadLogPath = Path.Combine(root, "game.log")
        };
    }
}
