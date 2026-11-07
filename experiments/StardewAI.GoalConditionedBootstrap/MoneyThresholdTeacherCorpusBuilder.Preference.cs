using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static void ValidatePreferenceHeader(GrandpaMoneyThreshold threshold, MoneyRevenueAlternativeComparison comparison,
        string sourceStateHash, int ledgerRevision, int denominatorCount)
    {
        if (comparison.CriterionId != threshold.CriterionId || comparison.TargetTotalMoneyEarned != threshold.Target ||
            comparison.SourceStateHash != sourceStateHash || comparison.ExpectedLedgerRevision != ledgerRevision ||
            !comparison.CandidateDenominatorComplete || comparison.CandidateDenominatorCount != denominatorCount ||
            comparison.SelectionDisposition != MoneyRevenueAlternativeSelectionDisposition.UniqueAuthoritativePreference ||
            !comparison.TeacherPreferenceLabelEligible || comparison.FormalTrainingAuthorized || comparison.UsesLearnerScore ||
            string.IsNullOrWhiteSpace(comparison.SelectedAlternativeId))
            throw new InvalidDataException("Money-threshold comparison is not an authoritative criterion-local preference.");
    }

    private static MoneyThresholdComparisonJoin CompletePreference(MoneyRevenueAlternativeComparison comparison, string[] concreteIds)
    {
        var rejected = concreteIds.Where(value => value != comparison.SelectedAlternativeId).Order(StringComparer.Ordinal).ToArray();
        var preferences = comparison.PairwisePreferences;
        if (rejected.Length == 0 || preferences.Length != rejected.Length ||
            preferences.Any(value => value.PreferredAlternativeId != comparison.SelectedAlternativeId ||
                value.Criterion != "authoritative_threshold_then_reserve_aware_strict_pareto") ||
            !preferences.Select(value => value.AlternativeId).Order(StringComparer.Ordinal).SequenceEqual(rejected, StringComparer.Ordinal))
            throw new InvalidDataException("Money-threshold comparison pairwise denominator is incomplete.");
        var selectedEvaluation = comparison.Evaluations.SingleOrDefault(value => value.AlternativeId == comparison.SelectedAlternativeId);
        if (selectedEvaluation is null || !selectedEvaluation.TargetSatisfied || !selectedEvaluation.OnParetoFrontier)
            throw new InvalidDataException("Money-threshold selected alternative does not satisfy its target.");
        return new(comparison.SelectedAlternativeId, rejected);
    }
}
