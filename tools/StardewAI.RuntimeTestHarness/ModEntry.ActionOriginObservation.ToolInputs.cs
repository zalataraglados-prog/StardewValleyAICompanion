using System.Globalization;
using HarmonyLib;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool nativeToolInputPatchesReady;
    private bool nativeToolInputPatchesFailed;

    private void EnsureNativeToolInputPatches()
    {
        if (nativeToolInputPatchesFailed)
            throw new InvalidOperationException("Native tool input observation patch installation previously failed.");
        if (nativeToolInputPatchesReady) return;
        try
        {
            NativeClearanceToolObservationHooks.Install(new Harmony(ModManifest.UniqueID + ".native-tool-input-observation"));
            nativeToolInputPatchesReady = true;
        }
        catch
        {
            nativeToolInputPatchesFailed = true;
            throw;
        }
    }

    private sealed partial class NativeCollectionObservation : INativeClearanceToolCallSink
    {
        private readonly BoundedNativeObservationBuffer<ActionOriginClearanceToolCall> toolCalls = new(256);
        private IDisposable? toolInputScope;

        internal void BeginToolInputs() => toolInputScope = NativeClearanceToolObservationHooks.Begin(this, farmer, capture.Location);

        private void StopToolInputs()
        {
            var scope = toolInputScope;
            toolInputScope = null;
            scope?.Dispose();
        }

        void INativeClearanceToolCallSink.Reject(string diagnostic) => Reject("tool_input:" + diagnostic);

        void INativeClearanceToolCallSink.Record(NativeClearanceToolCall call)
        {
            if (!active || !ReferenceEquals(current, this) || Environment.CurrentManagedThreadId != threadId ||
                !request.ActionOriginToolInputs || !ReferenceEquals(call.Farmer, farmer) ||
                !ReferenceEquals(call.Location, capture.Location) ||
                call.Tool.GetType() != typeof(Axe) && call.Tool.GetType() != typeof(Pickaxe))
            {
                Reject("tool_input:observation_context_mismatch");
                return;
            }
            var row = new ActionOriginClearanceToolCall
            {
                SchemaVersion = "native_clearance_tool_call.v1", Ordinal = call.Ordinal,
                ToolReferenceId = ReferenceIds.For(call.Tool), ToolRuntimeType = call.Tool.GetType().FullName!,
                QualifiedToolId = call.Tool.QualifiedItemId,
                ActorId = farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                LocationId = capture.Location.NameOrUniqueName, X = call.X, Y = call.Y,
                ArgumentPower = call.ArgumentPower, NativePower = call.NativePower,
                BeforeGameTick = call.BeforeGameTick, InputGameTick = call.InputGameTick,
                DebitGameTick = call.DebitGameTick, AfterGameTick = call.AfterGameTick,
                StaminaAtEntry = call.StaminaAtEntry, StaminaBeforeDebit = call.StaminaBeforeDebit,
                StaminaAfterDebit = call.StaminaAfterDebit, StaminaAtExit = call.StaminaAtExit,
                MaxStamina = call.MaxStamina, EffectiveSkillLevel = call.EffectiveSkillLevel,
                IsEfficient = call.IsEfficient, HasStaminaDecreaseProtection = call.HasStaminaDecreaseProtection,
                StaminaSetterInvoked = call.StaminaSetterInvoked
            };
            if (!toolCalls.TryAdd(row)) Reject("tool_input:call_limit_exceeded");
        }
    }
}
