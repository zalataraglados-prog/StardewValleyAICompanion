using System.Reflection;
using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class ManagedContinuationEntryTests
{
    [Fact]
    public void SameLiveOptionsDoNotPromoteReadonlyOrSavedReloadModes()
    {
        var options = LiveOptions();
        options.ValidateManagedCropTeacherLiveContinueMode();
        options.ValidateManagedCropTeacherResumeMode();
        Assert.Throws<ArgumentException>(() => options.ValidateManagedCropTeacherContinueMode());
        Assert.False(Directory.Exists(options.Root));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("relative")]
    [InlineData("owner")]
    [InlineData("reload")]
    [InlineData("training")]
    [InlineData("feedback")]
    [InlineData("legacy")]
    [InlineData("bridge")]
    public void SameLiveOptionsKeepOriginalIsolationAndExplicitEntryGuards(string fault)
    {
        var options = LiveOptions();
        switch (fault)
        {
            case "session": options.ManagedCropLiveSessionPath = ""; break;
            case "relative": options.ManagedCropLiveSessionPath = "relative-session.json"; break;
            case "owner": options.ResumeManagedCropOwnerDirectory = ""; break;
            case "reload": options.ManagedCropReloadProcessId = 1; break;
            case "training": options.SkipTraining = false; break;
            case "feedback": options.RequireExecutorFeedback = false; break;
            case "legacy": options.ResumeCropTeacherTransitionLedgerPath = Path.Combine(options.Root, "legacy.json"); break;
            case "bridge": options.BridgeSnapshotUrl = "http://remote.invalid/api/v1/snapshot"; break;
        }
        Assert.Throws<ArgumentException>(() => options.ValidateManagedCropTeacherLiveContinueMode());
        Assert.False(Directory.Exists(options.Root));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("run")]
    [InlineData("slot")]
    [InlineData("root")]
    [InlineData("duplicate")]
    [InlineData("process")]
    [InlineData("executable")]
    [InlineData("time")]
    public void SameLiveSessionRequiresItsOriginalRunSaveAndOneNativeProcess(string fault)
    {
        var options = LiveOptions();
        var game = new JsonObject { ["role"] = "game", ["id"] = 123,
            ["exe"] = Path.Combine(options.Root, "StardewModdingAPI.exe"), ["started"] = "2026-11-07T01:00:00Z" };
        var session = new JsonObject { ["run_id"] = options.RunId, ["slot"] = options.SaveSlot,
            ["save_root"] = options.SaveIsolationPath, ["processes"] = new JsonArray(game) };
        if (fault is "run" or "slot" or "root") session[fault == "run" ? "run_id" : fault == "slot" ? "slot" : "save_root"] = "another-source";
        if (fault == "duplicate") session["processes"]!.AsArray().Add(game.DeepClone());
        if (fault == "process") game["id"] = 0;
        if (fault == "executable") game["exe"] = "relative.exe";
        if (fault == "time") game["started"] = "invalid";
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program")!.GetMethod("ReadManagedLiveGame", BindingFlags.Static | BindingFlags.NonPublic)!;
        if (fault == "valid") Assert.NotNull(method.Invoke(null, [session, options]));
        else Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [session, options])).InnerException);
    }

    private static LiveTrainingOptions LiveOptions()
    {
        var options = Options();
        options.ManagedCropReloadProcessId = 0;
        options.ManagedCropReloadExecutablePath = options.ManagedCropReloadLogPath = "";
        options.ManagedCropLiveSessionPath = Path.Combine(options.Root, "original-session.json");
        return options;
    }
}
