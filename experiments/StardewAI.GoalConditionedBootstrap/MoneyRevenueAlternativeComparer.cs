using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class MoneyRevenueAlternativeComparer
{
    internal static MoneyRevenueAlternativeComparison Compare(
        string criterionId,
        string sourceStateHash,
        int expectedLedgerRevision,
        IReadOnlyList<string> expectedAlternativeIds,
        IReadOnlyList<MoneyRevenueAlternativeCandidate> alternatives)
    {
        var threshold = GrandpaEvaluationGoalEvaluator.MoneyThresholds.SingleOrDefault(
                value => value.CriterionId == criterionId) ??
            throw new InvalidDataException(
                "Money revenue comparison criterion is not authoritative.");
        var verified = ValidateDenominator(
            threshold.Target,
            sourceStateHash,
            expectedLedgerRevision,
            expectedAlternativeIds,
            alternatives);
        var dominatedBy = verified.ToDictionary(
            candidate => candidate.AlternativeId,
            candidate => verified.Where(other =>
                    other.AlternativeId != candidate.AlternativeId &&
                    Dominates(other, candidate))
                .Select(other => other.AlternativeId)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        var frontier = dominatedBy
            .Where(row => row.Value.Length == 0)
            .Select(row => row.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unique = frontier.Length == 1;
        var selected = unique ? frontier[0] : string.Empty;
        var evaluations = verified.Select(candidate =>
                new MoneyRevenueAlternativeEvaluation(
                    candidate.AlternativeId,
                    candidate.TargetSatisfied,
                    candidate.ProgressTowardTarget,
                    candidate.Prefix.FinalTotalDay,
                    candidate.Prefix.TotalRevenue,
                    candidate.ReservationNotRequired,
                    candidate.ReservationIds,
                    candidate.OpportunityCost,
                    dominatedBy[candidate.AlternativeId],
                    dominatedBy[candidate.AlternativeId].Length == 0))
            .OrderBy(value => value.AlternativeId, StringComparer.Ordinal)
            .ToArray();
        var preferences = unique
            ? verified.Where(candidate => candidate.AlternativeId != selected)
                .OrderBy(candidate => candidate.AlternativeId,
                    StringComparer.Ordinal)
                .Select(candidate =>
                    new MoneyRevenueAlternativePairwisePreference(
                        selected,
                        candidate.AlternativeId,
                        "authoritative_threshold_then_reserve_aware_strict_pareto"))
                .ToArray()
            : Array.Empty<MoneyRevenueAlternativePairwisePreference>();
        return new MoneyRevenueAlternativeComparison(
            criterionId,
            threshold.Target,
            sourceStateHash,
            expectedLedgerRevision,
            verified.Length,
            true,
            unique
                ? MoneyRevenueAlternativeSelectionDisposition
                    .UniqueAuthoritativePreference
                : MoneyRevenueAlternativeSelectionDisposition.IncomparableFrontier,
            selected,
            unique,
            evaluations,
            preferences);
    }

    private static bool Dominates(
        VerifiedAlternative candidate,
        VerifiedAlternative target)
    {
        if (candidate.TargetSatisfied != target.TargetSatisfied)
            return candidate.TargetSatisfied;
        var noWorse = candidate.ProgressTowardTarget >=
                target.ProgressTowardTarget &&
            candidate.Prefix.FinalTotalDay <= target.Prefix.FinalTotalDay &&
            AcquisitionRouteTargetDateOpportunityCostBuilder
                .OpportunityCostNoWorse(
                    candidate.OpportunityCost,
                    target.OpportunityCost);
        var strictlyBetter = candidate.ProgressTowardTarget >
                target.ProgressTowardTarget ||
            candidate.Prefix.FinalTotalDay < target.Prefix.FinalTotalDay ||
            AcquisitionRouteTargetDateOpportunityCostBuilder
                .OpportunityCostDominates(
                    candidate.OpportunityCost,
                    target.OpportunityCost);
        return noWorse && strictlyBetter;
    }
}
