using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class SelectedTillingExecutionObservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedTillObservationUsesAPrivateOptionsCopyAndFullOriginalIdentity(bool product)
    {
        var original = Options(product); var raw = JsonSerializer.Serialize(original);
        var selected = Copy(original); var request = Request();
        Apply(selected, request);
        Assert.True(NativeTillingObservationContract.IsCompleteRequest(request));
        Assert.StartsWith("native-output.v1:", request.ActionOriginObservationScopeId);
        Assert.NotSame(original, selected); Assert.Equal("full", selected.ExecutionSnapshotProfile);
        Assert.Equal("farm_layout", original.ExecutionSnapshotProfile);
        Assert.Equal(raw, JsonSerializer.Serialize(original));
        Assert.False(selected.ObserveNativeOutputs); Assert.Null(NativeOutputObservationMode.CreateScopeId(original, Request()));
        var next = Request(); Apply(selected, next);
        Assert.Equal(request.ActionOriginObservationScopeId, next.ActionOriginObservationScopeId);
        next.RequestNonce = "another-original-nonce"; Apply(selected, next);
        Assert.NotEqual(request.ActionOriginObservationScopeId, next.ActionOriginObservationScopeId);
    }

    [Theory]
    [InlineData("not_selected")]
    [InlineData("training")]
    [InlineData("feedback")]
    [InlineData("executor")]
    [InlineData("mode")]
    [InlineData("profile")]
    [InlineData("option")]
    [InlineData("source_option")]
    [InlineData("run")]
    [InlineData("save")]
    [InlineData("nonce")]
    public void SelectedTillObservationCannotBecomeAnImplicitGlobalOrOtherActionScope(string fault)
    {
        var options = Copy(Options(true)); var request = Request();
        switch (fault)
        {
            case "not_selected": options = Options(true); break;
            case "training": options.SkipTraining = false; break;
            case "feedback": options.RequireExecutorFeedback = false; break;
            case "executor": options.UseRuntimeTestHarnessExecutor = false; break;
            case "mode": options.TargetExecutionMode = "coop_companion"; break;
            case "profile": options.ExecutionSnapshotProfile = "farm_layout"; break;
            case "option": request.OptionId = "executor.move_to_tile"; break;
            case "source_option": request.SourcePlanningOptionId = "executor.move_to_tile"; break;
            case "run": request.RunId += "foreign"; break;
            case "save": request.SaveIsolationPath += "foreign"; break;
            case "nonce": request.RequestNonce = ""; break;
        }
        var raw = JsonSerializer.Serialize(request);
        if (fault is "run" or "save" or "nonce") Assert.Throws<InvalidOperationException>(() => Apply(options, request));
        else Assert.Throws<InvalidDataException>(() => Apply(options, request));
        Assert.Equal(raw, JsonSerializer.Serialize(request));
    }

    private static LiveTrainingOptions Options(bool product) => new() { RunId = "till-test-run", SaveIsolationPath = "isolated-till-test",
        SkipTraining = true, RequireExecutorFeedback = true, UseRuntimeTestHarnessExecutor = true, UseProductExecutor = product,
        TargetExecutionMode = "training_singleplayer", ExecutionSnapshotProfile = "farm_layout" };
    private static TrainingExecutionRequest Request() => new() { RunId = "till-test-run", SaveIsolationPath = "isolated-till-test",
        ExecutionMode = "training_singleplayer", OptionId = "executor.till_soil", SourcePlanningOptionId = "executor.till_soil",
        QueueId = "queue", QueueItemId = "item", RequestNonce = "nonce", BeforeStateHash = "before-hash" };
    private static LiveTrainingOptions Copy(LiveTrainingOptions options) => (LiveTrainingOptions)typeof(LiveTrainingOptions)
        .GetMethod("WithSelectedTillingResources", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(options, null)!;
    private static void Apply(LiveTrainingOptions options, TrainingExecutionRequest request)
    {
        var method = typeof(LiveTrainingOptions).Assembly.GetType("StardewAI.LiveTrainingLoop.NativeTillingExecutionObservation")!
            .GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!;
        try { method.Invoke(null, new object[] { options, request }); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException invalid) { throw invalid; }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException invalid) { throw invalid; }
    }
}
