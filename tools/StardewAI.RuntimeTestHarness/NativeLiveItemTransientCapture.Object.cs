using System.Reflection;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLiveObjectTransientState(bool HasHeldObject, bool HasLightSource, int ShakeTimer, int Health);

internal static partial class NativeLiveItemTransientCapture
{
    private static readonly FieldInfo ObjectHasHeldObjectField = RequireField(typeof(StardewValley.Object), "_hasHeldObject", typeof(bool));
    private static readonly FieldInfo ObjectHasLightSourceField = RequireField(typeof(StardewValley.Object), "_hasLightSource", typeof(bool));
    private static readonly FieldInfo ObjectHealthField = RequireField(typeof(StardewValley.Object), "health", typeof(int));

    // Original Capture has already admitted the exact native runtime type. Read raw state,
    // not a derived/repaired view of the held object, light source, elapsed timer or health.
    private static NativeLiveObjectTransientState? CaptureObject(Item item) => item is StardewValley.Object obj
        ? new(ReadValue<bool>(ObjectHasHeldObjectField, obj), ReadValue<bool>(ObjectHasLightSourceField, obj),
            obj.shakeTimer, ReadValue<int>(ObjectHealthField, obj))
        : null;
}
