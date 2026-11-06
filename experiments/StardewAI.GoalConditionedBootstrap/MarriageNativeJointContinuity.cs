using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

// Only passive handoffs between recorded actions, never the action's own effects.
public static partial class MarriageNativeJointContinuity
{
    // A bounded controller observation window, not elapsed in-game planning time.
    public const long MaximumPassiveGapTicks = 360;

    public static void Validate(SnapshotEnvelope previousAfter, SnapshotEnvelope currentBefore) =>
        ValidateCore(previousAfter, currentBefore, allowWeddingExit: false);

    private static void ValidateCore(SnapshotEnvelope previousAfter, SnapshotEnvelope currentBefore, bool allowWeddingExit)
    {
        ValidateSnapshot(previousAfter);
        ValidateSnapshot(currentBefore);
        Require(previousAfter.BridgeVersion == currentBefore.BridgeVersion &&
            Identity(previousAfter.SaveId) == Identity(currentBefore.SaveId) &&
            Identity(previousAfter.PlayerId) == Identity(currentBefore.PlayerId) &&
            currentBefore.GameTick >= previousAfter.GameTick &&
            currentBefore.GameTick - previousAfter.GameTick <= MaximumPassiveGapTicks &&
            TotalDay(previousAfter) == TotalDay(currentBefore) &&
            new[] { "year", "season", "day", "time" }.All(name => Same(previousAfter, currentBefore, "time", name)),
            "Marriage passive handoff changed identity/day or exceeded its observation bound.");
        foreach (var name in new[] { "training_run_id", "save_isolation_path", "training_mode" })
            Require(Same(previousAfter, currentBefore, "environment", name), "Marriage passive environment changed: " + name);
        var projector = new CurrentMarriageHouseStateBuilder();
        var left = projector.Build(previousAfter);
        var right = projector.Build(currentBefore);
        Require(left.Status == "ready_current_state_only" && right.Status == "ready_current_state_only" &&
            left.MarriedOrRoommate == right.MarriedOrRoommate && left.Engaged == right.Engaged &&
            left.HasCurrentOrPendingRoommate == right.HasCurrentOrPendingRoommate && left.Spouse == right.Spouse &&
            left.FarmhouseUpgradeLevel == right.FarmhouseUpgradeLevel &&
            left.DaysUntilFarmhouseUpgrade == right.DaysUntilFarmhouseUpgrade &&
            left.CurrentCriterionSatisfied == right.CurrentCriterionSatisfied,
            "Marriage/house progress changed without a recorded action.");
        foreach (var name in new[] { "money", "energy", "health", "inventory" })
            Require(Same(previousAfter, currentBefore, "player", name), "Marriage passive player state changed: " + name);
        // Empty/non-tool selections are unavailable in the original bridge. Keep
        // that observation, not an invented readable "no tool" value.
        var oldTool = previousAfter.State["player"].GetProperty("current_tool");
        var newTool = currentBefore.State["player"].GetProperty("current_tool");
        Require(oldTool.GetProperty("status").GetString() == newTool.GetProperty("status").GetString() &&
            JsonNode.DeepEquals(JsonNode.Parse(oldTool.GetProperty("value").GetRawText()), JsonNode.Parse(newTool.GetProperty("value").GetRawText())),
            "Marriage passive current-tool observation changed.");
        if (!allowWeddingExit)
            foreach (var name in new[] { "location_id", "tile_x", "tile_y" })
                Require(Same(previousAfter, currentBefore, "player", name), "Marriage passive player state changed: " + name);
        Require(Same(previousAfter, currentBefore, "npcs", "friendships"), "Marriage passive relationships changed.");
    }

    private static bool Same(SnapshotEnvelope left, SnapshotEnvelope right, string domain, string name) =>
        JsonNode.DeepEquals(JsonNode.Parse(Value(left, domain, name).GetRawText()), JsonNode.Parse(Value(right, domain, name).GetRawText()));

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
