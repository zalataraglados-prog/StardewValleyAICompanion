using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    private static void VerifyHarvestDropAccounting(SnapshotEnvelope before,
        IReadOnlyList<(SnapshotEnvelope After, long HeldIncrease)> acquired, string itemId, int minimumPerTile)
    {
        var prior = CropSeedResourceBoundary.CountHarvestDebris(before, itemId);
        Require(prior == 0, "Owned harvest cannot attribute pre-existing matching debris to this batch.");
        foreach (var (after, heldIncrease) in acquired)
        {
            var current = CropSeedResourceBoundary.CountHarvestDebris(after, itemId);
            // Natural pickup moves units from ground to inventory; it does not produce them again.
            Require(checked(heldIncrease + current - prior) >= minimumPerTile,
                "Owned harvest native step did not account for its minimum produced output.");
            prior = current;
        }
    }
}
