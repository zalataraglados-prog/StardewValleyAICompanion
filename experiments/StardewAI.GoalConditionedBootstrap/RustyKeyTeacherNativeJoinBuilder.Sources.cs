using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyTeacherNativeJoinBuilder
{
    private static readonly string[] AllowedOptions = { "executor.donate_museum_item", "executor.traverse_connector",
        "executor.wait_ticks", "executor.sleep", "executor.advance_story_event" };

    private static VerifiedNativeExecutionAction ReadAction(RustyKeyNativeActionSource source, RustyKeyNativeKeyReceipt key,
        IDictionary<string, string> hashes, int index)
    {
        var action = NativeExecutionActionSourceReader.Read(source.ActionQueuePath, source.ExecutionReceiptPath, source.AfterSnapshotPath);
        Require(AllowedOptions.Contains(action.Result.OptionId, StringComparer.Ordinal),
            "Rusty Key native option is outside the continuation scope.");
        Require(action.Before.GameVersion == key.GameVersion && action.After.GameVersion == key.GameVersion &&
            action.Before.BridgeVersion == key.BridgeVersion && action.After.BridgeVersion == key.BridgeVersion &&
            Identity(action.Before.SaveId) == key.SaveId && Identity(action.After.SaveId) == key.SaveId &&
            Identity(action.Before.PlayerId) == key.PlayerId && Identity(action.After.PlayerId) == key.PlayerId &&
            action.Result.RunId == key.RunId && TotalDay(action.Before) is >= 0 and < 224 &&
            TotalDay(action.After) is >= 0 and < 224, "Rusty Key native frame identity/version/date drifted.");
        if (action.Result.OptionId == "executor.traverse_connector")
            Require(Facts(action.Result).Overlaps(new[] { "connector_location_changed_as_expected", "connector_native_arrival_tile_adjusted" }),
                "Rusty Key native connector has no verified arrival fact.");
        if (action.Result.OptionId == "executor.wait_ticks")
            Require(Facts(action.Result).Overlaps(new[] { "bounded_wait_window_elapsed", "wait_until_time_reached" }),
                "Rusty Key native wait has no bounded native completion fact.");
        foreach (var hash in action.SourceSha256)
            hashes["action_" + index + "." + hash.Key] = hash.Value;
        return action;
    }
}
