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
                string.IsNullOrWhiteSpace(source.ExecutionEpisodePath) ==
                    string.IsNullOrWhiteSpace(
                        source.NativeTrajectoryReceiptPath) ||
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
                !ValidNativeOutcome(row) ||
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

    private static bool ValidNativeOutcome(
        MoneyThresholdTeacherEvidenceRow row)
    {
        if (string.IsNullOrWhiteSpace(row.NativeOutcomePrimaryPath) ||
            row.NativeOutcomePrimarySha256.Length != 64 ||
            row.RevenuePrefix is null ||
            row.NativeOutcomeArtifacts is not { Length: > 0 } artifacts ||
            artifacts.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() != artifacts.Length ||
            artifacts.Any(value =>
                string.IsNullOrWhiteSpace(value.Role) ||
                string.IsNullOrWhiteSpace(value.Path) ||
                value.Sha256.Length != 64) ||
            row.RevenuePrefix.SaveId != row.SaveId ||
            row.RevenuePrefix.PlayerId != row.PlayerId ||
            row.RevenuePrefix.InitialTotalMoneyEarned !=
                row.TotalMoneyEarnedBefore ||
            row.RevenuePrefix.FinalTotalMoneyEarned !=
                row.TotalMoneyEarnedAfter ||
            row.RevenuePrefix.TotalRevenue < 0 ||
            (uint)row.RevenuePrefix.TotalRevenue != row.EarnedDelta)
        {
            return false;
        }

        return row.NativeOutcomeKind switch
        {
            MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode =>
                !string.IsNullOrWhiteSpace(row.ExecutionEpisodePath) &&
                row.ExecutionEpisodeSha256.Length == 64 &&
                string.IsNullOrWhiteSpace(
                    row.NativeTrajectoryReceiptPath) &&
                string.IsNullOrWhiteSpace(
                    row.NativeTrajectoryReceiptSha256) &&
                string.IsNullOrWhiteSpace(row.ConcreteAlternativeId) &&
                string.IsNullOrWhiteSpace(
                    row.TerminalExecutionEvidenceSha256) &&
                !row.AcquisitionRolloutRequired,
            MoneyThresholdNativeOutcomeKinds.RevenueTrajectory =>
                string.IsNullOrWhiteSpace(row.ExecutionEpisodePath) &&
                string.IsNullOrWhiteSpace(row.ExecutionEpisodeSha256) &&
                !string.IsNullOrWhiteSpace(
                    row.NativeTrajectoryReceiptPath) &&
                row.NativeTrajectoryReceiptSha256.Length == 64 &&
                !string.IsNullOrWhiteSpace(row.ConcreteAlternativeId) &&
                row.TerminalExecutionEvidenceSha256.Length == 64,
            _ => false
        };
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
            (comparison.SelectedTerminalEpisodeSha256.Length == 0 ||
             comparison.SelectedTerminalEpisodeSha256.Length == 64) &&
            comparison.SelectedNativeOutcomeSha256.Length == 64 &&
            comparison.SelectionPolicy ==
                "authoritative_threshold_then_reserve_aware_strict_pareto.v1";
    }

}
