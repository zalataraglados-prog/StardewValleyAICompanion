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
                string.IsNullOrWhiteSpace(source.ExecutionEpisodePath)) ||
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
            rows.Any(row => row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified ||
                !string.IsNullOrWhiteSpace(row.PreferredAlternative) ||
                !string.IsNullOrWhiteSpace(row.RejectedAlternative) ||
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
                    !string.IsNullOrWhiteSpace(
                        value.PreferredAlternative) ||
                    !string.IsNullOrWhiteSpace(
                        value.RejectedAlternative) ||
                    value.TeacherComparisonVerified ||
                    !value.NativeTerminalOutcomeVerified)))
        {
            throw new InvalidDataException(
                "Money-threshold Teacher corpus rows are invalid or duplicate.");
        }
    }

}
