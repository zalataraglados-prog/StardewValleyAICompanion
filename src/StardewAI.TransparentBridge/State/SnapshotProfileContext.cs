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

    public static bool IncludesPersistentMaterialInventoryGraph =>
        Current is "daily" or "training_machine" or "fishing" or "full";

    public static bool IncludesNpcScheduleCatalog =>
        Current is "social" or "social_future" or "full";

    public static bool IncludesSocialFutureRouteDateEvidence =>
        Current is "social_future" or "full";

    public static bool IncludesCrabPotNetwork =>
        Current is "daily" or "fishing" or "full";
}
