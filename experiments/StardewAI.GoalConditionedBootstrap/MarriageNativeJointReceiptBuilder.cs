using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageNativeJointReceiptBuilder
{
    public static object Build(string requestPath) => BuildSource(requestPath).Receipt;

    private static MarriageNativeJointSource BuildSource(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MarriageNativeJointRequest>(requestPath, "marriage_native_joint_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.RunId) && !string.IsNullOrWhiteSpace(request.FinalSnapshotPath) &&
            request.Steps is { Length: >= 3 and <= 64 } && request.Steps.All(row => row is not null &&
                !string.IsNullOrWhiteSpace(row.BeforeSnapshotPath) && !string.IsNullOrWhiteSpace(row.ActionQueuePath) &&
                !string.IsNullOrWhiteSpace(row.ExecutionReceiptPath) && !string.IsNullOrWhiteSpace(row.AfterSnapshotPath) &&
                (row.PreferencePath is null || !string.IsNullOrWhiteSpace(row.PreferencePath))) &&
            !string.IsNullOrWhiteSpace(request.Steps[0].PreferencePath),
            "Marriage joint request needs a run, complete bounded steps beginning with a preference, and final capture.");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
            { ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath) };
        var context = new JointContext();
        var queues = new List<object>();
        var preparations = new List<CurrentMarriageTeacherPreference>();
        var gifts = new List<VerifiedNativeExecutionQueue>();
        var queueIds = new HashSet<string>(StringComparer.Ordinal);
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        SnapshotEnvelope? first = null;
        SnapshotEnvelope? previous = null;
        for (var index = 0; index < request.Steps.Length; index++)
        {
            var step = request.Steps[index];
            var queue = ReadTyped<ActionQueueEnvelope>(RequiredFile(step.ActionQueuePath), "action_queue.v1");
            Require(queue.CandidateAudit is { Length: > 0 }, "Marriage queue is missing its original candidate audit.");
            var accepted = queue.CandidateAudit.Where(row => row.Decision == "accepted").ToArray();
            Require(accepted.Length == 1 && !string.IsNullOrWhiteSpace(accepted[0].CandidateId), "Marriage queue has ambiguous accepted candidate.");
            var source = NativeExecutionActionSourceReader.ReadQueue(step.ActionQueuePath, step.BeforeSnapshotPath,
                step.ExecutionReceiptPath, step.AfterSnapshotPath, request.RunId, accepted[0].CandidateId);
            first ??= source.Before;
            if (previous is not null) MarriageNativeJointContinuity.Validate(previous, source.Before);
            Require(queueIds.Add(source.Queue.QueueId) && source.Actions.All(frame =>
                itemIds.Add(frame.Item.QueueItemId) && nonces.Add(frame.Request.RequestNonce)),
                "Marriage chain reuses a queue, primitive or nonce.");
            ValidateIdentity(first, source, request.RunId);
            var role = step.PreferencePath is not null ? "independent_gift_preparation" : "mechanical_control_not_preference_label";
            if (step.PreferencePath is not null)
            {
                Require(!context.ControlsStarted, "Marriage preparation appears after mechanical settlement started.");
                var preference = VerifyPreparation(step, source, context);
                preparations.Add(preference);
                gifts.Add(source);
                hashes[$"step_{index}.preference"] = CurrentTeacherFrontierSupport.HashFile(step.PreferencePath);
            }
            else
            {
                context.ControlsStarted = true;
                Require(context.GiftCount == 1, "Marriage controls require the recorded independently selected native gift.");
                VerifyControl(source, accepted[0], context);
            }
            foreach (var hash in source.SourceSha256) hashes[$"step_{index}." + hash.Key] = hash.Value;
            queues.Add(new
            {
                index, role, candidate_id = accepted[0].CandidateId, candidate_kind = accepted[0].Kind,
                queue_id = source.Queue.QueueId,
                queue_item_ids = source.Actions.Select(frame => frame.Item.QueueItemId).ToArray(),
                request_nonces = source.Actions.Select(frame => frame.Request.RequestNonce).ToArray(),
                primitive_options = source.Actions.Select(frame => frame.Result.OptionId).ToArray(),
                before_state_hash = source.Before.StateHash, after_state_hash = source.After.StateHash,
                before_game_tick = source.Before.GameTick, after_game_tick = source.After.GameTick,
                total_day_before = TotalDay(source.Before), total_day_after = TotalDay(source.After)
            });
            previous = source.After;
        }
        var finalPath = RequiredFile(request.FinalSnapshotPath);
        var final = ReadTyped<SnapshotEnvelope>(finalPath, "snapshot.v1");
        if (context.WeddingCompletionFrame is { } wedding &&
            Value(wedding.After, "player", "movement_timing_context").GetProperty("is_warping").ValueKind == JsonValueKind.True)
            MarriageNativeJointContinuity.ValidateWeddingReturn(wedding.Before, previous!, final);
        else MarriageNativeJointContinuity.Validate(previous!, final);
        var current = State(final);
        var relationship = Friend(final, context.NpcName);
        Require(context.ProposalCount == 1 && context.JointTransition is not null && context.WeddingCompleted && context.WeddingDay == TotalDay(final) &&
            context.SleepCount == TotalDay(final) - TotalDay(first!) &&
            current.CurrentCriterionSatisfied == true && current.Engaged == false && current.Spouse == context.NpcName &&
            relationship.GetProperty("status").GetString() == "Married" && !EventActive(final) && !MenuOpen(final),
            "Marriage native chain did not settle its exact scheduled wedding and joint house criterion.");
        hashes["final_snapshot"] = CurrentTeacherFrontierSupport.HashFile(finalPath);
        var receipt = new
        {
            schema_version = "marriage_native_joint_receipt.v1", status = "verified_local_joint_outcome",
            request_path = requestPath, source_sha256 = hashes, run_id = request.RunId,
            save_id = Identity(first!.SaveId), player_id = Identity(first.PlayerId),
            criterion_id = "married_or_roommate_house_2", direction_id = "marriage_and_house_upgrade", npc_name = context.NpcName,
            total_day_before = TotalDay(first), total_day_after = TotalDay(final), native_wedding_total_day = context.WeddingDay,
            initial_house_level = State(first).FarmhouseUpgradeLevel, initial_house_pending_days = State(first).DaysUntilFarmhouseUpgrade,
            final_house_level = current.FarmhouseUpgradeLevel, native_sleep_count = context.SleepCount,
            before_state_hash = first.StateHash, after_state_hash = final.StateHash, queue_chain = queues,
            joint_criterion_transition = context.JointTransition,
            preparation_comparisons = preparations.Select(preference => new
            {
                selected_candidate_id = preference.SelectedCandidate!.CandidateId,
                independent_pair_count = preference.PairwisePreferences.Length, pairs = preference.PairwisePreferences,
                uncompared_candidate_ids = preference.UncomparedCandidateIds, complete_order_verified = preference.CompletePreferenceLabelEligible
            }).ToArray(),
            scope = "same_run_current_partner_gift_to_native_wedding_with_declared_house_and_relationship_prerequisites",
            independent_current_preparation_comparison_verified = true, native_criterion_outcome_verified = true,
            mechanical_controls_are_preference_labels = false, prior_house_acquisition_verified = false,
            unknown_current_tool_is_not_inferred_as_absence = true,
            prior_relationship_acquisition_verified = false, teacher_dataset_row_verified = false,
            full_root_verified = false, formal_product_training_authorized = false
        };
        return new(receipt, request, preparations.ToArray(), gifts.ToArray(), final);
    }

    public static object Verify(string receiptPath) => VerifySource(receiptPath).Receipt;

    internal static MarriageNativeJointSource VerifySource(string receiptPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(receiptPath), "marriage_native_joint_receipt.v1");
        var rebuilt = BuildSource(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt.Receipt, JsonDefaults.Options)),
            "Marriage native joint receipt differs from its complete original sources.");
        return rebuilt;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }

    private sealed class JointContext
    {
        public string NpcName = string.Empty;
        public int GiftCount, ProposalCount, SleepCount;
        public int? WeddingDay;
        public bool ControlsStarted, WeddingCompleted;
        public object? JointTransition;
        public NativeExecutionActionFrame? WeddingCompletionFrame;
        public readonly StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator Evaluator =
            StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator.ForCompleteOfflineSocialComparison();
    }
}

internal sealed record MarriageNativeJointSource(object Receipt, MarriageNativeJointRequest Request,
    CurrentMarriageTeacherPreference[] Preparations, VerifiedNativeExecutionQueue[] GiftExecutions, SnapshotEnvelope Final);
