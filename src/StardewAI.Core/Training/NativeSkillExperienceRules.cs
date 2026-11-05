using System;

namespace StardewAI.Core.Training;

public static class NativeSkillExperienceRules
{
    public const string GameVersion = "1.6.15";

    // Live Farmer.getBaseExperienceForLevel; source identity is in skills-local-intake.md.
    private static readonly int[] Thresholds =
        { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };

    public static int ExperienceForLevel(int level) => level is >= 1 and <= 10
        ? Thresholds[level - 1] : throw new ArgumentOutOfRangeException(nameof(level));
}
