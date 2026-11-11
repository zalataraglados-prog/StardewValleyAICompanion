using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static void VerifyWildTreeSkillEffects(SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request)
    {
        var builder = new CurrentSkillExperienceDeficitBuilder();
        var first = builder.Build(before); var next = builder.Build(after);
        Require(first.Status == "ready_current_state_only" && next.Status == "ready_current_state_only",
            "Wild-tree clearance original skill/level projections are unavailable.");
        var priorSkill = first.Skills.Single(skill => skill.SkillId == "foraging");
        var skill = next.Skills.Single(skill => skill.SkillId == "foraging");
        Require(request.ExpectedForagingExperienceBefore == priorSkill.Experience && request.ExpectedForagingExperienceDelta == 16 &&
                request.ExpectedForagingExperienceAfter == checked(priorSkill.Experience + 16) &&
                skill.Experience == request.ExpectedForagingExperienceAfter &&
                skill.EffectiveLevel - priorSkill.EffectiveLevel == skill.UnmodifiedLevel - priorSkill.UnmodifiedLevel,
            "Wild-tree clearance foraging XP or preserved buff differs from the original native contract.");
        var expected = Field(before, "player", "skills_detail")!;
        var value = Value(expected).AsObject();
        var target = Unique(Array(value["skills"], "wild-tree skills"), "skill_id", "foraging");
        var actualTarget = Unique(Array(Value(Field(after, "player", "skills_detail")!)["skills"], "wild-tree after skills"), "skill_id", "foraging");
        foreach (var name in new[] { "experience", "unmodified_level", "effective_level", "next_level", "next_level_experience",
                     "experience_to_next_level", "at_vanilla_level_cap" })
            target[name] = actualTarget[name]?.DeepClone();
        value["scoring_level"] = next.ScoringLevel;
        var pending = Array(value["new_levels"], "wild-tree pending levels");
        Require(pending.All(row => row is JsonObject), "Wild-tree pending-level row is invalid.");
        for (var level = priorSkill.UnmodifiedLevel + 1; level <= skill.UnmodifiedLevel; level++)
            pending.Add(new JsonObject { ["skill_index"] = 2, ["level"] = level });
        value["new_levels"] = new JsonArray(pending.OrderBy(row => Int(row!, "skill_index")).ThenBy(row => Int(row!, "level"))
            .Select(row => row!.DeepClone()).ToArray());
        EqualField(expected, Field(after, "player", "skills_detail"), "wild-tree skill effects or unrelated skill/profession state");
        var score = Field(before, "player", "level")!;
        score["value"] = next.ScoringLevel;
        EqualField(score, Field(after, "player", "level"), "wild-tree scoring level");
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
