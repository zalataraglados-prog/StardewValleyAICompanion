using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherWateringRefillRecoveryPlannerTests
{
    private const string CommitmentId = "crop-1";

    [Fact]
    public void EmptyCanSelectsPersistedApproachForWaterBatch()
    {
        var decision = CropTeacherWateringRefillRecoveryPlanner.Build(
            WaterStep(),
            Request(new MoneyRevenueCropPlantingTile(8, 9)),
            0,
            4,
            5);

        Assert.Equal(
            new MoneyRevenueCropPlantingTile(8, 9),
            decision.ApproachTile);
        Assert.Equal(0, decision.ExecutableTileCount);
    }

    [Fact]
    public void ExistingWaterOrArrivalAtApproachDoesNotPreemptWaterBatch()
    {
        var target = new MoneyRevenueCropPlantingTile(8, 9);

        var partial = CropTeacherWateringRefillRecoveryPlanner.Build(
            WaterStep(),
            Request(target),
            4,
            4,
            5);
        Assert.Null(partial.ApproachTile);
        Assert.Equal(4, partial.ExecutableTileCount);

        var atApproach = CropTeacherWateringRefillRecoveryPlanner.Build(
            WaterStep(),
            Request(target),
            0,
            8,
            9);
        Assert.Null(atApproach.ApproachTile);
        Assert.Equal(6, atApproach.ExecutableTileCount);
    }

    [Fact]
    public void EmptyCanRejectsWaterBatchWithoutTeacherApproachEvidence()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            CropTeacherWateringRefillRecoveryPlanner.Build(
                WaterStep(),
                Request(null),
                0,
                4,
                5));

        Assert.Equal(
            "crop_teacher_watering_refill_approach_missing",
            error.Message);
    }

    private static CropTeacherRunStep WaterStep() => new(
        CropTeacherRunStageKinds.ExecuteCropBatch,
        7,
        CommitmentId,
        "Farm",
        "FarmHouse",
        string.Empty,
        string.Empty,
        0,
        0,
        new MoneyRevenueCropNativeProductionActionBatch(
            1,
            CommitmentId,
            "executor.water_crop",
            "crop_needs_watering",
            "Farm",
            string.Empty,
            Enumerable.Range(0, 6)
                .Select(index => new MoneyRevenueCropPlantingTile(
                    10 + index,
                    12))
                .ToArray()),
        "test");

    private static MoneyRevenueCropNativeProductionRunPlanRequest Request(
        MoneyRevenueCropPlantingTile? approach) => new()
    {
        LayoutIntents =
        [
            new MoneyRevenueCropNativeProductionLayoutIntent(
                CommitmentId,
                "Farm",
                [new MoneyRevenueCropPlantingTile(10, 12)],
                null,
                approach)
        ]
    };
}
