using System.Globalization;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static void ValidateRequest(
        MoneyThresholdTeacherCorpusRequest request)
    {
        if (request.SchemaVersion != MoneyThresholdTeacherCorpusVersions.Request ||
            string.IsNullOrWhiteSpace(request.CorpusId) ||
            request.FormalProductTrainingAuthorized ||
            request.Sources.Length == 0 ||
            request.Sources.Any(source =>
                string.IsNullOrWhiteSpace(source.SourceId) ||
                string.IsNullOrWhiteSpace(source.ExecutionEpisodePath) ||
                (source.ComparisonAdmissions ??
                    Array.Empty<MoneyThresholdTeacherComparisonSource>())
                    .Any(value =>
                        string.IsNullOrWhiteSpace(value.CriterionId) ||
                        string.IsNullOrWhiteSpace(
                            value.AdmissionManifestPath) ||
                        string.IsNullOrWhiteSpace(
                            value.AdmissionReceiptPath)) ||
                (source.ComparisonAdmissions ??
                    Array.Empty<MoneyThresholdTeacherComparisonSource>())
                    .Select(value => value.CriterionId)
                    .Distinct(StringComparer.Ordinal).Count() !=
                (source.ComparisonAdmissions ??
                    Array.Empty<MoneyThresholdTeacherComparisonSource>())
                    .Length) ||
            request.Sources.Select(source => source.SourceId)
                .Distinct(StringComparer.Ordinal).Count() !=
                request.Sources.Length)
        {
            throw new InvalidDataException(
                "Money-threshold Teacher corpus request is invalid.");
        }
    }

    private static void ValidateRows(MoneyThresholdTeacherEvidenceRow[] rows)
    {
        var authority = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .ToDictionary(value => value.CriterionId, StringComparer.Ordinal);
        if (rows.Length == 0 ||
            rows.Select(row => row.RowId)
                .Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Select(row => row.SplitKey)
                .Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Any(row => !row.NativeTerminalOutcomeVerified ||
                row.TeacherComparisonCount != row.CrossedThresholds.Count(
                    value => value.TeacherComparison is not null) ||
                row.DatasetPartition !=
                    PolicyTrajectoryDatasetBuilder.PartitionFor(row.SplitKey) ||
                row.CrossedThresholds.Length == 0 ||
                row.CrossedThresholds.Any(value =>
                    !authority.TryGetValue(value.CriterionId, out var expected) ||
                    value.Target != expected.Target ||
                    value.Points != expected.Points ||
                    value.TeacherGoalContextId !=
                        "grandpa.criterion:" + expected.CriterionId ||
                    value.TerminalAssertion !=
                        "player.total_money_earned >= " +
                        expected.Target.ToString(CultureInfo.InvariantCulture) ||
                    !ValidComparison(value, expected.CriterionId) ||
                    !value.NativeTerminalOutcomeVerified)))
        {
            throw new InvalidDataException(
                "Money-threshold Teacher corpus rows are invalid or duplicate.");
        }
    }

    private static bool ValidComparison(
        MoneyThresholdTeacherCriterion value,
        string criterionId)
    {
        var comparison = value.TeacherComparison;
        return comparison is null ||
            comparison.CriterionId == criterionId &&
            !string.IsNullOrWhiteSpace(comparison.AdmissionManifestPath) &&
            comparison.AdmissionManifestSha256.Length == 64 &&
            !string.IsNullOrWhiteSpace(comparison.AdmissionReceiptPath) &&
            comparison.AdmissionReceiptSha256.Length == 64 &&
            !string.IsNullOrWhiteSpace(
                comparison.ComparisonSourceStateHash) &&
            comparison.CandidateDenominatorCount >= 2 &&
            !string.IsNullOrWhiteSpace(comparison.PreferredAlternative) &&
            comparison.RejectedAlternatives is { } rejected &&
            rejected.Length ==
                comparison.CandidateDenominatorCount - 1 &&
            rejected.All(candidate =>
                !string.IsNullOrWhiteSpace(candidate) &&
                candidate != comparison.PreferredAlternative) &&
            rejected
                .Distinct(StringComparer.Ordinal).Count() ==
                rejected.Length &&
            comparison.SelectedTerminalEpisodeSha256.Length == 64 &&
            comparison.SelectionPolicy ==
                "authoritative_threshold_then_reserve_aware_strict_pareto.v1";
    }

}
