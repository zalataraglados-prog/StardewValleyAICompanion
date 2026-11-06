using System.Text.Json;
using System.Text.Json.Nodes;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageTeacherCorpusBuilder
{
    public static MarriageTeacherCorpus Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MarriageTeacherCorpusRequest>(requestPath, "marriage_teacher_corpus_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.CorpusId) && !request.FormalProductTrainingAuthorized &&
            request.Sources is { Length: > 0 } && request.Sources.All(source => source is not null &&
                !string.IsNullOrWhiteSpace(source.SourceId) && source.TeacherRowPaths is { Length: > 0 }) &&
            request.Sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Marriage corpus request is invalid or repeats source identities.");
        var paths = request.Sources.SelectMany(source => new[] { source.NativeReceiptPath }.Concat(source.TeacherRowPaths))
            .Select(RequiredFile).ToArray();
        Require(paths.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() == paths.Length,
            "Marriage corpus repeats an original native receipt or Teacher row path.");
        Require(request.Sources.Select(source => CurrentTeacherFrontierSupport.HashFile(source.NativeReceiptPath))
                .Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Marriage corpus repeats original joint receipt content under another path.");
        var knowledge = KnowledgeIndex.Load(RequiredFile(request.KnowledgePath));
        ValidateAuthority(knowledge);
        var runs = request.Sources.Select(source => BuildRun(source, knowledge.Sha256))
            .OrderBy(run => run.OriginSplitKey, StringComparer.Ordinal).ThenBy(run => run.SourceId, StringComparer.Ordinal).ToArray();
        ValidateRuns(runs);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), request.CorpusId, knowledge.Sha256, runs);
    }

    public static MarriageTeacherCorpus Verify(string corpusPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(corpusPath), "marriage_teacher_corpus.v1");
        var rebuilt = Build(Text(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Marriage corpus differs from its complete authority, original decisions or native joint sources.");
        return rebuilt;
    }

    private static MarriageTeacherCorpusRun BuildRun(MarriageTeacherCorpusSource source, string knowledgeHash)
    {
        var nativePath = RequiredFile(source.NativeReceiptPath);
        var nativeHash = CurrentTeacherFrontierSupport.HashFile(nativePath);
        // Reuse the complete verified source within this call; never cache by path across calls.
        var native = MarriageNativeJointReceiptBuilder.VerifySource(nativePath);
        Require(native.Preparations.Length == source.TeacherRowPaths.Length &&
            native.Preparations.All(preference => preference.Intake.KnowledgeSha256 == knowledgeHash),
            "Marriage corpus must retain every preparation from the same complete authority.");
        var raw = JsonSerializer.SerializeToElement(native.Receipt, JsonDefaults.Options);
        var rows = source.TeacherRowPaths.Select(path => (Path: RequiredFile(path),
                Row: MarriageTeacherRowBuilder.VerifyFromJointSource(path, nativePath, native)))
            .OrderBy(entry => entry.Row.Audit.TeacherSupervision!.Sources!.MarriagePreparationIndex).ToArray();
        var origin = rows[0].Row.Context;
        Require(rows.All(entry => entry.Row.Context.SaveId == origin.SaveId && entry.Row.Context.SplitKey == origin.SplitKey &&
            entry.Row.Context.DatasetPartition == origin.DatasetPartition && entry.Row.RunId == native.Request.RunId),
            "Marriage preparation rows cannot split a single joint outcome across decision partitions.");
        var decisions = rows.Select(entry =>
        {
            var row = entry.Row;
            var teacher = row.Audit.TeacherSupervision!;
            var index = teacher.Sources!.MarriagePreparationIndex!.Value;
            var queue = raw.GetProperty("queue_chain")[index];
            return new MarriageTeacherCorpusDecision(index, entry.Path, CurrentTeacherFrontierSupport.HashFile(entry.Path),
                row.TrajectoryId, row.SourceStateHash, Text(queue, "after_state_hash"), row.Selection.CandidateId,
                teacher.CandidateDenominatorSha256, row.Candidates.Length, row.Outcome.QueueId, teacher.SelectedQueueItemIds,
                teacher.PrimitiveOptionIds, Strings(queue, "request_nonces"), teacher.PairwisePreferences.Length,
                native.Preparations[index].UncomparedCandidateIds, native.Preparations[index].CompletePreferenceLabelEligible);
        }).ToArray();
        Require(CurrentTeacherFrontierSupport.HashFile(nativePath) == nativeHash,
            "Marriage native receipt changed while projecting its corpus decisions.");
        return new()
        {
            SourceId = source.SourceId, NativeReceiptPath = nativePath, NativeReceiptSha256 = nativeHash,
            RunId = Text(raw, "run_id"), SaveId = Text(raw, "save_id"), PlayerId = Text(raw, "player_id"),
            OriginSplitKey = origin.SplitKey, DatasetPartition = origin.DatasetPartition,
            BeforeStateHash = Text(raw, "before_state_hash"), AfterStateHash = Text(raw, "after_state_hash"),
            TotalDayBefore = raw.GetProperty("total_day_before").GetInt32(), TotalDayAfter = raw.GetProperty("total_day_after").GetInt32(),
            FinalSnapshotSha256 = Text(raw.GetProperty("source_sha256"), "final_snapshot"),
            QueueChain = raw.GetProperty("queue_chain").Clone(), JointCriterionTransition = raw.GetProperty("joint_criterion_transition").Clone(),
            PreparationDecisions = decisions, NativeOutcomeVerified = raw.GetProperty("native_criterion_outcome_verified").GetBoolean()
        };
    }

    private static string Text(JsonElement value, string name) => CurrentTeacherFrontierSupport.RequiredString(value, name);
    private static string[] Strings(JsonElement value, string name) => value.GetProperty(name).EnumerateArray().Select(row => row.GetString()!).ToArray();
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
