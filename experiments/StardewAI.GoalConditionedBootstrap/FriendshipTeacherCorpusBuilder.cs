using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class FriendshipTeacherCorpusBuilder
{
    public static FriendshipTeacherCorpus Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<FriendshipTeacherCorpusRequest>(requestPath, "friendship_teacher_corpus_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.CorpusId) && !request.FormalProductTrainingAuthorized &&
            request.Sources is { Length: > 0 } && request.Sources.All(source => source is not null && !string.IsNullOrWhiteSpace(source.SourceId)) &&
            request.Sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Friendship corpus request is invalid or repeats source identities.");
        Require(request.Sources.Select(source => RequiredFile(source.TeacherRowPath)).Distinct(
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() == request.Sources.Length,
            "Friendship corpus repeats an original Teacher row path.");
        var knowledge = KnowledgeIndex.Load(RequiredFile(request.KnowledgePath));
        ValidateAuthority(knowledge);
        var rows = request.Sources.Select(BuildRow).OrderBy(row => row.CriterionId, StringComparer.Ordinal)
            .ThenBy(row => row.SplitKey, StringComparer.Ordinal).ThenBy(row => row.SourceId, StringComparer.Ordinal).ToArray();
        ValidateRows(rows);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), request.CorpusId, knowledge.Sha256, rows);
    }

    public static FriendshipTeacherCorpus Verify(string corpusPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(corpusPath), "friendship_teacher_corpus.v1");
        var rebuilt = Build(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Friendship corpus differs from its original authority, Teacher rows or native sources.");
        return rebuilt;
    }

    private static FriendshipTeacherCorpusRow BuildRow(FriendshipTeacherCorpusSource source)
    {
        var path = RequiredFile(source.TeacherRowPath);
        var row = FriendshipTeacherRowBuilder.Verify(path);
        var teacher = row.Audit.TeacherSupervision!;
        var nativePath = RequiredFile(teacher.Sources!.FriendshipNativeThresholdReceiptPath!);
        // Row Verify already replays every native frame; read only its hash-bound metadata here.
        var native = ReadTyped<JsonElement>(nativePath, "friendship_native_threshold_receipt.v1");
        var nativeHash = CurrentTeacherFrontierSupport.HashFile(nativePath);
        var criterion = CurrentTeacherFrontierSupport.RequiredString(native, "criterion_id");
        var transition = teacher.RequirementTransitions.Single();
        string Text(string name) => CurrentTeacherFrontierSupport.RequiredString(native, name);
        string[] Strings(string name) => native.GetProperty(name).EnumerateArray().Select(value => value.GetString()!).ToArray();
        var countBefore = native.GetProperty("qualifying_count_before").GetInt32();
        var countAfter = native.GetProperty("qualifying_count_after").GetInt32();
        Require(nativeHash == teacher.FriendshipNativeThresholdReceiptSha256 &&
            row.Versions.KnowledgeDictionary == PolicyTrajectoryVersionPins.KnowledgeDictionary &&
            criterion is "friendships_5" or "friendships_10" && transition.RequirementId == criterion &&
            transition.RequirementSetId == "friendships" && transition.Verified && teacher.PairwisePreferences.Length > 0 &&
            Text("run_id") == row.RunId && Text("save_id") == row.Context.SaveId && Text("queue_id") == row.Outcome.QueueId &&
            Text("selected_candidate_id") == row.Selection.CandidateId && Text("before_state_hash") == row.SourceStateHash &&
            Strings("queue_item_ids").SequenceEqual(teacher.SelectedQueueItemIds) &&
            Text("comparison_admission") == "independent_pairs" &&
            native.GetProperty("independent_pair_count").GetInt32() == teacher.PairwisePreferences.Length &&
            transition.BeforeValue == countBefore.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            transition.AfterValue == countAfter.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            native.GetProperty("independent_current_comparison_verified").GetBoolean() &&
            native.GetProperty("native_criterion_outcome_verified").GetBoolean(),
            "Friendship Teacher/native criterion identities do not reconcile.");
        return new(source.SourceId, path, CurrentTeacherFrontierSupport.HashFile(path), nativePath, nativeHash,
            criterion, row.RunId, row.TrajectoryId, row.Context.SaveId, Text("player_id"), row.Context.SplitKey,
            row.Context.DatasetPartition, row.SourceStateHash, Text("after_state_hash"), row.Selection.CandidateId,
            teacher.CandidateDenominatorSha256, row.Candidates.Length, row.Outcome.QueueId,
            teacher.SelectedQueueItemIds, teacher.PrimitiveOptionIds, Strings("request_nonces"), countBefore, countAfter,
            teacher.PairwisePreferences.Length, Strings("uncompared_candidate_ids"),
            native.GetProperty("complete_order_verified").GetBoolean(), true);
    }

    private static void ValidateAuthority(GoalKnowledge knowledge)
    {
        Require(knowledge.GoalId == GoalMethodTeacherCoverageGoalIds.Authoritative && knowledge.CriterionCount == 19 &&
            knowledge.Criteria.Length == 19 && knowledge.Criteria.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() == 19 &&
            knowledge.Criteria.Sum(row => row.Points) == 21 && knowledge.TargetScore == 21,
            "Friendship corpus requires the complete authoritative goal denominator.");
        foreach (var count in new[] { 5, 10 })
        {
            var criterion = knowledge.Criteria.SingleOrDefault(row => row.Id == "friendships_" + count);
            Require(criterion is not null && criterion.Points == 1 && criterion.Operation == "count_points_gte_1975" &&
                criterion.Target == count.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                criterion.StatePaths.SequenceEqual(new[] { "npcs.friendships" }),
                "Friendship corpus authority differs from the versioned native scoring rule.");
        }
    }

    private static void ValidateRows(FriendshipTeacherCorpusRow[] rows)
    {
        var decisions = new HashSet<(string Save, string Player, string State, string Candidate)>();
        var trajectories = new HashSet<(string Run, string Trajectory)>();
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var actions = new HashSet<(string Run, string Queue, string Item)>();
        foreach (var row in rows)
            Require(row.NativeOutcomeVerified && row.StrictPairCount > 0 && row.DecisionCandidateCount > 1 &&
                row.CriterionId is "friendships_5" or "friendships_10" &&
                row.QualifyingCountAfter == (row.CriterionId == "friendships_5" ? 5 : 10) &&
                row.QualifyingCountBefore == row.QualifyingCountAfter - 1 &&
                row.DatasetPartition == StardewAI.Core.Training.PolicyTrajectoryDatasetBuilder.PartitionFor(row.SplitKey) &&
                row.QueueItemIds is { Length: > 0 and <= TeacherEvidenceRolloutLimits.MaxQueueItems } &&
                row.PrimitiveOptionIds is not null && row.PrimitiveOptionIds.Length == row.QueueItemIds.Length &&
                row.RequestNonces is not null && row.RequestNonces.Length == row.QueueItemIds.Length &&
                row.QueueItemIds.All(value => !string.IsNullOrWhiteSpace(value)) &&
                row.PrimitiveOptionIds.All(value => !string.IsNullOrWhiteSpace(value)) &&
                row.RequestNonces.All(value => !string.IsNullOrWhiteSpace(value)) &&
                decisions.Add((row.SaveId, row.PlayerId, row.BeforeStateHash, row.SelectedCandidateId)) &&
                trajectories.Add((row.RunId, row.TrajectoryId)) && row.RequestNonces.All(nonces.Add) &&
                row.QueueItemIds.All(item => actions.Add((row.RunId, row.QueueId, item))),
                "Friendship corpus repeats a decision, trajectory, native request/action, borrows a threshold or changes its original partition.");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
