using System.Threading;

namespace StardewAI.TransparentBridge.State;

public static class SnapshotProfileContext
{
    private static readonly AsyncLocal<string?> CurrentProfile = new();
    private static readonly AsyncLocal<string?> FishingLocation = new();
    private static readonly AsyncLocal<int?> FishingRodSlot = new();

    public static string Current
    {
        get => CurrentProfile.Value ?? "light";
        set => CurrentProfile.Value = value;
    }

    public static string? TargetFishingLocationId
    {
        get => FishingLocation.Value;
        set => FishingLocation.Value = value;
    }

    public static int? TargetFishingRodSlotIndex
    {
        get => FishingRodSlot.Value;
        set => FishingRodSlot.Value = value;
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
