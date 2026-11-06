using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentMarriageTeacherPreference
{
    public string SchemaVersion => "current_marriage_teacher_preference.v1";
    public string Status { get; set; } = "blocked";
    public string IntakePath { get; set; } = string.Empty;
    public string IntakeSha256 { get; set; } = string.Empty;
    public CurrentMarriageHouseIntake Intake { get; set; } = null!;
    public string CalibrationPath { get; set; } = string.Empty;
    public string CalibrationSha256 { get; set; } = string.Empty;
    public string LedgerPath { get; set; } = string.Empty;
    public string LedgerSha256 { get; set; } = string.Empty;
    public CurrentMarriageHouseSettlementProjection Settlement { get; set; } = new();
    public bool CandidateDenominatorComplete { get; set; }
    public PolicyEventCandidatePrediction[] DecisionCandidates { get; set; } = Array.Empty<PolicyEventCandidatePrediction>();
    public CurrentMarriageTeacherCandidate[] Candidates { get; set; } = Array.Empty<CurrentMarriageTeacherCandidate>();
    public PolicyEventCandidatePrediction? SelectedCandidate { get; set; }
    public PolicyTeacherPreferencePair[] PairwisePreferences { get; set; } = Array.Empty<PolicyTeacherPreferencePair>();
    public string[] UncomparedCandidateIds { get; set; } = Array.Empty<string>();
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    public bool IndependentPairwiseLabelEligible { get; set; }
    public bool CompletePreferenceLabelEligible { get; set; }
    public string ComparisonPolicyId => "same_partner_native_effect_registered_reserve_safe_liquidation_cost.v1";
    public string ControlPolicyId => "strict_pair_leader_then_stable_id_not_a_tie_label.v1";
    public string Scope => "current_gift_preparation_registered_commitments_and_liquidation_cost_not_unregistered_future_item_value_or_global_optimum";
    public string BindingRole { get; set; } = string.Empty;
    public bool UsesLearnerScores => false;
    public bool NativeOutcomeVerified => false;
    public bool FullCriterionVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public sealed class CurrentMarriageTeacherCandidate
{
    public string CandidateId { get; set; } = string.Empty;
    public string SourceStateHash { get; set; } = string.Empty;
    public string RegisteredLedgerSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "uncompared";
    public string[] Reasons { get; set; } = Array.Empty<string>();
    public string NpcName { get; set; } = string.Empty;
    public string ProposalSourceCandidateId { get; set; } = string.Empty;
    public string NativeEffectSignature { get; set; } = string.Empty;
    public string NativeActionIdentity { get; set; } = string.Empty;
    public string OpportunityId { get; set; } = string.Empty;
    public string TimingEvidenceId { get; set; } = string.Empty;
    public int ContactArrivalTime { get; set; }
    public int CompiledMinutes { get; set; }
    public int EnergyCost { get; set; }
    public int GiftSaleValue { get; set; }
    public int GiftSlotIndex { get; set; }
    public string GiftQualifiedItemId { get; set; } = string.Empty;
    public int FriendshipPointsBefore { get; set; }
    public int FriendshipPointsAfter { get; set; }
    public int? ConditionalJointSettlementTotalDay { get; set; }
    public CurrentSocialRouteContactBudget? RouteContactBudget { get; set; }
}
