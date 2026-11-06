using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageNativeJointReceiptBuilder
{
    private static void VerifyControl(VerifiedNativeExecutionQueue source, SmallModelPlanCandidateAudit accepted, JointContext context)
    {
        Require(!context.WeddingCompleted, "Marriage chain contains controls after its settled wedding.");
        var terminal = source.Actions[^1];
        var option = terminal.Result.OptionId == "executor.social_interact" ? "social.advance_partnership" :
            terminal.Result.OptionId == "executor.advance_story_event" ? "story.advance_event" : "recovery.stabilize_day";
        var parameters = option == "recovery.stabilize_day"
            ? new[] { new SmallModelActionParameter { Name = "control_plane.native_save_boundary", Value = "true" } }
            : Array.Empty<SmallModelActionParameter>();
        var availability = context.Evaluator.Evaluate(source.Before,
            new[] { new OptionAvailabilityCandidate { OptionId = option, Parameters = parameters } }, includeExecutorCalibrationOptions: true);
        var available = availability.Options.SelectMany(row => row.EventCandidates.Concat(row.SocialCandidates))
            .Where(row => row.CandidateId == accepted.CandidateId && row.Kind == accepted.Kind && row.Available).ToArray();
        Require(available.Length == 1, "Marriage control is not an original available candidate of its owning option.");
        if (option == "social.advance_partnership")
            VerifyProposal(source, context);
        else if (option == "story.advance_event")
            VerifyWedding(source, context);
        else
            VerifyRecovery(source, context);
        foreach (var frame in source.Actions)
        {
            var before = State(frame.Before);
            var after = State(frame.After);
            Require(before.CurrentCriterionSatisfied != true || after.CurrentCriterionSatisfied == true,
                "Marriage joint criterion regressed inside the native chain.");
            if (before.CurrentCriterionSatisfied == false && after.CurrentCriterionSatisfied == true)
            {
                Require(context.ProposalCount == 1 && context.JointTransition is null && TotalDay(frame.After) == context.WeddingDay &&
                    frame.Result.OptionId is "executor.sleep" or "executor.advance_story_event" &&
                    after.Spouse == context.NpcName && Friend(frame.After, context.NpcName).GetProperty("status").GetString() == "Married",
                    "Marriage joint progress appeared outside its recorded native wedding day phase.");
                context.JointTransition = new
                {
                    queue_id = frame.Queue.QueueId, queue_item_id = frame.Item.QueueItemId,
                    request_nonce = frame.Request.RequestNonce, option_id = frame.Result.OptionId,
                    before_state_hash = frame.Before.StateHash, after_state_hash = frame.After.StateHash,
                    total_day_before = TotalDay(frame.Before), total_day_after = TotalDay(frame.After)
                };
            }
        }
    }

    private static void VerifyProposal(VerifiedNativeExecutionQueue source, JointContext context)
    {
        var frame = source.Actions[^1];
        var before = State(frame.Before);
        var after = State(frame.After);
        var relationship = Friend(frame.After, context.NpcName);
        var calendar = new CurrentMarriageHouseSettlementProjector().Build(frame.Before);
        Require(context.ProposalCount == 0 && source.Actions.Take(source.Actions.Length - 1)
                .All(row => row.Result.OptionId == "executor.move_to_tile") &&
            frame.Request.SocialActionKind == "propose_marriage" && frame.Request.SocialNpcName == context.NpcName &&
            before.MarriedOrRoommate == false && before.Engaged == false && Int(Friend(frame.Before, context.NpcName), "points") >= 2500 &&
            after.Engaged == true && after.MarriedOrRoommate == false && after.HasCurrentOrPendingRoommate == false && after.Spouse == context.NpcName &&
            relationship.GetProperty("status").GetString() == "Engaged" && relationship.GetProperty("roommate_marriage").ValueKind == JsonValueKind.False &&
            calendar.Status == "ready_conditional_settlement_projection" &&
            calendar.ConditionalProposalWeddingTotalDay == Int(relationship, "wedding_date_total_days") &&
            TotalDay(source.Before) == TotalDay(source.After),
            "Marriage native proposal does not create its exact partner/native-calendar engagement.");
        Require(frame.Request.SocialGiftSlotIndex.HasValue && frame.Request.SocialGiftQualifiedItemId == "(O)460",
            "Marriage native proposal source lacks its original pendant slot.");
        VerifyConsumedOne(frame, frame.Request.SocialGiftSlotIndex!.Value, frame.Request.SocialGiftQualifiedItemId);
        context.WeddingDay = Int(relationship, "wedding_date_total_days");
        Require(context.WeddingDay > TotalDay(source.After) && context.WeddingDay < 224, "Marriage native wedding misses the scoring deadline.");
        context.ProposalCount++;
    }

    private static void VerifyRecovery(VerifiedNativeExecutionQueue source, JointContext context)
    {
        var sleeps = source.Actions.Count(row => row.Result.OptionId == "executor.sleep");
        Require(!EventActive(source.Before) && source.Actions.All(row => row.Result.OptionId is
            "executor.close_menu" or "executor.move_to_tile" or "executor.traverse_connector" or
            "executor.clear_obstacle" or "executor.wait_ticks" or "executor.sleep"),
            "Marriage recovery contains a story-owned or unrelated native action.");
        Require(sleeps <= 1 && TotalDay(source.After) == TotalDay(source.Before) + sleeps,
            "Marriage native recovery skipped/reversed a day or changed day without sleep.");
        if (sleeps == 1)
        {
            Require(context.ProposalCount == 1 && source.Actions[^1].Result.OptionId == "executor.sleep" &&
                TotalDay(source.After) <= context.WeddingDay, "Marriage native sleep precedes engagement or passes the scheduled wedding.");
            context.SleepCount++;
        }
        else
        {
            var before = State(source.Before);
            var after = State(source.After);
            Require(before.MarriedOrRoommate == after.MarriedOrRoommate && before.Engaged == after.Engaged &&
                before.Spouse == after.Spouse && before.FarmhouseUpgradeLevel == after.FarmhouseUpgradeLevel &&
                before.DaysUntilFarmhouseUpgrade == after.DaysUntilFarmhouseUpgrade,
                "Marriage recovery created unowned partnership or house progress.");
        }
    }

    private static void VerifyWedding(VerifiedNativeExecutionQueue source, JointContext context)
    {
        // In the pinned native Event constructor, eventID -2 sets isWedding=true.
        Require(context.ProposalCount == 1 && TotalDay(source.Before) == context.WeddingDay &&
            TotalDay(source.After) == context.WeddingDay && source.Actions.All(frame =>
                frame.Result.OptionId == "executor.advance_story_event" && EventActive(frame.Before) &&
                frame.Request.StoryEventId == "-2" && frame.Request.StoryEventBoundaryKind == "automatic_progress" &&
                frame.Request.StoryEventResponseIndex is null && string.IsNullOrEmpty(frame.Request.StoryEventResponseKey)),
            "Marriage story source is not the original automatic native wedding continuation.");
        if (!EventActive(source.After))
        {
            Require(!MenuOpen(source.After) && State(source.After).CurrentCriterionSatisfied == true,
                "Marriage wedding ended without stable joint native state.");
            context.WeddingCompleted = true;
            context.WeddingCompletionFrame = source.Actions[^1];
        }
    }
}
