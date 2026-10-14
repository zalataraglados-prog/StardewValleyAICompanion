using System.Globalization;
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
        return Build(request);
    }

    internal static MoneyThresholdTeacherCorpus Build(
        MoneyThresholdTeacherCorpusRequest request)
    {
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
        ValidateHeader(corpus);

        var rebuilt = corpus.Rows.Select(row => BuildRow(
                new MoneyThresholdTeacherCorpusSource(
                    row.SourceId,
                    row.NativeOutcomeKind ==
                        MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode
                            ? row.ExecutionEpisodePath
                            : string.Empty,
                    row.CrossedThresholds
                        .Where(value => value.TeacherComparison is not null)
                        .Select(value =>
                            new MoneyThresholdTeacherComparisonSource(
                                value.CriterionId,
                                value.TeacherComparison!
                                    .AdmissionManifestPath,
                                value.TeacherComparison!
                                    .AdmissionReceiptPath))
                        .ToArray(),
                    row.NativeOutcomeKind ==
                        MoneyThresholdNativeOutcomeKinds.RevenueTrajectory
                            ? row.NativeTrajectoryReceiptPath
                            : string.Empty)))
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

    internal static void ValidateInMemory(
        MoneyThresholdTeacherCorpus corpus)
    {
        ValidateHeader(corpus);
        ValidateRows(corpus.Rows);
    }

    private static void ValidateHeader(
        MoneyThresholdTeacherCorpus corpus)
    {
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
        var comparedCriteria = rows
            .SelectMany(row => row.CrossedThresholds)
            .Where(value => value.TeacherComparison is not null)
            .Select(value => value.CriterionId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var comparisonPairs = rows
            .SelectMany(row => row.CrossedThresholds)
            .Where(value => value.TeacherComparison is not null)
            .Sum(value => value.TeacherComparison!.RejectedAlternatives.Length);
        var nativeSplitComplete = GrandpaEvaluationGoalEvaluator.MoneyThresholds.All(
            threshold => RequiredPartitions.All(partition => rows.Any(row =>
                row.DatasetPartition == partition &&
                row.CrossedThresholds.Any(value =>
                    value.CriterionId == threshold.CriterionId))));
        var teacherSplitComplete = GrandpaEvaluationGoalEvaluator
            .MoneyThresholds.All(threshold => RequiredPartitions.All(
                partition => rows.Any(row =>
                    row.DatasetPartition == partition &&
                    row.CrossedThresholds.Any(value =>
                        value.CriterionId == threshold.CriterionId &&
                        value.TeacherComparison is not null))));
        return new MoneyThresholdTeacherCorpus
        {
            Status = nativeSplitComplete && teacherSplitComplete
                ? "ready_split_complete_money_threshold_teacher_corpus"
                : "verified_partial_money_threshold_teacher_corpus",
            CorpusId = corpusId,
            RowCount = rows.Length,
            CoveredCriterionCount = coveredCriteria,
            TeacherComparedCriterionCount = comparedCriteria,
            TeacherComparisonPairCount = comparisonPairs,
            NativeSplitComplete = nativeSplitComplete,
            TeacherComparisonSplitComplete = teacherSplitComplete,
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
        return BuildRow(
            source.SourceId,
            VerifyNativeOutcome(source),
            source.ComparisonAdmissions);
    }

    internal static MoneyThresholdTeacherEvidenceRow
        BuildNativeTrajectoryRowForTest(
            string sourceId,
            string trajectoryReceiptPath,
            MoneyThresholdNativeTrajectoryReceipt trajectory) => BuildRow(
                sourceId,
                ProjectNativeTrajectory(
                    trajectoryReceiptPath,
                    trajectory),
                Array.Empty<MoneyThresholdTeacherComparisonSource>());

    private static MoneyThresholdTeacherEvidenceRow BuildRow(
        string sourceId,
        MoneyThresholdVerifiedNativeOutcome outcome,
        MoneyThresholdTeacherComparisonSource[]? admissions)
    {
        var before = outcome.SourceCheckpoint;
        var after = outcome.FinalCheckpoint;
        var saveId = before.SaveId;
        var playerId = before.PlayerId;
        var totalDay = before.TotalDay;
        var moneyBefore = before.Money;
        var moneyAfter = after.Money;
        var earnedBefore = before.TotalMoneyEarned;
        var earnedAfter = after.TotalMoneyEarned;
        var earnedDelta = checked(earnedAfter - earnedBefore);
        var comparisonSources = (admissions ??
                Array.Empty<MoneyThresholdTeacherComparisonSource>())
            .ToDictionary(value => value.CriterionId, StringComparer.Ordinal);
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
                comparisonSources.TryGetValue(
                    threshold.CriterionId,
                    out var comparisonSource)
                    ? BuildComparisonEvidence(
                        threshold,
                        outcome,
                        comparisonSource)
                    : null,
                true))
            .ToArray();
        if (crossed.Length == 0)
        {
            throw new InvalidDataException(
                "Money-threshold source did not cross an authoritative threshold.");
        }
        var crossedIds = crossed.Select(value => value.CriterionId)
            .ToHashSet(StringComparer.Ordinal);
        if (comparisonSources.Keys.Any(value => !crossedIds.Contains(value)))
        {
            throw new InvalidDataException(
                "Money-threshold comparison does not belong to a crossed criterion.");
        }

        var splitKey = saveId + ":" + playerId + ":" + totalDay;
        var rowId = StableId(new
        {
            source_id = sourceId,
            before_state_hash = before.StateHash,
            after_state_hash = after.StateHash,
            native_outcome_sha256 = outcome.PrimarySha256,
            comparison_receipt_sha256 = crossed
                .Where(value => value.TeacherComparison is not null)
                .Select(value => value.TeacherComparison!
                    .AdmissionReceiptSha256)
                .Order(StringComparer.Ordinal)
                .ToArray()
        });
        return new MoneyThresholdTeacherEvidenceRow(
            rowId,
            sourceId,
            outcome.ExecutionEpisodePath,
            outcome.ExecutionEpisodeSha256,
            outcome.BeforeSnapshotPath,
            outcome.BeforeSnapshotSha256,
            before.StateHash,
            outcome.AfterSnapshotPath,
            outcome.AfterSnapshotSha256,
            after.StateHash,
            outcome.TerminalEvidencePath,
            outcome.TerminalEvidenceSha256,
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
            crossed.Count(value => value.TeacherComparison is not null),
            true,
            outcome.Kind,
            outcome.PrimaryPath,
            outcome.PrimarySha256,
            outcome.NativeTrajectoryReceiptPath,
            outcome.NativeTrajectoryReceiptSha256,
            outcome.ConcreteAlternativeId,
            outcome.AcquisitionRolloutRequired,
            outcome.TerminalExecutionEvidenceSha256,
            outcome.RevenuePrefix,
            outcome.Artifacts);
    }

}
