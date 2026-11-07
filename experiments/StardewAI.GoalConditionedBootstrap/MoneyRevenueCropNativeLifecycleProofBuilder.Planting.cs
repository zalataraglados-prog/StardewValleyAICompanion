using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    internal static void ValidateOwnedPlanting(SnapshotEnvelope before, SnapshotEnvelope after,
        string locationId, int x, int y, CropPlantingCommitment commitment) =>
        ValidateInitialPlanting(ReadCrop(before, locationId, x, y), ReadCrop(after, locationId, x, y),
            commitment.SeedQualifiedItemId, commitment.HarvestItemQualifiedId, commitment.BaseGrowDays);

    private static void ValidateInitialPlanting(CropState? before, CropState? after,
        string seedQualifiedId, string harvestQualifiedId, int baseGrowDays)
    {
        Require(before is null && after is not null &&
                NormalizeObjectId(after.SeedId) == seedQualifiedId &&
                after.HarvestQualifiedItemId == harvestQualifiedId &&
                !after.Dead && !after.ReadyForHarvest && after.CurrentPhase == 0 &&
                after.DayOfCurrentPhase == 0 && after.PhaseCount == after.PhaseDays.Length &&
                after.PhaseDays.Where(value => value < 99999).Sum() == baseGrowDays,
            "Money Revenue native planting transition is not exact.");
    }
}
