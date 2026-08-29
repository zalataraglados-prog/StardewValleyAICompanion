namespace StardewAI.RuntimeTestHarness;

internal static class NativeRightClickEdgePatch
{
    public static bool Active { get; private set; }

    public static bool WasObserved { get; private set; }

    public static void Arm()
    {
        Active = true;
        WasObserved = false;
    }

    public static bool Prefix(ref bool __result)
    {
        if (!Active)
            return true;
        WasObserved = true;
        __result = true;
        return false;
    }

    public static void Clear()
    {
        Active = false;
    }
}
