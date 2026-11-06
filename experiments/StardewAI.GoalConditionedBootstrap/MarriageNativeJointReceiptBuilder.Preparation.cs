using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageNativeJointReceiptBuilder
{
    private static CurrentMarriageTeacherPreference VerifyPreparation(MarriageNativeJointStep step,
        VerifiedNativeExecutionQueue source, JointContext context)
    {
        var dispatch = CurrentMarriageTeacherPreferenceBuilder.CompileSource(RequiredFile(step.PreferencePath!), independentPairs: true);
        var preference = dispatch.Preference;
        var selected = preference.Candidates.Single(row => row.CandidateId == preference.SelectedCandidate!.CandidateId);
        Require(context.GiftCount == 0 && State(source.Before).CurrentCriterionSatisfied == false &&
            State(source.Before).Engaged == false && State(source.Before).MarriedOrRoommate == false &&
            JsonNode.DeepEquals(JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(preference.Intake.SnapshotPath)),
                JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(step.BeforeSnapshotPath))) &&
            source.Queue.GoalId == dispatch.Queue.GoalId && source.Queue.Items.Length == dispatch.Queue.Items.Length &&
            source.Queue.Items.Select((item, index) => ExecutionReceiptValidationSupport.EffectiveQueueItemSemanticallyMatches(
                JsonSerializer.SerializeToElement(item, JsonDefaults.Options), dispatch.Queue.Items[index])).All(matches => matches),
            "Marriage preparation does not bind its exact independently replayed original compilation.");
        if (context.NpcName.Length == 0) context.NpcName = selected.NpcName;
        Require(context.NpcName == selected.NpcName && TotalDay(source.Before) == TotalDay(source.After),
            "Marriage preparation changed partner or crossed an unbudgeted day.");
        var terminal = source.Actions[^1];
        var gift = terminal.Result.OptionId == "executor.social_interact";
        Require(source.Actions.Take(source.Actions.Length - (gift ? 1 : 0)).All(frame =>
            frame.Result.OptionId is "executor.move_to_tile" or "executor.traverse_connector"),
            "Marriage preparation contains an unowned native action.");
        if (!gift)
        {
            Require(preference.SelectedCandidate!.Kind == "route_connector_tile" &&
                Int(Friend(source.Before, context.NpcName), "points") == Int(Friend(source.After, context.NpcName), "points"),
                "Marriage route was credited as an unrecorded gift.");
            return preference;
        }
        var friendBefore = Friend(terminal.Before, context.NpcName);
        var friendAfter = Friend(terminal.After, context.NpcName);
        Require(preference.SelectedCandidate!.Kind == "social_gift_current" && terminal.Request.SocialActionKind == "gift" &&
            terminal.Request.SocialNpcName == context.NpcName && terminal.Request.SocialGiftSlotIndex == selected.GiftSlotIndex &&
            terminal.Request.SocialGiftQualifiedItemId == selected.GiftQualifiedItemId &&
            Int(friendBefore, "points") == selected.FriendshipPointsBefore && Int(friendAfter, "points") == selected.FriendshipPointsAfter &&
            Int(friendAfter, "gifts_today") == (long)Int(friendBefore, "gifts_today") + 1 &&
            Int(friendAfter, "gifts_this_week") == (long)Int(friendBefore, "gifts_this_week") + 1 &&
            Int(friendAfter, "last_gift_date_total_days") == TotalDay(terminal.After),
            "Marriage native gift differs from the independent effect or selected item.");
        VerifyConsumedOne(terminal, selected.GiftSlotIndex, selected.GiftQualifiedItemId);
        context.GiftCount++;
        return preference;
    }

    private static void VerifyConsumedOne(NativeExecutionActionFrame frame, int slot, string itemId)
    {
        JsonElement Slot(SnapshotEnvelope snapshot)
        {
            var rows = Value(snapshot, "player", "inventory").EnumerateArray()
                .Where(row => row.GetProperty("slot_index").GetInt32() == slot).ToArray();
            Require(rows.Length == 1, "Marriage native consumed slot is missing or ambiguous.");
            return rows[0];
        }
        var before = Slot(frame.Before);
        var after = Slot(frame.After);
        Require(before.GetProperty("qualified_item_id").GetString() == itemId && Int(before, "stack") > 0 &&
            (Int(before, "stack") == 1
                ? after.TryGetProperty("is_empty", out var empty) && empty.ValueKind == JsonValueKind.True
                : after.GetProperty("qualified_item_id").GetString() == itemId && Int(after, "stack") == Int(before, "stack") - 1 &&
                  Int(before, "quality") == Int(after, "quality")),
            "Marriage native gift/proposal did not consume exactly one selected item.");
    }
}
