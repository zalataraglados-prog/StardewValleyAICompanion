using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class SelectedWateringExecutionObservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitWateringCopyKeepsOrdinaryOptionsAndIdentityUnchanged(bool product)
    {
        var options = Options(product); var raw = JsonSerializer.Serialize(options);
        var copy = Copy(options); var request = Request(); Apply(copy, request);
        Assert.True(NativeWateringObservationContract.IsCompleteRequest(request));
        Assert.False(request.ActionOriginTillingResources);
        Assert.Equal(raw, JsonSerializer.Serialize(options));
        Assert.Equal("farm_layout", options.ExecutionSnapshotProfile); Assert.Equal("full", copy.ExecutionSnapshotProfile);
        Assert.False(copy.ObserveNativeOutputs);
        var again = Request(); Apply(copy, again);
        Assert.Equal(request.ActionOriginObservationScopeId, again.ActionOriginObservationScopeId);
        again.RequestNonce += "next"; Apply(copy, again);
        Assert.NotEqual(request.ActionOriginObservationScopeId, again.ActionOriginObservationScopeId);
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
    [InlineData("till_request")]
    [InlineData("till_options")]
    [InlineData("run")]
    [InlineData("save")]
    [InlineData("nonce")]
    public void SelectedWateringDoesNotLeakToOtherActionsOrTraining(string fault)
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
            case "source_option": request.SourcePlanningOptionId = "executor.till_soil"; break;
            case "till_request": request.ActionOriginTillingResources = true; break;
            case "till_options": options = (LiveTrainingOptions)typeof(LiveTrainingOptions)
                .GetMethod("WithSelectedTillingResources", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(options, null)!; break;
            case "run": request.RunId += "foreign"; break;
            case "save": request.SaveIsolationPath += "foreign"; break;
            case "nonce": request.RequestNonce = ""; break;
        }
        var raw = JsonSerializer.Serialize(request);
        if (fault is "run" or "save" or "nonce") Assert.Throws<InvalidOperationException>(() => Apply(options, request));
        else Assert.Throws<InvalidDataException>(() => Apply(options, request));
        Assert.Equal(raw, JsonSerializer.Serialize(request));
    }

    [Fact]
    public void ManagedWateringOptInCannotBeAppliedWithoutItsOwner()
    {
        var options = Options(true); options.UseManagedWateringResources = true;
        Assert.Throws<ArgumentException>(() => options.ValidateManagedCropTeacherMode());
        Assert.False(new LiveTrainingOptions().UseManagedWateringResources);
    }

    private static LiveTrainingOptions Options(bool product) => new() { RunId = "water-test", SaveIsolationPath = "isolated-water-test",
        SkipTraining = true, RequireExecutorFeedback = true, UseRuntimeTestHarnessExecutor = true, UseProductExecutor = product,
        TargetExecutionMode = "training_singleplayer", ExecutionSnapshotProfile = "farm_layout" };
    private static TrainingExecutionRequest Request() => new() { RunId = "water-test", SaveIsolationPath = "isolated-water-test",
        ExecutionMode = "training_singleplayer", OptionId = "executor.water_crop", SourcePlanningOptionId = "executor.water_crop",
        QueueId = "queue", QueueItemId = "item", RequestNonce = "nonce", BeforeStateHash = "before-hash" };
    private static LiveTrainingOptions Copy(LiveTrainingOptions options) => (LiveTrainingOptions)typeof(LiveTrainingOptions)
        .GetMethod("WithSelectedWateringResources", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(options, null)!;
    private static void Apply(LiveTrainingOptions options, TrainingExecutionRequest request)
    {
        try { typeof(LiveTrainingOptions).Assembly.GetType("StardewAI.LiveTrainingLoop.NativeWateringExecutionObservation")!
            .GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [options, request]); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException invalid) { throw invalid; }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException invalid) { throw invalid; }
    }
}
