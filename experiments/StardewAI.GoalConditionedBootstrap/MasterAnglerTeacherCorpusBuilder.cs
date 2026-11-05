using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerTeacherCorpusBuilder
{
    public static MasterAnglerTeacherCorpus Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MasterAnglerTeacherCorpusRequest>(requestPath, "master_angler_teacher_corpus_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.CorpusId) && !request.FormalProductTrainingAuthorized &&
            request.Sources is { Length: > 0 } && request.Sources.All(source => source is not null && !string.IsNullOrWhiteSpace(source.SourceId)) &&
            request.Sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Master Angler corpus request is invalid or repeats source identities.");
        var rows = request.Sources.Select(source =>
        {
            var path = RequiredFile(source.JoinReceiptPath);
            var join = MasterAnglerTeacherNativeJoinBuilder.Verify(path);
            var joinRequest = ReadTyped<MasterAnglerTeacherNativeJoinRequest>(RequiredFile(join.RequestPath), "master_angler_teacher_native_join_request.v1");
            var row = ReadTyped<PolicyDecisionTrajectoryEnvelope>(RequiredFile(joinRequest.TeacherRowPath), PolicyTrajectoryVersionPins.TrajectorySchema);
            var sources = row.Audit.TeacherSupervision!.Sources!;
            var inventory = ReadTyped<AuthoritativeRequirementInventoryReport>(RequiredFile(sources.RequirementInventoryPath), "authoritative_goal_requirement_inventory.v1");
            var set = CurrentTeacherFrontierSupport.SingleSet(inventory.RequirementSets, entry => entry.RequirementSetId, "master_angler", "Master Angler corpus");
            var terminal = IsCriterionTerminal(ReadTyped<SnapshotEnvelope>(RequiredFile(sources.BeforeSnapshotPath), "snapshot.v1"),
                ReadTyped<SnapshotEnvelope>(RequiredFile(sources.AfterSnapshotPath), "snapshot.v1"), set);
            return new MasterAnglerTeacherCorpusRow(source.SourceId, path, CurrentTeacherFrontierSupport.HashFile(path), join, terminal);
        }).OrderBy(row => row.Evidence.SplitKey, StringComparer.Ordinal).ThenBy(row => row.SourceId, StringComparer.Ordinal).ToArray();
        ValidateRows(rows);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), request.CorpusId, rows);
    }

    public static MasterAnglerTeacherCorpus Verify(string corpusPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(corpusPath), "master_angler_teacher_corpus.v1");
        var rebuilt = Build(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Master Angler corpus differs from original joins or criterion terminal states.");
        return rebuilt;
    }

    private static void ValidateRows(MasterAnglerTeacherCorpusRow[] rows)
    {
        var decisions = new HashSet<string>(StringComparer.Ordinal);
        var trajectories = new HashSet<string>(StringComparer.Ordinal);
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var actions = new HashSet<string>(StringComparer.Ordinal);
        var authority = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var e = row.Evidence;
            Require(e.DatasetPartition == PolicyTrajectoryDatasetBuilder.PartitionFor(e.SplitKey) &&
                decisions.Add(e.RunId + ":" + e.BeforeStateHash + ":" + e.SelectedCandidateId) &&
                trajectories.Add(e.RunId + ":" + e.TrajectoryId) && nonces.Add(e.RequestNonce) &&
                actions.Add(e.RunId + ":" + e.QueueId + ":" + e.QueueItemId),
                "Master Angler corpus reuses a decision, trajectory, native request/action, or changes its original partition.");
            authority.Add(e.SourceSha256["native.requirement_inventory"] + ":" + e.SourceSha256["native.acquisition_lowering"]);
        }
        Require(authority.Count == 1, "Master Angler corpus mixes different authority/lowering versions.");
    }

    private static bool IsCriterionTerminal(SnapshotEnvelope before, SnapshotEnvelope after, GoalRequirementSet set)
    {
        var beforeProgress = CurrentMasterAnglerTeacherFrontierBuilder.ReadExactProgress(before, set);
        var afterProgress = CurrentMasterAnglerTeacherFrontierBuilder.ReadExactProgress(after, set);
        var beforeAchievements = ReadAchievements(before);
        var afterAchievements = ReadAchievements(after);
        return beforeProgress.Values.Count(caught => !caught) == 1 && afterProgress.Values.All(caught => caught) &&
            !beforeAchievements.Contains(26) && afterAchievements.Contains(26);
    }

    private static HashSet<int> ReadAchievements(SnapshotEnvelope snapshot)
    {
        var value = Value(snapshot, "world_progress", "achievements");
        Require(value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(row => row.ValueKind == JsonValueKind.Number && row.TryGetInt32(out var id) && id >= 0),
            "Master Angler native achievements are missing or mistyped.");
        var ids = value.EnumerateArray().Select(row => row.GetInt32()).ToArray();
        Require(ids.Distinct().Count() == ids.Length, "Master Angler native achievements repeat identities.");
        return ids.ToHashSet();
    }
}
