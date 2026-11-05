using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class SkillTeacherCorpusBuilder
{
    public static SkillTeacherCorpus Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<SkillTeacherCorpusRequest>(requestPath, "skill_teacher_corpus_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.CorpusId) && !request.FormalProductTrainingAuthorized &&
            request.Sources is { Length: > 0 } && request.Sources.All(source => source is not null && !string.IsNullOrWhiteSpace(source.SourceId)) &&
            request.Sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Skills corpus request is invalid or repeats source identities.");
        Require(request.Sources.Select(source => RequiredFile(source.TeacherRowPath)).Distinct(
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() == request.Sources.Length,
            "Skills corpus repeats an original Teacher row path.");
        var knowledge = KnowledgeIndex.Load(RequiredFile(request.KnowledgePath));
        ValidateAuthority(knowledge);
        var rows = request.Sources.Select(BuildRow).OrderBy(row => row.CriterionId, StringComparer.Ordinal)
            .ThenBy(row => row.SplitKey, StringComparer.Ordinal).ThenBy(row => row.SourceId, StringComparer.Ordinal).ToArray();
        ValidateRows(rows);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), request.CorpusId, knowledge.Sha256, rows);
    }

    public static SkillTeacherCorpus Verify(string corpusPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(corpusPath), "skill_teacher_corpus.v1");
        var rebuilt = Build(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Skills corpus differs from its original authority, Teacher rows or native sources.");
        return rebuilt;
    }

    private static SkillTeacherCorpusRow BuildRow(SkillTeacherCorpusSource source)
    {
        var path = RequiredFile(source.TeacherRowPath);
        var row = SkillTeacherRowBuilder.Verify(path);
        var teacher = row.Audit.TeacherSupervision!;
        var nativePath = RequiredFile(teacher.Sources!.SkillNativeThresholdReceiptPath!);
        // Row Verify already replays the complete native chain; only read its bound metadata here.
        var native = ReadTyped<JsonElement>(nativePath, "skill_native_threshold_receipt.v1");
        var nativeHash = CurrentTeacherFrontierSupport.HashFile(nativePath);
        var criterion = CurrentTeacherFrontierSupport.RequiredString(native, "criterion_id");
        var transition = teacher.RequirementTransitions.Single();
        Require(nativeHash == teacher.SkillNativeThresholdReceiptSha256 &&
            row.Versions.KnowledgeDictionary == PolicyTrajectoryVersionPins.KnowledgeDictionary &&
            criterion is "player_level_15" or "player_level_25" && transition.RequirementId == criterion &&
            transition.RequirementSetId == "skills" && transition.Verified && teacher.PairwisePreferences.Length > 0 &&
            CurrentTeacherFrontierSupport.RequiredString(native, "run_id") == row.RunId &&
            CurrentTeacherFrontierSupport.RequiredString(native, "save_id") == row.Context.SaveId &&
            CurrentTeacherFrontierSupport.RequiredString(native, "queue_id") == row.Outcome.QueueId &&
            CurrentTeacherFrontierSupport.RequiredString(native, "queue_item_id") == teacher.SelectedQueueItemId &&
            CurrentTeacherFrontierSupport.RequiredString(native, "before_state_hash") == row.SourceStateHash &&
            native.GetProperty("native_criterion_outcome_verified").GetBoolean(),
            "Skills Teacher/native criterion identities do not reconcile.");
        return new(source.SourceId, path, CurrentTeacherFrontierSupport.HashFile(path), nativePath, nativeHash,
            criterion, row.RunId, row.TrajectoryId, row.Context.SaveId,
            CurrentTeacherFrontierSupport.RequiredString(native, "player_id"), row.Context.SplitKey,
            row.Context.DatasetPartition, row.SourceStateHash, row.Selection.CandidateId, row.Outcome.QueueId,
            teacher.SelectedQueueItemId, CurrentTeacherFrontierSupport.RequiredString(native, "request_nonce"),
            teacher.PairwisePreferences.Length, true);
    }

    private static void ValidateAuthority(GoalKnowledge knowledge)
    {
        Require(knowledge.GoalId == GoalMethodTeacherCoverageGoalIds.Authoritative && knowledge.CriterionCount == 19 &&
            knowledge.Criteria.Length == 19 && knowledge.Criteria.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() == 19 &&
            knowledge.Criteria.Sum(row => row.Points) == 21 && knowledge.TargetScore == 21,
            "Skills corpus requires the complete authoritative goal denominator.");
        foreach (var level in new[] { 15, 25 })
        {
            var criterion = knowledge.Criteria.SingleOrDefault(row => row.Id == "player_level_" + level);
            Require(criterion is not null && criterion.Points == 1 && criterion.Operation == "gte" &&
                criterion.Target == level.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                criterion.StatePaths.SequenceEqual(new[] { "player.level" }),
                "Skills corpus authority differs from the versioned native scoring rule.");
        }
    }

    private static void ValidateRows(SkillTeacherCorpusRow[] rows)
    {
        var decisions = new HashSet<(string Save, string State, string Candidate)>();
        var trajectories = new HashSet<(string Run, string Trajectory)>();
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var actions = new HashSet<(string Run, string Queue, string Item)>();
        foreach (var row in rows)
            Require(row.NativeOutcomeVerified && row.StrictPairCount > 0 &&
                row.DatasetPartition == PolicyTrajectoryDatasetBuilder.PartitionFor(row.SplitKey) &&
                decisions.Add((row.SaveId, row.BeforeStateHash, row.SelectedCandidateId)) &&
                trajectories.Add((row.RunId, row.TrajectoryId)) && nonces.Add(row.RequestNonce) &&
                actions.Add((row.RunId, row.QueueId, row.QueueItemId)),
                "Skills corpus repeats a decision, trajectory, native request/action or changes its original partition.");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
