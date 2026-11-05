using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record SkillNativeThresholdRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("preference_path")] string PreferencePath,
    [property: JsonPropertyName("action_queue_path")] string ActionQueuePath,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("target_scoring_level")] int TargetScoringLevel);

public static class SkillNativeThresholdReceiptBuilder
{
    public static object Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<SkillNativeThresholdRequest>(requestPath, "skill_native_threshold_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.RunId) && request.TargetScoringLevel is 15 or 25,
            "Skills native request needs a run and exact scoring threshold.");
        var dispatch = SkillTeacherDispatchBuilder.Compile(RequiredFile(request.PreferencePath));
        var action = NativeExecutionActionSourceReader.Read(request.ActionQueuePath, request.ExecutionReceiptPath, request.AfterSnapshotPath);
        var result = CurrentTeacherFrontierSupport.Read<JsonElement>(request.ExecutionReceiptPath, "Skills native result");
        var journal = CurrentTeacherFrontierSupport.Read<JsonElement>(CurrentTeacherFrontierSupport.RequiredString(result,
            "execution_request_journal_path"), "Skills native journal");
        var nativeBeforePath = CurrentTeacherFrontierSupport.RequiredString(journal, "before_snapshot_path");
        Require(action.Result.RunId == request.RunId && TotalDay(action.Before) is >= 0 and < 224 &&
            TotalDay(action.Before) == TotalDay(action.After) && dispatch.Queue.GoalId == action.Queue.GoalId &&
            dispatch.Preference.Deficit.SourceStateHash == action.Before.StateHash &&
            dispatch.Preference.Deficit.GameTick == action.Before.GameTick &&
            SameDecisionSnapshot(dispatch.Preference.SnapshotPath, nativeBeforePath),
            "Skills preference and native action do not share one exact original decision frame.");
        Require(action.Queue.Items.Length == dispatch.Queue.Items.Length &&
            action.Queue.Items.Select((item, index) => ExecutionReceiptValidationSupport.EffectiveQueueItemSemanticallyMatches(
                JsonSerializer.SerializeToElement(item, JsonDefaults.Options), dispatch.Queue.Items[index])).All(value => value) &&
            ExecutionReceiptValidationSupport.EffectiveQueueItemSemanticallyMatches(
                JsonSerializer.SerializeToElement(action.Item, JsonDefaults.Options), dispatch.Queue.Items[0]) &&
            action.Queue.CandidateAudit.Count(row => row.Decision == "accepted" &&
                row.CandidateId == dispatch.Preference.SelectedCandidate!.CandidateId) == 1,
            "Native Skills action is not the independently selected original compilation's first action.");
        var before = new CurrentSkillExperienceDeficitBuilder().Build(action.Before);
        var after = new CurrentSkillExperienceDeficitBuilder().Build(action.After);
        Require(before.Status == "ready_current_state_only" && after.Status == "ready_current_state_only" &&
            before.ScoringLevel < request.TargetScoringLevel && after.ScoringLevel >= request.TargetScoringLevel,
            "Native Skills scoring threshold did not cross from incomplete to complete.");
        var selected = dispatch.Preference.Candidates.Single(row => row.CandidateId == dispatch.Preference.SelectedCandidate!.CandidateId);
        var projected = selected.NativeXp.ToDictionary(row => row.SkillId, StringComparer.Ordinal);
        var deltas = before.Skills.Select(prior =>
        {
            var current = after.Skills.Single(row => row.SkillId == prior.SkillId);
            var delta = current.Experience - prior.Experience;
            var expectedMin = projected.TryGetValue(prior.SkillId, out var range) ? range.Minimum : 0;
            var expectedMax = range?.Maximum ?? 0;
            Require(delta >= expectedMin && delta <= expectedMax && current.UnmodifiedLevel >= prior.UnmodifiedLevel,
                "Native Skills XP differs from the independent current projection: " + prior.SkillId);
            return new { skill_id = prior.SkillId, experience_before = prior.Experience, experience_after = current.Experience,
                experience_delta = delta, level_before = prior.UnmodifiedLevel, level_after = current.UnmodifiedLevel };
        }).ToArray();
        Require(deltas.Any(row => row.experience_delta > 0), "Skills threshold has no native XP gain.");
        var hashes = new Dictionary<string, string>(action.SourceSha256, StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["preference"] = CurrentTeacherFrontierSupport.HashFile(request.PreferencePath),
            ["decision_snapshot"] = dispatch.Preference.SnapshotSha256
        };
        return new
        {
            schema_version = "skill_native_threshold_receipt.v1", status = "verified_local_threshold",
            request_path = requestPath, source_sha256 = hashes, run_id = request.RunId,
            save_id = Identity(action.Before.SaveId), player_id = Identity(action.Before.PlayerId),
            total_day = TotalDay(action.Before), target_scoring_level = request.TargetScoringLevel,
            criterion_id = request.TargetScoringLevel == 15 ? "player_level_15" : "player_level_25",
            selected_candidate_id = dispatch.Preference.SelectedCandidate!.CandidateId,
            queue_id = action.Queue.QueueId, queue_item_id = action.Item.QueueItemId, request_nonce = action.Request.RequestNonce,
            before_state_hash = action.Before.StateHash, after_state_hash = action.After.StateHash,
            before_game_tick = action.Before.GameTick, after_game_tick = action.After.GameTick,
            scoring_level_before = before.ScoringLevel, scoring_level_after = after.ScoringLevel, skill_deltas = deltas,
            independent_current_comparison_verified = true, native_criterion_outcome_verified = true,
            prior_skill_acquisition_verified = false, teacher_dataset_row_verified = false,
            full_root_verified = false, formal_product_training_authorized = false
        };
    }

    public static object Verify(string receiptPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(receiptPath), "skill_native_threshold_receipt.v1");
        var rebuilt = Build(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Native Skills threshold receipt differs from original sources.");
        return rebuilt;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }

    // The loop may store the same JSON as a hashed manifest; keep both physical
    // identities and compare the complete materialized frame, not just state_hash.
    private static bool SameDecisionSnapshot(string decisionPath, string nativePath) =>
        JsonNode.DeepEquals(JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(decisionPath)),
            JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(nativePath)));
}
