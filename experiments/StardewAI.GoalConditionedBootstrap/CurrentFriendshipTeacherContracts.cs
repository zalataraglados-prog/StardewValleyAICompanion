using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentFriendshipTeacherPreference
{
    public string SchemaVersion { get; set; } = "current_friendship_teacher_preference.v1";
    public string Status { get; set; } = "blocked";
    public string SnapshotPath { get; set; } = string.Empty;
    public string SnapshotSha256 { get; set; } = string.Empty;
    public string CalibrationPath { get; set; } = string.Empty;
    public string CalibrationSha256 { get; set; } = string.Empty;
    public CurrentFriendshipDeficit Deficit { get; set; } = new();
    public CurrentFriendshipTeacherControl OriginalTeacher { get; set; } = new();
    public string ComparisonPolicyId => "current_friendship_saturated_progress_contact_budget_frontier.v1";
    public string[] ConsideredOptionIds { get; set; } = Array.Empty<string>();
    public CurrentFriendshipTeacherOptionAudit[] OptionAudit { get; set; } = Array.Empty<CurrentFriendshipTeacherOptionAudit>();
    public bool CandidateDenominatorComplete { get; set; }
    public CurrentFriendshipTeacherCandidate[] Candidates { get; set; } = Array.Empty<CurrentFriendshipTeacherCandidate>();
    public PolicyEventCandidatePrediction[] DecisionCandidates { get; set; } = Array.Empty<PolicyEventCandidatePrediction>();
    public PolicyEventCandidatePrediction? SelectedCandidate { get; set; }
    public PolicyTeacherPreferencePair[] PairwisePreferences { get; set; } = Array.Empty<PolicyTeacherPreferencePair>();
    public string[] UncomparedCandidateIds { get; set; } = Array.Empty<string>();
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    public bool TeacherPreferenceLabelEligible { get; set; }
    public bool UsesLearnerRankOrScore => false;
    public bool EmitsNegativeLabelsForUnavailableRoutes => false;
    public bool UsesObservedInteractionDuration => false;
    public bool NativeOutcomeVerified => false;
    public bool FullCriterionVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public string Scope => "original_social_control_current_saturated_Grandpa_progress_and_calibrated_contact_arrival_budget_not_global_optimum";
}

public sealed class CurrentFriendshipTeacherControl
{
    public string Status { get; set; } = string.Empty;
    public string SourceStateHash { get; set; } = string.Empty;
    public bool TrainingLabelEligible { get; set; }
    public string SelectedCandidateId { get; set; } = string.Empty;
    public CurrentSocialDayItineraryPlan Itinerary { get; set; } = new();
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class CurrentFriendshipTeacherOptionAudit
{
    public string OptionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string[] MissingStateFactors { get; set; } = Array.Empty<string>();
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    public int CandidateCount { get; set; }
    public int SourceCandidateCount { get; set; }
    public bool AtLegacySocialCandidateLimit { get; set; }
    public string[] OmittedAvailableCandidateIds { get; set; } = Array.Empty<string>();
}

public sealed class CurrentFriendshipTeacherCandidate
{
    public string CandidateId { get; set; } = string.Empty;
    public string OptionId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = "excluded";
    public string[] Reasons { get; set; } = Array.Empty<string>();
    public bool AvailableCurrentSource { get; set; }
    public bool ComparisonRequired { get; set; }
    public string NpcName { get; set; } = string.Empty;
    public string NativeActionIdentity { get; set; } = string.Empty;
    public int FriendshipPointsBefore { get; set; }
    public int ExpectedFriendshipPointsAfter { get; set; }
    public int UsefulFriendshipDelta { get; set; }
    public int ExpectedNewQualifiers { get; set; }
    public int CompiledMinutes { get; set; }
    public int EnergyCost { get; set; }
    public int ContactBudgetArrivalTime { get; set; }
    public string TimingEvidenceId { get; set; } = string.Empty;
    public string TimingEvidenceKind { get; set; } = string.Empty;
    public string GiftQualifiedItemId { get; set; } = string.Empty;
    public int? GiftQuality { get; set; }
    public int? GiftSlotIndex { get; set; }
    public string GiftNormalLimitEffect { get; set; } = string.Empty;
    public string OpportunityId { get; set; } = string.Empty;
}
