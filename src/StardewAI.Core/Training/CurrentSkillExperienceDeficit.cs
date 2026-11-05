using System;

namespace StardewAI.Core.Training;

public sealed class CurrentSkillExperienceDeficit
{
    public string SchemaVersion { get; set; } = "current_skill_experience_deficit.v1";

    public string Status { get; set; } = "blocked";

    public string SourceStateHash { get; set; } = string.Empty;

    public long GameTick { get; set; }

    public int? ScoringLevel { get; set; }

    public int? BaseLevelsMissingForFirstCriterion { get; set; }

    public int? BaseLevelsMissingForSecondCriterion { get; set; }

    public int? ExperienceMissingForFiveLevelTenSkills { get; set; }

    public CurrentSkillExperienceDeficitRow[] Skills { get; set; } =
        Array.Empty<CurrentSkillExperienceDeficitRow>();

    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    public bool TrainingLabelEligible => false;

    public bool NativeOutcomeVerified => false;
}

public sealed class CurrentSkillExperienceDeficitRow
{
    public string SkillId { get; set; } = string.Empty;

    public int SkillIndex { get; set; }

    public int UnmodifiedLevel { get; set; }

    public int EffectiveLevel { get; set; }

    public int Experience { get; set; }

    public int? NextLevel { get; set; }

    public int? ExperienceToNextLevel { get; set; }

    public int ExperienceToLevelTen { get; set; }
}
