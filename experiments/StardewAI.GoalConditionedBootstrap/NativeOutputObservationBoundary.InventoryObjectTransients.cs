using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedObjectTransientState(bool HasHeldObject, bool HasLightSource, int ShakeTimer, int Health);

public static partial class NativeOutputObservationBoundary
{
    private static NativeObservedObjectTransientState? ReadObjectTransient(ActionOriginObjectTransientState? state,
        string runtimeType, bool required)
    {
        var isObject = NativeInventoryPayloadTypes.TryGet(runtimeType, out var type) &&
            type.IdentityRule == NativeInventoryPayloadIdentityRule.ObjectBigCraftable;
        if (!isObject)
        {
            Require(state is null, "non-Object inventory slot has fabricated Object transients");
            return null;
        }
        if (state is null && !required) return null; // Legacy common-root proof is not Object-subtype proof.
        Require(state?.HasHeldObject is not null && state.HasLightSource.HasValue &&
            state.ShakeTimer.HasValue && state.Health.HasValue, "raw native Object subtype state is unavailable");
        return new(state!.HasHeldObject!.Value, state.HasLightSource!.Value, state.ShakeTimer!.Value, state.Health!.Value);
    }
}
