using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentSkillTeacherPreference
{
    public string SchemaVersion { get; set; } = "current_skill_teacher_preference.v1";
    public string Status { get; set; } = "blocked";
    public string SnapshotPath { get; set; } = string.Empty;
    public string SnapshotSha256 { get; set; } = string.Empty;
    public string StrategyLedgerPath { get; set; } = string.Empty;
    public string StrategyLedgerSha256 { get; set; } = string.Empty;
    public CurrentSkillExperienceDeficit Deficit { get; set; } = new();
    public string SelectionPolicyId { get; set; } = "current_skill_same_effect_xp_cost_dominance.v1";
    public string[] ConsideredOptionIds { get; set; } = Array.Empty<string>();
    public string[] CompilerOwnedOptionIds { get; set; } = Array.Empty<string>();
    public CurrentSkillTeacherOptionAudit[] OptionAudit { get; set; } = Array.Empty<CurrentSkillTeacherOptionAudit>();
    public CurrentSkillTeacherCandidate[] Candidates { get; set; } = Array.Empty<CurrentSkillTeacherCandidate>();
    public PolicyTeacherPreferencePair[] PairwisePreferences { get; set; } = Array.Empty<PolicyTeacherPreferencePair>();
    public string[] NonDominatedCandidateIds { get; set; } = Array.Empty<string>();
    public PolicyEventCandidatePrediction? SelectedCandidate { get; set; }
    public bool TeacherPreferenceLabelEligible { get; set; }
    public bool UsesLearnerRankOrScore => false;
    public bool EmitsNegativeLabelsForUnavailableRoutes => false;
    public bool NativeOutcomeVerified => false;
    public bool FullCriterionVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public string Scope => "one_current_state_same_effect_positive_xp_comparison_not_global_schedule";
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class CurrentSkillTeacherOptionAudit
{
    public string OptionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string[] MissingStateFactors { get; set; } = Array.Empty<string>();
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    public int NativeCandidateCount { get; set; }
}

public sealed class CurrentSkillTeacherCandidate
{
    public string CandidateId { get; set; } = string.Empty;
    public string OptionId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = "excluded";
    public string[] Reasons { get; set; } = Array.Empty<string>();
    public string NativeEffectSignature { get; set; } = string.Empty;
    public string NativeActionIdentity { get; set; } = string.Empty;
    public int EstimatedTicks { get; set; }
    public int CompiledMinutes { get; set; }
    public int EnergyCost { get; set; }
    public SkillExperienceProjectionRange[] NativeXp { get; set; } = Array.Empty<SkillExperienceProjectionRange>();
    public SkillExperienceProjectionRange[] UsefulXp { get; set; } = Array.Empty<SkillExperienceProjectionRange>();
}
