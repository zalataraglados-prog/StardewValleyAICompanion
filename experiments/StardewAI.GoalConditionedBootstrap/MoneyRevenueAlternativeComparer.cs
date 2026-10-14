using System.Text.Json;
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
        return CompareCore(
            criterionId,
            threshold.Target,
            sourceStateHash,
            expectedLedgerRevision,
            verified);
    }

    internal static MoneyRevenueAlternativeComparison CompareVerifiedArtifacts(
        string criterionId,
        string sourceStateHash,
        int expectedLedgerRevision,
        IReadOnlyList<string> expectedAlternativeIds,
        IReadOnlyList<MoneyRevenueVerifiedArtifactAlternative> alternatives)
    {
        var threshold = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SingleOrDefault(value => value.CriterionId == criterionId) ??
            throw new InvalidDataException(
                "Money revenue comparison criterion is not authoritative.");
        ValidateArtifactDenominator(
            sourceStateHash,
            expectedLedgerRevision,
            expectedAlternativeIds,
            alternatives);
        var verified = alternatives.Select(candidate =>
            {
                var prefix = MoneyRevenueRecurrenceVerifier.VerifyPrefix(
                    candidate.RevenuePrefix.Transitions);
                Require(JsonSerializer.Serialize(prefix, JsonDefaults.Options) ==
                        JsonSerializer.Serialize(
                            candidate.RevenuePrefix,
                            JsonDefaults.Options),
                    "Money revenue artifact prefix drifted after replay.");
                ValidateOpportunityCost(candidate.OpportunityCost);
                var reservationIds = candidate.ReservationIds
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                Require(reservationIds.All(id =>
                            !string.IsNullOrWhiteSpace(id)) &&
                        reservationIds.Distinct(StringComparer.Ordinal)
                            .Count() == reservationIds.Length &&
                        (candidate.ReservationNotRequired
                            ? reservationIds.Length == 0
                            : reservationIds.Length > 0),
                    "Money revenue artifact reservation identity is invalid.");
                var before = Math.Min(
                    prefix.InitialTotalMoneyEarned,
                    threshold.Target);
                var after = Math.Min(
                    prefix.FinalTotalMoneyEarned,
                    threshold.Target);
                return new VerifiedAlternative(
                    candidate.AlternativeId,
                    prefix,
                    candidate.OpportunityCost,
                    candidate.ReservationNotRequired,
                    reservationIds,
                    prefix.FinalTotalMoneyEarned >= threshold.Target,
                    checked(after - before));
            })
            .OrderBy(candidate => candidate.AlternativeId,
                StringComparer.Ordinal)
            .ToArray();
        return CompareCore(
            criterionId,
            threshold.Target,
            sourceStateHash,
            expectedLedgerRevision,
            verified);
    }

    private static MoneyRevenueAlternativeComparison CompareCore(
        string criterionId,
        uint target,
        string sourceStateHash,
        int expectedLedgerRevision,
        VerifiedAlternative[] verified)
    {
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
            target,
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

    private static void ValidateArtifactDenominator(
        string sourceStateHash,
        int expectedLedgerRevision,
        IReadOnlyList<string> expectedAlternativeIds,
        IReadOnlyList<MoneyRevenueVerifiedArtifactAlternative> alternatives)
    {
        if (string.IsNullOrWhiteSpace(sourceStateHash) ||
            expectedLedgerRevision < 0 ||
            expectedAlternativeIds is null ||
            alternatives is null)
        {
            throw new InvalidDataException(
                "Money revenue artifact comparison identity is invalid.");
        }
        var expected = expectedAlternativeIds.ToArray();
        var actual = alternatives.ToArray();
        Require(expected.Length >= 2 &&
                expected.All(id => !string.IsNullOrWhiteSpace(id)) &&
                expected.Distinct(StringComparer.Ordinal).Count() ==
                    expected.Length &&
                actual.Length == expected.Length &&
                actual.All(candidate => candidate is not null) &&
                actual.Select(candidate => candidate.AlternativeId)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(
                        expected.Order(StringComparer.Ordinal),
                        StringComparer.Ordinal),
            "Money revenue artifact candidate denominator is incomplete.");
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

internal sealed record MoneyRevenueVerifiedArtifactAlternative(
    string AlternativeId,
    MoneyRevenueRecurrencePrefixEvidence RevenuePrefix,
    AcquisitionOpportunityCostVector OpportunityCost,
    bool ReservationNotRequired,
    string[] ReservationIds);
