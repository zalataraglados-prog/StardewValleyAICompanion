using StardewValley;

namespace StardewAI.RuntimeTestHarness;

internal static class MachineHarvestExperienceFixture
{
    public static void ApplySkillProfile(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return;
        }

        if (profile == "zero")
        {
            Game1.player.farmingLevel.Value = 0;
            Game1.player.fishingLevel.Value = 0;
            Game1.player.foragingLevel.Value = 0;
            Game1.player.miningLevel.Value = 0;
            Game1.player.combatLevel.Value = 0;
            for (var index = 0; index < 5; index++)
            {
                Game1.player.experiencePoints[index] = 0;
            }
            return;
        }

        if (profile == "mastery_threshold_order")
        {
            Game1.player.farmingLevel.Value = 9;
            Game1.player.fishingLevel.Value = 10;
            Game1.player.foragingLevel.Value = 10;
            Game1.player.miningLevel.Value = 10;
            Game1.player.combatLevel.Value = 10;
            Game1.player.experiencePoints[Farmer.farmingSkill] = 14999;
            Game1.player.experiencePoints[Farmer.fishingSkill] = 15000;
            Game1.player.experiencePoints[Farmer.foragingSkill] = 15000;
            Game1.player.experiencePoints[Farmer.miningSkill] = 15000;
            Game1.player.experiencePoints[Farmer.combatSkill] = 15000;
        }
    }
}
