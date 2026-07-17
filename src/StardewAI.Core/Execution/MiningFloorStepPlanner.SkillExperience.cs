using System.Text.Json;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static void ApplyStoneExperienceProjection(MiningFloorStepPlan plan, JsonElement stone)
        {
            plan.SkillExperienceSkillId = "mining";
            plan.SkillExperienceMinimum = ReadInt(stone, "mining_experience_on_break_min");
            plan.SkillExperienceMaximum = ReadInt(stone, "mining_experience_on_break_max");
            plan.SkillExperienceCondition = ReadString(stone, "mining_experience_condition");
            plan.SkillExperienceProjectionStatus = ReadString(stone, "mining_experience_projection_status");

            plan.SecondarySkillExperienceSkillId = "luck";
            plan.SecondarySkillExperienceMinimum = ReadInt(stone, "luck_experience_on_break_min");
            plan.SecondarySkillExperienceMaximum = ReadInt(stone, "luck_experience_on_break_max");
            plan.SecondarySkillExperienceCondition = ReadString(stone, "luck_experience_condition");
            plan.SecondarySkillExperienceProjectionStatus = ReadString(stone, "luck_experience_projection_status");
        }
    }
}
