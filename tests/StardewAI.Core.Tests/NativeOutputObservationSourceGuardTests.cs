namespace StardewAI.Core.Tests;

public sealed class NativeOutputObservationSourceGuardTests
{
    [Fact]
    public void ExplicitSnapshotAnchorCapturesBeforeEndingTheSameObserverAndDoesNotChangeTools()
    {
        var scope = RuntimeHarnessSources.LoadFile("ModEntry.ActionOriginObservation.cs");
        var anchor = RuntimeHarnessSources.LoadFile("ModEntry.ActionOriginObservation.SnapshotAnchor.cs");
        Assert.True(scope.IndexOf("CaptureOutputSnapshotAnchor(pending, capture)", StringComparison.Ordinal) <
            scope.IndexOf("pending.OutputObservation?.Finish(capture!)", StringComparison.Ordinal));
        Assert.Contains("pending.Request.ActionOriginSnapshotAnchor && pending.Completion.Task.IsCompletedSuccessfully", scope);
        Assert.Contains("receipt.AfterGameTick != anchor.GameTick", scope);
        Assert.Contains("snapshot_anchor_requires_native_observation", scope);
        Assert.Contains("GetApi<INativeOutputSnapshotReadApi>", anchor);
        Assert.Contains("NativeOutputSnapshotAnchor.Read", anchor);
        foreach (var forbidden in new[] { ".collect(", ".DoFunction(", "Thread.Sleep", "Game1.paused =", "Game1.random", "HttpClient" })
            Assert.DoesNotContain(forbidden, anchor);
    }

    [Fact]
    public void OptionalResponseWaitsForCaptureWithoutChangingTheDefaultPath()
    {
        var entry = RuntimeHarnessSources.LoadFile("ModEntry.cs");
        var origin = RuntimeHarnessSources.LoadFile("ModEntry.ActionOriginOutputs.cs");
        var scope = RuntimeHarnessSources.LoadFile("ModEntry.ActionOriginObservation.cs");
        var state = RuntimeHarnessSources.LoadFile("ModEntry.State.cs");
        Assert.Contains("request.ActionOriginObservationScopeId is null ? pending.Completion.Task", entry);
        Assert.Contains("NativeObservationCompletion.AwaitFinalObservationAsync", entry);
        Assert.Contains("if (request.ActionOriginObservationScopeId is not null)", state);
        Assert.Contains("if (capture.RetainAcquisitionOutput) StoreActionOriginOutput(record)", origin);
        Assert.Contains("RetainAcquisitionOutput = retainAcquisitionOutput", origin);
        Assert.Contains("observe && ValidateExecutionRequest(request).Count == 0", origin);
        Assert.Contains("if (authorizedObservation) BeginOutputObservation", origin);
        Assert.Contains("NativeCollectionObservation.CanCapture(location, Game1.player)", origin);
        Assert.Contains("if (!capture.RetainAcquisitionOutput)", origin);
        Assert.Contains("!string.IsNullOrWhiteSpace(request.AcquisitionRouteOccurrenceId)", origin);
        Assert.Contains("CompleteOutputObservation(pending, capture)", origin);
        Assert.Contains("pending.Completion.Task.Result.ActionOriginObservation = receipt", scope);
        Assert.Contains("pending.OutputObservationCompleted?.TrySetResult(true)", scope);
        Assert.DoesNotContain("native-output-observation", entry);
        Assert.Contains("harmony.Unpatch(collect, HarmonyPatchType.All, harmony.Id)", scope);
    }

    [Fact]
    public void NativeTraceCannotSuppressCollectionOrSynthesizeDrops()
    {
        var hooks = RuntimeHarnessSources.LoadFile("ModEntry.ActionOriginObservation.Hooks.cs");
        var recorder = RuntimeHarnessSources.LoadFile("ModEntry.ActionOriginObservation.Recorder.cs");
        Assert.Contains("public static void CollectPrefix", hooks);
        Assert.Contains("public static void CollectPostfix(bool __result", hooks);
        Assert.Contains("public static void AdmissionPrefix", hooks);
        Assert.Contains("public static void AdmissionPostfix(Farmer __instance, bool __result", hooks);
        Assert.DoesNotContain("ref bool __result", hooks);
        Assert.Contains("if (owner is null || !owner.active) return", hooks);
        Assert.Contains("ItemStackAfterCall = __state.Item.Stack", hooks);
        Assert.Contains("ReferenceIds.For(debris)", recorder);
        Assert.Contains("SaveSerializer.GetSerializer(item.GetType()).Serialize(writer, item)", recorder);
        Assert.DoesNotContain("ClearanceOutputItemKey.From", recorder);
        Assert.Contains("observation_native_return_missing", recorder);
        Assert.Contains("observation_chunk_limit_exceeded", recorder);
        Assert.Contains("observation_nested_item_graph_not_supported", recorder);
        foreach (var source in new[] { hooks, recorder })
        {
            Assert.DoesNotContain("ItemRegistry.Create", source);
            Assert.DoesNotContain("Game1.random", source);
            Assert.DoesNotContain(".collect(", source);
            Assert.DoesNotContain(".addItemToInventoryBool(", source);
            Assert.DoesNotContain(".Stack =", source);
            Assert.DoesNotContain(".Chunks.Remove", source);
            Assert.DoesNotContain(".getOne(", source);
        }
    }
}
