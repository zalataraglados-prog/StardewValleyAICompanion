namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSupportingRoute
{
    private CropTeacherSupportingRoute(Preparation preparation,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        SiteApproachTarget = preparation.SiteApproachTarget
            ?? throw new InvalidDataException("Native site approach requires its original selected stand.");
        var target = SiteApproachTarget;
        nativeSiteApproach = new(preparation, transition, stepId, "move_to_tile", frame =>
        {
            if (frame.After.State["player"].GetProperty("location_id").GetProperty("value").GetString() !=
                    preparation.ExpectedHopTargetLocation ||
                MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(frame.After, "player", "tile_x") != target.TileX ||
                MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(frame.After, "player", "tile_y") != target.TileY)
                throw new InvalidDataException("Native site approach did not reach its exact selected stand.");
        });
        AfterSnapshotPath = nativeSiteApproach.AfterSnapshotPath;
        RouteTargetReached = false;
    }
}
