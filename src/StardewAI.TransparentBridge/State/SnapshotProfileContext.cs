using System.Threading;

namespace StardewAI.TransparentBridge.State;

public static class SnapshotProfileContext
{
    private static readonly AsyncLocal<string?> CurrentProfile = new();
    private static readonly AsyncLocal<string?> FishingLocation = new();
    private static readonly AsyncLocal<int?> FishingRodSlot = new();
    private static readonly AsyncLocal<FarmSitePreparationBounds?>
        FarmSitePreparationRegion = new();

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

    public static FarmSitePreparationBounds? SitePreparationBounds
    {
        get => FarmSitePreparationRegion.Value;
        set => FarmSitePreparationRegion.Value = value;
    }

    public static bool IncludesPersistentMaterialInventoryGraph =>
        Current is "daily" or "training_machine" or "fishing" or "full" or "collection_teacher" or "marriage_teacher";

    public static bool IncludesNpcScheduleCatalog =>
        Current is "social" or "social_future" or "full" or "marriage_teacher";

    public static bool IncludesSocialFutureRouteDateEvidence =>
        Current is "social_future" or "full" or "collection_teacher" or "marriage_teacher";

    public static bool IncludesCrabPotNetwork =>
        Current is "daily" or "fishing" or "full" or "collection_teacher";

    public static bool IncludesFarmSitePreparation =>
        Current == "farm_layout";

    public static bool IncludesHayStorage =>
        Current is "daily" or "full" or "collection_teacher";
}

public sealed record FarmSitePreparationBounds(
    int MinX,
    int MinY,
    int MaxX,
    int MaxY);
