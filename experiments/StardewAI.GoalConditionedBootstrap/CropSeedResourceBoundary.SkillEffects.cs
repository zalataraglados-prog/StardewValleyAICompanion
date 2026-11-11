using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static (CurrentSkillExperienceDeficit Before, CurrentSkillExperienceDeficit After) VerifySkillExperienceDelta(
        SnapshotEnvelope before, SnapshotEnvelope after, string skillId, int skillIndex,
        int expectedExperienceBefore, int expectedDelta, string label)
    {
        Require(skillIndex is >= 0 and < 5 && expectedExperienceBefore >= 0 && expectedDelta >= 0 &&
                (long)expectedExperienceBefore + expectedDelta <= int.MaxValue,
            label + " skill index or XP contract is invalid.");
        var builder = new CurrentSkillExperienceDeficitBuilder();
        var first = builder.Build(before); var next = builder.Build(after);
        Require(first.Status == "ready_current_state_only" && next.Status == "ready_current_state_only",
            label + " original skill/level projections are unavailable.");
        var priorSkill = first.Skills.Single(row => row.SkillIndex == skillIndex);
        var skill = next.Skills.Single(row => row.SkillIndex == skillIndex);
        Require(priorSkill.SkillId == skillId && skill.SkillId == skillId && priorSkill.Experience == expectedExperienceBefore &&
                skill.Experience == checked(expectedExperienceBefore + expectedDelta) &&
                (long)skill.EffectiveLevel - priorSkill.EffectiveLevel == (long)skill.UnmodifiedLevel - priorSkill.UnmodifiedLevel,
            label + " skill XP or preserved buff differs from the original native contract.");
        var expected = Field(before, "player", "skills_detail")!;
        var value = Value(expected).AsObject();
        var target = Unique(Array(value["skills"], label + " skills"), "skill_id", skillId);
        var actualTarget = Unique(Array(Value(Field(after, "player", "skills_detail")!)["skills"], label + " after skills"), "skill_id", skillId);
        foreach (var name in new[] { "experience", "unmodified_level", "effective_level", "next_level", "next_level_experience",
                     "experience_to_next_level", "at_vanilla_level_cap" })
            target[name] = actualTarget[name]?.DeepClone();
        value["scoring_level"] = next.ScoringLevel;
        var pending = Array(value["new_levels"], label + " pending levels");
        Require(pending.All(row => row is JsonObject), label + " pending-level row is invalid.");
        for (var level = priorSkill.UnmodifiedLevel + 1; level <= skill.UnmodifiedLevel; level++)
            pending.Add(new JsonObject { ["skill_index"] = skillIndex, ["level"] = level });
        value["new_levels"] = new JsonArray(pending.OrderBy(row => Int(row!, "skill_index")).ThenBy(row => Int(row!, "level"))
            .Select(row => row!.DeepClone()).ToArray());
        EqualField(expected, Field(after, "player", "skills_detail"), label + " skill effects or unrelated skill/profession state");
        var score = Field(before, "player", "level")!;
        score["value"] = next.ScoringLevel;
        EqualField(score, Field(after, "player", "level"), label + " scoring level");
        return (first, next);
    }
}
