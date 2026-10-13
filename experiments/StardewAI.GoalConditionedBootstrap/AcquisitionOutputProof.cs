namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionOutputProof
{
    public static bool CanSatisfy(
        int? provenQuantity,
        int? provenMinimumQuality,
        int requiredQuantity,
        int requiredMinimumQuality)
    {
        if (requiredQuantity <= 0 || requiredMinimumQuality < 0)
        {
            throw new InvalidDataException(
                "Required quantity must be positive and quality non-negative.");
        }
        if (provenQuantity is < 0 || provenMinimumQuality is < 0)
        {
            throw new InvalidDataException(
                "Proven quantity and quality must be non-negative when present.");
        }
        return provenQuantity >= requiredQuantity &&
            provenMinimumQuality >= requiredMinimumQuality;
    }

    public static bool MatchesAuthoritativeRoute(
        AcquisitionMachineActiveOutputState? output,
        string routeKind,
        string sourceId,
        string qualifiedItemId)
    {
        if (output is null ||
            string.IsNullOrWhiteSpace(routeKind) ||
            string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return false;
        }
        return output.QualifiedItemId == qualifiedItemId &&
            output.RouteSources.Any(source =>
                source.RouteKind == routeKind &&
                source.SourceId == sourceId &&
                source.QualifiedItemId == qualifiedItemId);
    }

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
