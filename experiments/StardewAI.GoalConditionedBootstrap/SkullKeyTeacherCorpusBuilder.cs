using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class SkullKeyTeacherCorpusBuilder
{
    private const int BottomOfOrdinaryMines = 120;
    private const int StageOneDeadlineExclusive = 224;

    public static SkullKeyTeacherCorpus Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Skull Key corpus request");
        var request = CurrentTeacherFrontierSupport.Read<
            SkullKeyTeacherCorpusRequest>(
            fullRequestPath,
            "Skull Key corpus request");
        ValidateRequest(request);
        var rows = request.Sources
            .Select(BuildRow)
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        ValidateRows(rows);
        var partitions = rows.Select(row => row.DatasetPartition)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new SkullKeyTeacherCorpus
        {
            Status = RequiredPartitions.All(partitions.Contains)
                ? "ready_split_complete_skull_key_teacher_corpus"
                : "verified_partial_skull_key_teacher_corpus",
            CorpusId = request.CorpusId,
            RowCount = rows.Length,
            PartitionCount = partitions.Length,
            SplitComplete = RequiredPartitions.All(partitions.Contains),
            Rows = rows,
            FormalProductTrainingAuthorized = false
        };
    }

    public static SkullKeyTeacherCorpus Verify(string corpusPath)
    {
        var fullPath = RequiredFile(corpusPath, "Skull Key Teacher corpus");
        var corpus = CurrentTeacherFrontierSupport.Read<SkullKeyTeacherCorpus>(
            fullPath,
            "Skull Key Teacher corpus");
        if (corpus.SchemaVersion != SkullKeyTeacherCorpusVersions.Corpus ||
            string.IsNullOrWhiteSpace(corpus.CorpusId) ||
            corpus.GoalId != GoalMethodTeacherCoverageGoalIds.Authoritative ||
            corpus.DirectionId != "obtain_skull_key" ||
            corpus.MethodId != "grandpa.direct.obtain_skull_key" ||
            corpus.FormalProductTrainingAuthorized ||
            corpus.Rows.Length == 0 ||
            corpus.RowCount != corpus.Rows.Length)
        {
            throw new InvalidDataException(
                "Skull Key Teacher corpus header is invalid.");
        }

        var rebuilt = corpus.Rows.Select(row => BuildRow(
                new SkullKeyTeacherCorpusSource(
                    row.SourceId,
                    row.ExecutionEpisodePath)))
            .OrderBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
        ValidateRows(rebuilt);
        if (!EqualJson(corpus.Rows, rebuilt))
        {
            throw new InvalidDataException(
                "Skull Key Teacher corpus rows drifted from source evidence.");
        }
        var partitions = rebuilt.Select(row => row.DatasetPartition)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var splitComplete = RequiredPartitions.All(partitions.Contains);
        var expectedStatus = splitComplete
            ? "ready_split_complete_skull_key_teacher_corpus"
            : "verified_partial_skull_key_teacher_corpus";
        if (corpus.PartitionCount != partitions.Length ||
            corpus.SplitComplete != splitComplete ||
            corpus.Status != expectedStatus)
        {
            throw new InvalidDataException(
                "Skull Key Teacher corpus partition summary drifted.");
        }
        return corpus;
    }

    private static SkullKeyTeacherEvidenceRow BuildRow(
        SkullKeyTeacherCorpusSource source)
    {
        if (string.IsNullOrWhiteSpace(source.SourceId))
            throw new InvalidDataException("Skull Key source ID is required.");
        var episodePath = RequiredFile(
            source.ExecutionEpisodePath,
            "Skull Key execution episode");
        var episode = CurrentTeacherFrontierSupport.Read<
            PlanExecutionEpisodeEnvelope>(
            episodePath,
            "Skull Key execution episode");
        ValidateEpisodeHeader(episode);
        var beforePath = RequiredFile(
            episode.BeforeSnapshotPath,
            "Skull Key before snapshot");
        var afterPath = RequiredFile(
            episode.AfterSnapshotPath,
            "Skull Key after snapshot");
        var resultPath = RequiredFile(
            episode.ExecutionResultPath,
            "Skull Key execution result");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Skull Key before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Skull Key after snapshot");
        var result = CurrentTeacherFrontierSupport.Read<TrainingExecutionResult>(
            resultPath,
            "Skull Key execution result");
        ValidateSnapshot(before, "before");
        ValidateSnapshot(after, "after");
        ValidateEpisodeBinding(episode, before, after, result);

        var saveId = RequiredIdentity(before.SaveId, "save_id");
        var playerId = RequiredIdentity(before.PlayerId, "player_id");
        if (saveId != RequiredIdentity(after.SaveId, "after save_id") ||
            playerId != RequiredIdentity(after.PlayerId, "after player_id") ||
            before.GameVersion != after.GameVersion ||
            before.BridgeVersion != after.BridgeVersion)
        {
            throw new InvalidDataException(
                "Skull Key snapshot identity or version changed during one action.");
        }
        var totalDay = ReadSnapshotTotalDay(before);
        if (totalDay < 0 || totalDay >= StageOneDeadlineExclusive ||
            ReadSnapshotTotalDay(after) != totalDay)
        {
            throw new InvalidDataException(
                "Skull Key terminal interaction is outside one Stage-1 day.");
        }
        if (ReadEnvelopeBool(before, "player", "has_skull_key") ||
            !ReadEnvelopeBool(after, "player", "has_skull_key"))
        {
            throw new InvalidDataException(
                "Skull Key source does not contain a false-to-true native key transition.");
        }

        ValidateOrdinaryFloor120(before, "before");
        ValidateOrdinaryFloor120(after, "after");
        var beforeObjectives = ReadEnvelopeObject(
            before,
            "mining",
            "floor_objectives");
        var afterObjectives = ReadEnvelopeObject(
            after,
            "mining",
            "floor_objectives");
        if (!RequiredBool(beforeObjectives, "skull_key_applicable") ||
            RequiredBool(beforeObjectives, "skull_key_acquired") ||
            !RequiredBool(afterObjectives, "skull_key_applicable") ||
            !RequiredBool(afterObjectives, "skull_key_acquired"))
        {
            throw new InvalidDataException(
                "Skull Key floor objective flags do not bind one floor-120 claim.");
        }
        var beforeChests = RequiredArray(
            beforeObjectives,
            "skull_key_reward_chests").ToArray();
        var afterChests = RequiredArray(
            afterObjectives,
            "skull_key_reward_chests").ToArray();
        if (beforeChests.Length != 1 || afterChests.Length != 0)
        {
            throw new InvalidDataException(
                "Skull Key source does not remove exactly one terminal reward chest.");
        }
        var chest = beforeChests[0];
        ValidateRewardChest(chest);
        var chestX = RequiredInt(chest, "tile_x");
        var chestY = RequiredInt(chest, "tile_y");
        ValidateQueueItem(episode, result, chestX, chestY);
        ValidateExecutionResult(episode, result);

        var splitKey = saveId + ":" + playerId + ":" + totalDay;
        var partition = PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey);
        var rowId = StableId(new
        {
            source_id = source.SourceId,
            before_state_hash = before.StateHash,
            after_state_hash = after.StateHash,
            execution_episode_sha256 =
                CurrentTeacherFrontierSupport.HashFile(episodePath)
        });
        return new SkullKeyTeacherEvidenceRow(
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
            partition,
            saveId,
            playerId,
            totalDay,
            chestX,
            chestY,
            "claim_native_skull_key_reward_now",
            "defer_native_skull_key_reward",
            true,
            true);
    }

    private static void ValidateRequest(SkullKeyTeacherCorpusRequest request)
    {
        if (request.SchemaVersion != SkullKeyTeacherCorpusVersions.Request ||
            string.IsNullOrWhiteSpace(request.CorpusId) ||
            request.FormalProductTrainingAuthorized ||
            request.Sources.Length == 0 ||
            request.Sources.Select(source => source.SourceId)
                .Distinct(StringComparer.Ordinal).Count() !=
                request.Sources.Length)
        {
            throw new InvalidDataException(
                "Skull Key Teacher corpus request is invalid.");
        }
    }

    private static void ValidateRows(SkullKeyTeacherEvidenceRow[] rows)
    {
        if (rows.Length == 0 ||
            rows.Select(row => row.RowId)
                .Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Select(row => row.SplitKey)
                .Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Any(row => !row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified ||
                row.DatasetPartition !=
                    PolicyTrajectoryDatasetBuilder.PartitionFor(row.SplitKey)))
        {
            throw new InvalidDataException(
                "Skull Key Teacher corpus rows are invalid or duplicate.");
        }
    }
}
