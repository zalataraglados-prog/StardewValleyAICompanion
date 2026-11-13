using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static class CropTeacherSitePreparationFrontier
{
    public static MoneyRevenueCropSitePreparationStep? SelectNextExecutableStep(
        MoneyRevenueCropSitePreparationPlan plan)
        => MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(plan);
}
