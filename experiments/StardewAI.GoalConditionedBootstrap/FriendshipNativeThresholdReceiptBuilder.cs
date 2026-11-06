using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record FriendshipNativeThresholdRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("preference_path")] string PreferencePath,
    [property: JsonPropertyName("action_queue_path")] string ActionQueuePath,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("target_qualifying_count")] int TargetQualifyingCount);

public static class FriendshipNativeThresholdReceiptBuilder
{
    public static object Build(string requestPath) => BuildSource(requestPath).Receipt;

    private static FriendshipNativeThresholdSource BuildSource(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<FriendshipNativeThresholdRequest>(requestPath, "friendship_native_threshold_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.RunId) && request.TargetQualifyingCount is 5 or 10,
            "Friendship native request needs a run and exact five/ten count threshold.");
        var dispatch = FriendshipTeacherDispatchBuilder.CompileSource(RequiredFile(request.PreferencePath),
            FriendshipTeacherAdmissionMode.IndependentPairs);
        var preference = dispatch.Preference;
        var selected = preference.Candidates.Single(row => row.CandidateId == preference.SelectedCandidate!.CandidateId);
        Require(selected.Kind == "social_talk_current" && selected.OptionId == "social.talk_npc",
            "Friendship threshold source requires an independently selected current talk, not an uncompleted route or gift.");
        var source = NativeExecutionActionSourceReader.ReadQueue(request.ActionQueuePath, preference.SnapshotPath,
            request.ExecutionReceiptPath, request.AfterSnapshotPath, request.RunId, selected.CandidateId);
        Require(TotalDay(source.Before) is >= 0 and < 224 && TotalDay(source.Before) == TotalDay(source.After) &&
            source.Queue.GoalId == dispatch.Queue.GoalId && source.Queue.Items.Length == dispatch.Queue.Items.Length &&
            source.Queue.Items.Select((item, index) => ExecutionReceiptValidationSupport.EffectiveQueueItemSemanticallyMatches(
                JsonSerializer.SerializeToElement(item, JsonDefaults.Options), dispatch.Queue.Items[index])).All(matches => matches) &&
            source.Queue.CandidateAudit.Count(row => row.Decision == "accepted" && row.CandidateId == selected.CandidateId) == 1 &&
            source.Actions[^1].Result.OptionId == "executor.social_interact" &&
            source.Actions[^1].Request.SocialNpcName == selected.NpcName && source.Actions[^1].Request.SocialActionKind == "talk" &&
            source.Actions.Take(source.Actions.Length - 1).All(frame => frame.Result.OptionId == "executor.move_to_tile"),
            "Friendship native queue is not the exact original independent current-talk compilation.");
        var projector = new CurrentFriendshipDeficitBuilder();
        var before = projector.Build(source.Before);
        var after = projector.Build(source.After);
        Require(before.Status == "ready_current_state_only" && after.Status == "ready_current_state_only" &&
            before.QualifyingVillagerCount == request.TargetQualifyingCount - 1 &&
            after.QualifyingVillagerCount == request.TargetQualifyingCount && selected.ExpectedNewQualifiers == 1,
            "Native friendship qualifying count did not cross the exact declared boundary.");
        var previous = before;
        for (var index = 0; index < source.Actions.Length; index++)
        {
            var current = projector.Build(source.Actions[index].After);
            Require(current.Status == "ready_current_state_only" && previous.Villagers.Length == current.Villagers.Length,
                "Native friendship population changed or became incomplete inside the queue.");
            var terminal = index == source.Actions.Length - 1;
            foreach (var pair in previous.Villagers.Zip(current.Villagers))
            {
                Require(pair.First.NativeIterationIndex == pair.Second.NativeIterationIndex &&
                    pair.First.NpcName == pair.Second.NpcName && pair.First.UniqueNpcIdentity == pair.Second.UniqueNpcIdentity &&
                    pair.First.FriendshipRowExists == pair.Second.FriendshipRowExists,
                    "Native friendship population identity drifted.");
                if (terminal && pair.First.NpcName == selected.NpcName)
                    Require(pair.First.UniqueNpcIdentity && pair.First.FriendshipPoints == selected.FriendshipPointsBefore &&
                        pair.Second.FriendshipPoints == selected.ExpectedFriendshipPointsAfter && !pair.First.Qualifies && pair.Second.Qualifies,
                        "Native friendship target delta differs from the original independent projection.");
                else
                    Require(pair.First.FriendshipPoints == pair.Second.FriendshipPoints && pair.First.Qualifies == pair.Second.Qualifies,
                        "Movement or another NPC was credited with the native friendship threshold.");
            }
            previous = current;
        }
        var hashes = new Dictionary<string, string>(source.SourceSha256, StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["preference"] = CurrentTeacherFrontierSupport.HashFile(request.PreferencePath),
            ["decision_snapshot"] = preference.SnapshotSha256
        };
        return new(new
        {
            schema_version = "friendship_native_threshold_receipt.v1", status = "verified_local_threshold",
            request_path = requestPath, source_sha256 = hashes, run_id = request.RunId,
            save_id = Identity(source.Before.SaveId), player_id = Identity(source.Before.PlayerId), total_day = TotalDay(source.Before),
            criterion_id = request.TargetQualifyingCount == 5 ? "friendships_5" : "friendships_10",
            target_qualifying_count = request.TargetQualifyingCount, qualifying_count_before = before.QualifyingVillagerCount,
            qualifying_count_after = after.QualifyingVillagerCount, npc_name = selected.NpcName,
            friendship_points_before = selected.FriendshipPointsBefore, friendship_points_after = selected.ExpectedFriendshipPointsAfter,
            selected_candidate_id = selected.CandidateId, queue_id = source.Queue.QueueId,
            queue_item_ids = source.Actions.Select(frame => frame.Item.QueueItemId).ToArray(),
            request_nonces = source.Actions.Select(frame => frame.Request.RequestNonce).ToArray(),
            before_state_hash = source.Before.StateHash, after_state_hash = source.After.StateHash,
            before_game_tick = source.Before.GameTick, after_game_tick = source.After.GameTick,
            comparison_admission = "independent_pairs", independent_pair_count = preference.PairwisePreferences.Length,
            complete_order_verified = preference.TeacherPreferenceLabelEligible,
            uncompared_candidate_ids = preference.UncomparedCandidateIds,
            independent_current_comparison_verified = true, native_criterion_outcome_verified = true,
            prior_friendship_acquisition_verified = false, prior_story_progress_verified = false,
            teacher_dataset_row_verified = false, full_root_verified = false, formal_product_training_authorized = false
        }, preference, source, request);
    }

    public static object Verify(string receiptPath) => VerifySource(receiptPath).Receipt;

    internal static FriendshipNativeThresholdSource VerifySource(string receiptPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(receiptPath), "friendship_native_threshold_receipt.v1");
        var rebuilt = BuildSource(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt.Receipt, JsonDefaults.Options)),
            "Native friendship threshold receipt differs from original sources.");
        return rebuilt;
    }
}

internal sealed record FriendshipNativeThresholdSource(object Receipt, CurrentFriendshipTeacherPreference Preference,
    VerifiedNativeExecutionQueue Execution, FriendshipNativeThresholdRequest Request);
