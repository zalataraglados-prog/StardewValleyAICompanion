using System.Globalization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static readonly string[] RequiredPartitions =
    {
        PolicyDatasetPartitions.Train,
        PolicyDatasetPartitions.Validation,
        PolicyDatasetPartitions.Test
    };

    public static MoneyThresholdTeacherCorpus Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money-threshold corpus request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdTeacherCorpusRequest>(
            fullRequestPath,
            "Money-threshold corpus request");
        ValidateRequest(request);
        var rows = request.Sources.Select(BuildRow)
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        ValidateRows(rows);
        return CreateCorpus(request.CorpusId, rows);
    }

    public static MoneyThresholdTeacherCorpus Verify(string corpusPath)
    {
        var fullPath = RequiredFile(
            corpusPath,
            "Money-threshold Teacher corpus");
        var corpus = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdTeacherCorpus>(
            fullPath,
            "Money-threshold Teacher corpus");
        if (corpus.SchemaVersion != MoneyThresholdTeacherCorpusVersions.Corpus ||
            string.IsNullOrWhiteSpace(corpus.CorpusId) ||
            corpus.GoalId != GoalMethodTeacherCoverageGoalIds.Authoritative ||
            corpus.DirectionId != "earn_money" ||
            corpus.MethodId != "grandpa.direct.earn_money" ||
            corpus.FormalProductTrainingAuthorized ||
            corpus.Rows.Length == 0 ||
            corpus.RowCount != corpus.Rows.Length)
        {
            throw new InvalidDataException(
                "Money-threshold Teacher corpus header is invalid.");
        }

        var rebuilt = corpus.Rows.Select(row => BuildRow(
                new MoneyThresholdTeacherCorpusSource(
                    row.SourceId,
                    row.ExecutionEpisodePath)))
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        ValidateRows(rebuilt);
        var expected = CreateCorpus(corpus.CorpusId, rebuilt);
        if (!EqualJson(corpus, expected))
        {
            throw new InvalidDataException(
                "Money-threshold Teacher corpus drifted from source evidence.");
        }
        return corpus;
    }

    private static MoneyThresholdTeacherCorpus CreateCorpus(
        string corpusId,
        MoneyThresholdTeacherEvidenceRow[] rows)
    {
        var coveredCriteria = rows
            .SelectMany(row => row.CrossedThresholds)
            .Select(value => value.CriterionId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var splitComplete = GrandpaEvaluationGoalEvaluator.MoneyThresholds.All(
            threshold => RequiredPartitions.All(partition => rows.Any(row =>
                row.DatasetPartition == partition &&
                row.CrossedThresholds.Any(value =>
                    value.CriterionId == threshold.CriterionId))));
        return new MoneyThresholdTeacherCorpus
        {
            Status = splitComplete
                ? "ready_split_complete_money_threshold_teacher_corpus"
                : "verified_partial_money_threshold_teacher_corpus",
            CorpusId = corpusId,
            RowCount = rows.Length,
            CoveredCriterionCount = coveredCriteria,
            Rows = rows,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyThresholdTeacherEvidenceRow BuildRow(
        MoneyThresholdTeacherCorpusSource source)
    {
        if (string.IsNullOrWhiteSpace(source.SourceId))
        {
            throw new InvalidDataException(
                "Money-threshold corpus source ID is required.");
        }
        var episodePath = RequiredFile(
            source.ExecutionEpisodePath,
            "Money-threshold execution episode");
        var episode = CurrentTeacherFrontierSupport.Read<
            PlanExecutionEpisodeEnvelope>(
            episodePath,
            "Money-threshold execution episode");
        ValidateEpisodeHeader(episode);
        var beforePath = RequiredFile(
            episode.BeforeSnapshotPath,
            "Money-threshold before snapshot");
        var afterPath = RequiredFile(
            episode.AfterSnapshotPath,
            "Money-threshold after snapshot");
        var resultPath = RequiredFile(
            episode.ExecutionResultPath,
            "Money-threshold execution result");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Money-threshold before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Money-threshold after snapshot");
        var result = CurrentTeacherFrontierSupport.Read<TrainingExecutionResult>(
            resultPath,
            "Money-threshold execution result");
        ValidateSnapshot(before, "before");
        ValidateSnapshot(after, "after");
        ValidateEpisodeBinding(episode, before, after, result);

        var progression =
            MoneyRevenueProgressionVerifier.VerifyImmediateShopSale(
                before,
                after);
        var saveId = progression.Before.SaveId;
        var playerId = progression.Before.PlayerId;
        var totalDay = progression.Before.TotalDay;
        var moneyBefore = progression.Before.Money;
        var moneyAfter = progression.After.Money;
        var earnedBefore = progression.Before.TotalMoneyEarned;
        var earnedAfter = progression.After.TotalMoneyEarned;
        var earnedDelta = progression.TotalMoneyEarnedDelta;
        ValidateQueueItem(episode, result, earnedDelta);
        ValidateExecutionResult(result, episode, moneyBefore, moneyAfter);

        var crossed = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .Where(threshold => earnedBefore < threshold.Target &&
                earnedAfter >= threshold.Target)
            .Select(threshold => new MoneyThresholdTeacherCriterion(
                threshold.CriterionId,
                threshold.Target,
                threshold.Points,
                "grandpa.criterion:" + threshold.CriterionId,
                "player.total_money_earned >= " +
                    threshold.Target.ToString(CultureInfo.InvariantCulture),
                "execute_verified_native_sale_now:" +
                    threshold.CriterionId,
                "defer_verified_native_sale:" + threshold.CriterionId,
                true,
                true))
            .ToArray();
        if (crossed.Length == 0)
        {
            throw new InvalidDataException(
                "Money-threshold source did not cross an authoritative threshold.");
        }

        var splitKey = saveId + ":" + playerId + ":" + totalDay;
        var rowId = StableId(new
        {
            source_id = source.SourceId,
            before_state_hash = before.StateHash,
            after_state_hash = after.StateHash,
            execution_episode_sha256 =
                CurrentTeacherFrontierSupport.HashFile(episodePath)
        });
        return new MoneyThresholdTeacherEvidenceRow(
            rowId,
            source.SourceId,
            episodePath,
            CurrentTeacherFrontierSupport.HashFile(episodePath),
            beforePath,
            CurrentTeacherFrontierSupport.HashFile(beforePath),
            before.StateHash,
            afterPath,
            CurrentTeacherFrontierSupport.HashFile(afterPath),
            after.StateHash,
            resultPath,
            CurrentTeacherFrontierSupport.HashFile(resultPath),
            splitKey,
            PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey),
            saveId,
            playerId,
            totalDay,
            moneyBefore,
            moneyAfter,
            earnedBefore,
            earnedAfter,
            earnedDelta,
            crossed,
            "execute_verified_native_sale_now",
            "defer_verified_native_sale",
            true,
            true);
    }

}
