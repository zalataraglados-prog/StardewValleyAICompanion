using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static void VerifyWildTreeSkillEffects(SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request)
    {
        Require(request.ExpectedForagingExperienceBefore is >= 0 && request.ExpectedForagingExperienceDelta == 16 &&
                request.ExpectedForagingExperienceAfter == (long)request.ExpectedForagingExperienceBefore + 16,
            "Wild-tree clearance XP differs from the original exact16 native contract.");
        _ = VerifySkillExperienceDelta(before, after, "foraging", 2, request.ExpectedForagingExperienceBefore!.Value, 16,
            "Wild-tree clearance");
    }

    private static void VerifyWildTreeStatEffects(SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request)
    {
        var expected = Field(before, "world_progress", "game_state_query_unlock_state")!;
        var value = Value(expected).AsObject();
        var actor = before.PlayerId.Value!;
        Require(value["current_player_id"]?.GetValue<string>() == actor, "Wild-tree stat actor context differs.");
        var player = Unique(Array(value["players"], "wild-tree stat players"), "player_id", actor);
        Require(player["is_current"]?.GetValue<bool>() == true && player["stats"] is JsonObject,
            "Wild-tree actor stat state is unavailable.");
        var stats = player["stats"]!.AsObject();
        long prior = 0;
        if (stats.TryGetPropertyValue("TreesChopped", out var treeCount))
            Require(treeCount is JsonValue count && count.TryGetValue<long>(out prior) && prior >= 0,
                "Wild-tree original TreesChopped is invalid.");
        Require(request.ExpectedTreesChoppedBefore == prior && request.ExpectedTreesChoppedDelta == 1 &&
                request.ExpectedTreesChoppedAfter == checked(prior + 1), "Wild-tree stat contract differs from its raw before state.");
        stats["TreesChopped"] = request.ExpectedTreesChoppedAfter;
        EqualField(expected, Field(after, "world_progress", "game_state_query_unlock_state"), "wild-tree stat effects or unrelated player/unlock state");
    }
}
