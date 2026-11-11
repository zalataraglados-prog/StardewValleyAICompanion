using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyCropHarvestTarget(SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request)
    {
        _ = ReadCropHarvestTarget(before, after, request);
        var x = request.TargetTileX!.Value; var y = request.TargetTileY!.Value;
        var regrow = MoneyRevenueCropNativeLifecycleProofBuilder.VerifyOwnedHarvestTarget(before, after, request.LocationId!, x, y);
        var expected = Field(before, "farm", "crops")!;
        var rows = Array(Value(expected), "harvest crop rows");
        var target = rows.OfType<JsonObject>().Single(row => row["location_id"]?.GetValue<string>() == request.LocationId &&
            Int(row, "tile_x") == x && Int(row, "tile_y") == y);
        if (!regrow.HasValue) rows.Remove(target);
        else
        {
            target["fully_grown"] = true;
            target["day_of_current_phase"] = regrow.Value;
            target["ready_for_harvest"] = false;
            target["days_until_next_harvest_if_watered"] = regrow.Value;
        }
        EqualField(expected, Field(after, "farm", "crops"), "harvest crop target, other crops or unknown metadata");
    }
}
