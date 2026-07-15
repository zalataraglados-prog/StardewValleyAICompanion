using System.Threading;

namespace StardewAI.TransparentBridge.State;

public static class SnapshotProfileContext
{
    private static readonly AsyncLocal<string?> CurrentProfile = new();

    public static string Current
    {
        get => CurrentProfile.Value ?? "light";
        set => CurrentProfile.Value = value;
    }
}
