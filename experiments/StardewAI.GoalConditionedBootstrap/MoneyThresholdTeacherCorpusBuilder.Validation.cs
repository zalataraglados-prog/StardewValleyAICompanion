using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static void ValidateEpisodeHeader(
        PlanExecutionEpisodeEnvelope episode)
    {
        if (episode.SchemaVersion != "plan_execution_episode.v1" ||
            string.IsNullOrWhiteSpace(episode.EpisodeId) ||
            string.IsNullOrWhiteSpace(episode.RunId) ||
            episode.OptionId != "executor.sell_shop_item" ||
            episode.Status != "applied" ||
            !episode.Success ||
            episode.TrainingRole != TrainingRoles.ExecutorCalibration ||
            episode.BlockReasons.Length != 0 ||
            episode.PrimitiveKind != "sell_shop_item" ||
            episode.PrimitiveVerificationStatus != "verified")
        {
            throw new InvalidDataException(
                "Money-threshold execution episode header is invalid.");
        }
    }

    private static void ValidateEpisodeBinding(
        PlanExecutionEpisodeEnvelope episode,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        TrainingExecutionResult result)
    {
        if (episode.SourceStateHash != before.StateHash ||
            episode.AfterStateHash != after.StateHash ||
            !episode.StateHashChanged ||
            episode.SourceStateHash == episode.AfterStateHash ||
            !episode.AfterSnapshotFresh ||
            episode.BeforeGameTick != before.GameTick ||
            episode.AfterGameTick != after.GameTick ||
            episode.AfterGameTick <= episode.BeforeGameTick ||
            episode.RunId != result.RunId ||
            episode.QueueId != result.QueueId ||
            episode.OptionId != result.OptionId ||
            episode.Status != result.Status ||
            episode.PrimitiveKind != result.PrimitiveKind ||
            episode.PrimitiveVerificationStatus !=
                result.PrimitiveVerificationStatus ||
            !episode.PrimitiveVerificationReasons.SequenceEqual(
                result.PrimitiveVerificationReasons,
                StringComparer.Ordinal) ||
            episode.RequestedEffect != result.RequestedEffect ||
            episode.ObservedEffect != result.ObservedEffect ||
            !EqualJson(episode.ChangedFacts, result.ChangedFacts))
        {
            throw new InvalidDataException(
                "Money-threshold episode does not bind its native artifacts.");
        }
    }

    private static void ValidateExecutionResult(
        TrainingExecutionResult result,
        PlanExecutionEpisodeEnvelope episode,
        int moneyBefore,
        int moneyAfter)
    {
        var moneyChanges = result.ChangedFacts.Where(change =>
                change.Path == "player.money" &&
                change.Before == moneyBefore.ToString(
                    CultureInfo.InvariantCulture) &&
                change.After == moneyAfter.ToString(
                    CultureInfo.InvariantCulture))
            .ToArray();
        var reasons = new[]
        {
            "native_shop_menu_click_applied",
            "money_increased_by_exact_sale_value",
            "inventory_count_decreased_by_exact_stack"
        };
        if (result.SchemaVersion != "training_execution_result.v1" ||
            result.BeforeStateHash != episode.SourceStateHash ||
            result.OptionId != "executor.sell_shop_item" ||
            result.Status != "applied" ||
            !result.FeedbackAvailable ||
            result.BlockReasons.Length != 0 ||
            result.PrimitiveKind != "sell_shop_item" ||
            result.PrimitiveVerificationStatus != "verified" ||
            !reasons.All(reason => result.PrimitiveVerificationReasons.Contains(
                reason,
                StringComparer.Ordinal)) ||
            moneyChanges.Length != 1)
        {
            throw new InvalidDataException(
                "Money-threshold result is not one verified native shop sale.");
        }
    }

    private static void ValidateQueueItem(
        PlanExecutionEpisodeEnvelope episode,
        TrainingExecutionResult result,
        uint earnedDelta)
    {
        if (!episode.EffectiveQueueItem.HasValue ||
            episode.EffectiveQueueItem.Value.ValueKind !=
                JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Money-threshold episode has no effective queue item.");
        }
        var item = episode.EffectiveQueueItem.Value.Deserialize<
            ActionQueueItem>(JsonDefaults.Options) ??
            throw new InvalidDataException(
                "Money-threshold effective queue item is invalid.");
        var parameters = item.NormalizedCommand.Parameters
            .GroupBy(value => value.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        if (!TrySinglePositiveInt(
                parameters,
                "quantity",
                out var quantity) ||
            !TrySinglePositiveInt(
                parameters,
                "expected_unit_price",
                out var unitPrice))
        {
            throw new InvalidDataException(
                "Money-threshold sale command has no exact positive quantity and unit price.");
        }
        var expectedDelta = checked((uint)quantity * (uint)unitPrice);
        if (item.QueueItemId != result.QueueItemId ||
            item.OptionId != "executor.sell_shop_item" ||
            item.Status != "pending" ||
            item.BlockingReasons.Length != 0 ||
            item.NormalizedCommand.OptionId != item.OptionId ||
            item.NormalizedCommand.StateHash != episode.SourceStateHash ||
            !item.NormalizedCommand.Steps.Any(step =>
                step.StepType == "sell_shop_item") ||
            expectedDelta != earnedDelta)
        {
            throw new InvalidDataException(
                "Money-threshold episode does not bind the exact compiled sale.");
        }
    }

    private static bool TrySinglePositiveInt(
        IReadOnlyDictionary<string, string[]> parameters,
        string name,
        out int value)
    {
        value = 0;
        return parameters.TryGetValue(name, out var values) &&
            values.Length == 1 &&
            int.TryParse(
                values[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value) &&
            value > 0;
    }

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
            rows.Any(row => !row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified ||
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
                    value.PreferredAlternative !=
                        "execute_verified_native_sale_now:" +
                        expected.CriterionId ||
                    value.RejectedAlternative !=
                        "defer_verified_native_sale:" +
                        expected.CriterionId ||
                    !value.TeacherComparisonVerified ||
                    !value.NativeTerminalOutcomeVerified)))
        {
            throw new InvalidDataException(
                "Money-threshold Teacher corpus rows are invalid or duplicate.");
        }
    }

    private static void ValidateSnapshot(
        SnapshotEnvelope snapshot,
        string label)
    {
        if (snapshot.SchemaVersion != "snapshot.v1" ||
            snapshot.GameVersion != "1.6.15" ||
            string.IsNullOrWhiteSpace(snapshot.BridgeVersion) ||
            string.IsNullOrWhiteSpace(snapshot.StateHash) ||
            snapshot.StateHash != SnapshotHash.ComputeStateHash(snapshot.State))
        {
            throw new InvalidDataException(
                "Money-threshold " + label + " snapshot is invalid.");
        }
    }
}
