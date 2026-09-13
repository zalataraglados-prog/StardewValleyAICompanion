namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionOutputProof
{
    public static bool CanGuaranteeQuality(
        int provenMinimumQuality,
        int requiredMinimumQuality)
    {
        if (provenMinimumQuality < 0 || requiredMinimumQuality < 0)
        {
            throw new InvalidDataException(
                "Proven and required minimum quality must be non-negative.");
        }
        return provenMinimumQuality >= requiredMinimumQuality;
    }

    public static int ReadyQuantity(
        IEnumerable<AcquisitionProcessingLeadTimeEvaluation> evaluations,
        int requiredMinimumQuality)
    {
        if (requiredMinimumQuality < 0)
        {
            throw new InvalidDataException(
                "Required minimum quality must be non-negative.");
        }
        var result = 0;
        foreach (var evaluation in evaluations)
        {
            if (evaluation.OutputReadyOnTargetDate != true ||
                !evaluation.ProvenMinimumQuality.HasValue ||
                evaluation.ProvenMinimumQuality.Value < requiredMinimumQuality)
            {
                continue;
            }
            if (!evaluation.ProvenOutputQuantityLowerBound.HasValue ||
                evaluation.ProvenOutputQuantityLowerBound.Value < 0)
            {
                throw new InvalidDataException(
                    "A ready output lacks a non-negative quantity proof.");
            }
            result = checked(result +
                evaluation.ProvenOutputQuantityLowerBound.Value);
        }
        return result;
    }
}
