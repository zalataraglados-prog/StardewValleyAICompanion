using HarmonyLib;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool nativeObservationPatchesReady;
    private bool nativeObservationPatchesFailed;

    private static string? ObservationRequestRejection(TrainingExecutionRequest request) =>
        request.ActionOriginObservationScopeId is null ? null : NativeObservationScopePolicy.Rejection(request.ActionOriginObservationScopeId, request.ExecutionMode, request.OptionId,
            new[] { request.RunId, request.QueueId, request.QueueItemId, request.RequestNonce, request.BeforeStateHash, request.SaveIsolationPath });

    private void BeginOutputObservation(PendingExecution pending, ActionOriginOutputCapture capture)
    {
        try
        {
            if (nativeObservationPatchesFailed)
            {
                pending.OutputObservationFailure = "observation_patch_installation_failed";
                return;
            }
            if (!nativeObservationPatchesReady)
            {
                var collect = AccessTools.Method(typeof(Debris), nameof(Debris.collect), new[] { typeof(Farmer), typeof(Chunk) })
                    ?? throw new MissingMethodException("Native Debris.collect signature is unavailable.");
                var admission = AccessTools.Method(typeof(Farmer), nameof(Farmer.addItemToInventoryBool), new[] { typeof(Item), typeof(bool) })
                    ?? throw new MissingMethodException("Native Farmer admission signature is unavailable.");
                var harmony = new Harmony(ModManifest.UniqueID + ".native-output-observation");
                try
                {
                    harmony.Patch(collect,
                        prefix: new HarmonyMethod(typeof(NativeCollectionObservation), nameof(NativeCollectionObservation.CollectPrefix)),
                        postfix: new HarmonyMethod(typeof(NativeCollectionObservation), nameof(NativeCollectionObservation.CollectPostfix)));
                    harmony.Patch(admission,
                        prefix: new HarmonyMethod(typeof(NativeCollectionObservation), nameof(NativeCollectionObservation.AdmissionPrefix)),
                        postfix: new HarmonyMethod(typeof(NativeCollectionObservation), nameof(NativeCollectionObservation.AdmissionPostfix)));
                    nativeObservationPatchesReady = true;
                }
                catch
                {
                    nativeObservationPatchesFailed = true;
                    harmony.Unpatch(collect, HarmonyPatchType.All, harmony.Id);
                    harmony.Unpatch(admission, HarmonyPatchType.All, harmony.Id);
                    throw;
                }
            }
            pending.OutputObservation = new NativeCollectionObservation(pending.Request, capture);
            NativeCollectionObservation.Begin(pending.OutputObservation);
        }
        catch (Exception error)
        {
            if (!nativeObservationPatchesReady) nativeObservationPatchesFailed = true;
            pending.OutputObservationFailure = "observation_begin_failed:" + error.GetType().Name;
            pending.OutputObservation?.Stop();
            pending.OutputObservation = null;
        }
    }

    private static void CompleteOutputObservation(PendingExecution pending, ActionOriginOutputCapture? capture)
    {
        if (pending.Request.ActionOriginObservationScopeId is null) return;
        try
        {
            var receipt = pending.OutputObservation?.Finish(capture!) ?? new ActionOriginObservationReceipt
            {
                ScopeId = pending.Request.ActionOriginObservationScopeId,
                RunId = pending.Request.RunId, QueueId = pending.Request.QueueId, QueueItemId = pending.Request.QueueItemId,
                RequestNonce = pending.Request.RequestNonce, BeforeStateHash = pending.Request.BeforeStateHash,
                Diagnostics = new[] { pending.OutputObservationFailure ?? "observation_world_unavailable" }
            };
            if (pending.Completion.Task.IsCompletedSuccessfully)
                pending.Completion.Task.Result.ActionOriginObservation = receipt;
        }
        catch (Exception error)
        {
            if (pending.Completion.Task.IsCompletedSuccessfully)
                pending.Completion.Task.Result.ActionOriginObservation = new ActionOriginObservationReceipt
                {
                    ScopeId = pending.Request.ActionOriginObservationScopeId,
                    RunId = pending.Request.RunId, QueueId = pending.Request.QueueId, QueueItemId = pending.Request.QueueItemId,
                    RequestNonce = pending.Request.RequestNonce, BeforeStateHash = pending.Request.BeforeStateHash,
                    Diagnostics = new[] { "observation_finalize_failed:" + error.GetType().Name }
                };
        }
        finally
        {
            pending.OutputObservation?.Stop();
            pending.OutputObservation = null;
            pending.OutputObservationCompleted?.TrySetResult(true);
        }
    }
}
