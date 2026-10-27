using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherSitePreparationFrontierTests
{
    [Fact]
    public void SelectsExecutableFrontierEvenWhenLaterTilesAreBlocked()
    {
        var ready = Step("direct_till_ready", "executor.till_soil");
        var plan = Plan(
            Step("blocked", string.Empty, "till_soil_no_adjacent_route_stand_tile"),
            ready,
            Step("blocked", string.Empty, "clear_obstacle_no_adjacent_route_stand_tile"));

        Assert.Same(
            ready,
            CropTeacherSitePreparationFrontier.SelectNextExecutableStep(plan));
    }

    [Fact]
    public void ReturnsCompleteOnlyWhenEveryTileIsAlreadyPrepared()
    {
        var plan = Plan(
            Step("already_prepared", string.Empty),
            Step("already_prepared", string.Empty));

        Assert.Null(
            CropTeacherSitePreparationFrontier.SelectNextExecutableStep(plan));
    }

    [Fact]
    public void SelectsReachableAccessClearanceForAnIsolatedTillTile()
    {
        var access = Step(
            "clear_access_then_fresh_snapshot_rebind",
            "executor.clear_obstacle");
        var plan = Plan(
            access,
            Step(
                "blocked",
                string.Empty,
                "till_soil_no_adjacent_route_stand_tile"));

        Assert.Same(
            access,
            CropTeacherSitePreparationFrontier.SelectNextExecutableStep(plan));
    }

    [Fact]
    public void FailsClosedWhenUnfinishedTilesHaveNoExecutableFrontier()
    {
        var plan = Plan(
            Step("already_prepared", string.Empty),
            Step("blocked", string.Empty, "till_soil_no_adjacent_route_stand_tile"),
            Step("blocked", string.Empty, "clear_obstacle_no_adjacent_route_stand_tile"));

        var error = Assert.Throws<InvalidDataException>(() =>
            CropTeacherSitePreparationFrontier.SelectNextExecutableStep(plan));

        Assert.Equal(
            "crop_teacher_site_preparation_blocked:" +
            "till_soil_no_adjacent_route_stand_tile," +
            "clear_obstacle_no_adjacent_route_stand_tile",
            error.Message);
    }

    private static MoneyRevenueCropSitePreparationPlan Plan(
        params MoneyRevenueCropSitePreparationStep[] steps)
    {
        return new MoneyRevenueCropSitePreparationPlan
        {
            Steps = steps
        };
    }

    private static MoneyRevenueCropSitePreparationStep Step(
        string status,
        string optionId,
        params string[] blockReasons)
    {
        return new MoneyRevenueCropSitePreparationStep(
            1,
            1,
            string.Empty,
            status,
            optionId,
            optionId.Length == 0 ? string.Empty : "candidate",
            Array.Empty<SmallModelActionParameter>(),
            string.Empty,
            blockReasons);
    }
}
