using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherWateringRefillRecoveryDecision(
    MoneyRevenueCropPlantingTile? ApproachTile,
    int ExecutableTileCount);

public static class CropTeacherWateringRefillRecoveryPlanner
{
    public static CropTeacherWateringRefillRecoveryDecision Build(
        CropTeacherRunStep step,
        MoneyRevenueCropNativeProductionRunPlanRequest request,
        int wateringCanWaterLeft,
        int playerTileX,
        int playerTileY)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(request);
        if (wateringCanWaterLeft < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wateringCanWaterLeft));
        }
        var batch = step.ActionBatch;
        if (step.Stage != CropTeacherRunStageKinds.ExecuteCropBatch ||
            batch?.OptionId != "executor.water_crop" ||
            batch.Tiles.Length == 0)
        {
            throw new ArgumentException(
                "A non-empty crop watering batch is required.",
                nameof(step));
        }
        if (wateringCanWaterLeft > 0)
        {
            return new CropTeacherWateringRefillRecoveryDecision(
                null,
                Math.Min(wateringCanWaterLeft, batch.Tiles.Length));
        }

        var matches = request.LayoutIntents
            .Where(value => value.CommitmentId == step.CommitmentId)
            .ToArray();
        var approach = matches.Length == 1
            ? matches[0].WateringRefillApproachTile
            : null;
        if (approach is null)
        {
            throw new InvalidDataException(
                "crop_teacher_watering_refill_approach_missing");
        }

        var atApproach = approach.TileX == playerTileX &&
            approach.TileY == playerTileY;
        return new CropTeacherWateringRefillRecoveryDecision(
            atApproach ? null : approach,
            atApproach ? batch.Tiles.Length : 0);
    }
}
