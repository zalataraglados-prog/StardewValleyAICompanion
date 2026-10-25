using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static class CropTeacherSitePreparationFrontier
{
    public static MoneyRevenueCropSitePreparationStep? SelectNextExecutableStep(
        MoneyRevenueCropSitePreparationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var next = plan.Steps.FirstOrDefault(step =>
            step.PlanStatus is
                "direct_till_ready" or
                "clear_then_fresh_snapshot_rebind" or
                "clear_access_then_fresh_snapshot_rebind");
        if (next is not null)
        {
            return next;
        }

        if (plan.Steps.All(step => step.PlanStatus == "already_prepared"))
        {
            return null;
        }

        var reasons = plan.Steps
            .Where(step => step.PlanStatus != "already_prepared")
            .SelectMany(step => step.BlockReasons)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        throw new InvalidDataException(
            "crop_teacher_site_preparation_blocked:" +
            string.Join(",", reasons));
    }
}
